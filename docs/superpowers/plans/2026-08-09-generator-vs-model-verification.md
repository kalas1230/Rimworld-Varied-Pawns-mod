# Generator-vs-Model Verification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add the class of check this project has never had — one that compares **rolled pawns against the scoring model** — and make the two ad-hoc checks that caught this week's defects permanent instead of scratch files.

**Architecture:** Three independent additions, no behaviour changes to pawn generation. (1) A new `DispersionModel.ExpectedPassionPips` predicts the mean and sd of delivered passion pips; the existing `Roll pawns and dump distribution` debug action measures the same quantity and now asserts them equal within a sampling-derived tolerance. (2) `envelope_check.py` gains a pointwise zero-spread invariant that the existing integrated self-check structurally cannot reach. (3) A generator-branch/mirror-site table in `envelope_check.py`, enforced by marker comments, so a new generator branch cannot be added without declaring which model sites mirror it.

**Tech Stack:** C# 7.3 targeting .NET Framework 4.7.2 (`net472`), RimWorld 1.6 + Harmony, `UnityEngine.Mathf`. Python 3 standard library only for `docs/tools/*.py`.

## Global Constraints

- **RimWorld 1.6 only.** No 1.5 support, no version shims, no `v1.5` mapping (audit finding Q-10).
- **`docs/tools/*.py` may import from the Python standard library only.** No numpy — it is not installed and `envelope_check.py` has a no-third-party-dependency rule.
- **No task in this plan may change a pawn.** `PassionVarianceApplier`, `SkillVarianceApplier`, `TraitVarianceApplier`, `QualityRoller` and `HarmonyPatches` are not to be edited. If a task appears to require it, stop and report.
- **No task in this plan may move a shipped figure.** After every task, `python docs/tools/envelope_check.py` must print `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.` A task that changes the `Scores` array has a bug — do not regenerate the golden file to make it agree.
- **Constants.cs right-hand sides must stay numeric literals.** `envelope_check.py` parses the file with the regex `public\s+const\s+float\s+(\w+)\s*=\s*(-?[\d.]+(?:[eE][-+]?\d+)?)f?\s*;`. An expression such as `12f * MajorPassionCost` makes the tool exit with `Constants.cs is missing: <name>`.
- **Mirrors change together.** `DispersionModel.Moments`, `PawnVarianceSettings.CalculateCompositeScore`, `envelope_check.py`'s `grid_moments`/`make_composite`, and `dispersion_mc.py` are four mirrors of one model. Editing one without the others is the root cause of findings Q-01, Q-04 and Q-14.
- **Build must be clean at 0 warnings:** `dotnet build Source/PawnVarianceMod.csproj -v minimal`.
- **Scratch and throwaway files go in `zzz-Do-Not-Commit/`**, which is excluded via `.git/info/exclude`. Never commit them.

---

## File Structure

| File | Responsibility | Task |
|---|---|---|
| `Source/DispersionModel.cs` | **Modify.** Add `ExpectedPassionPips` — the model's prediction of delivered pips, in raw pip units rather than the normalised composite axis. | 1 |
| `Source/DebugActions.cs` | **Modify.** `DumpDistribution` gains an eligibility filter and a generator-vs-model assertion block replacing the prose "compare these by eye" instruction at `:940-943`. | 1 |
| `docs/tools/envelope_check.py` | **Modify.** Add `check_mean_band_consistency` (Task 2) and `GENERATOR_BRANCHES` + `check_mirror_markers` (Task 3); both called from `main()`. | 2, 3 |
| `Source/DispersionModel.cs`, `Source/PawnVarianceSettings.cs`, `docs/tools/dispersion_mc.py` | **Modify.** Marker comments declaring which generator branches each mirrors. | 3 |

Tasks are independent and may be executed in any order, but the numbering is by value: Task 1 is the one that addresses the defect class both audit registers keep rediscovering.

---

### Task 1: Assert rolled pawns against the passion model

**Why this task exists.** Every automated check in this project compares a model against another model. `DebugActions.cs` states the consequence in its own comment: *"Cross-checking two mirrors cannot catch a branch both mirrors are missing."* Finding Q-14 is the proof — the model assumed `Faithful` delivers 5.001 pips, pawns actually receive ~4.55, and **the in-game measurement that proves it was already written down** in `HANDOVER.md:111-113` as `4.59` and read as evidence for something else. This task turns that comparison into an assertion.

The passion axis is chosen deliberately over the skill axis: the mod fully controls the passion budget, so model and generator are supposed to agree *exactly*. Skill levels are contaminated by vanilla's own base level distribution, which `AssumedVanillaSkillBaseline = 5f` only approximates — a skill-axis assertion would fail for reasons that are not defects.

**Files:**
- Modify: `Source/DispersionModel.cs` (add a method after `TypicalAt`, around `:232`)
- Modify: `Source/DebugActions.cs:794-949` (`DumpDistribution`)
- Test: no test framework exists in this project. Verification is (a) a Python oracle computing the same integral independently, and (b) an in-game run. Both are explicit steps below.

**Interfaces:**
- Consumes: `PassionSpend.Outcomes(float budget, float bias) -> PassionSpend.Outcome[]`, where `Outcome` has `readonly float Pips` and `readonly float Weight`; `VarianceProfileValues.GetBetaAlphaBeta(out float alpha, out float beta)`; `VarianceProfileValues.PassionNoiseScalar`; `Constants.PassionBudgetSpreadMin/Max`, `Constants.MinorPassionCost`, `Constants.MajorPassionCost`, `Constants.MaxPassionPips`, `Constants.VanillaAdultPassionAge`; `PawnVarianceSettings.ValuesFor(Pawn, PawnGenerationRequest)`; `PawnVarianceSettings.IsExcludedAsHostile(Pawn, PawnGenerationRequest)`.
- Produces: `public static void DispersionModel.ExpectedPassionPips(VarianceProfileValues v, out float mean, out float sd)` — mean and population sd of delivered passion pips per pawn, in pips. Returns `0, 0` for `null` or `enablePassionVariance == false`.

- [ ] **Step 1: Write the Python oracle that the C# must match**

This is the failing test. It computes the same integral using the shipped Python mirror, which is independent code from the C# being written. Create `zzz-Do-Not-Commit/oracle_expected_pips.py`:

```python
"""Independent oracle for DispersionModel.ExpectedPassionPips.

Computes E[delivered pips] and sd[delivered pips] per preset using the SHIPPED Python
mirror (make_spend, _gauss_nodes), so agreement with the C# is evidence and not a
tautology. Read-only: writes nothing.
"""
import math
import os
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(ROOT, "docs", "tools"))
os.chdir(ROOT)

import envelope_check as ec  # noqa: E402

C = ec.parse_constants(ec.read(ec.CONSTANTS))
P = ec.parse_profiles(ec.read(ec.PROFILES))
outcomes = ec.make_spend(C)
ZS, ZW = ec._gauss_nodes(C)

QN = 256  # must equal DispersionModel.QNodes
major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
n_skills = int(round(C["MaxPassionPips"] / major))


def expected_pips(p):
    eps, K = C["QualityClampEpsilon"], C["BetaConcentrationK"]
    m = min(max(p["averageQuality"], eps), 1.0 - eps)
    a, b = m * K, (1.0 - m) * K

    dq = 1.0 / QN
    wq, qs, total = [], [], 0.0
    for i in range(QN):
        q = (i + 0.5) * dq
        qs.append(q)
        w = math.exp((a - 1.0) * math.log(q) + (b - 1.0) * math.log(1.0 - q))
        wq.append(w)
        total += w * dq
    wq = [w * dq / total for w in wq]

    sig = p["passionSpread"]
    capacity = n_skills * (minor + (major - minor) * p["passionMajorBias"])
    m1 = m2 = 0.0
    for q, wqi in zip(qs, wq):
        bmean = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
        for z, wz in zip(ZS, ZW):
            bb = bmean + z * sig
            if bb < 1.0 and p["passionCountMin"] > 0.0:
                bb = 1.0
            if bb < 0.0:
                bb = 0.0
            for pips, pw in outcomes(bb, p["passionMajorBias"]):
                pips = min(pips, capacity)
                w = wqi * wz * pw
                m1 += w * pips
                m2 += w * pips * pips
    return m1, math.sqrt(max(0.0, m2 - m1 * m1))


print(f"{'profile':<12}{'E[pips]':>10}{'sd[pips]':>10}")
for name in sorted(P):
    mean, sd = expected_pips(P[name])
    print(f"{name:<12}{mean:>10.4f}{sd:>10.4f}")
```

- [ ] **Step 2: Run the oracle and record its output**

Run: `python zzz-Do-Not-Commit/oracle_expected_pips.py`

**This table was produced and validated on 2026-08-09 while writing this plan.** The oracle must
reproduce it exactly — it is the specification the C# in Step 3 is written against, not a rough
expectation:

| profile | `E[pips]` | `sd[pips]` | 4×SE at n=1000 |
|---|---|---|---|
| Desperate | 3.5731 | 1.1803 | 0.1493 |
| Distinct | 3.8542 | 1.6318 | 0.2064 |
| Elite | 5.1225 | 1.2064 | 0.1526 |
| **Faithful** | **4.5503** | 1.2339 | 0.1561 |
| Scavenger | 3.8533 | 1.1847 | 0.1499 |
| Sovereign | 5.1849 | 1.2688 | 0.1605 |
| Specialist | 4.6584 | 1.2334 | 0.1560 |
| Wildcard | 4.9681 | 2.3494 | 0.2972 |

`Faithful`'s **4.5503** is the number that matters, and it is corroborated three ways: finding
Q-14's independent 400k-pawn simulation put it at `4.552`, its re-measurement with a different seed
agreed, and the in-game 1000-pawn dump recorded in `HANDOVER.md:111-113` measured `4.59`. The
scoring model assumed `5.001`. **If the oracle prints ≈5.00 for `Faithful`, the spend loop is not
being applied** — stop and fix the oracle before writing any C#.

The right-hand column is why Step 6's tolerance is built the way it is: at n=1000 the sampling
band is 0.15–0.30 pips depending on the profile's spread, which is the same order as the 0.15-pip
floor covering model approximation. Neither term dominates, so both are needed.

**Paste this table into the commit message in Step 8.**

- [ ] **Step 3: Add `ExpectedPassionPips` to `Source/DispersionModel.cs`**

Insert immediately after `TypicalAt` (currently ends at `:232`):

```csharp
        // E[delivered passion pips] and sd, in PIPS -- not the normalised composite axis.
        //
        // This exists to be compared against ROLLED PAWNS (DebugActions' distribution dump), which
        // is a kind of check nothing else in this project performs: every other gate compares a
        // model against another model, and the defect class this project keeps rediscovering is the
        // generator doing something no model mirrors. Finding Q-14 is the canonical case -- the
        // score assumed 5.001 pips where pawns receive ~4.55, both mirrors agreed with each other,
        // and the in-game measurement proving it sat unread in HANDOVER.md for weeks.
        //
        // Deliberately NOT normalised: the dump measures pips, so predicting pips keeps the
        // comparison in units a human can check against the log by hand.
        //
        // MIRRORS THE PASSION BRANCH OF Moments ABOVE, minus the efficiency/MaxPassionPips
        // normalisation and the Clamp01. Do not let the two drift -- VerifyBestOfN asserts they
        // agree (invariant 3), which is what keeps this honest.
        public static void ExpectedPassionPips(VarianceProfileValues v, out float mean, out float sd)
        {
            mean = 0f;
            sd = 0f;
            if (v == null || !v.enablePassionVariance) return;

            EnsureNodes();
            v.GetBetaAlphaBeta(out float alpha, out float beta);

            float dq = 1f / QNodes;
            var qs = new float[QNodes];
            var wq = new float[QNodes];
            float total = 0f;
            for (int i = 0; i < QNodes; i++)
            {
                float q = (i + 0.5f) * dq;
                qs[i] = q;
                wq[i] = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                total += wq[i] * dq;
            }
            for (int i = 0; i < QNodes; i++) wq[i] = wq[i] * dq / total;

            float sig = Mathf.Lerp(Constants.PassionBudgetSpreadMin,
                                   Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
            int nSkills = Mathf.RoundToInt(Constants.MaxPassionPips / Constants.MajorPassionCost);
            float capacity = nSkills * (Constants.MinorPassionCost
                + (Constants.MajorPassionCost - Constants.MinorPassionCost) * v.passionMajorBias);

            float m1 = 0f, m2 = 0f;
            for (int i = 0; i < QNodes; i++)
            {
                float bmean = Mathf.Lerp(v.passionCountMin, v.passionCountMax, qs[i]);
                for (int g = 0; g < GaussNodes; g++)
                {
                    float b = bmean + gaussZ[g] * sig;
                    // Identical to the applier's floor and to Moments' -- see the note there.
                    if (b < 1f && v.passionCountMin > 0f) b = 1f;
                    if (b < 0f) b = 0f;

                    var outcomes = PassionSpend.Outcomes(b, v.passionMajorBias);
                    for (int k = 0; k < outcomes.Length; k++)
                    {
                        float pips = Mathf.Min(outcomes[k].Pips, capacity);
                        float w = wq[i] * gaussW[g] * outcomes[k].Weight;
                        m1 += w * pips;
                        m2 += w * pips * pips;
                    }
                }
            }

            mean = m1;
            sd = Mathf.Sqrt(Mathf.Max(0f, m2 - m1 * m1));
        }
```

- [ ] **Step 4: Add the C#-vs-oracle cross-check to the verify gate**

Without this, `ExpectedPassionPips` is a fifth mirror with nothing holding it in place — the exact
situation this plan exists to end. It proves the C# matches the oracle without a running game being
the only way to find out.

The comparison is made **at a single quality**, not integrated: `Moments` is conditional on `q`
while `ExpectedPassionPips` integrates `q` against the Beta density, so comparing them directly
would fail for a reason that is not a defect. `ExpectedPassionPipsAt` is the per-`q` form.

First add two helpers to `Source/DispersionModel.cs`, immediately after `ExpectedPassionPips`:

```csharp
        // Single-q version of ExpectedPassionPips: no Beta weighting, just the budget Gaussian and
        // the spend loop at one quality. Used by the verify gate to compare against Moments, which
        // is also a conditional-on-q quantity -- comparing the Beta-integrated figure against a
        // per-q one would fail for a reason that is not a defect.
        public static void ExpectedPassionPipsAt(VarianceProfileValues v, float q, out float mean)
        {
            mean = 0f;
            if (v == null || !v.enablePassionVariance) return;

            EnsureNodes();
            float sig = Mathf.Lerp(Constants.PassionBudgetSpreadMin,
                                   Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
            int nSkills = Mathf.RoundToInt(Constants.MaxPassionPips / Constants.MajorPassionCost);
            float capacity = nSkills * (Constants.MinorPassionCost
                + (Constants.MajorPassionCost - Constants.MinorPassionCost) * v.passionMajorBias);
            float bmean = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);

            float acc = 0f;
            for (int g = 0; g < GaussNodes; g++)
            {
                float b = bmean + gaussZ[g] * sig;
                if (b < 1f && v.passionCountMin > 0f) b = 1f;
                if (b < 0f) b = 0f;
                var outcomes = PassionSpend.Outcomes(b, v.passionMajorBias);
                for (int k = 0; k < outcomes.Length; k++)
                    acc += gaussW[g] * outcomes[k].Weight * Mathf.Min(outcomes[k].Pips, capacity);
            }
            mean = acc;
        }

        // The skill term Moments computes, exposed so the verify gate can subtract it back out of
        // mu and recover the passion term in isolation.
        public static float SkillTermAt(VarianceProfileValues v, float q)
        {
            EnsureNodes();
            float top = Constants.AssumedMaxSkillLevel;
            if (!v.enableSkillVariance)
                return Constants.AssumedVanillaSkillBaseline / top;

            float mag = Mathf.Lerp(Constants.MagnitudeLerpLow, Constants.MaxMagnitude,
                                   v.SkillNoiseScalar);
            float baseline = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, q);
            float s1 = 0f;
            for (int i = 0; i < TriNodes; i++)
            {
                float lvl = Mathf.Clamp(Constants.AssumedVanillaSkillBaseline
                                        + baseline + triT[i] * mag, 0f, top);
                s1 += triW[i] * (lvl / top);
            }
            return s1;
        }
```

Then in `Source/DebugActions.cs`, immediately before the `if (failures > 0)` block that closes `VerifyBestOfN`'s toggle-invariant section, add:

```csharp
            // Invariant 3: ExpectedPassionPips must agree with Moments' own passion branch.
            // The two integrate the same thing in different units -- recover Moments' passion term
            // by subtracting its skill term back out of mu, and it must equal the pip prediction
            // scaled by efficiency / MaxPassionPips. Without this the new prediction is a fifth
            // mirror with nothing holding it to the four that already exist.
            int pipFailures = 0;
            foreach (VarianceProfile preset in VarianceProfiles.Presets)
            {
                VarianceProfileValues pv = preset.MakeValues();
                float pipEff = PawnVarianceSettings.PassionPipEfficiency(pv.passionMajorBias);
                float wS = Constants.CompositeSkillWeight;
                float wP = Constants.CompositePassionWeight;

                DispersionModel.ExpectedPassionPipsAt(pv, 0.50f, out float pipAtHalf);
                DispersionModel.Moments(pv, 0.50f, out float muHalf, out _);
                float skillHalf = DispersionModel.SkillTermAt(pv, 0.50f);
                float passionHalf = ((wS + wP) * muHalf - wS * skillHalf) / wP;
                float expectHalf = pipAtHalf * pipEff / Constants.MaxPassionPips;

                // 1e-4 is loose by twelve orders of magnitude and deliberately so: the identity was
                // validated in the Python mirror on 2026-08-09 and holds to 3.89e-16 (worst of
                // eight presets, Wildcard). float32 in the C# widens that, but nowhere near 1e-4.
                // A failure here is a structural divergence, never accumulated rounding.
                if (Mathf.Abs(passionHalf - expectHalf) > 1e-4f)
                {
                    sb.AppendLine($"  {preset.label,-12} pip/moment mismatch at q=0.50: "
                        + $"moments {passionHalf:F6} vs pips {expectHalf:F6}  *** PIP MISMATCH ***");
                    pipFailures++;
                }
            }
            failures += pipFailures;
            if (pipFailures == 0)
                sb.AppendLine("  pip prediction matches Moments' passion term on all presets at q=0.50.");
```

- [ ] **Step 5: Build and confirm 0 warnings**

Run: `dotnet build Source/PawnVarianceMod.csproj -v minimal`
Expected: `Build succeeded.` / `0 Warning(s)` / `0 Error(s)`.

If `triT`/`triW`/`gaussZ`/`gaussW` are reported inaccessible, they are `private static` fields of `DispersionModel` and the new methods are in the same class — check the methods were pasted inside the class body, not after its closing brace.

Then confirm the C# reproduces the Step 2 table. There is no C# test runner, so use the in-game
`Verify Best-of-N` action's new invariant-3 line as the proxy — it compares the two C# paths against
each other and is checked in Task 1's in-game step. The offline evidence that the *values* are right
is the Python oracle: both implementations were written from the same specification and the identity
in Step 4 pins them together.

- [ ] **Step 6: Replace the eyeball instruction in the dump with an assertion**

In `Source/DebugActions.cs`, inside `DumpDistribution`'s generation loop, replace this existing line:

```csharp
                        string label = settings.ValuesFor(pawn, request)?.profileLabel ?? "(null)";
```

with:

```csharp
                        VarianceProfileValues pawnValues = settings.ValuesFor(pawn, request);
                        string label = pawnValues?.profileLabel ?? "(null)";
```

Add these declarations beside the existing `var passionPips = new List<float>();`:

```csharp
            // Pips for ONLY the pawns the passion model actually describes. The model predicts what
            // PassionVarianceApplier delivers, so a pawn the applier never touched must not be
            // averaged into the comparison: HarmonyPatches skips pawns under VanillaAdultPassionAge
            // (vanilla gives them no budget at all), hostile-excluded pawns, and any profile with
            // the passion axis switched off. Including them would drag the observed mean down and
            // read as a model defect.
            var modelPips = new List<float>();
            VarianceProfileValues modelValues = null;
```

Immediately after the existing `passionPips.Add(pips);` line, add:

```csharp
                        bool adultForPassions = pawn.ageTracker == null
                            || pawn.ageTracker.AgeBiologicalYears >= Constants.VanillaAdultPassionAge;
                        if (adultForPassions
                            && pawnValues != null
                            && pawnValues.enablePassionVariance
                            && !settings.IsExcludedAsHostile(pawn, request))
                        {
                            modelPips.Add(pips);
                            modelValues = pawnValues;
                        }
```

Then replace the four prose lines at the end of the method:

```csharp
            sb.AppendLine("  Compare 'per-skill level' sd against the 'per-skill sd' column in");
            sb.AppendLine("  `python docs/tools/envelope_check.py`. Observed should exceed the");
            sb.AppendLine("  predicted figure — the tool models noise only, this also carries the");
            sb.AppendLine("  spread of the quality roll itself.");
```

with:

```csharp
            // GENERATOR vs MODEL. This is the only check in the project that compares rolled pawns
            // against the scoring model; everything else compares one model to another, which
            // cannot catch a branch both models are missing (see the note on VerifyBestOfN).
            //
            // The passion axis, not the skill axis, and that is deliberate: the mod supplies the
            // whole passion budget, so model and generator are meant to agree EXACTLY. Skill levels
            // are built on vanilla's own base distribution, which AssumedVanillaSkillBaseline only
            // approximates -- asserting on those would fail for reasons that are not defects. The
            // per-skill sd comparison this replaces stays available by eye in the table above.
            sb.AppendLine();
            if (byCount.Count > 1)
            {
                sb.AppendLine("  model check SKIPPED: mixed sample, so there is no single profile "
                    + "to predict from.");
            }
            else if (modelPips.Count == 0 || modelValues == null)
            {
                sb.AppendLine("  model check SKIPPED: no pawn in this sample was eligible for "
                    + "rolled passions (all under age " + Constants.VanillaAdultPassionAge
                    + ", hostile-excluded, or passion variance off).");
            }
            else
            {
                DispersionModel.ExpectedPassionPips(modelValues, out float predMean, out float predSd);
                float obsMean = modelPips.Average();

                // Tolerance is DERIVED from the sample, not a hardcoded pip count: the standard
                // error shrinks as sqrt(n), so a fixed threshold would either false-fail at n=50 or
                // sleep through real drift at n=1000. Four standard errors is ~1-in-16000 per run.
                // The 0.15-pip floor covers what the model knowingly does not represent: capacity
                // assumes MaxPassionPips/MajorPassionCost eligible skills, while real pawns lose
                // eligibility to conflicting traits and DropAll genes, so the observed mean sits
                // slightly BELOW the prediction for budgets near capacity.
                float se = predSd / Mathf.Sqrt(modelPips.Count);
                float tol = Mathf.Max(4f * se, 0.15f);
                float delta = obsMean - predMean;

                sb.AppendLine($"  GENERATOR vs MODEL -- passion pips, {modelPips.Count} eligible pawns");
                sb.AppendLine($"    model predicts {predMean:F3} pips/pawn (sd {predSd:F3})");
                sb.AppendLine($"    pawns delivered {obsMean:F3} pips/pawn");
                sb.AppendLine($"    delta {delta:+0.000;-0.000} against tolerance {tol:F3} "
                    + $"(4 x SE {se:F3}, floored at 0.150)");
                if (Mathf.Abs(delta) > tol)
                {
                    sb.AppendLine("    *** MODEL/GENERATOR MISMATCH *** the score is describing a "
                        + "pawn the generator does not roll.");
                    sb.AppendLine("    This is the shape of audit findings Q-01, Q-04 and Q-14. "
                        + "Check which generator branch has no mirror before adjusting anything.");
                }
                else
                {
                    sb.AppendLine("    OK -- the model describes the pawns being rolled.");
                }
            }
```

- [ ] **Step 7: Build and re-run the offline gate**

Run: `dotnet build Source/PawnVarianceMod.csproj -v minimal`
Expected: `Build succeeded.` / `0 Warning(s)`.

Run: `python docs/tools/envelope_check.py`
Expected: `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.`

If the golden file reports `REWRITTEN`, a shipped figure moved — that is a bug in this task, not a reason to commit the new file. `ExpectedPassionPips` is additive and reads nothing the scoring path writes.

- [ ] **Step 8: Commit**

```bash
git add Source/DispersionModel.cs Source/DebugActions.cs
git commit -m "feat: assert rolled pawns against the passion model

The distribution dump measured delivered pips and then told a human to
compare them by eye. It now predicts them via DispersionModel.ExpectedPassionPips
and fails loudly on a mismatch.

This is the first check in the project that compares the generator against a
model rather than one model against another -- the class of defect that
cross-checking two mirrors structurally cannot catch (findings Q-01, Q-04, Q-14).
Q-14's evidence was an in-game measurement already sitting in HANDOVER.md,
read as confirming something else.

Oracle figures the C# was written against (zzz-Do-Not-Commit/oracle_expected_pips.py):
<paste the eight-row table from Step 2 here>

No shipped figure moves; EnvelopeFigures.g.cs unchanged.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

- [ ] **Step 9: Record the in-game verification as outstanding**

This task's runtime half **cannot be verified offline**. Append to `HANDOVER.md`'s in-game verification list:

```markdown
- [ ] **`Roll pawns and dump distribution` at 1000 pawns, on `Faithful`, with no overrides
  active.** Expected: `GENERATOR vs MODEL` reports `model predicts ~4.55` and
  `pawns delivered` within tolerance. A delta beyond tolerance on a preset means a
  generator branch has no mirror. **This has never been run** — until it is, the
  assertion is argued, not measured.
```

Run: `git add HANDOVER.md && git commit -m "docs: record the outstanding in-game run for the generator/model assertion"`

---

### Task 2: Promote the zero-spread invariant into the shipped gate

**Why this task exists.** The check that proves finding Q-04's fix — at zero spread, `TypicalAt` and the mean-band composite must agree exactly, which is only true if both apply the vanilla passion floor — currently lives in `zzz-Do-Not-Commit/` and will be lost. `zzz-Do-Not-Commit/` already holds six `test_*.py` files from previous sessions. The pattern of writing a verification, running it once and discarding it is why Q-04 survived the 2026-08-08 floor fix that was supposed to close its whole class.

The existing self-check does **not** cover this. It compares `grid_score(p, with_noise=False)` against `expected_best_of_n(...)` — an *integrated* Best-of-N quantity at `1e-3` tolerance, whose `4.10e-04` residual is large enough to mask a pointwise defect. And it runs only on the eight presets, none of which has `passionCountMin < 1`, so **the floor branch is unreachable from it**. A pointwise check on a synthetic low-budget profile is a genuinely different test.

**Files:**
- Modify: `docs/tools/envelope_check.py` (add a function before `main()`, call it from `main()`)

**Interfaces:**
- Consumes: `make_composite(C) -> composite(q, p)`; `grid_moments(C) -> moments(p, q, with_noise=True)`; `parse_constants`, `parse_profiles`; module constant `FIELDS`.
- Produces: `check_mean_band_consistency(C, P) -> float` returning the worst absolute deviation found; `main()` returns `1` if it exceeds `1e-9`.

- [ ] **Step 1: Write the check, and confirm it fails against the pre-fix behaviour**

Add to `docs/tools/envelope_check.py`, immediately before `def main():`:

```python
def check_mean_band_consistency(C, P):
    """At zero spread the dispersion model must reduce to the mean-band composite EXACTLY.

    grid_moments with with_noise=False sets both spread terms to zero, so every integral it
    performs collapses onto the mean band -- which is precisely what make_composite computes.
    The two therefore have to agree to floating-point noise for EVERY profile at EVERY quality,
    and they can only do so if both apply the same floor, the same spend loop, the same capacity
    limit and the same clamps. It is a single equality that pins four separate correspondences.

    WHY THIS IS NOT COVERED BY main()'s EXISTING SELF-CHECK. That one compares an INTEGRATED
    Best-of-N figure at 1e-3, with a standing 4.1e-04 residual of quadrature error that would
    comfortably hide a pointwise defect. More importantly it runs on the eight presets only, and
    no preset has passionCountMin below 2.2 -- so vanilla's floor branch is unreachable from it.
    Finding Q-04 lived in exactly that gap: the floor was mirrored into four sites, missed in the
    fifth, and no gate could see the difference. The probes below are what make it reachable.
    """
    composite = make_composite(C)
    moments = grid_moments(C)

    probes = dict(P)

    # Budget band entirely under one pip, with passionCountMin > 0: the floor MUST engage.
    # Built from Faithful so every unrelated field is a real shipped value.
    floor_probe = dict(P["Faithful"])
    floor_probe["passionCountMin"] = 0.2
    floor_probe["passionCountMax"] = 0.8
    probes["_probe-floor"] = floor_probe

    # passionCountMin == 0 is an explicit request for passionless pawns (Desperate's shape), and
    # the floor must NOT engage. Same band otherwise, so a fix that floors unconditionally passes
    # the probe above and fails this one.
    open_probe = dict(floor_probe)
    open_probe["passionCountMin"] = 0.0
    probes["_probe-no-floor"] = open_probe

    # Both axes off: the fallback branches, which have their own floor and their own spend loop.
    off_probe = dict(P["Faithful"])
    off_probe["enableSkillVariance"] = False
    off_probe["enablePassionVariance"] = False
    probes["_probe-both-off"] = off_probe

    worst, worst_where = 0.0, ""
    for name, p in sorted(probes.items()):
        for i in range(41):
            q = i / 40.0
            mu, _ = moments(p, q, with_noise=False)
            gap = abs(mu - composite(q, p))
            if gap > worst:
                worst, worst_where = gap, f"{name} @ q={q:.3f}"

    print(f"mean-band consistency (zero spread, pointwise): {worst:.2e}  worst at {worst_where}")
    return worst
```

Then in `main()`, immediately after the existing `dispersion model self-check` block that ends with `return 1`, add:

```python
    worst_meanband = check_mean_band_consistency(C, P)
    if worst_meanband > 1e-9:
        print("FAIL: the dispersion model and the mean-band composite disagree at zero spread.")
        print("      They integrate the same thing there, so a gap means one of them is missing")
        print("      a branch the other has -- the floor, the spend loop, capacity or a clamp.")
        return 1
```

- [ ] **Step 2: Prove the check has teeth by breaking the thing it guards**

A check that has never failed is not known to work. Temporarily remove the floor from `make_composite`'s `passion_from` — change:

```python
        if budget < 1.0 and floor_to_one:
            budget = 1.0
```

to:

```python
        if False and budget < 1.0 and floor_to_one:
            budget = 1.0
```

Run: `python docs/tools/envelope_check.py`

Expected: `FAIL: the dispersion model and the mean-band composite disagree at zero spread.` with the worst deviation reported at `_probe-floor` and a magnitude around `3e-2`. If it still passes, the probes are not reaching the floor branch and the check is worthless — fix the probes before continuing.

- [ ] **Step 3: Restore the floor and confirm the check passes**

Revert the `if False and` edit back to `if budget < 1.0 and floor_to_one:`.

Run: `python docs/tools/envelope_check.py`

Expected:
- `mean-band consistency (zero spread, pointwise): ` a value below `1e-9` (measured `1.4e-16` on 2026-08-09)
- `Source/EnvelopeFigures.g.cs: unchanged.`
- `PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.`

- [ ] **Step 4: Commit**

```bash
git add docs/tools/envelope_check.py
git commit -m "test: pin the zero-spread mean-band equality in the shipped gate

At zero spread the dispersion model and the mean-band composite integrate the
same thing, so they must agree pointwise -- an equality that pins the floor, the
spend loop, the capacity limit and the clamps in one assertion.

The existing self-check could not see this: it compares an INTEGRATED Best-of-N
figure at 1e-3 with a standing 4.1e-04 residual, and it runs only on presets,
none of which has passionCountMin < 1. Finding Q-04 lived in that gap. Synthetic
probes make the floor branch reachable, including a no-floor probe so an
unconditional floor fails too.

Verified to have teeth by removing the floor from make_composite and watching it
fail at ~3e-2 on the floor probe.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Make the generator-branch/mirror checklist executable

**Why this task exists.** Findings Q-01 and Q-14 both conclude that the missing artifact is *"a checklist of every branch in the generator and which model sites mirror it"*. `HANDOVER.md` already carries the warning in prose, and the warning did not prevent the next two instances — which is the argument that prose is the wrong medium. Q-02 is the same lesson about citations: unenforced documentation drifts.

This makes the checklist a table the tool enforces. Each generator branch names the sites that must mirror it; each named site must carry a `MIRRORS: <branch-id>` marker comment. Adding a generator branch without declaring its mirrors fails the build gate.

**Files:**
- Modify: `docs/tools/envelope_check.py` (add the table and check, call from `main()`)
- Modify: `Source/DispersionModel.cs`, `Source/PawnVarianceSettings.cs`, `docs/tools/dispersion_mc.py` (add marker comments)

**Interfaces:**
- Consumes: `read(path)` from this module.
- Produces: module constant `GENERATOR_BRANCHES` (tuple of `(branch_id, generator_site, (mirror_paths...))`); `check_mirror_markers() -> int` returning the number of missing markers; `main()` returns `1` if non-zero.

- [ ] **Step 1: Add the table and the checker**

Add to `docs/tools/envelope_check.py`, immediately after the `GEN_CONSTANTS` block:

```python
# EVERY BRANCH IN THE GENERATOR THAT SHAPES THE OUTCOME, AND WHICH MODEL SITES MIRROR IT.
#
# This is the artifact findings Q-01 and Q-14 both concluded was missing. Their shared shape:
# the generator does something no model mirrors, the models agree with EACH OTHER, and "both
# implementations agree" gets read as "the metric is right". Three separate defects had it --
# the enable toggles (mirrored in 0 sites), the spend-loop discretization (0), and the vanilla
# passion floor (4 of 5).
#
# HANDOVER.md already warned about this in prose and the warning did not prevent the next two
# instances, which is the argument for making it mechanical. Each mirror file must carry a
# comment `MIRRORS: <branch-id>`. Adding a generator branch without declaring its mirrors, or
# deleting a mirror's implementation along with its marker, fails this tool.
#
# The marker proves a DECLARATION, not a correct implementation -- that is what the numeric
# checks are for. What it prevents is the branch nobody remembered to mirror at all, which is
# the one that has actually bitten, three times.
MIRROR_SITES = ("Source/DispersionModel.cs",
                "Source/PawnVarianceSettings.cs",
                "docs/tools/envelope_check.py",
                "docs/tools/dispersion_mc.py")

GENERATOR_BRANCHES = (
    # (branch id, where the generator does it, which sites must mirror it)
    ("passion-floor", "PassionVarianceApplier.cs:76-77", MIRROR_SITES),
    ("passion-spend-loop", "PassionVarianceApplier.cs:81-93", MIRROR_SITES),
    ("passion-capacity", "PassionVarianceApplier assignment (one passion per eligible skill)",
     MIRROR_SITES),
    ("enable-toggles", "HarmonyPatches.cs:58-60, GrowUpVariance.cs:68-69", MIRROR_SITES),
    ("skill-clamp", "SkillVarianceApplier.cs:73 (RoundToInt then Clamp 0..20)", MIRROR_SITES),
)


def check_mirror_markers():
    """Every model site must DECLARE which generator branches it mirrors.

    Paths in GENERATOR_BRANCHES are repo-relative with forward slashes; they are resolved
    against ROOT the same way CONSTANTS and PROFILES are (module top), so this works from any
    working directory and on Windows.
    """
    missing = []
    for branch, generator_site, sites in GENERATOR_BRANCHES:
        marker = f"MIRRORS: {branch}"
        for path in sites:
            full = os.path.join(ROOT, *path.split("/"))
            try:
                src = read(full)
            except OSError:
                missing.append(f"{path} (unreadable) for branch '{branch}'")
                continue
            if marker not in src:
                missing.append(f"{path} is missing `{marker}`  "
                               f"(generator: {generator_site})")
    if missing:
        print("FAIL: generator branches without a declared mirror:")
        for m in missing:
            print(f"  {m}")
    else:
        print(f"generator/mirror checklist: {len(GENERATOR_BRANCHES)} branches x "
              f"{len(MIRROR_SITES)} sites, all declared")
    return len(missing)
```

Add the call in `main()`, immediately after `check_mean_band_consistency`'s block:

```python
    if check_mirror_markers() > 0:
        return 1
```

- [ ] **Step 2: Run it and watch every row fail**

Run: `python docs/tools/envelope_check.py`

Expected: `FAIL: generator branches without a declared mirror:` followed by **20 lines** (5 branches × 4 sites). This is the correct starting state — no marker exists yet.

- [ ] **Step 3: Add the markers to `Source/DispersionModel.cs`**

Add to the class-level comment block at the top of `DispersionModel`, after the existing `IF YOU CHANGE ONE, CHANGE BOTH` paragraph:

```csharp
    // MIRRORS: passion-floor
    // MIRRORS: passion-spend-loop
    // MIRRORS: passion-capacity
    // MIRRORS: enable-toggles
    // MIRRORS: skill-clamp
    //
    // Those five lines are enforced by docs/tools/envelope_check.py's GENERATOR_BRANCHES table.
    // They declare which branches of the GENERATOR this file reproduces. Removing an
    // implementation without removing its line, or adding a generator branch without adding a
    // line here, fails the tool. See that table for why prose warnings were not enough.
```

- [ ] **Step 4: Add the markers to `Source/PawnVarianceSettings.cs`**

Add immediately above `private static float PassionNormFor(...)`, inside the existing comment block:

```csharp
        // MIRRORS: passion-floor
        // MIRRORS: passion-spend-loop
        // MIRRORS: passion-capacity
        // MIRRORS: enable-toggles
        // MIRRORS: skill-clamp
```

- [ ] **Step 5: Add the markers to `docs/tools/dispersion_mc.py`**

Add to the module docstring:

```python
MIRRORS: passion-floor
MIRRORS: passion-spend-loop
MIRRORS: passion-capacity
MIRRORS: enable-toggles
MIRRORS: skill-clamp

Enforced by envelope_check.py's GENERATOR_BRANCHES table. This file mirrors the generator by
SAMPLING rather than by quadrature, which is the point of it -- but sampling the wrong model is
still the wrong model, so it carries the same declarations as the analytic sides.
```

- [ ] **Step 6: Add the markers to `docs/tools/envelope_check.py` itself**

Add to the module docstring at the top of the file:

```python
MIRRORS: passion-floor
MIRRORS: passion-spend-loop
MIRRORS: passion-capacity
MIRRORS: enable-toggles
MIRRORS: skill-clamp
```

Note this file is both a mirror and the enforcer. That is intentional — it is a model side like any other, and exempting it would leave the gap in the one file most likely to be edited when a branch changes.

- [ ] **Step 7: Run and confirm the checklist passes**

Run: `python docs/tools/envelope_check.py`

Expected:
- `generator/mirror checklist: 5 branches x 4 sites, all declared`
- `Source/EnvelopeFigures.g.cs: unchanged.`
- `PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.`

- [ ] **Step 8: Prove the checker has teeth**

Delete the `// MIRRORS: passion-floor` line from `Source/DispersionModel.cs`.

Run: `python docs/tools/envelope_check.py`
Expected: `FAIL` naming `Source/DispersionModel.cs is missing \`MIRRORS: passion-floor\``.

Restore the line and re-run to confirm PASS.

- [ ] **Step 9: Commit**

```bash
git add docs/tools/envelope_check.py docs/tools/dispersion_mc.py Source/DispersionModel.cs Source/PawnVarianceSettings.cs
git commit -m "test: enforce the generator-branch/mirror checklist mechanically

Findings Q-01 and Q-14 both concluded the missing artifact was a checklist of
every generator branch and which model sites mirror it. HANDOVER.md carried that
warning in prose and it did not prevent the next two instances.

GENERATOR_BRANCHES lists five branches; each model site must carry a
`MIRRORS: <id>` marker. Adding a generator branch without declaring its mirrors
fails the tool. The marker proves a declaration, not a correct implementation --
the numeric checks cover correctness; this covers the branch nobody mirrored at
all, which is the failure that has actually happened three times.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Verification Summary

After all three tasks:

| Check | Command | Expected |
|---|---|---|
| Build | `dotnet build Source/PawnVarianceMod.csproj -v minimal` | `Build succeeded.` 0 warnings, 0 errors |
| Offline gate | `python docs/tools/envelope_check.py` | `mean-band consistency … < 1e-9`; `generator/mirror checklist: 5 branches x 4 sites, all declared`; `EnvelopeFigures.g.cs: unchanged.`; `PASS` |
| Independent MC | `python docs/tools/dispersion_mc.py` | reproduces all 32 cells to ≤ 0.0004 |
| **In game (outstanding)** | `Verify Best-of-N against envelope_check.py` | 32/32, plus `pip prediction matches Moments' passion term on all presets` |
| **In game (outstanding)** | `Roll pawns and dump distribution`, 1000 pawns, `Faithful` | `GENERATOR vs MODEL` reports `OK`, prediction ≈4.55 pips |

**The two in-game rows have never been run**, and that is the honest state of this plan on completion: Tasks 1–3 are verified offline; Task 1's whole purpose is a runtime comparison that only a running game produces. Do not describe this work as finished until those two rows are green.

---

## Out of Scope

- **A C# test project.** Only `Constants.cs`, `EnvelopeFigures.g.cs` and `PassionSpend.cs` are free of `Verse`/`UnityEngine`, so the reachable surface today is one class already covered by the Python mirror and the Monte Carlo. Revisit if the growth-path state machine (`GrowthUpPatch`'s retry logic, currently untestable — it takes a `Pawn`) is ever extracted.
- **Tightening `RawToleranceRelPct` from 3.0.** Finding Q-09 established it is now ~4 orders of magnitude looser than needed, but the gate has never been run against a running build, so the expected gap is predicted rather than measured. Tighten it after the in-game rows above are green, not before.
- **Asserting the skill axis against rolled pawns.** Blocked on `AssumedVanillaSkillBaseline` being an assumption about vanilla's base level distribution rather than a measurement of it. Measuring it is its own piece of work.
- **Findings Q-07 and Q-08**, deferred by the owner.
