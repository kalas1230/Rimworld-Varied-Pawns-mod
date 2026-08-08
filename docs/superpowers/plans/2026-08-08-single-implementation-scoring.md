# Single-Implementation Scoring Implementation Plan

> [!CAUTION]
> # ⛔ SHELVED — REJECTED 2026-08-08, BEFORE ANY CODE WAS WRITTEN
>
> **Do not execute this plan. It is kept as a record of a decision, not as queued work.**
>
> **Why it was rejected.** The plan's premise is that two implementations of one integral (the C#
> `DispersionModel` and `docs/tools/envelope_check.py`) are a liability to be eliminated. Checked
> against this project's actual defect history, that premise is wrong:
>
> - The mirror has **caught two real defects** — both Best-of-N integrator bugs, which survived
>   clean builds and static review and were caught by the in-game `Verify Best-of-N` action, i.e.
>   by the two sides disagreeing.
> - The mirror has **caused zero.** Every other defect this project shipped — the sig-gated passion
>   floor (`52602f7`), the `Wildcard` band retune, both "24-pip era" recurrences — was *both sides
>   jointly wrong relative to the game*, which collapsing to one implementation does nothing about.
>
> **Mirror drift is the one failure mode that has never happened here.** Phase 1 would retire a
> defence with a proven record to eliminate a risk that has never materialised.
>
> **Phase 1 also would not have caught the bug that prompted this plan** — that was scoring-model
> vs pawn-generator, genuinely different code. This was stated twice in the plan below and remains
> true; it is what makes Phase 2 the only part with real value, and Phase 2 depends on Phase 1's
> harness, so it cannot be salvaged on its own.
>
> **What to do instead.** Aim at the model-vs-generator gap: an in-game debug action that rolls N
> pawns and prints their realised mean composite beside `DispersionModel`'s prediction. Directly
> measures the quantity that has failed four times, costs one debug action rather than nine tasks,
> and touches no Rule 8 file.
>
> **Worth keeping from this document regardless:** the `Mathf`-semantics trap table under "The risk
> this plan exists to manage" (`Mathf.Lerp` clamps `t`; `Mathf.RoundToInt` is banker's rounding;
> `Mathf.Exp` and friends round back to `float` immediately). Anyone writing float code that must
> agree with Unity's should read it.
>
> Full reasoning: `HANDOVER.md` → "Settled and not to be relitigated" → "Why the C#/Python
> duplication STAYS".

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Retire the C#/Python mirror by making the offline envelope gate run the *same source* as the shipped mod, then extend the same treatment to the pawn generator so the Monte Carlo exercises real applier code instead of a copy of it.

**Architecture:** A new platform-free source folder `Source/Scoring/` holds the scoring math with zero `UnityEngine` and zero `Verse` references. Both the mod csproj and a new C# console harness compile those *same files* via `<Compile Include>` links — no second DLL ships, so there is no assembly-loading risk in RimWorld. `docs/tools/envelope_check.py` is ported to that harness and deleted once the harness reproduces its stdout byte-for-byte. Phase 2 extracts the appliers' decision math into the same folder behind an injectable RNG, so `dispersion_mc.py`'s successor calls the real budget logic rather than mirroring it.

**Tech Stack:** C# 9, net472, .NET SDK. No new NuGet packages. Python 3 remains installed only until Task 5 and Task 8 delete the two tools.

## Global Constraints

- **Rule 8 sign-off is REQUIRED before Task 2 and again before Task 6.** This plan modifies
  `Source/VarianceProfile.cs`, `Source/PawnVarianceSettings.cs`, `Source/SkillVarianceApplier.cs`
  and `Source/PassionVarianceApplier.cs`, all marked protected. **`Source/SettingsTransfer.cs` is
  NOT touched by any task** — if a task appears to need it, stop and ask the owner.
- **Rule 5 consultation:** no task may change any scoring constant, preset value, weight, or
  profile parameter. This is a pure refactor. **Every numeric literal that exists today must
  survive with the same value.** If a task seems to require a number to change, that is a defect
  in the task — stop and report it.
- **The binding gate for the whole plan is `Source/EnvelopeFigures.g.cs: unchanged`.** It stores
  six decimals, so `unchanged` asserts agreement to 1e-6 against the pre-refactor figures. Any
  task that rewrites it has changed behaviour and has failed.
- **There is no unit-test project and one must NOT be created.** The plan's verification is the
  byte-identical stdout comparison in Task 4 plus the existing gates. Do not fabricate unit tests
  to satisfy a TDD rubric. (Task 4's diff *is* the test, and it is a far stronger one.)
- **`Source/EnvelopeFigures.g.cs` is generated and never hand-edited.**
- **Shell: Git Bash, not PowerShell.** Every shell block below is POSIX.
- **Branch `main`**, commit per task, nothing pushed.
- **In-game verification is deferred by owner instruction (2026-08-08)** and is not a per-task
  blocker. Task 9 records it as the one gate left open.

---

# The risk this plan exists to manage

`Source/DispersionModel.cs` and `Source/MathUtil.cs` currently call `UnityEngine.Mathf`. Two of
those functions do **not** mean what `System.Math` means:

| Unity | Behaviour | Naive shim | Consequence if shimmed naively |
|---|---|---|---|
| `Mathf.Lerp(a,b,t)` | **Clamps `t` to [0,1]** | `a+(b-a)*t` extrapolates | Every out-of-band quality or spread silently extrapolates |
| `Mathf.RoundToInt(f)` | `(int)Math.Round(f)` — **banker's rounding**, 2.5→2 | `(int)(f+0.5)` | Off-by-one on exact .5 boundaries |
| `Mathf.Clamp01(f)` | clamps to [0,1] | — | direct equivalent |
| `Mathf.Pow/Exp/Log/Sqrt/Abs/Min/Max` | float overloads of `System.Math` | — | direct equivalents, but must cast to `float` at every step, not accumulate in `double` |

**The float-vs-double point is the subtle one.** `Mathf.Exp(x)` is
`(float)Math.Exp((double)x)` — it rounds back to `float` immediately. A shim that returns `double`
or that lets an expression stay `double` longer than Unity does will produce different last-digit
results, which `EnvelopeFigures.g.cs`'s six decimals may or may not catch. Every shim function
below returns `float` and rounds at exactly the same points Unity does.

---

# File Structure

**Created:**
- `Source/Scoring/PlatformMath.cs` — the `Mathf` replacement. Platform-free.
- `Source/Scoring/ScoringInputs.cs` — the ~8-field POCO the scoring math consumes. Platform-free.
- `Source/Scoring/ScoringPresets.cs` — the eight presets' *scoring* numbers, single source of truth.
- `Source/Scoring/CompositeScore.cs` — `PassionPipEfficiency` + `CalculateCompositeScore`, moved out of `PawnVarianceSettings.cs`.
- `Source/Scoring/PassionBudget.cs` (Phase 2) — the passion budget decision math.
- `Source/Scoring/SkillShift.cs` (Phase 2) — the skill shift decision math.
- `Source/Scoring/IRandomSource.cs` (Phase 2) — RNG seam.
- `docs/tools/EnvelopeCheck/EnvelopeCheck.csproj` — console harness, links `Source/Scoring/*.cs`.
- `docs/tools/EnvelopeCheck/Program.cs` — the port of `envelope_check.py`.
- `docs/tools/EnvelopeCheck/MonteCarlo.cs` (Phase 2) — the port of `dispersion_mc.py`.

**Moved into `Source/Scoring/` (git mv, then de-Unity'd):**
- `Source/DispersionModel.cs` → `Source/Scoring/DispersionModel.cs`
- `Source/MathUtil.cs` → `Source/Scoring/MathUtil.cs`
- `Source/Constants.cs` → `Source/Scoring/Constants.cs`

**Modified:**
- `Source/PawnVarianceMod.csproj` — nothing needed; the SDK globs `Source/**/*.cs` already. Verify.
- `Source/VarianceProfile.cs` — `VarianceProfileValues.ToScoring()`; presets built from `ScoringPresets`.
- `Source/PawnVarianceSettings.cs` — scoring functions delegate to `Source/Scoring/`.
- `Source/PassionVarianceApplier.cs`, `Source/SkillVarianceApplier.cs` — Phase 2 only.

**Deleted:**
- `docs/tools/envelope_check.py` (Task 5)
- `docs/tools/dispersion_mc.py` (Task 8)

---

# PHASE 1 — One implementation of the scoring math

### Task 1: The platform-free math shim

**Files:**
- Create: `Source/Scoring/PlatformMath.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `static class PlatformMath` with `Lerp`, `Clamp`, `Clamp01`, `Abs`, `Min`, `Max`, `Exp`, `Log`, `Pow`, `Sqrt`, `FloorToInt`, `RoundToInt`, and `const float PI`. All `float` in, `float` (or `int`) out. Tasks 2–8 use these instead of `Mathf`.

- [ ] **Step 1: Create the shim**

Create `Source/Scoring/PlatformMath.cs`:

```csharp
using System;

namespace PawnVarianceMod
{
    // Replaces UnityEngine.Mathf for code that must also compile outside RimWorld (the offline
    // envelope harness). EVERY function here must match Unity's semantics EXACTLY -- this file
    // existing is what lets the harness and the mod be one implementation instead of two, so a
    // discrepancy here reintroduces the whole class of bug the split was removing.
    //
    // Two of these are NOT the obvious System.Math call. See the table in
    // docs/superpowers/plans/2026-08-08-single-implementation-scoring.md.
    internal static class PlatformMath
    {
        public const float PI = 3.14159274f;   // Unity's Mathf.PI, float-rounded

        // Unity's Mathf.Lerp CLAMPS t to [0,1]. Mathf.LerpUnclamped is the extrapolating one.
        // Getting this wrong changes every out-of-band quality and spread silently.
        public static float Lerp(float a, float b, float t) => a + (b - a) * Clamp01(t);

        public static float Clamp(float v, float lo, float hi) => v < lo ? lo : (v > hi ? hi : v);
        public static float Clamp01(float v) => v < 0f ? 0f : (v > 1f ? 1f : v);
        public static float Abs(float v) => Math.Abs(v);
        public static float Min(float a, float b) => a < b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;

        // Unity rounds back to float immediately; do not let these stay double.
        public static float Exp(float v) => (float)Math.Exp(v);
        public static float Log(float v) => (float)Math.Log(v);
        public static float Pow(float a, float b) => (float)Math.Pow(a, b);
        public static float Sqrt(float v) => (float)Math.Sqrt(v);

        public static int FloorToInt(float v) => (int)Math.Floor(v);

        // Unity's Mathf.RoundToInt is (int)Math.Round(f), which is BANKER'S rounding:
        // 2.5 -> 2, 3.5 -> 4. NOT (int)(f + 0.5f).
        public static int RoundToInt(float v) => (int)Math.Round(v);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`. Nothing consumes the shim yet; this only proves it compiles under net472.

- [ ] **Step 3: Confirm the SDK picks up the new folder**

Run: `dotnet build Source/PawnVarianceMod.csproj -v:n 2>&1 | grep -c 'PlatformMath.cs'`
Expected: a count of `1` or more. If `0`, the csproj is not globbing `Source/**`; add
`<Compile Include="Scoring\**\*.cs" />` to an `ItemGroup` and re-run.

- [ ] **Step 4: Commit**

```bash
git add Source/Scoring/PlatformMath.cs
git commit -m "refactor: add a platform-free Mathf replacement for the scoring math"
```

---

### Task 2: Move the scoring math off UnityEngine

**Files:**
- Move: `Source/Constants.cs` → `Source/Scoring/Constants.cs`
- Move: `Source/MathUtil.cs` → `Source/Scoring/MathUtil.cs`
- Move: `Source/DispersionModel.cs` → `Source/Scoring/DispersionModel.cs`
- Create: `Source/Scoring/ScoringInputs.cs`
- Modify: `Source/VarianceProfile.cs` (add `ToScoring()`)

**Interfaces:**
- Consumes: `PlatformMath` from Task 1.
- Produces: `class ScoringInputs` with public fields `averageQuality`, `skillSpread`, `passionSpread`, `passionMajorBias`, `skillShiftMin`, `skillShiftMax`, `passionCountMin`, `passionCountMax`; properties `SkillNoiseScalar`, `PassionNoiseScalar`; method `void GetBetaAlphaBeta(out float alpha, out float beta)`. Also `VarianceProfileValues.ToScoring()` returning it. `DispersionModel.Moments/BuildCdf/BestOfN/TypicalAt/OutcomeDensity` all take `ScoringInputs` from here on.

- [ ] **Step 1: Move the three files with git mv (preserves history)**

```bash
git mv Source/Constants.cs Source/Scoring/Constants.cs
git mv Source/MathUtil.cs Source/Scoring/MathUtil.cs
git mv Source/DispersionModel.cs Source/Scoring/DispersionModel.cs
```

- [ ] **Step 2: Strip UnityEngine from all three**

In each of the three moved files: delete `using UnityEngine;`, and replace every `Mathf.` with
`PlatformMath.`. Do not change any other token, any numeric literal, or any comment's meaning.

```bash
sed -i 's/^using UnityEngine;$//; s/\bMathf\./PlatformMath./g' \
  Source/Scoring/Constants.cs Source/Scoring/MathUtil.cs Source/Scoring/DispersionModel.cs
grep -n 'Mathf\.\|UnityEngine' Source/Scoring/*.cs
```

Expected from the `grep`: **no output.** Any hit is a missed replacement.

- [ ] **Step 3: Create ScoringInputs**

Create `Source/Scoring/ScoringInputs.cs`. The two accessor bodies and the Beta formula are copied
**verbatim** from `Source/VarianceProfile.cs` — do not re-derive them, and keep the comments,
which document traps that have bitten:

```csharp
namespace PawnVarianceMod
{
    // The scoring half of a profile: exactly the fields DispersionModel and CalculateCompositeScore
    // read, and nothing else. Deliberately platform-free and Scribe-free so the offline envelope
    // harness can build one without RimWorld -- that is what makes the harness and the mod ONE
    // implementation rather than two that have to be kept in step by hand.
    //
    // VarianceProfileValues (the mod-side type) keeps the full field set, the Scribe plumbing and
    // the Beta cache, and converts to this via ToScoring().
    public class ScoringInputs
    {
        public float averageQuality;
        public float skillSpread;
        public float passionSpread;
        public float passionMajorBias;
        public float skillShiftMin;
        public float skillShiftMax;
        public float passionCountMin;
        public float passionCountMax;

        // skillSpread stores a STANDARD DEVIATION in levels. The applier's triangular term
        // (Rand.Value+Rand.Value-1) has variance 1/6, so sd = magnitude/sqrt(6) and the Lerp
        // scalar is sd*sqrt(6)/MaxMagnitude. Getting this wrong divides all skill noise by 2.449
        // and NOTHING would catch it.
        //
        // Valid ONLY while the Lerp low endpoint is 0. If MinMagnitudeFloor ever goes non-zero
        // again (it was 0.5f before 2026-08-06), this must become
        //   (skillSpread*sqrt(6) - MinMagnitudeFloor) / (MaxMagnitude - MinMagnitudeFloor).
        public float SkillNoiseScalar => skillSpread * PlatformMath.Sqrt(6f) / Constants.MaxMagnitude;

        // passionSpread is already the Gaussian's sigma in pips -- no conversion. The two fields
        // are NOT symmetric; do not merge these into one helper.
        public float PassionNoiseScalar => passionSpread / Constants.PassionBudgetSpreadMax;

        // No cache here: the mod-side VarianceProfileValues keeps the cached version for the
        // per-pawn generation path. This is the single definition of the formula; that one
        // delegates to it.
        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            float m = PlatformMath.Clamp(averageQuality,
                                         Constants.QualityClampEpsilon,
                                         1f - Constants.QualityClampEpsilon);
            alpha = m * Constants.BetaConcentrationK;
            beta = (1f - m) * Constants.BetaConcentrationK;
        }
    }
}
```

- [ ] **Step 4: Add ToScoring() to VarianceProfileValues**

In `Source/VarianceProfile.cs`, inside `class VarianceProfileValues`, add:

```csharp
        // The scoring subset, for DispersionModel and CalculateCompositeScore. Everything else on
        // this class (trait counts, child shift, enable flags, Scribe plumbing) is invisible to
        // scoring by design.
        public ScoringInputs ToScoring() => new ScoringInputs
        {
            averageQuality   = averageQuality,
            skillSpread      = skillSpread,
            passionSpread    = passionSpread,
            passionMajorBias = passionMajorBias,
            skillShiftMin    = skillShiftMin,
            skillShiftMax    = skillShiftMax,
            passionCountMin  = passionCountMin,
            passionCountMax  = passionCountMax,
        };
```

- [ ] **Step 5: Point the cached Beta accessor at the single definition**

In `Source/VarianceProfile.cs`, replace the body of `GetBetaAlphaBeta` so the formula is not
duplicated. Keep the cache — it is on the per-pawn path:

```csharp
        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            if (distributionParamsDirty)
            {
                // Single definition of the formula lives on ScoringInputs; this only caches it.
                ToScoring().GetBetaAlphaBeta(out cachedAlpha, out cachedBeta);
                distributionParamsDirty = false;
            }
            alpha = cachedAlpha;
            beta = cachedBeta;
        }
```

- [ ] **Step 6: Change DispersionModel's signatures**

In `Source/Scoring/DispersionModel.cs`, change every `VarianceProfileValues v` parameter to
`ScoringInputs v`. The bodies need no other change — they already touch only fields that exist on
`ScoringInputs`.

```bash
sed -i 's/VarianceProfileValues v/ScoringInputs v/g' Source/Scoring/DispersionModel.cs
grep -n 'VarianceProfileValues' Source/Scoring/DispersionModel.cs
```

Expected from the `grep`: **no output.**

- [ ] **Step 7: Fix the call sites**

Build and let the compiler find them:

Run: `dotnet build Source/PawnVarianceMod.csproj 2>&1 | grep -E 'error' | head -20`

At each reported site, append `.ToScoring()` to the `VarianceProfileValues` argument. Expected
sites are in `Source/PawnVarianceSettings.cs` (the Best-of-N core and the curve) and
`Source/ProfileEditorTab.cs` (the header curve). Do not change anything else at those sites.

- [ ] **Step 8: Build clean**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 9: Prove no figure moved**

Run: `python docs/tools/envelope_check.py`
Expected: `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS: Rule 1 and Rule 2 hold`.

> This step still runs the **Python**, which this task did not touch. That is deliberate: at this
> point Python is the independent witness that the C# move changed nothing. It stops being
> independent in Task 4 and is deleted in Task 5.

- [ ] **Step 10: Commit**

```bash
git add Source/Scoring Source/VarianceProfile.cs Source/PawnVarianceSettings.cs Source/ProfileEditorTab.cs
git commit -m "refactor: the scoring math no longer depends on UnityEngine or Verse"
```

---

### Task 3: One source of truth for the preset scoring numbers

**Files:**
- Create: `Source/Scoring/ScoringPresets.cs`
- Modify: `Source/VarianceProfile.cs`

**Interfaces:**
- Consumes: `ScoringInputs` from Task 2.
- Produces: `static class ScoringPresets` with `public static readonly ScoringInputs Faithful, Distinct, Wildcard, Desperate, Elite, Sovereign, Specialist, Scavenger` and `public static readonly (string label, ScoringInputs v)[] All` in the order Faithful, Distinct, Wildcard, Desperate, Elite, Sovereign, Specialist, Scavenger. Task 4's harness enumerates `All`.

> **Why this task exists.** Today `envelope_check.py` regex-parses `Source/VarianceProfile.cs` to
> learn the preset numbers — which is why `Constants.cs` is forbidden from using expressions
> (`HANDOVER.md`'s CAUTION on `MaxPassionPips`). Moving the scoring numbers into a plain
> platform-free file lets the harness *reference* them instead of parsing them, and retires that
> constraint. The numbers move; **not one of them changes.**

- [ ] **Step 1: Create the preset table**

Create `Source/Scoring/ScoringPresets.cs`. Copy each value **verbatim** from the corresponding
`VarianceProfiles.*` initialiser in `Source/VarianceProfile.cs`. Copy the explanatory comments too
— in particular the `WildSpread` band-reasoning table, which belongs with these numbers:

```csharp
namespace PawnVarianceMod
{
    // The SCORING half of every shipped preset, and the single source of truth for those numbers.
    // VarianceProfiles (mod side) builds its VarianceProfileValues from these and adds the
    // non-scoring fields (trait counts, child shift, enable flags, countProtectedTraits).
    //
    // The offline envelope harness references this directly. It used to regex-parse
    // VarianceProfile.cs instead, which is why Constants.cs was once forbidden from using
    // expressions -- that constraint is retired.
    public static class ScoringPresets
    {
        public static readonly ScoringInputs Faithful = new ScoringInputs
        {
            averageQuality = 0.5f, skillSpread = 0.489898f, passionSpread = 1.0f,
            passionMajorBias = 0.5f, skillShiftMin = -3f, skillShiftMax = 3f,
            passionCountMin = 3f, passionCountMax = 7f,
        };

        // ... one block per preset, values copied verbatim from VarianceProfiles ...

        public static readonly (string label, ScoringInputs v)[] All =
        {
            ("Faithful", Faithful), ("Distinct", Distinct), ("Wildcard", Wildcard),
            ("Desperate", Desperate), ("Elite", Elite), ("Sovereign", Sovereign),
            ("Specialist", Specialist), ("Scavenger", Scavenger),
        };
    }
}
```

> **Do not invent the remaining seven blocks from memory.** Read each initialiser in
> `Source/VarianceProfile.cs` (they start at the `public static readonly VarianceProfile` lines)
> and copy the eight scoring fields from each. `Faithful` = `VanillaLike`,
> `Distinct` = `BalancedVariance`, `Wildcard` = `WildSpread`, `Desperate` = `Hardscrabble`.

- [ ] **Step 2: Build the mod presets from the table**

In `Source/VarianceProfile.cs`, change each preset initialiser so its eight scoring fields come
from `ScoringPresets` instead of being written twice. For `VanillaLike`:

```csharp
            new VarianceProfileValues
            {
                averageQuality   = ScoringPresets.Faithful.averageQuality,
                skillSpread      = ScoringPresets.Faithful.skillSpread,
                passionSpread    = ScoringPresets.Faithful.passionSpread,
                passionMajorBias = ScoringPresets.Faithful.passionMajorBias,
                skillShiftMin    = ScoringPresets.Faithful.skillShiftMin,
                skillShiftMax    = ScoringPresets.Faithful.skillShiftMax,
                passionCountMin  = ScoringPresets.Faithful.passionCountMin,
                passionCountMax  = ScoringPresets.Faithful.passionCountMax,
                // non-scoring fields stay here, unchanged:
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
                traitCountMax = 3f,
                // ... etc, exactly as before ...
            }
```

Repeat for all eight. **Delete no non-scoring field and change no value.**

- [ ] **Step 3: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 4: Prove the numbers survived**

Run: `python docs/tools/envelope_check.py`
Expected: `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS`.

> The Python still parses `VarianceProfile.cs` with its regex. If Step 2 changed a field to an
> expression the regex cannot read, the tool exits with `ERROR: <var> has no '<field>' assignment`.
> **That is expected and is not a failure of this task** — it means the regex can no longer see the
> numbers, which is precisely the coupling Task 4 removes. If it happens, note it in the task
> report and proceed to Task 4; do not contort the C# to keep a regex happy.

- [ ] **Step 5: Commit**

```bash
git add Source/Scoring/ScoringPresets.cs Source/VarianceProfile.cs
git commit -m "refactor: preset scoring numbers have one home, in platform-free source"
```

---

### Task 4: The C# harness, proven byte-identical to the Python

**Files:**
- Create: `docs/tools/EnvelopeCheck/EnvelopeCheck.csproj`
- Create: `docs/tools/EnvelopeCheck/Program.cs`
- Modify: `Source/PawnVarianceSettings.cs` (move the composite out)
- Create: `Source/Scoring/CompositeScore.cs`

**Interfaces:**
- Consumes: `ScoringPresets.All`, `DispersionModel`, `Constants`, `PlatformMath`.
- Produces: a console app whose stdout is byte-identical to `python docs/tools/envelope_check.py`, and which regenerates `Source/EnvelopeFigures.g.cs`.

- [ ] **Step 1: Move the composite into the scoring folder**

Cut `PassionPipEfficiency` (currently `Source/PawnVarianceSettings.cs:1362`) and
`CalculateCompositeScore` (currently `:1375`) into a new `Source/Scoring/CompositeScore.cs`,
changing `Mathf.` → `PlatformMath.` and `VarianceProfileValues v` → `ScoringInputs v`. Keep every
comment — the `PassionPipEfficiency` derivation comment is cited by `HANDOVER.md` as the argument
to beat.

```csharp
namespace PawnVarianceMod
{
    public static class CompositeScore
    {
        // <keep the existing PassionPipEfficiency comment block verbatim>
        public static float PassionPipEfficiency(float majorBias) { /* moved body */ }

        // <keep the existing CalculateCompositeScore comment block verbatim>
        public static float Calculate(float q, ScoringInputs v) { /* moved body */ }
    }
}
```

In `Source/PawnVarianceSettings.cs`, replace the two removed members with forwarding calls so the
existing callers keep working:

```csharp
        internal static float PassionPipEfficiency(float majorBias)
            => CompositeScore.PassionPipEfficiency(majorBias);

        private static float CalculateCompositeScore(float q, VarianceProfileValues v)
            => CompositeScore.Calculate(q, v.ToScoring());
```

- [ ] **Step 2: Create the harness project**

Create `docs/tools/EnvelopeCheck/EnvelopeCheck.csproj`. It compiles the **same source files** the
mod does — no project reference, no shipped DLL:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>9.0</LangVersion>
    <RootNamespace>PawnVarianceMod</RootNamespace>
    <Nullable>disable</Nullable>
    <AssemblyName>EnvelopeCheck</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <!-- The SAME files the mod compiles. This is what makes the gate and the mod one
         implementation. Never copy a scoring file in here; always link it. -->
    <Compile Include="..\..\..\Source\Scoring\**\*.cs" />
  </ItemGroup>
</Project>
```

> `net472` deliberately matches the mod so both sides run identical IEEE float paths. Do not
> retarget this to `net8.0` for convenience.

- [ ] **Step 3: Port the tool**

Create `docs/tools/EnvelopeCheck/Program.cs`. Reproduce `docs/tools/envelope_check.py`'s output
**exactly**, section by section: the self-check line, the `wS=.. wP=.. pips/.. skill/.. K=..`
header, the `R` lines, the Faithful baseline, the profile table, the Rule 2 block, the tightest
margins, the dispersion table, the `.g.cs` status line, and the trailing PASS/FAIL block.

Read the Python and mirror its `print` calls and format specifiers one for one. The float
formatting is where this will fight you: Python's `f"{x:.4f}"` and C#'s `x.ToString("F4")` agree
on these values, but Python's `f"{x:+.1f}%"` sign handling and column padding must be matched with
explicit width specifiers. Work iteratively against Step 4's diff rather than guessing.

Keep `.g.cs` generation behaviourally identical to `write_generated`, including the
`unchanged`/`REWRITTEN` status line and the non-zero exit on a Rule 1 or Rule 2 violation.

- [ ] **Step 4: THE GATE — byte-identical stdout**

```bash
python docs/tools/envelope_check.py > /tmp/py.txt 2>&1
dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj > /tmp/cs.txt 2>&1
diff /tmp/py.txt /tmp/cs.txt && echo "IDENTICAL"
```

Expected: `IDENTICAL`, with `diff` producing no output.

> **This diff is the entire proof of the refactor.** It demonstrates that the shim is exact, that
> the moved math is unchanged, that `ScoringInputs` carries the right fields, and that
> `ScoringPresets` copied every number correctly — all at once. Do not proceed to Task 5 until it
> is clean. If Task 3 Step 4 left the Python unable to parse the presets, restore the Python's
> ability to run by checking out the pre-Task-3 `VarianceProfile.cs` into a temp worktree and
> running the Python there against the same constants:
> `git worktree add /tmp/pre-t3 HEAD~1 && (cd /tmp/pre-t3 && python docs/tools/envelope_check.py)`.

- [ ] **Step 5: Confirm the generated file still matches**

Run: `git status --short Source/EnvelopeFigures.g.cs`
Expected: **no output** — the harness regenerated it to the same bytes.

- [ ] **Step 6: Commit**

```bash
git add Source/Scoring/CompositeScore.cs Source/PawnVarianceSettings.cs docs/tools/EnvelopeCheck
git commit -m "feat: a C# envelope harness that runs the mod's own scoring source"
```

---

### Task 5: Delete the Python and retire the mirror contract

**Files:**
- Delete: `docs/tools/envelope_check.py`
- Modify: `HANDOVER.md`, `Source/Scoring/Constants.cs`, `Source/Scoring/DispersionModel.cs`, `Source/DebugActions.cs`

- [ ] **Step 1: Delete the tool**

```bash
git rm docs/tools/envelope_check.py
```

- [ ] **Step 2: Retire the literal-only constraint**

In `Source/Scoring/Constants.cs`, remove the comment block warning that parsed constants must stay
plain numeric literals (it cites the regex and the *"Constants.cs is missing: MaxPassionPips"*
error). Replace it with one line noting the constraint is gone because the harness references
these constants directly. **Do not change any value**, and do not convert `MaxPassionPips` to an
expression in this task — that is now *permitted*, but it is a separate decision.

- [ ] **Step 3: Rewrite the mirror comments**

`Source/Scoring/DispersionModel.cs`'s header currently reads *"Mirrors make_grid_score /
grid_moments in docs/tools/envelope_check.py -- IF YOU CHANGE ONE, CHANGE BOTH."* That contract no
longer exists. Replace with:

```csharp
    // Deterministic, dispersion-aware Best-of-N. This file is compiled into BOTH the mod and the
    // offline envelope harness (docs/tools/EnvelopeCheck), so there is exactly one implementation
    // and no mirror to keep in step. It used to have a Python twin; the in-game "Verify Best-of-N"
    // action existed to police the two, and now checks something narrower -- see below.
```

- [ ] **Step 4: Repoint the in-game verify action**

`Source/DebugActions.cs`'s Verify Best-of-N action compares the mod's live computation against the
Python's figures. Change it to compare against the baked `Source/EnvelopeFigures.g.cs` values
instead, and update its on-screen text.

> **Its meaning narrows and that is worth stating in the action's own output.** It no longer
> proves two implementations agree — they are one. It now proves the *shipped assembly running
> under RimWorld's Mono* reproduces the figures the harness generated under .NET Framework. That
> is a smaller claim but still a real one, and it is the only check that covers runtime
> divergence.

- [ ] **Step 5: Update HANDOVER.md**

- The `envelope_check.py` command in the Rule 6 CAUTION becomes
  `dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj`.
- Rule 6's text stops describing "two implementations of one integral".
- The `MaxPassionPips`-must-be-a-literal CAUTION is deleted.
- **The four-site passion-floor CAUTION drops to three sites** (the applier plus the two remaining
  models) and is marked as shrinking again in Phase 2.
- The "What the model cannot see" limit 3 (`OutcomeDensity` has no Python counterpart) is now
  vacuous — nothing has a Python counterpart. Replace it with the accurate residual limit: it is
  still the one scoring function no gate exercises, because the harness does not call it.
- Re-paste the verbatim envelope block from the **harness's** stdout.

- [ ] **Step 6: Verify**

```bash
dotnet build Source/PawnVarianceMod.csproj
dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj | tail -5
git status --short Source/EnvelopeFigures.g.cs
```

Expected: build `0/0`; harness prints `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS`;
`git status` on the generated file prints nothing.

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "refactor: retire the Python envelope tool and the two-implementation contract"
```

---

# PHASE 2 — One implementation of the generator's decisions

> **Phase 1 does not fix the bug that prompted this plan.** That divergence was between the
> scoring *model* and the pawn *generator* — genuinely different code. Phase 2 narrows that gap by
> extracting the generator's decision math so the Monte Carlo calls it instead of copying it.
> **Get Rule 8 sign-off for `SkillVarianceApplier.cs` and `PassionVarianceApplier.cs` before
> starting Task 6.**

### Task 6: Extract the passion budget decision

**Files:**
- Create: `Source/Scoring/IRandomSource.cs`
- Create: `Source/Scoring/PassionBudget.cs`
- Modify: `Source/PassionVarianceApplier.cs`

**Interfaces:**
- Produces: `interface IRandomSource { float Value { get; } float Gaussian(float mean, float sd); bool Chance(float p); }` and
  `static class PassionBudget` with
  `static float Roll(ScoringInputs v, float quality, float alreadyCommittedPips, IRandomSource rng)` and
  `static (int minor, int major) Spend(float budget, float majorBias, int eligibleSkills, IRandomSource rng)`.

- [ ] **Step 1: Create the RNG seam**

Create `Source/Scoring/IRandomSource.cs`:

```csharp
namespace PawnVarianceMod
{
    // Lets the budget math run under RimWorld's Verse.Rand in game and under a seeded generator in
    // the offline Monte Carlo, WITHOUT the math itself being written twice. The mirror between
    // PassionVarianceApplier and dispersion_mc.py is exactly how the sig-gated floor bug (fixed in
    // 52602f7) reached three files and stayed invisible to every gate.
    public interface IRandomSource
    {
        float Value { get; }                          // uniform [0,1)
        float Gaussian(float mean, float sd);
        bool Chance(float probability);
    }
}
```

- [ ] **Step 2: Move the budget math**

Create `Source/Scoring/PassionBudget.cs`. Move the body of the budget roll from
`Source/PassionVarianceApplier.cs` (the `budgetMean`/`spread`/`clampWindow`/`budget` block and the
floor, currently around `:80-77`) and the spend loop that follows it. **Copy the comments** —
especially the vanilla-floor comment, which now also has to say the condition is shared.

Preserve the floor exactly as it stands after `52602f7`:

```csharp
            bool flooredBudget = budget < 1f && v.passionCountMin > 0f && alreadyCommittedPips <= 0f;
            if (flooredBudget) budget = 1f;
```

- [ ] **Step 3: Make the applier call it**

In `Source/PassionVarianceApplier.cs`, replace the moved block with a call to
`PassionBudget.Roll(...)` and `PassionBudget.Spend(...)`, passing a `VerseRandomSource` adapter
(a small `IRandomSource` implementation wrapping `Verse.Rand`, defined in the applier's file since
it is RimWorld-coupled). Keep every surrounding behaviour — the trace logging, the
`alreadyCommittedPips` plumbing, the eligible-skill selection — exactly as it is.

- [ ] **Step 4: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 5: Confirm scoring is untouched**

Run: `dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj | tail -4`
Expected: `Source/EnvelopeFigures.g.cs: unchanged.` and `PASS`. This task moved generator code, not
scoring code; any figure movement is a defect.

- [ ] **Step 6: Commit**

```bash
git add Source/Scoring/IRandomSource.cs Source/Scoring/PassionBudget.cs Source/PassionVarianceApplier.cs
git commit -m "refactor: the passion budget decision is callable without a Pawn"
```

---

### Task 7: Extract the skill shift decision

**Files:**
- Create: `Source/Scoring/SkillShift.cs`
- Modify: `Source/SkillVarianceApplier.cs`

**Interfaces:**
- Produces: `static class SkillShift` with
  `static float Roll(ScoringInputs v, float quality, IRandomSource rng)` returning the per-skill
  shift **before** the `Clamp(0,20)` (which needs the pawn's existing level and stays in the applier), and
  `static int ApplyToLevel(int currentLevel, float shift)` performing `PlatformMath.Clamp(PlatformMath.RoundToInt(currentLevel + shift), 0, 20)`.

- [ ] **Step 1: Move the shift math**

Create `Source/Scoring/SkillShift.cs`, moving the `magnitude`/triangular-draw/`baseline` arithmetic
out of `Source/SkillVarianceApplier.cs`'s private `Shift` (around `:57`). Carry the comments,
including the `clampToRange` semantics warning.

> **`skillShiftMin` means two different things in two code paths** — a soft target in `Apply`, a
> hard floor in `ApplyGrowUp`. `HANDOVER.md` calls this the most likely future bug in this area.
> `SkillShift.Roll` implements the **generation** (soft target) path only. Do not route
> `ApplyGrowUp` through it in this task.

- [ ] **Step 2: Make the applier call it**

Replace the moved arithmetic in `SkillVarianceApplier.Shift` with `SkillShift.Roll(...)` and
`SkillShift.ApplyToLevel(...)`, reusing the `VerseRandomSource` adapter from Task 6.

- [ ] **Step 3: Build and confirm scoring is untouched**

```bash
dotnet build Source/PawnVarianceMod.csproj
dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj | tail -4
```

Expected: build `0/0`; `Source/EnvelopeFigures.g.cs: unchanged.`; `PASS`.

- [ ] **Step 4: Commit**

```bash
git add Source/Scoring/SkillShift.cs Source/SkillVarianceApplier.cs
git commit -m "refactor: the skill shift decision is callable without a Pawn"
```

---

### Task 8: The Monte Carlo calls the real generator

**Files:**
- Create: `docs/tools/EnvelopeCheck/MonteCarlo.cs`
- Modify: `docs/tools/EnvelopeCheck/Program.cs`
- Delete: `docs/tools/dispersion_mc.py`

- [ ] **Step 1: Add a seeded RNG for the harness**

In `docs/tools/EnvelopeCheck/MonteCarlo.cs`, implement `IRandomSource` over
`System.Random` with an explicit seed, using Box-Muller for `Gaussian` and applying the same
`PassionBudgetClampFactor` window the applier applies.

- [ ] **Step 2: Port the simulation to call the real code**

Reproduce `docs/tools/dispersion_mc.py`'s `simulate` — but instead of reimplementing the budget
roll and the skill draws, call `PassionBudget.Roll`, `PassionBudget.Spend` and `SkillShift.Roll`.
Keep the zero-noise analytic self-check and the per-preset Best-of-N table.

> **This is the point of the whole phase.** The old Python `simulate` was a third mirror, and it
> carried the identical sig-gated-floor bug the two quadratures had. Calling the extracted
> functions means the Monte Carlo now disagrees with the quadrature whenever the *generator* and
> the *model* disagree — which is the divergence no gate could previously see.

- [ ] **Step 3: Compare against the Python before deleting it**

```bash
python docs/tools/dispersion_mc.py > /tmp/mcpy.txt 2>&1
dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj -- --mc > /tmp/mccs.txt 2>&1
diff /tmp/mcpy.txt /tmp/mccs.txt || true
```

Expected: the self-check line agrees to ~1e-16 and the per-preset figures agree to within Monte
Carlo noise at 200,000 pawns. **They will NOT be byte-identical** — the RNGs differ, so the
sampling differs. That is expected and is not a failure; unlike Task 4, this port cannot be proved
by a byte diff. Record the largest per-cell gap in the task report.

> If any preset's figure differs by more than ~0.003 (the documented grid-vs-MC gap scale), stop
> and report it — that is a real divergence between the extracted generator code and what the
> Python was doing, not sampling noise.

- [ ] **Step 4: Delete the Python MC**

```bash
git rm docs/tools/dispersion_mc.py
```

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "feat: the Monte Carlo exercises the shipped generator instead of mirroring it"
```

---

### Task 9: Documentation

**Files:**
- Modify: `HANDOVER.md`

- [ ] **Step 1: Rewrite the mirror-era sections**

- The four-site passion-floor CAUTION becomes a **two**-site note (the extracted
  `PassionBudget.Roll`, and `DispersionModel`'s deliberate model-side approximation of it) and
  explains why those two legitimately differ: one rolls, one predicts.
- The "two implementations of one integral" framing is gone everywhere.
- Record the residual gap honestly: **the model still predicts and the generator still rolls.**
  Phase 2 makes the Monte Carlo a real witness to that gap; it does not close it, and nothing can.
- Note the new build/verify commands and the `Source/Scoring/` boundary rule: *anything in that
  folder must compile without `UnityEngine` and without `Verse`.*

- [ ] **Step 2: Re-paste the envelope block from the harness**

Run: `dotnet run --project docs/tools/EnvelopeCheck/EnvelopeCheck.csproj`
Paste stdout verbatim into "The verified envelope". Rule 6.

- [ ] **Step 3: Record the open gate**

State plainly that the whole plan has **not** been verified in game, list the two actions that
must be run (`Verify Best-of-N`, and a 1000-pawn `Roll pawns and dump distribution` on `Wildcard`
and `Faithful`), and give the expected results: 32/32 unchanged, and dump figures matching the
`−4.0/4.2` band's recorded medians and per-pawn sds.

- [ ] **Step 4: Commit**

```bash
git add HANDOVER.md
git commit -m "docs: the scoring math has one implementation, and what that does not fix"
```

---

# Verification summary

| Gate | Where | Catches |
|---|---|---|
| `dotnet build` `0/0` | every task | compile breakage |
| **Python vs C# stdout byte-diff** | Task 4 Step 4 | the entire Phase 1 port, including shim exactness |
| `EnvelopeFigures.g.cs: unchanged` | Tasks 2, 3, 5, 6, 7 | any figure moving at 1e-6 |
| MC vs quadrature agreement | Task 8 Step 3 | generator/model divergence — the bug class this plan targets |
| **In-game `Verify Best-of-N`** | **DEFERRED — Task 9 Step 3** | Mono-vs-.NET runtime divergence |
| **In-game 1000-pawn dump** | **DEFERRED — Task 9 Step 3** | anything the offline gates structurally cannot see |

**The two deferred rows are the ones this project has been burned by four times.** Deferring them
is the owner's explicit instruction of 2026-08-08, not an oversight, and the plan is not closed
until they are run.

---

# Self-Review

**Spec coverage.** This plan derives from a design discussion rather than a written spec. The two
goals stated there both have tasks: *"share the scoring code so the gate runs one implementation"*
→ Tasks 1–5; *"extract the applier's decision math so the MC is ground truth"* → Tasks 6–8. The
third point raised — that the shared-library move would **not** have caught the `52602f7` bug — is
recorded at the head of Phase 2 and again in Task 9 Step 1, so no implementer can come away
believing Phase 1 closed it.

**Placeholders.** One deliberate omission: Task 3 Step 1 shows the `Faithful` block and instructs
the implementer to read the other seven from `VarianceProfile.cs` rather than transcribing 56
numbers into this plan. Transcribing them here would create a third copy of the very numbers the
task exists to deduplicate, and a typo in the plan would be indistinguishable from a typo in the
code. Task 4 Step 4's byte-diff catches any copying error mechanically. Task 4 Step 3 similarly
directs the implementer to read the Python's `print` calls rather than pasting ~120 lines of format
strings; the same diff gates it.

**Type consistency.** `ScoringInputs` is introduced in Task 2 and consumed by name in Tasks 3, 4,
6 and 7. `PlatformMath` is introduced in Task 1 and used in 2, 4, 7. `IRandomSource` is introduced
in Task 6 and reused in 7 and 8. `DispersionModel`'s signature change (`VarianceProfileValues` →
`ScoringInputs`) happens once, in Task 2 Step 6, before any task depends on it.
`CompositeScore.Calculate` (Task 4) is deliberately named differently from the
`CalculateCompositeScore` forwarder left behind in `PawnVarianceSettings.cs`, so the two are never
confused; `HANDOVER.md` and `T7-M2` both note that forwarder must not be deleted as dead code —
`FaithfulBaseline()` is its one live caller.

**Ordering risk.** Task 3 may break the Python's regex parse, which Task 4 then makes moot. That is
called out in Task 3 Step 4 with a worktree fallback so the Task 4 diff can still be produced
against a runnable Python.
