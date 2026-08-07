# Dispersion-Aware Scoring Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the Best-of-N power score see the two noise sliders, put those sliders into real units, replace the header curve with a realised-outcome distribution, and retune Wildcard back inside the ±35% envelope.

**Architecture:** A new deterministic quadrature model computes `composite | q ~ Normal(μ(q), σ(q))`, from which one CDF `F(x)` feeds three consumers: the typical-pawn readout, the Best-of-N figure, and the curve. It is mirrored in `envelope_check.py` and validated offline by an independent Monte Carlo. Monte Carlo cannot ship — the in-game gate cross-checks two implementations at 0.5pp, which requires both to be deterministic.

**Tech Stack:** C# 9 / .NET Framework 4.7.2 (`net472`) against RimWorld 1.5 + Harmony; Python 3 standard library only (no numpy — it is not installed and `envelope_check.py` has a no-third-party-dependency rule); GABS/RimBridge for in-game verification.

**Spec:** [`docs/superpowers/specs/2026-08-07-dispersion-aware-scoring-design.md`](../specs/2026-08-07-dispersion-aware-scoring-design.md)

## Global Constraints

Every task's requirements implicitly include this section.

- **There is no unit-test project and you must not create one.** `HANDOVER.md`: the interesting code is `Pawn`-coupled, so an out-of-game double tests a copy of the logic. The test cycle is: Python tools' own self-checks offline, and debug actions in `Source/DebugActions.cs` run through GABS in game.
- **Rule 8 — get explicit owner permission before modifying any of these five `DONE (REVIEWED)` files:** `PawnVarianceSettings.cs`, `ProfileEditorTab.cs`, `VarianceProfile.cs`, `SkillVarianceApplier.cs`, `PassionVarianceApplier.cs`. Sign-off for those five is granted (spec §9). **Re-read the code-review status list in `HANDOVER.md` before starting** — `PassionVarianceApplier.cs` flipped to `[x]` mid-design-session, so any cached reading is unreliable.
- **`Source/SettingsTransfer.cs` is ALSO `[x]` reviewed (`HANDOVER.md:1257`) and is NOT covered by that sign-off — so this plan does not touch it.** The spec §9 called it unreviewed; that was wrong. It also needs no edit: it round-trips profiles through `VarianceProfileValues.ExposeData()` and contains **zero** references to `skillNoise`/`passionNoise`, so the Task 5 rename reaches it automatically via the `Scribe` calls. If a genuine need to edit it appears, **stop and get owner permission first.**
- **`Source/DebugActions.cs` is unreviewed (`HANDOVER.md:1275`), so no Rule 8 gate.** Its only `skillNoise`/`passionNoise` occurrences are in comments (`:326`, `:721`) — the Task 5 rename there is comment text, not code.
- **Rule 6 — after any scoring-constant change:** run `python docs/tools/envelope_check.py`, paste its verbatim output into `HANDOVER.md`, commit `Source/EnvelopeFigures.g.cs` if the run rewrote it, and run the in-game verify action.
- **`Constants.cs` parsing constraint:** `envelope_check.py` parses it with a regex accepting only `public const float X = <number>f;`. Never write an expression there — the tool exits with "Constants.cs is missing: X".
- **Grid sizes:** q=256, x=512, triangular=65, gaussian=65. Drag-time downsample: q=64, x=128.
- **The `[0,1]` integration bound IS the Clamp01.** Do not extend the integral or pre-clamp `F`.
- **`√6` is load-bearing.** `skillSpread` stores a standard deviation; the applier needs `magnitude = spread * √6`. `passionSpread` needs no conversion. The two fields are not symmetric.
- **Never hand-edit `Source/EnvelopeFigures.g.cs`.** It is generated. It stores scores at **six
  decimals**, so a change of ~1e-6 in any scoring input rewrites it. Any step whose expected output
  is `unchanged` is therefore asserting agreement to 1e-6, not "roughly the same".
- **Run every shell block in this plan through the Bash tool (Git Bash), not PowerShell.** The
  commands use POSIX syntax (`test -f`, `&&`/`||` chaining, `$?`, `/c/Program Files/...` paths,
  backslash line-continuation) which is a parse error in this project's primary shell. This is not
  optional — `A && B || C` in PowerShell 5.1 fails outright rather than falling through.
- **Every in-game step carries two traps, and they are restated at each one rather than cross-
  referenced,** because a subagent executing a single task never reads the other tasks:
  1. **GABS must launch RimWorld itself** (`games_start rimworld`). It injects the bridge port and
     token at launch, so a hand-started game cannot connect.
  2. **Read the `ACTUALLY RESOLVED TO:` line** on any pawn-dump action. A faction, race or xenotype
     override outranks the Active Colony Profile, and that has already invalidated two 1000-pawn
     runs on this project.
- Build must return `0 Error(s), 0 Warning(s)`.
- Close RimWorld before copying the DLL, or the copy fails on a file lock.

---

## File Structure

| File | Responsibility |
|---|---|
| `Source/MathUtil.cs` | **new** — `Erf` and `NormalCdf`. Nothing else. |
| `Source/DispersionModel.cs` | **new** — per-q moments, `F(x)`, `E[max of N]`, outcome density. The only place the statistics live. |
| `docs/tools/dispersion_mc.py` | **new** — independent Monte Carlo, the offline ground truth. |
| `docs/tools/envelope_check.py` | modify — mirror the model; switch Rule 1 onto it. |
| `Source/PawnVarianceSettings.cs` | modify — readouts call the model; cache; **`PassionPipEfficiency` visibility (Task 3 Step 1a)**. |
| `Source/ProfileEditorTab.cs` | modify — sliders, derived readouts, curve. |
| `Source/VarianceProfile.cs` | modify — field rename/rescale, clamp bounds, Scribe defaults, Wildcard values. |
| `Source/SkillVarianceApplier.cs` | modify — one line, the `√6` conversion. |
| `Source/PassionVarianceApplier.cs` | modify — one line, the renamed field. |
| `Source/DebugActions.cs` | modify — new dump action, extend the verify gate, rename comment references. |

`Source/SettingsTransfer.cs` is deliberately **absent**: it is `[x]` reviewed and needs no edit. See
Global Constraints.

---

## Task 1: Promote the Monte Carlo to a checked-in tool

The independent method. Everything later is validated against this, so it lands first.

**Files:**
- Create: `docs/tools/dispersion_mc.py` (content derived from `zzz-Do-Not-Commit/noise_bestofn_mc.py`)
- Delete: `zzz-Do-Not-Commit/noise_bestofn_mc.py` (a move, not a copy — two copies will diverge)

**Interfaces:**
- Consumes: `envelope_check.py`'s `parse_constants`, `parse_profiles`, `read`, `CONSTANTS`, `PROFILES`, `make_efficiency`.
- Produces: `make_realised(C) -> (shifts, budget, profile) -> float`, `expected_max_of_n(sorted_xs, N) -> float`, `simulate(p, C, realised, n_skills, with_noise) -> sorted list[float]`.

- [ ] **Step 1: Write the self-check that must fail first**

Create `docs/tools/dispersion_mc.py` with only the self-check wired up, so the failure is real:

```python
"""Independent Monte-Carlo ground truth for the dispersion model.

Deliberately a DIFFERENT numerical method from the quadrature in envelope_check.py, so it can
catch shared quadrature errors. It does NOT make verification fully independent: this and the
quadrature both substitute a flat AssumedVanillaSkillBaseline for each skill's real vanilla level.
Only `Roll pawns and dump distribution` sees real baselines. Do not describe this as independent
verification without that qualifier.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import envelope_check as ec

NS = (1, 5, 25, 50)
SEED = int(sys.argv[1]) if len(sys.argv) > 1 else 20260807
M = int(sys.argv[2]) if len(sys.argv) > 2 else 200_000


def make_realised(C):
    """The composite, taking REALISED per-skill shifts and a REALISED budget.

    Mirrors envelope_check.make_composite term for term; only the inputs differ.
    """
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    skills = pdiv / major
    efficiency = ec.make_efficiency(C)

    def realised(shifts, budget, p):
        acc = 0.0
        for s in shifts:
            lvl = base + s
            if lvl < 0.0:
                lvl = 0.0
            elif lvl > top:
                lvl = top
            acc += lvl
        skill_norm = (acc / len(shifts)) / top
        if skill_norm > 1.0:
            skill_norm = 1.0

        capacity = skills * (minor + (major - minor) * p["passionMajorBias"])
        eff = efficiency(p["passionMajorBias"])
        b = budget if budget < capacity else capacity
        passion_norm = b * eff / pdiv
        if passion_norm < 0.0:
            passion_norm = 0.0
        elif passion_norm > 1.0:
            passion_norm = 1.0

        c = (wS * skill_norm + wP * passion_norm) / (wS + wP)
        return c if c < 1.0 else 1.0

    return realised


def expected_max_of_n(sorted_xs, N):
    """E[max of N] under the empirical distribution, via order statistics."""
    m = len(sorted_xs)
    acc = 0.0
    prev = 0.0
    for k in range(1, m + 1):
        cur = (k / m) ** N
        acc += sorted_xs[k - 1] * (cur - prev)
        prev = cur
    return acc


def simulate(p, C, realised, n_skills, with_noise):
    eps = C["QualityClampEpsilon"]
    K = C["BetaConcentrationK"]
    m = min(max(p["averageQuality"], eps), 1.0 - eps)
    a, b = m * K, (1.0 - m) * K

    mag = (C["MinMagnitudeFloor"] + (C["MaxMagnitude"] - C["MinMagnitudeFloor"])
           * p["skillNoise"]) if with_noise else 0.0
    sig = (C["PassionBudgetSpreadMin"] + (C["PassionBudgetSpreadMax"]
           - C["PassionBudgetSpreadMin"]) * p["passionNoise"]) if with_noise else 0.0
    window = sig * C["PassionBudgetClampFactor"]

    smin, smax = p["skillShiftMin"], p["skillShiftMax"]
    bmin, bmax = p["passionCountMin"], p["passionCountMax"]

    rnd = random.Random(SEED)
    out = []
    for _ in range(M):
        q = rnd.betavariate(a, b)
        baseline = smin + (smax - smin) * q
        if mag:
            shifts = [baseline + (rnd.random() + rnd.random() - 1.0) * mag
                      for _ in range(n_skills)]
        else:
            shifts = [baseline] * n_skills

        budget = bmin + (bmax - bmin) * q
        if sig:
            g = rnd.gauss(0.0, sig)
            if g > window:
                g = window
            elif g < -window:
                g = -window
            budget += g
            if budget < 1.0 and bmin > 0.0:
                budget = 1.0
            if budget < 0.0:
                budget = 0.0
        out.append(realised(shifts, budget, p))

    out.sort()
    return out


def main():
    C = ec.parse_constants(ec.read(ec.CONSTANTS))
    P = ec.parse_profiles(ec.read(ec.PROFILES))
    composite = ec.make_composite(C)
    realised = make_realised(C)
    n_skills = int(round(C["MaxPassionPips"] / C["MajorPassionCost"]))

    worst = 0.0
    for name, p in P.items():
        for i in range(21):
            q = i / 20.0
            baseline = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
            budget = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
            got = realised([baseline] * n_skills, budget, p)
            want = composite(q, p)
            worst = max(worst, abs(got - want))
    print(f"self-check: max |realised - analytic| at zero noise = {worst:.2e}")
    if worst >= 1e-12:
        print("FAIL: realised composite does not reproduce the analytic one")
        return 1
    print(f"skills={n_skills}  pawns/preset={M:,}  seed={SEED}\n")

    print(f"{'profile':<12}{'N':>4}  {'best-of-N':>10}")
    for name, p in P.items():
        s = simulate(p, C, realised, n_skills, with_noise=True)
        for N in NS:
            print(f"{name:<12}{N:>4}  {expected_max_of_n(s, N):10.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
```

- [ ] **Step 2: Run it and confirm the self-check passes**

Run: `python docs/tools/dispersion_mc.py 20260807 20000`

Expected: `self-check: max |realised - analytic| at zero noise = 1.11e-16`, then a table. The small `M` keeps this fast; use it for iteration.

If the self-check fails, the realised composite has drifted from `make_composite` — fix that before anything else, because every later validation is measured against this file.

- [ ] **Step 3: Run at full sample size and record the reference figures**

Run: `python docs/tools/dispersion_mc.py 20260807 200000`

Expected (these are the spec's headline numbers — Wildcard is pre-retune here):

```
Faithful       1      0.2568      Faithful      50      0.3723
Wildcard       1      0.2531      Wildcard      50      0.5547
```

- [ ] **Step 4: Confirm the scratch original is gone**

Run: `test -f zzz-Do-Not-Commit/noise_bestofn_mc.py && echo "STILL THERE - delete it" || echo "moved cleanly"`

Expected: `moved cleanly`. Two copies of the ground truth will diverge.

- [ ] **Step 5: Commit**

```bash
git add docs/tools/dispersion_mc.py
git commit -m "test: add an independent Monte-Carlo ground truth for the dispersion model"
```

---

## Task 2: Mirror the deterministic model in envelope_check.py, reported not enforced

Rule 1 stays on the old figures here, so the gate stays green and this task is committable on its own.

**Files:**
- Modify: `docs/tools/envelope_check.py`

**Interfaces:**
- Consumes: `parse_constants`, `parse_profiles`, `make_efficiency`, `beta_grid`, `expected_best_of_n`, `make_composite`.
- Produces: `make_grid_score(C) -> (profile, with_noise) -> {N: float}` and `grid_moments(C) -> (profile, q) -> (mu, sd)`. Task 4 switches Rule 1 onto `make_grid_score`; Task 3's C# mirrors it exactly.

- [ ] **Step 1: Add the quadrature helpers and the model**

Add to `docs/tools/envelope_check.py`, after `make_spread`:

```python
QGRID, XGRID, TGRID, GGRID = 256, 512, 65, 65


def _phi(z):
    return 0.5 * (1.0 + math.erf(z / math.sqrt(2.0)))


def _tri_nodes():
    """Triangular density on [-1,1]: f(t) = 1-|t|. Mirrors (Rand.Value+Rand.Value)/2*2-1."""
    dt = 2.0 / TGRID
    ts, ws, tot = [], [], 0.0
    for i in range(TGRID):
        t = -1.0 + (i + 0.5) * dt
        w = (1.0 - abs(t)) * dt
        ts.append(t)
        ws.append(w)
        tot += w
    return ts, [w / tot for w in ws]


def _gauss_nodes():
    """Standard normal truncated to +-4, matching PassionBudgetClampFactor."""
    lo, hi = -4.0, 4.0
    dz = (hi - lo) / GGRID
    zs, ws, tot = [], [], 0.0
    for i in range(GGRID):
        z = lo + (i + 0.5) * dz
        w = math.exp(-0.5 * z * z) * dz
        zs.append(z)
        ws.append(w)
        tot += w
    return zs, [w / tot for w in ws]


def grid_moments(C):
    """Mean and sd of the composite CONDITIONAL on q. Mirror of DispersionModel.Moments."""
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    n_skills = int(round(pdiv / major))
    efficiency = make_efficiency(C)
    TS, TW = _tri_nodes()
    ZS, ZW = _gauss_nodes()
    wsum = wS + wP

    def moments(p, q, with_noise=True):
        mag = (C["MinMagnitudeFloor"] + (C["MaxMagnitude"] - C["MinMagnitudeFloor"])
               * p["skillNoise"]) if with_noise else 0.0
        sig = (C["PassionBudgetSpreadMin"] + (C["PassionBudgetSpreadMax"]
               - C["PassionBudgetSpreadMin"]) * p["passionNoise"]) if with_noise else 0.0

        baseline = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
        s1 = s2 = 0.0
        for t, w in zip(TS, TW):
            lvl = base + baseline + t * mag
            lvl = 0.0 if lvl < 0.0 else (top if lvl > top else lvl)
            u = lvl / top
            s1 += w * u
            s2 += w * u * u
        # The pawn's AVERAGE over n_skills iid draws: variance divides by n_skills (CLT).
        s_var = max(0.0, s2 - s1 * s1) / n_skills

        bmean = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
        capacity = n_skills * (minor + (major - minor) * p["passionMajorBias"])
        eff = efficiency(p["passionMajorBias"])
        p1 = p2 = 0.0
        for z, w in zip(ZS, ZW):
            b = bmean + z * sig
            if sig > 0.0 and b < 1.0 and p["passionCountMin"] > 0.0:
                b = 1.0
            if b < 0.0:
                b = 0.0
            if b > capacity:
                b = capacity
            u = b * eff / pdiv
            if u > 1.0:
                u = 1.0
            p1 += w * u
            p2 += w * u * u
        p_var = max(0.0, p2 - p1 * p1)

        mu = (wS * s1 + wP * p1) / wsum
        var = (wS * wS * s_var + wP * wP * p_var) / (wsum * wsum)
        return mu, math.sqrt(var)

    return moments


def make_grid_score(C):
    """Dispersion-aware Best-of-N. Mirror of DispersionModel.BestOfN."""
    moments = grid_moments(C)

    def best_of_n(p, with_noise=True):
        eps, K = C["QualityClampEpsilon"], C["BetaConcentrationK"]
        m = min(max(p["averageQuality"], eps), 1.0 - eps)
        a, b = m * K, (1.0 - m) * K
        lb = math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
        dq = 1.0 / QGRID
        qs, dens, tot = [], [], 0.0
        for i in range(QGRID):
            q = (i + 0.5) * dq
            d = math.exp(lb + (a - 1.0) * math.log(q) + (b - 1.0) * math.log(1.0 - q))
            qs.append(q)
            dens.append(d)
            tot += d * dq
        wq = [d * dq / tot for d in dens]
        ms = [moments(p, q, with_noise) for q in qs]

        dx = 1.0 / XGRID
        F = []
        for j in range(XGRID):
            x = (j + 0.5) * dx
            acc = 0.0
            for (mu, sd), w in zip(ms, wq):
                # sd == 0 is the zero-noise self-check path; Phi would divide by zero.
                acc += w * (_phi((x - mu) / sd) if sd > 1e-12 else (1.0 if x >= mu else 0.0))
            F.append(acc)

        # The [0,1] bound IS the Clamp01 on the composite. Do not widen it.
        return {N: sum((1.0 - F[j] ** N) * dx for j in range(XGRID)) for N in (1, 5, 25, 50)}

    return best_of_n
```

- [ ] **Step 2: Add the zero-noise self-check to `main()` and print the dispersed table**

In `main()`, after `composite = make_composite(C)`, insert:

```python
    grid_score = make_grid_score(C)
    worst_selfcheck = 0.0
    for n, p in P.items():
        got = grid_score(p, with_noise=False)
        for N in (1, 5, 25, 50):
            want = expected_best_of_n(p, N, grids[n], composite)
            worst_selfcheck = max(worst_selfcheck, abs(got[N] - want))
    print(f"dispersion model self-check (zero noise vs analytic): {worst_selfcheck:.2e}")
    if worst_selfcheck > 1e-3:
        print("FAIL: the dispersion model does not reduce to the analytic score at zero noise")
        return 1

    print("\nDispersion-aware figures (REPORTED, not yet enforced -- see Task 4):")
    dispersed = {n: grid_score(p, with_noise=True) for n, p in P.items()}
    for n in P:
        cells = "  ".join(
            f"N={N}: {dispersed[n][N] / dispersed['Faithful'][N] * 100.0 - 100.0:+6.1f}%"
            for N in (1, 5, 25, 50))
        print(f"  {n:<12} {cells}")
```

- [ ] **Step 3: Run and verify the self-check plus the old gate**

Run: `python docs/tools/envelope_check.py; echo "EXIT=$?"`

Expected: `dispersion model self-check (zero noise vs analytic): 2.01e-04`, the new reported table showing `Wildcard  N=50: +48.1%`, the unchanged old table, `PASS`, and `EXIT=0`. The gate is still green because Rule 1 has not moved yet.

- [ ] **Step 4: Verify the quadrature agrees with the Monte Carlo**

Run: `python docs/tools/dispersion_mc.py 20260807 200000`

Compare Wildcard N=50: MC gives `0.5547`, the grid's raw value is `0.5519`. Agreement to 0.005 absolute is expected pre-retune — the Normal-per-q fit is weakest at `passionNoise` 0.85. Post-retune (Task 4) this tightens to 0.08pp.

- [ ] **Step 5: Commit**

```bash
git add docs/tools/envelope_check.py
git commit -m "feat: mirror the dispersion-aware Best-of-N in envelope_check, reported not enforced"
```

---

## Task 3: The C# model, with an in-game cross-check

Still not wired to any readout, so nothing player-visible changes and the existing gate keeps passing.

**Files:**
- Create: `Source/MathUtil.cs`
- Create: `Source/DispersionModel.cs`
- Modify: `Source/PawnVarianceSettings.cs` (one visibility modifier — Step 1a)
- Modify: `Source/VarianceProfile.cs` (the two scalar accessors — Step 3)
- Modify: `Source/DebugActions.cs`

**Interfaces:**
- Consumes: `Constants`, `VarianceProfileValues`, `VarianceProfileValues.GetBetaAlphaBeta(out float, out float)`, `PawnVarianceSettings.PassionPipEfficiency(float)`.
- Produces: `MathUtil.Erf(float)`, `MathUtil.NormalCdf(float)`, `DispersionModel.Moments(VarianceProfileValues, float q, out float mu, out float sd)`, `DispersionModel.BestOfN(VarianceProfileValues, int n, bool lowRes = false)`, `DispersionModel.OutcomeDensity(VarianceProfileValues, float[] into)`.

- [ ] **Step 1a: Make `PassionPipEfficiency` reachable — do this FIRST or Step 2 will not compile**

`DispersionModel.Moments` calls it, and at `Source/PawnVarianceSettings.cs:1362` it is declared
`private static`. A private member is inaccessible from another class: **CS0122, a hard compile
error**, not a warning. Change only the modifier:

```csharp
        internal static float PassionPipEfficiency(float majorBias)
```

`internal` rather than `public`: `DispersionModel` is in the same assembly, and this is an internal
modelling helper, not API. The three existing call sites (`:1403`, `:1445`, and the comment at
`:1410`) are unaffected — widening visibility never breaks a caller.

This edits a `DONE (REVIEWED)` file, but `PawnVarianceSettings.cs` is inside the Rule 8 sign-off.

- [ ] **Step 1: Create the error function**

Create `Source/MathUtil.cs`:

```csharp
using UnityEngine;

namespace PawnVarianceMod
{
    // .NET Framework 4.7.2 (this project's TargetFramework) has no Math.Erf -- it arrived in
    // .NET Core. The dispersion model needs a normal CDF, so we carry our own.
    //
    // envelope_check.py uses Python's math.erf, which is near machine precision, so THIS
    // approximation sets the accuracy floor for the whole two-implementation contract. A&S 7.1.26
    // is good to 1.5e-7 absolute -- about 2600x tighter than the gate's 0.5pp tolerance -- and the
    // error averages rather than accumulating across the grid, because every use is inside a
    // normalised weighted sum.
    public static class MathUtil
    {
        public static float Erf(float x)
        {
            // Abramowitz & Stegun 7.1.26.
            float sign = x < 0f ? -1f : 1f;
            x = Mathf.Abs(x);

            const float a1 = 0.254829592f;
            const float a2 = -0.284496736f;
            const float a3 = 1.421413741f;
            const float a4 = -1.453152027f;
            const float a5 = 1.061405429f;
            const float p = 0.3275911f;

            float t = 1f / (1f + p * x);
            float y = 1f - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Mathf.Exp(-x * x);
            return sign * y;
        }

        public static float NormalCdf(float z)
        {
            return 0.5f * (1f + Erf(z / Mathf.Sqrt(2f)));
        }
    }
}
```

- [ ] **Step 2: Create the dispersion model**

Create `Source/DispersionModel.cs`:

```csharp
using UnityEngine;

namespace PawnVarianceMod
{
    // Deterministic, dispersion-aware Best-of-N. Mirrors make_grid_score / grid_moments in
    // docs/tools/envelope_check.py -- IF YOU CHANGE ONE, CHANGE BOTH. The in-game
    // "Verify Best-of-N" action enforces that mechanically.
    //
    // Monte Carlo deliberately does NOT live here: the gate cross-checks this against the Python
    // at 0.5pp, which only works while both sides are reproducible.
    public static class DispersionModel
    {
        public const int QNodes = 256;
        public const int XNodes = 512;
        public const int TriNodes = 65;
        public const int GaussNodes = 65;

        // Drag-time resolution. Measured drift vs the full grid is 0.001pp, i.e. free, so this is
        // taken unconditionally rather than gated on a profiling result.
        public const int QNodesDrag = 64;
        public const int XNodesDrag = 128;

        private static float[] triT, triW, gaussZ, gaussW;

        private static void EnsureNodes()
        {
            if (triT != null) return;

            triT = new float[TriNodes];
            triW = new float[TriNodes];
            float dt = 2f / TriNodes, ttot = 0f;
            for (int i = 0; i < TriNodes; i++)
            {
                float t = -1f + (i + 0.5f) * dt;
                triT[i] = t;
                triW[i] = (1f - Mathf.Abs(t)) * dt;   // triangular density 1-|t|
                ttot += triW[i];
            }
            for (int i = 0; i < TriNodes; i++) triW[i] /= ttot;

            gaussZ = new float[GaussNodes];
            gaussW = new float[GaussNodes];
            float dz = 8f / GaussNodes, gtot = 0f;    // +-4 sigma, matching PassionBudgetClampFactor
            for (int i = 0; i < GaussNodes; i++)
            {
                float z = -4f + (i + 0.5f) * dz;
                gaussZ[i] = z;
                gaussW[i] = Mathf.Exp(-0.5f * z * z) * dz;
                gtot += gaussW[i];
            }
            for (int i = 0; i < GaussNodes; i++) gaussW[i] /= gtot;
        }

        // Mean and sd of the composite CONDITIONAL on q. The two axes are independent given q, so
        // their means and variances combine under the composite weights.
        public static void Moments(VarianceProfileValues v, float q, out float mu, out float sd)
        {
            EnsureNodes();

            float wS = Constants.CompositeSkillWeight;
            float wP = Constants.CompositePassionWeight;
            float wsum = wS + wP;
            float top = Constants.AssumedMaxSkillLevel;
            float pdiv = Constants.MaxPassionPips;
            int nSkills = Mathf.RoundToInt(pdiv / Constants.MajorPassionCost);

            float mag = Mathf.Lerp(Constants.MinMagnitudeFloor, Constants.MaxMagnitude,
                                   v.SkillNoiseScalar);
            float baseline = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, q);

            float s1 = 0f, s2 = 0f;
            for (int i = 0; i < TriNodes; i++)
            {
                float lvl = Mathf.Clamp(Constants.AssumedVanillaSkillBaseline
                                        + baseline + triT[i] * mag, 0f, top);
                float u = lvl / top;
                s1 += triW[i] * u;
                s2 += triW[i] * u * u;
            }
            // Pawn's AVERAGE over nSkills iid draws -> variance divides by nSkills.
            float sVar = Mathf.Max(0f, s2 - s1 * s1) / nSkills;

            float sig = Mathf.Lerp(Constants.PassionBudgetSpreadMin,
                                   Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
            float bmean = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
            float capacity = nSkills * (Constants.MinorPassionCost
                + (Constants.MajorPassionCost - Constants.MinorPassionCost) * v.passionMajorBias);
            float eff = PawnVarianceSettings.PassionPipEfficiency(v.passionMajorBias);

            float p1 = 0f, p2 = 0f;
            for (int i = 0; i < GaussNodes; i++)
            {
                float b = bmean + gaussZ[i] * sig;
                if (sig > 0f && b < 1f && v.passionCountMin > 0f) b = 1f;   // vanilla's floor
                if (b < 0f) b = 0f;
                if (b > capacity) b = capacity;
                float u = Mathf.Min(1f, b * eff / pdiv);
                p1 += gaussW[i] * u;
                p2 += gaussW[i] * u * u;
            }
            float pVar = Mathf.Max(0f, p2 - p1 * p1);

            mu = (wS * s1 + wP * p1) / wsum;
            sd = Mathf.Sqrt((wS * wS * sVar + wP * wP * pVar) / (wsum * wsum));
        }

        // F(x) on a midpoint grid over [0,1]. The [0,1] domain IS the composite's Clamp01 --
        // integrating the unclamped normal CDF over [0,1] is exact for the clamped variable.
        // Do not widen the range or pre-clamp F.
        private static float[] BuildCdf(VarianceProfileValues v, int qNodes, int xNodes)
        {
            v.GetBetaAlphaBeta(out float alpha, out float beta);
            float dq = 1f / qNodes;

            var qs = new float[qNodes];
            var wq = new float[qNodes];
            float total = 0f;
            for (int i = 0; i < qNodes; i++)
            {
                float q = (i + 0.5f) * dq;
                qs[i] = q;
                wq[i] = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                total += wq[i] * dq;
            }
            for (int i = 0; i < qNodes; i++) wq[i] = wq[i] * dq / total;

            var mus = new float[qNodes];
            var sds = new float[qNodes];
            for (int i = 0; i < qNodes; i++) Moments(v, qs[i], out mus[i], out sds[i]);

            float dx = 1f / xNodes;
            var F = new float[xNodes];
            for (int j = 0; j < xNodes; j++)
            {
                float x = (j + 0.5f) * dx;
                float acc = 0f;
                for (int i = 0; i < qNodes; i++)
                {
                    // sd == 0 at zero noise; NormalCdf would divide by zero. Step function there.
                    acc += wq[i] * (sds[i] > 1e-12f
                        ? MathUtil.NormalCdf((x - mus[i]) / sds[i])
                        : (x >= mus[i] ? 1f : 0f));
                }
                F[j] = acc;
            }
            return F;
        }

        public static float BestOfN(VarianceProfileValues v, int n, bool lowRes = false)
        {
            int xNodes = lowRes ? XNodesDrag : XNodes;
            float[] F = BuildCdf(v, lowRes ? QNodesDrag : QNodes, xNodes);
            float dx = 1f / xNodes;
            float acc = 0f;
            for (int j = 0; j < xNodes; j++) acc += (1f - Mathf.Pow(F[j], n)) * dx;
            return acc;
        }

        // E[composite | q] -- the dispersion-aware "typical pawn" at a given quality.
        public static float TypicalAt(VarianceProfileValues v, float q)
        {
            Moments(v, q, out float mu, out _);
            return mu;
        }

        // The realised-outcome density for the header curve. Analytic Gaussian mixture rather than
        // finite differences of F -- same inputs, visibly smoother line.
        //
        // Moments() is hoisted out of the x loop, exactly as BuildCdf does it. Calling it inside
        // would evaluate it qNodes*xNodes times (131k per frame), each doing its own tri+gauss
        // quadrature internally -- ~500x the work for an identical result, since mu and sd do not
        // depend on x. This is the single most expensive thing on the editor's per-frame path.
        public static void OutcomeDensity(VarianceProfileValues v, float[] into)
        {
            v.GetBetaAlphaBeta(out float alpha, out float beta);
            int qNodes = QNodes, xNodes = into.Length;
            float dq = 1f / qNodes;

            var wq = new float[qNodes];
            var mus = new float[qNodes];
            var sds = new float[qNodes];
            float total = 0f;
            for (int i = 0; i < qNodes; i++)
            {
                float q = (i + 0.5f) * dq;
                wq[i] = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                total += wq[i] * dq;
                Moments(v, q, out mus[i], out sds[i]);
            }
            for (int i = 0; i < qNodes; i++) wq[i] = wq[i] * dq / total;

            float invSqrt2Pi = 1f / Mathf.Sqrt(2f * Mathf.PI);
            for (int j = 0; j < xNodes; j++)
            {
                float x = (j + 0.5f) / xNodes;
                float acc = 0f;
                for (int i = 0; i < qNodes; i++)
                {
                    float sd = sds[i];
                    if (sd <= 1e-12f) continue;
                    float z = (x - mus[i]) / sd;
                    acc += wq[i] * invSqrt2Pi / sd * Mathf.Exp(-0.5f * z * z);
                }
                into[j] = acc;
            }
        }
    }
}
```

> **Note on `v.SkillNoiseScalar` / `v.PassionNoiseScalar`.** Task 5 renames the stored fields to
> real units. To keep this task independent of that rename, add two read-only helpers to
> `VarianceProfileValues` now that return the 0–1 scalar the Lerps expect:
>
> ```csharp
> public float SkillNoiseScalar => skillNoise;
> public float PassionNoiseScalar => passionNoise;
> ```
>
> Task 5 changes only these two accessors, and `DispersionModel` needs no edit.

- [ ] **Step 3: Add the scalar accessors**

In `Source/VarianceProfile.cs`, inside `VarianceProfileValues`, immediately after the
`passionMajorBias` field declaration:

```csharp
        // Indirection so DispersionModel and the appliers do not care whether the stored field is
        // a 0-1 scalar or a real-unit spread. Task 5 of the dispersion plan changes ONLY these.
        public float SkillNoiseScalar => skillNoise;
        public float PassionNoiseScalar => passionNoise;
```

> **Note for Task 5:** these lines shift every line number below them in `VarianceProfile.cs` by
> about four. Task 5 cites `:97-98` and `:125-126` — those are correct **today** but will read
> `~:101-102` and `~:129-130` once this step lands. Task 5 anchors on surrounding code instead of
> the numbers; do not trust either number literally.

- [ ] **Step 4: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`

Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 5: Add an in-game cross-check action**

In `Source/DebugActions.cs`, add a new action in the `Varied Pawns` category. Declare it
`PlayingOnMap` only — ORing states makes an action *less* visible, not more.

```csharp
        [DebugAction("Varied Pawns", "Dump dispersion-aware Best-of-N",
                     allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpDispersionBestOfN()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("[PawnVarianceMod] Dispersion-aware Best-of-N (C# side)");
            sb.AppendLine($"  grid q={DispersionModel.QNodes} x={DispersionModel.XNodes} "
                          + $"tri={DispersionModel.TriNodes} gauss={DispersionModel.GaussNodes}");
            sb.AppendLine("  profile           N=1      N=5     N=25     N=50");
            foreach (var preset in VarianceProfiles.Presets)
            {
                var v = preset.MakeValues();
                sb.AppendLine($"  {preset.displayName,-12}"
                    + $"{DispersionModel.BestOfN(v, 1),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 5),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 25),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 50),9:F4}");
            }
            Log.Message(sb.ToString());
        }
```

- [ ] **Step 6: Deploy and run it in game**

```bash
tasklist /FI "IMAGENAME eq RimWorldWin64.exe"    # must show no instance
dotnet build Source/PawnVarianceMod.csproj
cp Assemblies/PawnVarianceMod.dll Assemblies/PawnVarianceMod.pdb \
   "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/"
```

Then through GABS: `games_start rimworld` → `games_connect` → `start_debug_game_ready` with
`readiness: "visual"` → `execute_debug_action` on
`Actions\Dump dispersion-aware Best-of-N`.

**GABS must launch RimWorld itself** — it injects the bridge port and token at launch, so a
hand-started game cannot connect.

Expected: the C# figures match Task 2's Python reported table to ~1e-4. Wildcard N=50 ≈ `0.5519`.
If they diverge, the two implementations disagree — stop and find out why before Task 4.

- [ ] **Step 7: Commit**

```bash
git add Source/MathUtil.cs Source/DispersionModel.cs Source/DebugActions.cs Source/VarianceProfile.cs
git commit -m "feat: add the deterministic dispersion model and an in-game cross-check"
```

---

## Task 4: Switch the readouts and Rule 1 onto the model, and retune Wildcard — ONE COMMIT

**These cannot be split.** Switching the metric on before the retune puts Wildcard at ≈+48%, and
`envelope_check.py` returns exit code 1 on any Rule 1 breach — that intermediate state cannot
satisfy the project's own gate. Land the metric first *within* the commit, then tune against it.

**Files:**
- Modify: `Source/VarianceProfile.cs` (Wildcard values)
- Modify: `Source/PawnVarianceSettings.cs` (readouts + cache)
- Modify: `Source/ProfileEditorTab.cs` (Step 2 — the typical readout and the mean marker)
- Modify: `docs/tools/envelope_check.py` (Rule 1 onto the dispersed figures)
- Modify: `Source/EnvelopeFigures.g.cs` (regenerated, never hand-edited)
- Modify: `HANDOVER.md` (every pasted table)

**Interfaces:**
- Consumes: `DispersionModel.BestOfN`, `DispersionModel.TypicalAt`, `make_grid_score`.
- Produces: no new API. Task 6 and 7 rely on the readouts already reading from the model.

- [ ] **Step 1: Point the C# readouts at the model**

In `Source/PawnVarianceSettings.cs`, change `CalculateBestOfNScoreCore` to delegate. Keep
`CalculateCompositeScore` exactly as it is — it remains the mean-band function and the zero-noise
reference.

```csharp
        private static float CalculateBestOfNScoreCore(VarianceProfileValues v, int n)
        {
            return DispersionModel.BestOfN(v, n);
        }
```

The existing single-slot cache in `CalculateBestOfNScore` is unchanged and still applies.

- [ ] **Step 2: Point the typical-pawn readout at the model**

The typical figure must also be dispersion-aware — mean-zero noise only leaves it unchanged where
the composite is linear, and `Clamp(0,20)` breaks that for any band under the floor.

**There are exactly three `CalculateCompositeScore` call sites in `ProfileEditorTab.cs`, not one.**
"Wherever the Row 3 readout is" is not a sufficient instruction — two of the three are on the curve,
and leaving them behind puts a mean-only marker on a dispersion-aware curve. Handle all three:

| Site | What it is | Change |
|---|---|---|
| `:261` `meanComposite` | the "→ Typical" readout string | **→ `DispersionModel.TypicalAt(v, v.averageQuality)`** |
| `:491` `rawComposite` | the curve's x-position per sample | **leave alone in this task** — Task 7 replaces this whole loop with `OutcomeDensity`, and touching it here would be undone |
| `:514` `meanRawComposite` | the yellow mean marker on the curve | **→ `DispersionModel.TypicalAt(v, v.averageQuality)`** |

So at `:261`:

```csharp
            float meanComposite = DispersionModel.TypicalAt(v, v.averageQuality);
```

and at `:514`:

```csharp
            float meanRawComposite = DispersionModel.TypicalAt(v, v.averageQuality);
```

Both now compute the same quantity from the same inputs, as they did before.

While at `:261`, the tooltip immediately below it (`:266-270`) states the readout "structurally
cannot see" `skillNoise`/`passionNoise`. **That is now false and must be rewritten** — but keep the
exclusion clause's real point (Distinct's negative figure is not weakness), per Task 7 Step 3.

- [ ] **Step 3: Retune Wildcard**

In `Source/VarianceProfile.cs`, in the `WildSpread` preset:

```csharp
                passionNoise = 0.50f,
                passionMajorBias = 0.35f,
                skillShiftMax = 2.0f,
```

Leave `skillNoise = 0.85f`, `skillShiftMin = -5.0f`, `passionCountMin = 2.2f`,
`passionCountMax = 10.8f`, `traitCountMin = 0f`, `traitCountMax = 8f`,
`averageQuality = 0.37f` untouched.

Replace the stale measured figures in the Wildcard comment block with:

```csharp
                // Retuned 2026-08-07 for the dispersion-aware envelope. Under the old mean-band
                // metric Wildcard read +22.3% at N=50; measured with dispersion it was +49.0%,
                // OUTSIDE the +-35% envelope. Rule 1 passed only because the metric could not see
                // the axis that broke it.
                //
                // passionNoise 0.85 -> 0.50 is the load-bearing change: passion budget is a single
                // per-pawn draw, so it reaches Best-of-N in full. skillNoise is deliberately LEFT
                // at 0.85 -- taking it to 0.00 moves N=50 by only 0.3pp, because per-skill noise
                // averages down by sqrt(12) and is then censored by Clamp(0,20).
                //
                // passionMajorBias 0.6 -> 0.35 nerfs through the pip EXCHANGE RATE, not through
                // spread (R 1.99 -> 1.91; Rule 7 trigger). skillShiftMax 4.2 -> 2.0 lowers the
                // cherry-picked ceiling. Result: -11.5/+5.1/+14.7/+17.7%, 17.3pp of margin,
                // dispersion still 1.71x Faithful, still below Faithful at N=1.
```

- [ ] **Step 4: Switch Rule 1 in the tool onto the dispersed figures**

In `docs/tools/envelope_check.py`'s `main()`, replace the score used for the enforced table and the
Rule 1/Rule 2 checks with `make_grid_score(C)`'s output, and delete the "REPORTED, not yet
enforced" block added in Task 2 — it is now the main table.

Keep `expected_best_of_n` and the zero-noise self-check: the self-check is what proves the new
model reduces to the old one.

- [ ] **Step 5: Run the tool and regenerate the figures**

Run: `python docs/tools/envelope_check.py; echo "EXIT=$?"`

Expected: `EXIT=0`, `PASS`, Wildcard at `+17.7%` at N=50, and
`Source/EnvelopeFigures.g.cs: REWRITTEN` (every figure moves, since dispersion is now included).

If it prints `FAIL`, do not proceed — read which preset breached.

- [ ] **Step 6: Paste the verbatim output into HANDOVER.md**

Replace the code block under "The verified envelope" with the tool's exact stdout. Rule 6: pasted,
never hand-edited.

- [ ] **Step 6a: Rule 7 — update the `R` table, which the pasted block does NOT cover**

`passionMajorBias` 0.6 → 0.35 moves `R` for Wildcard, and the tool's own footer says so:
*"If any number moved, update the table in HANDOVER.md 'The skill <-> passion exchange rate'."*

That table is at **`HANDOVER.md:252-254`** and is indexed by bias, not by preset — so Step 6's paste
does not touch it. The tool's `R = 1.96 ... at vanilla bias 0.5` line is anchored at vanilla bias and
does **not** move either; only the per-bias table does. Add Wildcard's new column:

| Major bias | 0.00 | **0.35 (`Wildcard`)** | 0.50 (vanilla, `Faithful`) | 0.70 (`Sovereign`) | 1.00 |
|---|---|---|---|---|---|
| `R` | 1.77 | **1.91** | **1.96** | 2.01 | 2.08 |

Confirm `1.91` against the tool's own printed range (1.77 at bias 0 .. 2.08 at bias 1) rather than
copying it from here. Also note capacity moves `15.6 → 14.1` pips — still non-binding, but state it
if the surrounding prose quotes the old figure.

- [ ] **Step 7: Rebuild, redeploy, and run the in-game gate**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Stop RimWorld, copy the DLL and PDB, restart via GABS, and run
`Actions\Verify Best-of-N against envelope_check.py`.

**The gate needs no code change to cover the dispersed score, and this is why:** it diffs the live
`CalculateBestOfNScoreCore` against `EnvelopeFigures.g.cs`. Step 1 repointed that method at
`DispersionModel`, and Step 5 regenerated the `.g.cs` from `make_grid_score`. So the *existing* gate
is now comparing the two new implementations automatically. The spec's §8.2 requirement that the
gate "must be extended" is satisfied by the repoint, not by new gate code — if you find yourself
writing new comparison logic here, re-read Step 1, because it means the repoint did not land.

Expected: `PASS: the live integrator agrees with the reference everywhere`, 32/32.

**GABS must launch RimWorld itself** (`games_start rimworld`) — it injects the bridge port and token
at launch, so a hand-started game cannot connect.

- [ ] **Step 8: Confirm the retune held in real pawns**

Run `Actions\Roll pawns and dump distribution` at 1000 pawns against Wildcard.

**Read the `ACTUALLY RESOLVED TO:` line.** A faction, race or xenotype override outranks the Active
Colony Profile, and that has already invalidated two 1000-pawn runs on this project. If it does not
say Wildcard, fix the override before believing any figure.

Expected: per-skill median ≈ 1.0 and per-pawn skill sd comparable to the pre-retune run —
dispersion held. The passion budget mean drops, which is the intended nerf.

- [ ] **Step 9: Commit — one commit, everything above**

```bash
git add Source/VarianceProfile.cs Source/PawnVarianceSettings.cs Source/ProfileEditorTab.cs \
        Source/EnvelopeFigures.g.cs docs/tools/envelope_check.py HANDOVER.md
git commit -m "feat: enforce the envelope on dispersion-aware scores and retune Wildcard

The composite could not see skillNoise or passionNoise, so Best-of-N -- a MAX statistic, which
rewards spread -- systematically understated high-dispersion profiles. Measured by Monte Carlo,
Wildcard sat at +49.0% at N=50 against a reported +22.3%: OUTSIDE the +-35% envelope. Rule 1 was
passing only because the metric enforcing it was blind to the axis that broke it.

Wildcard: passionNoise 0.85 -> 0.50, passionMajorBias 0.6 -> 0.35, skillShiftMax 4.2 -> 2.0.
Lands at -11.5/+5.1/+14.7/+17.7% with 17.3pp of margin, dispersion still 1.71x Faithful and still
below Faithful at N=1. skillNoise stays 0.85 -- it moves N=50 by 0.3pp across its whole range.

The metric switch and the retune are one commit deliberately: between them the gate cannot pass.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Rename the noise fields to real units

Two independent silent-truncation traps here. Both are invisible to every gate.

**Files:**
- Modify: `Source/VarianceProfile.cs`
- Modify: `Source/SkillVarianceApplier.cs`
- Modify: `Source/PassionVarianceApplier.cs`
- Modify: `Source/DebugActions.cs` (comment text only)
- Modify: `docs/tools/envelope_check.py`
- **NOT** `Source/SettingsTransfer.cs` — reviewed, and it has no references to either field.

**Interfaces:**
- Consumes: `Constants.MaxMagnitude`, `Constants.PassionBudgetSpreadMax`.
- Produces: `VarianceProfileValues.skillSpread` (levels, 0–2.45), `VarianceProfileValues.passionSpread` (pips, 0–4.0), with `SkillNoiseScalar` / `PassionNoiseScalar` still returning the 0–1 form.

- [ ] **Step 1: Rename the fields and rescale every preset**

In `Source/VarianceProfile.cs`, rename `skillNoise` → `skillSpread` and `passionNoise` →
`passionSpread`, then rescale all eight presets (`× √6` and `× 4` respectively).

> [!CAUTION]
> **Use these literals exactly. Do NOT round them to two decimals.** Step 7 requires
> `EnvelopeFigures.g.cs: unchanged`, and that file stores scores at **six decimals**. Two-decimal
> literals do not round-trip: `0.49 × √6 = 1.20025` against the true magnitude `1.20000`, a 2e-4
> relative shift that lands in the 6th decimal of several scores and prints `REWRITTEN`. A worker
> would then hunt a conversion bug that does not exist.
>
> Measured round-trip error of the six-decimal literals below, `spread × √6` against the true
> `Lerp(0, 6, noise)` magnitude, in float32: **worst case 7.9e-7 relative** (Sovereign), i.e. a few
> ulps — versus **2.1e-4 for the two-decimal form**, 250× larger. The score depends only weakly on
> magnitude, so the six-decimal error stays well inside the 6th decimal of every figure.

| preset | old `skillNoise` | `skillSpread` (= × √6) | old `passionNoise` | `passionSpread` (= × 4) |
|---|---|---|---|---|
| Faithful | 0.2 | `0.489898f` | 0.25 | `1.0f` |
| Distinct | 0.35 | `0.857321f` | 0.35 | `1.4f` |
| Wildcard | 0.85 | `2.082066f` | **0.50** (Task 4's retune) | `2.0f` |
| Desperate | 0.25 | `0.612372f` | 0.25 | `1.0f` |
| Elite | 0.22 | `0.538888f` | 0.25 | `1.0f` |
| Sovereign | 0.24 | `0.587878f` | 0.25 | `1.0f` |
| Specialist | 0.25 | `0.612372f` | 0.25 | `1.0f` |
| Scavenger | 0.25 | `0.612372f` | 0.25 | `1.0f` |

Verify the "old" column against the live file before rescaling — these are the values as of this
plan's writing, at `VarianceProfile.cs:227-228, 250-251, 275-276, 325-326, 350-351, 371-372,
401-402, 422-423`. **Wildcard's `passionNoise` reads 0.85 in the file but Task 4 already changed it
to 0.50**; rescale from 0.50, not 0.85, or you will silently revert the retune.

The two-decimal values (`0.49`, `0.86`, `2.08`, …) still appear in `HANDOVER.md`'s dispersion table —
that is display rounding produced by the tool's `%.2f`, and it stays as-is. Do not reconcile the two.

- [ ] **Step 2: Fix the two accessors — this is the whole point of the indirection**

```csharp
        // skillSpread stores a STANDARD DEVIATION in levels. The applier's triangular term
        // (Rand.Value+Rand.Value-1) has variance 1/6, so sd = magnitude/sqrt(6) and the Lerp
        // scalar is sd*sqrt(6)/MaxMagnitude. Getting this wrong divides all skill noise by 2.449
        // and NOTHING would catch it.
        public float SkillNoiseScalar => skillSpread * Mathf.Sqrt(6f) / Constants.MaxMagnitude;

        // passionSpread is already the Gaussian's sigma in pips -- no conversion. The two fields
        // are NOT symmetric; do not merge these into one helper.
        public float PassionNoiseScalar => passionSpread / Constants.PassionBudgetSpreadMax;
```

> [!CAUTION]
> **Both accessors are plain ratios, and that is only correct while the Lerp low endpoints are
> zero.** Consumers feed these into `Lerp(MinMagnitudeFloor, MaxMagnitude, s)` and
> `Lerp(PassionBudgetSpreadMin, PassionBudgetSpreadMax, s)`. A ratio inverts a Lerp only when the
> low endpoint is 0. Both are `0f` today (`Constants.cs:16` and `:27`) — but `MinMagnitudeFloor` was
> `0.5f` until 2026-08-06, so this is a live hazard, not a hypothetical.
>
> The general inverse is `(value - lo) / (hi - lo)`. Rather than write that, pin the assumption
> where it would break, as a comment on **both** accessors:
>
> ```csharp
>         // Valid ONLY while the Lerp low endpoint is 0. If MinMagnitudeFloor ever goes non-zero
>         // again (it was 0.5f before 2026-08-06), this must become
>         //   (skillSpread*sqrt(6) - MinMagnitudeFloor) / (MaxMagnitude - MinMagnitudeFloor).
>         // Nothing in any gate would catch the omission: both implementations read this accessor.
> ```
>
> Same comment, `PassionBudgetSpreadMin`/`Max`, on the passion one.

- [ ] **Step 3: Fix the clamp bounds — a second, independent truncation bug**

`Mathf.Clamp01` on a 0–2.45 field silently clips Wildcard's `2.08` and Distinct's `1.40` to `1.00`,
on every load, edit and import. Find the two `Mathf.Clamp01(skillNoise)` / `(passionNoise)` lines —
`:97-98` before Task 3, roughly `:101-102` after it added the accessors — and replace them:

```csharp
            skillSpread = Mathf.Clamp(skillSpread, 0f,
                                      Constants.MaxMagnitude / Mathf.Sqrt(6f));
            passionSpread = Mathf.Clamp(passionSpread, 0f, Constants.PassionBudgetSpreadMax);
```

- [ ] **Step 4: Fix the Scribe defaults**

Find the two `Scribe_Values.Look(ref skillNoise, ...)` / `(ref passionNoise, ...)` calls — `:125-126`
before Task 3, roughly `:129-130` after. Their defaults are still the old 0–1 values (`0.2f` /
`0.25f`). Rescale them, or every loaded profile picks up near-zero spread:

```csharp
            Scribe_Values.Look(ref skillSpread, "skillSpread", 0.489898f);
            Scribe_Values.Look(ref passionSpread, "passionSpread", 1.0f);
```

The **field initialisers** at the top of `VarianceProfileValues` (`:28` `= 0.35f`, `:35` `= 0.25f`)
are a separate pair and also need rescaling, to `0.857321f` and `1.0f`. They are easy to miss
because they sit ~90 lines from the Scribe calls.

- [ ] **Step 5: Update the two appliers**

`Source/SkillVarianceApplier.cs:57`:

```csharp
            float magnitude = Mathf.Lerp(Constants.MinMagnitudeFloor, Constants.MaxMagnitude,
                                         v.SkillNoiseScalar);
```

`Source/PassionVarianceApplier.cs:62`:

```csharp
            float spread = Mathf.Lerp(Constants.PassionBudgetSpreadMin,
                                      Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
```

- [ ] **Step 6: Update the Python side — three places, one of them load-bearing**

In `docs/tools/envelope_check.py`:
- **`:71`** — the field-name list `parse_profiles` pulls from `VarianceProfile.cs`. Change
  `"skillNoise", "passionNoise"` to `"skillSpread", "passionSpread"`. **Miss this and the tool
  fails to parse or silently loses the fields.**
- **`:198-199`** in `make_spread` — the values are now sd directly:
  ```python
          magnitude = p["skillSpread"] * math.sqrt(6.0)
          budget_sigma = p["passionSpread"]
  ```
- **`:378-383`** — the dispersion table's column headers and the values it prints.
- `grid_moments` — replace the two `Lerp` reconstructions with the same direct reads.
- `make_grid_score`'s caller and `dispersion_mc.py`'s `simulate` — both build `mag`/`sig` from the
  old `Lerp(..., p["skillNoise"])` form. Same direct-read substitution, or the MC and the grid stop
  agreeing and Task 1's ground truth silently measures a different model.

**`Source/DebugActions.cs`:** the only `skillNoise`/`passionNoise` occurrences are in **comments**
(`:326` and `:721`) — prose, not code. `:721` in particular explains the `magnitude/sqrt(6)`
prediction and should be reworded to say the field now *stores* that sd rather than deriving it.

**Do NOT touch `Source/SettingsTransfer.cs`.** It is `[x]` reviewed (Rule 8, and not in the sign-off)
and it has **zero** references to either field — it round-trips profiles through
`VarianceProfileValues.ExposeData()`, so Step 4's `Scribe` rename covers import/export for free.
An earlier draft listed it; that was based on a wrong reading of the file.

- [ ] **Step 7: Verify nothing moved**

Run: `python docs/tools/envelope_check.py; echo "EXIT=$?"`

Expected: `EXIT=0`, `PASS`, `Source/EnvelopeFigures.g.cs: unchanged`, and the dispersion table
reporting the **same** per-skill sd values as before the rename (Faithful `0.49 lv`, Wildcard
`2.08 lv`). **A pure rename must move no number.** If any figure moved, a conversion is wrong.

- [ ] **Step 8: Verify in game — the check that catches a √6 error in the APPLIER**

Build, redeploy, and run `Actions\Roll pawns and dump distribution` at 1000 pawns on Wildcard.

**GABS must launch RimWorld itself** (`games_start rimworld`) — a hand-started game cannot connect.
**Read the `ACTUALLY RESOLVED TO:` line**: a faction, race or xenotype override outranks the Active
Colony Profile and has already invalidated two 1000-pawn runs here. If it does not say Wildcard, fix
the override before believing any figure.

Expected: per-skill sd matching the pre-rename run (≈2.08 lv). If it came out ~2.4× narrower,
`SkillNoiseScalar` is missing its `√6`.

**Also re-run the automated gate in the same session** —
`Actions\Verify Best-of-N against envelope_check.py`, expecting 32/32. An earlier draft called the
pawn dump "the only check that would catch the √6 bug"; that stopped being true at Task 4. Since
`CalculateBestOfNScoreCore` now routes through `DispersionModel`, which reads `SkillNoiseScalar`, a
C#-side √6 error diverges from the Python and the 0.5pp gate catches it.
The pawn dump remains the only instrument that catches the error if **both** sides drop the `√6`
together — which is the realistic failure, since one person edits both.

- [ ] **Step 9: Commit**

```bash
git add Source/ docs/tools/envelope_check.py
git commit -m "refactor: store noise as real-unit standard deviations rather than 0-1 scalars"
```

---

## Task 6: Slider units and derived readouts

**Files:**
- Modify: `Source/ProfileEditorTab.cs`

**Interfaces:**
- Consumes: `v.skillSpread`, `v.passionSpread`, `Constants.MaxMagnitude`, `Constants.PassionBudgetSpreadMax`, `Constants.PassionBudgetClampFactor`.

- [ ] **Step 1: Replace the skill noise slider**

At `Source/ProfileEditorTab.cs:333-335`:

```csharp
            float sMax = Constants.MaxMagnitude / Mathf.Sqrt(6f);
            Widgets.Label(noiseLabelRect, $"Skill spread:  ±{v.skillSpread:F2} lv");
            float sSpreadVal = Widgets.HorizontalSlider(noiseRow.RightPart(0.56f),
                                                        v.skillSpread, 0f, sMax);
            if (EditingCustom) v.skillSpread = sSpreadVal;
```

- [ ] **Step 2: Add the derived lines under it**

The extreme matters because triangular noise has `sd = bound/√6 ≈ 0.41 × bound` — a label showing
only the typical hides that one skill can move 5 levels, and one showing only the bound overstates
the normal pawn by ~2.5×.

**Use the profile's own Beta median, not `q = 0.5`.** The band is evaluated at the quality a typical
pawn of *this* profile actually rolls, and for a skewed profile those differ materially — Wildcard's
`averageQuality` 0.37 gives a median near `q ≈ 0.354`, not 0.5. Add the helper once, next to
`GetBetaAlphaBeta` in `Source/VarianceProfile.cs`:

```csharp
        // Beta has no closed-form median; this is the standard (a-1/3)/(a+b-2/3) approximation,
        // accurate to ~1e-3 for a,b > 1. Used for derived readouts only, never for scoring.
        public float MedianQuality()
        {
            GetBetaAlphaBeta(out float a, out float b);
            if (a <= 1f || b <= 1f) return Mathf.Clamp01(a / (a + b));   // fall back to the mean
            return Mathf.Clamp01((a - 1f / 3f) / (a + b - 2f / 3f));
        }
```

`BetaConcentrationK` is `8f`, so `a = 8m` and `b = 8(1-m)`: both exceed 1 for `m` in
`[0.125, 0.875]`, which covers every shipped preset but **not** the whole slider range — hence the
guard. Then:

```csharp
            Rect skillDerived = listing.GetRect(18f);
            float skillMag = v.skillSpread * Mathf.Sqrt(6f);
            float qMed = v.MedianQuality();
            float bandAtMedian = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, qMed);
            float lo = Mathf.Max(0f, Constants.AssumedVanillaSkillBaseline
                                     + bandAtMedian - v.skillSpread);
            float hi = Mathf.Min(Constants.AssumedMaxSkillLevel,
                                 Constants.AssumedVanillaSkillBaseline
                                 + bandAtMedian + v.skillSpread);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(skillDerived,
                $"extreme ±{skillMag:F1} lv per skill · most skills land {lo:F1} – {hi:F1}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
```

The spec's worked example (`most skills land 0.4 – 4.6` for Wildcard) assumes this median form —
`q = 0.5` gives different numbers and will not reproduce it.

- [ ] **Step 3: Replace the passion noise slider and its derived line**

At `Source/ProfileEditorTab.cs:394-396`:

```csharp
            Widgets.Label(passionNoiseLabelRect, $"Passion spread:  ±{v.passionSpread:F2} pips");
            float pSpreadVal = Widgets.HorizontalSlider(leftHalf.RightPart(0.46f),
                v.passionSpread, 0f, Constants.PassionBudgetSpreadMax);
            if (EditingCustom) v.passionSpread = pSpreadVal;
```

then the derived line immediately under it, mirroring Step 2's shape:

```csharp
            Rect passionDerived = listing.GetRect(18f);
            float pipExtreme = v.passionSpread * Constants.PassionBudgetClampFactor;
            float qMedP = v.MedianQuality();
            float budgetMid = Mathf.Lerp(v.passionCountMin, v.passionCountMax, qMedP);
            float bLo = Mathf.Max(0f, budgetMid - v.passionSpread);
            float bHi = budgetMid + v.passionSpread;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(passionDerived,
                $"extreme ±{pipExtreme:F1} pips · budget usually {bLo:F1} – {bHi:F1}");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
```

`PassionBudgetClampFactor` is `4f`, so the extreme is the ±4σ clamp window — the true hard bound,
not a percentile. `bLo` floors at 0 rather than at 1: the vanilla floor-at-1 only applies when
`passionCountMin > 0`, and mirroring that conditional in a readout label is more precision than the
line is worth.

- [ ] **Step 4: Update both tooltips to name the units**

Keep the existing "within a pawn" vs "between pawns" contrast — that distinction is the real one and
is easy to lose. Add the units and the fact that the value is a typical, not a maximum.

- [ ] **Step 5: Check the header row arithmetic**

`ProfileEditorTab.cs:78` declares `private const float HeaderHeight = 162f;` and its row sums are
arithmetic, verified only in aggregate. Steps 2 and 3 add two 18px lines, so the new value is
**198f** — but derive it by re-measuring the rendered rows rather than trusting that addition, since
`listing.GetRect` may also consume inter-row gap. Update the constant and the comment beside it.

Symptom of getting this wrong: the last row clips or the curve overlaps the sliders. Check it
visually in Step 6, not just arithmetically.

- [ ] **Step 6: Build, deploy, and look at it**

Expected: sliders read in levels and pips; derived lines update as either the spread or the band
moves; nothing overlaps; the read-only preset case still greys the sliders but not the curve.
**GABS must launch RimWorld itself** (`games_start rimworld`) — a hand-started game cannot connect.

- [ ] **Step 7: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: noise sliders read in levels and pips, with derived extreme and outcome band"
```

---

## Task 7: The outcome curve

**Files:**
- Modify: `Source/ProfileEditorTab.cs`

- [ ] **Step 1: Swap the curve's data source**

Replace the Beta-density sampling in the curve drawing with:

The existing loop is at `ProfileEditorTab.cs:487-495`, with `int samples = 70;` declared at `:483`
and `GetBetaAlphaBeta` at `:482`. Replace the per-sample Beta-density evaluation — including the
`CalculateCompositeScore(q, v)` call at `:491`, which Task 4 deliberately left alone for this step —
with a single density fill:

```csharp
        private const int CurveSamples = 70;          // was the local `int samples = 70`
        private static float[] curveDensityScratch;   // reused; the curve redraws every frame
```

```csharp
            if (curveDensityScratch == null || curveDensityScratch.Length != CurveSamples)
                curveDensityScratch = new float[CurveSamples];
            DispersionModel.OutcomeDensity(v, curveDensityScratch);
```

The x-position of sample `j` is now simply `(j + 0.5f) / CurveSamples` — realised power on `[0,1]` —
so `MapToCenteredX` is applied to that directly instead of to a composite score. Then normalise to
the tallest sample for plotting, as the existing code does.

The yellow mean marker at `:514` stays where Task 4 put it (`TypicalAt`), and still needs
`MapToCenteredX`: it marks a power value on the same axis.

- [ ] **Step 2: Add the drag-time downsample**

Mandatory, not conditional: the single-slot cache misses every frame while a slider moves, and the
cheap grid's measured accuracy cost is 0.001pp — free, so there is no trade to evaluate.

Add the drag state next to `curveDensityScratch`:

```csharp
        // True while the mouse is held anywhere in the editor -- i.e. a slider may be moving. The
        // single-slot Best-of-N cache keys on the profile values, so a live drag changes the key
        // every frame and the hit rate is ~0%. Each missed frame pays the full 131k Erf-bearing
        // evaluations, single-threaded in Mono.
        private static bool dragActive;
        private static bool needsFullResRefresh;
```

In the editor's draw method, before the readouts:

```csharp
            bool held = Input.GetMouseButton(0);
            if (held) needsFullResRefresh = true;      // something may have moved
            else if (dragActive) PawnVarianceSettings.InvalidateBestOfNCache();  // released: recompute
            dragActive = held;
```

Then pass the flag through at every `BestOfN` call site in this file:

```csharp
            float bestOf25 = DispersionModel.BestOfN(v, 25, lowRes: dragActive);
```

and clear `needsFullResRefresh` once the post-release full-resolution pass has run.

**Two things to check rather than assume.** `InvalidateBestOfNCache` does not exist yet — if
`PawnVarianceSettings`'s single-slot cache has no public reset, add one (that file is inside the
Rule 8 sign-off), or make the cache key include the `lowRes` flag so the release frame misses
naturally. The second option is smaller and is preferred if the key is already a value tuple.

`OutcomeDensity` has no `lowRes` parameter — it is bounded by `into.Length` (70), so its cost is
`70 × 256` moment lookups, already hoisted by Task 3. It does not need gating; only `BestOfN` does.

- [ ] **Step 3: Update the axis label and tooltip**

The x-axis is now realised power, not quality. **Keep the exclusion clause** in the Row 3 tooltip —
without it a player reads Distinct's negative figure as "weaker" and picks against the profile for
the exact reason it exists.

- [ ] **Step 4: Build, deploy, and verify visually**

Expected: moving either spread slider visibly widens or narrows the curve; Wildcard shows a pile
against the left edge (censoring); the curve is a single line and is never greyed.

Use `rimworld/take_screenshot` cropped to the editor header for a genuine visual check.
**GABS must launch RimWorld itself** (`games_start rimworld`) — a hand-started game cannot connect.

- [ ] **Step 5: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: the header curve now plots realised outcomes, so the spread sliders move it"
```

---

## Task 8: Update HANDOVER.md

**Files:**
- Modify: `HANDOVER.md`

- [ ] **Step 1: Rewrite "What the envelope does NOT measure"**

The envelope now measures it. Keep the mechanism (why per-skill noise averages down by `√12`) and
the warning against reading dispersion out of the mean band. Delete the claim that no percentage
responds to the noise scalars.

- [ ] **Step 2: Record the model's limits**

Add: the Normal-per-q approximation and its measured error (0.08pp at shipped values, 1.5pp at
`passionNoise` 0.85); that a **custom** profile with high passion spread will drift from reality
while the gate stays green, because both implementations share the approximation; and that the MC
is an independent *numerical method* but **not** independent verification, since it shares the flat
`AssumedVanillaSkillBaseline` with the quadrature.

- [ ] **Step 3: Update Rule 6's recalculate-trigger list**

Add `skillSpread` and `passionSpread` — they are now scoring inputs. This is the single most
important line in the change: they were previously exempt precisely because the composite ignored
them.

- [ ] **Step 4: Update the profiles table and the code-review status list**

Wildcard's description still says "zero or many passions" — check it still reads true at
`passionSpread` 2.0. Mark the files touched here as needing re-review.

- [ ] **Step 5: Verify the pasted tables are current**

Run: `python docs/tools/envelope_check.py`

Expected: `Source/EnvelopeFigures.g.cs: unchanged`, and every figure matches what is pasted in the
document. If the tool rewrote the file, a table is stale.

- [ ] **Step 6: Commit**

```bash
git add HANDOVER.md
git commit -m "docs: the envelope now measures dispersion, and record what the model cannot see"
```

---

## Self-Review

**Spec coverage.** §3 architecture → Tasks 2, 3. §3.1 grid sizes → Task 3 Step 2. §3.2 accuracy →
Tasks 1, 2 Step 4. §4 scoring changes → Task 4 Steps 1–2. §5 Wildcard retune → Task 4 Step 3.
§6 sliders → Tasks 5, 6. §7 curve → Task 7. §8 verification → Tasks 1, 3 Step 6, 4 Steps 7–8.
§9 rules → Global Constraints + Task 8. §10 risks → Task 7 Step 2 (drag), Task 8 Step 2 (limits).
§11 phasing → task order, with the spec's phases 2+3 fused into Task 4. No gaps.

**Placeholders.** None. Every code step carries the code. Task 6 Step 3 and Task 7 Step 2 were prose
in an earlier draft and now carry theirs; Task 7 Step 2 additionally flags the one thing it cannot
resolve from the plan alone (whether `PawnVarianceSettings`'s cache has a reset hook) rather than
inventing an API for it.

**Type consistency.** `SkillNoiseScalar`/`PassionNoiseScalar` introduced in Task 3 Step 3 and
redefined in Task 5 Step 2 — same names, same return type, consumed identically in
`DispersionModel.Moments` and both appliers. `DispersionModel.BestOfN(v, n, lowRes)` declared in
Task 3 and called with `lowRes` in Task 7. `OutcomeDensity(v, float[])` declared in Task 3, called
in Task 7.

**Resolved since the first draft.** Task 6's outcome-band arithmetic used `q = 0.5` while the spec's
worked example used each profile's own median quality (`q ≈ 0.354` for Wildcard), and this section
told the implementer to silently override the step's own code. That is now fixed *in the step*: Task
6 Step 2 adds `VarianceProfileValues.MedianQuality()` and both derived lines use it. A subagent
executing Task 6 in isolation never reads this section, so a contradiction parked here was not a
disclosure — it was a defect.

**Review findings folded in (2026-08-07, two Gemini reviewers).** Task 3 Step 1a exposes
`PassionPipEfficiency`, which is `private static` and would have made Task 3 fail to compile
(CS0122). `OutcomeDensity` hoists `Moments` out of the x loop. Task 4 Step 2 enumerates all three
`CalculateCompositeScore` call sites instead of "wherever". Task 5 uses six-decimal preset literals
so its own `unchanged` gate can pass. `SettingsTransfer.cs` is dropped as both reviewed and
irrelevant. Task 4 Step 6a adds the missing Rule 7 update. Shell blocks are pinned to Git Bash.
