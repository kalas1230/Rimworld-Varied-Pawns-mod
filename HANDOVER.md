# Handover — Varied Pawns Mod

Repo: `C:\Users\gokal\Desktop\Rimworld-mod\Rimworld-Pawn-variance-mod` · Branch `main`.

**What this document is.** The durable reference for the mod: the scoring model and where its
numbers come from, the invariants that are easy to break by accident, the decisions that have
already been argued out, and what is still open. **It is not a changelog** — git holds the history
of what changed when. Nothing here should be phrased as "on date X we did Y"; if a fact only
matters as history, it belongs in a commit message.

The mod is **unreleased**. There are no existing users and no backward-compatibility obligation —
do not add migration shims. If a saved config breaks, the fix is to reset it.

---

# 🔴 OPEN WORK

## 1. Run the in-game Best-of-N gate

**`Varied Pawns > Verify Best-of-N against envelope_check.py`** has not been run since the passion
axis was rebuilt. Both sides of that gate (the C# integrator and `EnvelopeFigures.g.cs`) were
edited together, so it *should* pass 32/32 — but that is a prediction, and predictions about the
numerical code in this project have been wrong twice. It takes seconds. **Do this before the
retune.**

## 2. Preset retune — not started

Every preset's numbers predate the passion-axis rebuild. The retune was deliberately sequenced
*after* the passion audit, because it calibrates numbers against the passion model, and auditing
that model after tuning would invalidate the tuning.

Constraints that are already settled — tuning without knowing them will fight the code:

- **No downside floor on skills, and do not add one.** `skillShiftMin` and `skillNoise` are the
  downside controls. See "Why a clamp is the wrong tool" below.
- **Noise floors are `0f`.** Both noise constants are Lerp *low endpoints*, so dropping them
  rescaled dispersion at every setting, hardest at the quiet end (`Faithful` −25%). Read the
  current dispersion table, not any remembered figures.
- **`countProtectedTraits` is `true`.** `traitCountMin`/`Max` bound the pawn's **total** traits,
  including xenotype- and scenario-forced ones — not the number this mod adds. **Tune them as
  totals.**
- **No passion-budget clamp.** A rolled budget above what the pawn's eligible skills can hold is
  discarded, and that is what lets restricted-skill pawns max out. Widening `passionCountMax` past
  ~12 buys progressively less. See "Why the budget is not clamped".
- **The `Faithful` baseline is `0.2231`, not `0.2500`.** Whether to restore an exactly-`0.2500`
  baseline is a *retune* decision and belongs here, not in `Constants.cs`: it needs `Faithful`'s
  budget band to have a q=0.50 midpoint of **4.79** pips (`0.25 × 18 / 0.9391`), against 4.0 today.
  **Not 4.5** — that figure predates the pip-efficiency term and is what the budget would have to be
  if a pip were worth its face value. Worth considering on its own merits regardless of the round
  number, since **vanilla's flat budget is 5 pips** (which lands the axis at `0.2609`) and
  `Faithful` is the vanilla-like preset.

**Two hard gates on any retune:** `envelope_check.py` must still PASS Rule 1 and Rule 2 at
N = 1, 5, 25, 50; and if any figure moves, `Source/EnvelopeFigures.g.cs` **and** every pasted table
in this document must be regenerated together. The tool prints
`Source/EnvelopeFigures.g.cs: unchanged` when nothing moved — trust that line, not memory.

> [!CAUTION]
> **Retuning is where this project has shipped its worst defects.** Both Best-of-N integrator bugs
> and the ~36pp Best-of-25 inversion were introduced during retune-adjacent work and survived clean
> builds and static review. Run the in-game `Verify Best-of-N` action afterwards.

## 3. Carried items — known, quantified, not fixed

| Item | Why it is carried |
|---|---|
| **The shared right-edge CDF is first-order accurate.** Both `envelope_check.py`'s `beta_grid` and `CalculateBestOfNScoreCore` do `run += v * dq` *before* appending. Error ∝ `dq`, so 1024 and 20000 nodes differ by up to ~0.9% at N=50. | Both sides have it, so they agree with each other and **nothing on screen is wrong** (the gap cancels in the ratio to `Faithful`). **DECIDED 2026-08-07: carried permanently — do not raise it again.** See "Why the integration slip is carried" below for the argument and for what fixing it would cost. |
| **Are Milians reachable by race override?** `Milian_Race` does not appear in the Add menu: its only def, `Milian_Base`, is `Abstract="True"` with zero concrete children, so no `PawnKindDef` spawns it and the traversal filter drops it. The filter is behaving as specified. | If Milians are spawned in code rather than through a `PawnKindDef`, they are unreachable by race override and the traversal needs a second source. Owner question. |
| **Init-vs-`Scribe` default mismatch on skill and trait fields** — `skillNoise` 1.0 init vs 0.2 Scribe, `skillShift` −4/6 vs −3/3, `traitCount` 1/6 vs 2/3 on `VarianceProfileValues`. | Unreachable either way (every creation path passes explicit values), but they read as live defaults that contradict `Faithful`. The passion fields were aligned; the rest were left alone as out of scope. |
| **`CopyFrom` does not validate imported profile ids** (T5-M1, Minor). | Belongs with the load-validation cluster, not worth fixing piecemeal. |
| **Single-slot cache thrashing in `CalculateBestOfNScore`** (Minor). | UI-only path. |
| Five further Minor findings | In `.superpowers/sdd/progress.md`. |

> [!NOTE]
> `.superpowers/sdd/progress.md` is **gitignored and gets overwritten in place** by each batch. The
> 2026-08-04 batch's original per-task findings (T1-M1 … T6-M3) survive only in
> `git show fb1d8a8:HANDOVER.md`. Nothing to recover — just know it before going looking.

---

# 🔒 MANDATORY ARCHITECTURAL RULES

1. **Mean-power envelope (±35%)** — every preset MUST stay within ±35% of `Faithful` **at every
   batch size** (N = 1, 5, 25, 50), not only at Best-of-1. Read "mean-power" as a scope limit, not
   decoration: the rule does not constrain dispersion at all.
2. **Monotonic power-tier ordering at any N** —
   `Desperate < Scavenger < Faithful < Specialist < Elite < Sovereign`.
   **`Distinct` and `Wildcard` are exempt** — they are *variance* presets, not power tiers. They sit
   below `Faithful` at N=1 and cross above it as N rises; that is cherry-picking working as
   designed, not an inversion. They are **not** exempt from the ±35% envelope.
3. **Never put trait count back into the quality score.** `CalculateCompositeScore` must not contain
   a trait term — see "Trait count is not a quality axis". Seven approaches were evaluated and
   rejected with measured data in `TRAIT-DESIRABILITY-RESEARCH.md` §4–§5.
4. **Do not touch kids by default.** `applyVarianceToChildren = false` and
   `applyChildSkillShift = false`. Growth moments stay untouched out of the box.
5. **Mandatory consultation.** Do not modify the percentage bounds, statistical scaling rules,
   children/growth defaults, or profile parameters without explicit owner approval.
6. **Recalculate the envelope after any scoring-constant change** — run
   `python docs/tools/envelope_check.py`, paste its output into the table below, commit
   `Source/EnvelopeFigures.g.cs` if the run rewrote it, and run the in-game verify action. The
   weights are shared across all eight presets, so one constant moves every profile at once. Never
   hand-edit the percentages.
7. **`R` depends on the normalizer and on the pip-efficiency term, not just the weights** —
   `R(bias) = (AssumedMaxSkillLevel / MaxPassionPips) · (wP / wS) · PassionPipEfficiency(bias)`.
   Changing `MaxPassionPips` alone silently moves the exchange rate with no weight touched. This
   nearly reverted the retune once: `/12 → /18` on its own would have cut `R` from 1.94 to 1.33,
   *below* the 1.389 it replaced.
   **This rule used to name three inputs and say "recompute `R` before touching any of the three."
   That was wrong from the moment the efficiency term landed** — a retuner who changes a preset's
   `passionMajorBias` touches none of the three, moves `R` anyway, and was told by a mandatory rule
   that they were safe. There are four inputs, one of them per-profile, and the recalculate-trigger
   list under "The verified envelope" is the authoritative one.
8. **Protection of reviewed code.** Do not modify, refactor or rewrite any file marked
   `DONE (REVIEWED)` in "Code review status" without presenting the rationale and getting explicit
   permission.

---

# 📐 THE SCORING MODEL

## How the percentages are derived — Best-of-N, not the mean

**All envelope figures come from a Best-of-N simulation, never a raw average.** Do not "simplify"
this back to a mean.

**Why:** the player *chooses which pawns to keep* — rerolling starts, picking from raid captures,
accepting or refusing quest pawns. The pawn that ends up in the colony is the **maximum of N rolls**.
A profile's felt power is set by its upper tail, so a mean-based figure systematically understates
any high-dispersion profile.

1. Quality is Beta-distributed: `q ~ Beta(m·K, (1−m)·K)`, `m = averageQuality`,
   `K = Constants.BetaConcentrationK` (8). See `QualityRoller.RollQuality`.
2. Draw N qualities, take the max. `CalculateCompositeScore` is monotonic in `q`, so
   Best-of-N score `= composite(max(q₁…q_N))`.
3. `composite = (0.8·skillNorm + 1.4·passionNorm) / 2.2`, where `skillNorm = (5 + skillShift)/20`
   and `passionNorm` is the passion axis below.
4. Compare each profile to `Faithful` **at the same N**.

## The passion axis

`passionNorm = min(pips, capacity) / MaxPassionPips · PassionPipEfficiency(majorBias)`.

Two terms, each with a reason:

**Capacity cap** — `skills × (MinorCost + (MajorCost − MinorCost) · bias)` = 12 / 15 / 18 pips at
bias 0 / 0.5 / 1. A *low* Major bias saturates *early* (12 Minors fill all 12 skills for 12 pips),
which is the opposite of what the old formula assumed. **The cap binds no shipped preset** — the
widest is `Wildcard` at 9.8 against a 15.6 capacity — so it changes nothing today and is correct for
custom profiles, which can reach 18.

**Pip efficiency — what a Major is actually worth.** Without it, `passionMajorBias` could not move
the score at all: `passionNorm` was identical to four decimals at bias 0 and bias 1 for all eight
presets, because the capacity cap only binds above ~14 pips. A slider that visibly changes colonists
and never changes the number beside them is its own defect. The derivation:

```
SkillRecord.LearnRateFactor   None 0.35x    Minor 1.00x    Major 1.50x
gain over having none                       Minor +0.65    Major +1.15

=> a Major is worth 1.15 / 0.65 = 1.769 Minors, and costs 1.5 pips.
```

**Majors are underpriced by the pip currency.** Two profiles spending an identical budget are not
equally strong; the Major-heavy one is ahead by ~18% at the extremes. `PassionPipEfficiency(bias)`
is value-per-pip at that bias, normalised against an all-Major roll:

| Major bias | 0.00 | 0.35 | 0.50 | 0.65 | 0.80 | 1.00 |
|---|---|---|---|---|---|---|
| pip efficiency | 0.848 | 0.916 | 0.939 | 0.960 | 0.978 | **1.000** |

**Normalised at bias 1.0, not at vanilla's 0.5, and that choice is load-bearing.** Anchoring high
keeps `MaxPassionPips` meaning what it says — 18 all-Major pips is exactly a saturated axis, 1.0,
verified exact. Anchoring at 0.5 would push Major-heavy profiles above 1.0 into the clamp, so the
axis would saturate *before* 18 pips and the ceiling would stop being the ceiling. The price of
anchoring high is that every profile below bias 1.0 scores a little lower; that is a scale shift,
not a ranking change.

> [!WARNING]
> **What this model does NOT capture — read before treating 1.769 as precise.** It values a passion
> purely by its XP-rate increment over having none. It therefore:
> - **assumes all twelve skills are equally worth training** — they are not;
> - **ignores concentration**, even though Majors always land on the pawn's best skills first;
> - **ignores diminishing returns** as a skill approaches level 20;
> - **has no time axis**, the same limitation documented for the exchange rate `R`.
>
> It does *not* double-count `R`'s discount for passions landing on skills the colony never
> assigns: `R` prices a pip in skill-levels, this re-weights pips by grade. Different axes.
>
> **1.769 is defensible and derived from mechanics that actually run. It is not the only defensible
> number.** Two alternatives were weighed and rejected: vanilla's own
> `Pawn_SkillTracker.MajorPassionWeight = 2` (declared and never called — see below), and a bare
> `1.25` derived from nothing. If this is revisited, the argument to beat is in the comment on
> `PassionPipEfficiency`, not here.

Both implementations carry the term (`PawnVarianceSettings.PassionPipEfficiency` and
`make_composite` in `envelope_check.py`), the three `LearnRateFactor` values are drift-checked
scoring constants in `EnvelopeFigures.g.cs`, and the disabled-passion-axis fallback runs through the
same term at vanilla's own 50/50 bias so it cannot sit on a different scale. That fallback is
`Constants.VanillaPassionBudget · PassionPipEfficiency(0.5) / MaxPassionPips` =
`5 × 0.9391 / 18` = **0.2609** — **not** the skill axis's 0.25 baseline, which is a different quantity
that was once copied across by mistake, and **not** the `0.2778` this line read until 2026-08-07,
which was `5/18` computed before the efficiency term existed.

> [!IMPORTANT]
> ## Where the `24` comes from — vanilla has TWO passion scales
>
> | Scale | Value of a Major | Status |
> |---|---|---|
> | `PawnGenerator.GenerateSkills`' spend loop | **1.5** pips | Runs on every pawn. **This is ours.** |
> | `Pawn_SkillTracker.PassionCount` | **2** (`MajorPassionWeight = 2`) | Public API with **zero consumers anywhere in the assembly.** Declared, never used. |
>
> `12 × 2 = 24` is a *correct reading of the second scale*. The "24-pip era" was never an invented
> number — it was right arithmetic on the wrong scale, which is why it recurred twice and why both
> recurrences looked defensible to reviewers. The defence is naming the scales, which `Constants.cs`
> now does at the price list. **A future reader who finds `Major = 2` in vanilla has found
> `PassionCount`, not a bug in this mod.**

> [!CAUTION]
> **`Constants.MaxPassionPips` must stay a numeric literal.** It reads `18f`, not
> `12f * MajorPassionCost`, because `envelope_check.py` parses `Constants.cs` with a regex that only
> accepts `public const float X = <number>f;`. An expression there makes the tool exit with
> *"Constants.cs is missing: MaxPassionPips"*. Same constraint on the other parsed constants.

**The assumed skill count is derived from `MaxPassionPips / MajorPassionCost`, not stored.** The
hardcoded 12 disagrees with `pawn.skills.skills.Count` under skill-adding mods, but nothing depends
on them agreeing: `passionCountMin`/`Max` are clamped to `MaxPassionPips` at both the slider *and*
`VarianceProfile.cs`, so the budget can never exceed 18 whatever the skill count. A 13th skill
raises capacity to 19.5 against a budget still capped at 18 — the surplus-discard path fires *less*,
not more. Mild dilution, no wrong arithmetic. The in-game verify action prints the live
`DefDatabase<SkillDef>` count and says so when it is not 12; deliberately a NOTE, not a failure.

## The skill ↔ passion exchange rate (`R`)

**`R(bias) = (20 / MaxPassionPips) · (wP / wS) · PassionPipEfficiency(bias)`
`= (20/18) · (1.5/0.8) · eff(bias)` skill levels per passion pip.**

> [!IMPORTANT]
> **`R` is a function, not a scalar, and every quoted figure is anchored at vanilla's 0.5 bias.**
> The pip-efficiency term made it bias-dependent; above the capacity cap it is also piecewise, since
> the marginal pip there is worth **zero**. The cap binds no shipped preset but is reachable on
> custom profiles — which is exactly where a live `R` would be used.
>
> | Major bias | 0.00 | 0.50 (vanilla, `Faithful`) | 0.70 (`Sovereign`) | 1.00 |
> |---|---|---|---|---|
> | `R` | 1.77 | **1.96** | 2.01 | 2.08 |

Decided after a four-agent review (2 Claude, 2 Gemini) that landed on **≈2.0**. `wP` moved `1.4 → 1.5`
on 2026-08-07 to **restore** that conclusion rather than to change it: the efficiency term had
rescaled the passion axis downward, dragging the realised rate to `1.83` at vanilla bias while every
statement of it still read `1.94`. Measured rather than assumed — envelope headroom *improved*
(`Sovereign` @ N=1, 6.6pp → 7.0pp), because the power tiers differ from `Faithful` mostly in **skill**,
so weighting passion higher pulls them toward the reference. `Wildcard` is the one preset that
genuinely moves, being the profile with the wide passion budget.

What that review established:

- Passion is an **XP-rate multiplier**, not an additive gift. A Minor pip is a 2.86× learning-rate
  advantage over no passion.
- Its value in skill-levels is **time-dependent**: ≈0 on day 1 (pure future value), peaking near 4.8
  around day 30, saturating near 3.2 once skill decay reaches equilibrium. The owner's intuition —
  *skill dominates in emergencies and early game, passion dominates long-run* — is correct and
  quantified.
- **A generation-time score has no time axis**, so a single scalar can only be a colony-lifetime
  average. `≈2.0` is that average after discounting for the ~40–60% chance a passion lands on a
  skill the colony never assigns. Agent estimates spanned `0.78` to `6.5`, with an independent
  Gemini derivation at `2.70`.
- **`CalculateCompositeScore` is display-only.** Verified consumer trace: the readout string, the
  curve x-axis, the mean marker in `ProfileEditorTab.cs`. Zero pawn-generation, storyteller or
  raid-scaling consumers. This caps how much precision is worth buying — round weights are
  deliberate; do not chase significant figures here.
- **Direction-of-risk correction:** moving toward *skill* is what stresses the envelope, not passion.
  Two of the four agents asserted the opposite; the simulation says otherwise.

## The verified envelope

Verbatim output of `python docs/tools/envelope_check.py` (deterministic integration over
`q ~ Beta(m·8, (1−m)·8)`, density of the max `= N·F(q)^(N−1)·f(q)`; % vs `Faithful` at the same N).
Pasted, not hand-edited — Rule 6.

```
wS=0.8  wP=1.5  pips/18  skill/20  K=8
Exchange rate R(bias) = (20/18) * (1.5/0.8) * eff(bias)
  R = 1.96 skill levels per passion pip at vanilla bias 0.5   (range 1.77 at bias 0 .. 2.08 at bias 1)
Faithful baseline @ q=0.50: 0.2231

profile                     N=1                N=5               N=25               N=50
Faithful        0.2231   +0.0%     0.2699   +0.0%     0.2978   +0.0%     0.3059   +0.0% 
Distinct        0.1995  -10.6%     0.2717   +0.7%     0.3244   +8.9%     0.3418  +11.7%   (variance)
Wildcard        0.1767  -20.8%     0.2721   +0.8%     0.3424  +15.0%     0.3649  +19.3%   (variance)
Desperate       0.1704  -23.6%     0.2105  -22.0%     0.2381  -20.0%     0.2469  -19.3% 
Elite           0.2759  +23.7%     0.3167  +17.4%     0.3402  +14.2%     0.3469  +13.4% 
Sovereign       0.2855  +28.0%     0.3276  +21.4%     0.3512  +17.9%     0.3579  +17.0% 
Specialist      0.2451   +9.9%     0.2906   +7.7%     0.3177   +6.7%     0.3257   +6.4% 
Scavenger       0.1892  -15.2%     0.2314  -14.3%     0.2586  -13.2%     0.2669  -12.8% 

Rule 2 - power-tier ordering at the same N:
  N=1   Desperate(0.170) < Scavenger(0.189) < Faithful(0.223) < Specialist(0.245) < Elite(0.276) < Sovereign(0.286)   OK
  N=5   Desperate(0.210) < Scavenger(0.231) < Faithful(0.270) < Specialist(0.291) < Elite(0.317) < Sovereign(0.328)   OK
  N=25  Desperate(0.238) < Scavenger(0.259) < Faithful(0.298) < Specialist(0.318) < Elite(0.340) < Sovereign(0.351)   OK
  N=50  Desperate(0.247) < Scavenger(0.267) < Faithful(0.306) < Specialist(0.326) < Elite(0.347) < Sovereign(0.358)   OK

Tightest envelope margins:
  Sovereign @ N=1: +28.0%  (7.0pp of headroom)
  Elite @ N=1: +23.7%  (11.3pp of headroom)
  Desperate @ N=1: -23.6%  (11.4pp of headroom)

Within-pawn dispersion (REPORTED, NOT ENFORCED -- invisible to every % above):
  profile      skillNoise   per-skill sd  vs Faithful  passionNoise   budget sd
  Faithful           0.20        0.49 lv        1.00x          0.25     1.00 pips
  Distinct           0.35        0.86 lv        1.75x          0.35     1.40 pips
  Wildcard           0.85        2.08 lv        4.25x          0.85     3.40 pips
  Desperate          0.25        0.61 lv        1.25x          0.25     1.00 pips
  Elite              0.22        0.54 lv        1.10x          0.25     1.00 pips
  Sovereign          0.24        0.59 lv        1.20x          0.25     1.00 pips
  Specialist         0.25        0.61 lv        1.25x          0.25     1.00 pips
  Scavenger          0.25        0.61 lv        1.25x          0.25     1.00 pips
  A profile can be flat in the table above and 3x wider here. Wildcard is exactly
  that case: its 2026-08-04 retune narrowed skillShift (the mean band), not skillNoise.

Source/EnvelopeFigures.g.cs: unchanged.

PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.
If any number moved, update the table in HANDOVER.md "The skill <-> passion exchange rate".
```

## Why the integration slip is carried — decided, do not reopen

Stated plainly, because the technical one-liner in "Carried items" is opaque unless you already know
what it means, and the decision was made on the plain version.

**What `N` is.** The player does not keep the first pawn they are offered — they reroll starts, pick
from quest pawns, accept or refuse captures. So `N` is simply **how many pawns were looked at before
one was kept**. `N=1` is "took the first". `N=25` is "looked at 25, kept the best". Nothing more.

**What the slip is.** To work out "the best of 25", both implementations chop the quality range into
thin slices and add up their contributions. Each slice is counted as very slightly too big — half a
slice too big. That is the whole defect.

**Why it is harmless.** At `N=1` the slip does not enter the arithmetic at all, so the tightest
figure in this project (`Sovereign` at N=1, the one with 7.0pp of headroom) is exact. For larger `N`
the error compounds to at most ~0.9% at `N=50`. But `envelope_check.py` and the C# integrator make
the **identical** slip, and every figure a player ever sees is a comparison against `Faithful`, which
carries the same slip. It cancels. **Nothing displayed is wrong, and no decision has ever been made
on a number this affects.**

**What fixing it would cost — measured 2026-08-07, not estimated.** The midpoint correction was
applied to `beta_grid` and the tool re-run, then reverted. Every raw `N≥2` figure shifts, so
`EnvelopeFigures.g.cs` regenerates and every pasted table is repasted — but the shift is **one digit**:
`Faithful` N=50 goes `0.3059 → 0.3058`, and exactly one displayed percentage moves at all
(`Scavenger` N=50, `−12.8% → −12.7%`). `N=1` is bit-identical.

**Do not confuse that with the ~0.9% figure in "Carried items".** They are different quantities. The
0.9% is the gap between the 20000-node reference and the mod's 1024-node integrator; it is dominated
by this same slip, but the slip is ~20× larger there because the slices are ~20× fatter. The
reference is already nearly exact — **the shipped C# carries the visible share of the error**, not the
tool.

> [!CAUTION]
> **If it is ever fixed, fix BOTH sides in the same edit.** The in-game gate's tolerance is 0.5pp on
> the displayed quantity and the effect is ~0.1pp, so the gate would **not** catch the two
> implementations diverging in method. That is the one genuinely dangerous way to touch this: a
> silent breach of the "two implementations of one integral" contract, with a green light.

**The call (2026-08-07): leave it.** It was surfaced only because doing it *after* a retune would
mean redoing numbers that had just been tuned. The owner's decision is to carry it indefinitely. A
future agent that rediscovers the `run += v * dq` ordering has found a documented decision, not a
bug.

**Interpretation note on Rule 2:** an earlier wording ("even a Best-of-50 `Desperate` pawn must
remain below `Faithful`") is ambiguous and, read strictly as *Best-of-50 of a lower tier < Best-of-1
of a higher tier*, is violated **9 times by the shipped presets** — and is arguably impossible to
satisfy for any profile with real dispersion. **The enforceable reading is same-N ordering.**

> [!CAUTION]
> ## Recalculate after any constant change — the table above goes stale silently
>
> ```powershell
> python docs/tools/envelope_check.py
> ```
>
> Run it, and update the table, after changing ANY of:
> - `CompositeSkillWeight`, `CompositePassionWeight`, `MaxPassionPips`
> - `AssumedVanillaSkillBaseline`, `AssumedMaxSkillLevel`, `BetaConcentrationK`
> - `MajorPassionCost`, `MinorPassionCost` — scoring constants via the capacity term
> - `PassionLearnRateNone / Minor / Major` — scoring constants via `PassionPipEfficiency`. These are
>   *vanilla's* numbers, so they should only move if a RimWorld update moves
>   `SkillRecord.LearnRateFactor` — which is exactly the drift the in-game check exists to catch,
>   since nothing else here would notice.
> - any preset's `averageQuality`, `skillShiftMin/Max`, `passionCountMin/Max`, `passionMajorBias`
>
> The tool **parses `Source/Constants.cs` and `Source/VarianceProfile.cs` directly** rather than
> hardcoding values, so it cannot drift from what ships. Deterministic integration, not sampling.
> No third-party dependencies. It exits non-zero on a Rule 1 or Rule 2 violation, so it can gate a
> commit, and it regenerates `Source/EnvelopeFigures.g.cs` (checked in, auto-generated, **never**
> hand-edited). If `git status` shows that file dirty after a run, the shipped figures were stale —
> commit it.
>
> **Why this matters more than it looks:** the tightest preset has **7.0pp** of headroom. A change
> that *feels* cosmetic — nudging one preset's `averageQuality` by 0.02, or "tidying" a normalizer —
> can breach the envelope without touching the preset that breaks, because the weights are shared.

## What the envelope does NOT measure — `skillNoise` and `passionNoise`

**The model treats a pawn as fully determined by its quality roll `q`.** `CalculateCompositeScore`
reads exactly six fields: `averageQuality`, `skillShiftMin/Max`, `passionCountMin/Max`,
`passionMajorBias`. **`skillNoise` and `passionNoise` are not inputs**, and no percentage in the
envelope table responds to them.

That is a real gap. `skillNoise` drives the per-skill excursion in `SkillVarianceApplier.Shift` —
`magnitude = Lerp(0, 6, skillNoise)`, so up to **±6 levels per skill** (`Constants.MaxMagnitude`).
Two profiles with identical envelope percentages can produce visibly different populations:

| Profile | `skillNoise` | per-skill sd | vs `Faithful` |
|---|---|---|---|
| Faithful | 0.20 | 0.49 levels | 1.00× |
| Distinct | 0.35 | 0.86 levels | 1.75× |
| **Wildcard** | **0.85** | **2.08 levels** | **4.25×** |

(`sd = magnitude/√6`; the `TriangularSample()*2−1` term is triangular on [−1,1], variance 1/6.)

**Two consequences:**

1. **Do not cite the envelope as a general safety guarantee.** It bounds *mean power*. A profile can
   pass Rule 1 at every N and still be far swingier than `Faithful`.
2. **"Narrowing a profile's dispersion" usually means narrowing `skillShift`, which is the mean
   band, not the noise.** The Wildcard retune did exactly this: it moved `skillShiftMin/Max` and
   left `skillNoise` at `0.85`. The envelope figures improved; actual dispersion did not move.

These figures are printed by `envelope_check.py` as a dispersion table but are **reported, not
enforced** — deliberately. There is no Rule 1 equivalent for spread and none is wanted: the point is
to make the axis visible to whoever reaches for `skillNoise`, not to add another architectural rule.
Observed (as opposed to derived) dispersion comes from the `Roll pawns and dump distribution` debug
action.

**The scope limit is stated to the player too.** The Row 3 power readout's tooltip says the figure
is *"Based on starting skill levels and the passion budget only. It does not include traits, and it
does not show how much pawns differ from each other, so two profiles with the same figure can still
play very differently."* That second sentence is load-bearing: without it a player reads `Distinct`'s
−10% as "weaker" and picks against the profile for the exact reason it exists. **If you reword that
tooltip, keep the exclusion clause.**

## Trait count is NOT a quality axis — more traits is *worse*

Trait *selection* is delegated entirely to vanilla's `PawnGenerator.GenerateTraitsFor`, which is
**quality-blind**. Scaling trait count with quality does not buy better traits — it buys **more
independent draws from an unchanged urn**, including the colony-ruining ones. Roughly 4% of vanilla
trait degrees can trigger uncontrolled behaviour (`randomMentalState`/`forcedMentalState`:
Pyromaniac, Gourmand, Void Fascination):

| Traits | P(at least one hazardous trait) |
|---|---|
| 2 | 8.0% |
| 3 | 11.8% |
| 4 | 15.4% |
| 5 | 18.9% |
| 8 | 28.5% |

**Consequence:** a wide `traitCountMin → traitCountMax` spread makes high-quality pawns *more* likely
to roll a colony-ender than low-quality ones. Scoring trait count as a straight positive would make
the composite metric **actively reward** a change that makes pawns worse to play with.

**Rules that follow:**
- Keep preset spreads close to vanilla's **2–3**. `2–4` is a reasonable ceiling for high-quality
  presets; wider ranges belong to explicitly chaotic presets (`Wildcard`) or to the user's slider.
- Widening a spread to raise a composite score is **forbidden** — it is gaming the metric. Raise
  `averageQuality`, skills or passions instead.

See `TRAIT-DESIRABILITY-RESEARCH.md` §1 and §3.1 for the derivation.

> [!IMPORTANT]
> **Do not build a trait desirability engine.** The underlying problem — trait *count* scaling with
> quality while trait *selection* stayed quality-blind — is already fixed, without new runtime code:
> the trait term was removed from the composite, and preset trait ranges were narrowed toward
> vanilla's 2–3 (the inversion's size is proportional to `traitCountMax − traitCountMin`).
>
> **Why a scoring engine was rejected: 46.7% of modded trait degrees in the Progression Modpack
> contain no mechanical XML at all** — their effects live in Harmony patches. A scoring engine would
> have sorted traits by *mod authorship style* rather than by quality. Scope also grew ~3× under
> review while the value stayed small, and this project had already built and retracted a
> trait-quality axis once (`traitNoise`, see `TraitVarianceApplier.cs:19-22`). Full record in
> `TRAIT-DESIRABILITY-RESEARCH.md` (rev 3, after a six-agent review) — read §10 for what shipped.

---

# 🎲 HOW A PAWN IS ACTUALLY ROLLED

**Quality is rolled ONCE per pawn** (`HarmonyPatches.cs:36`) and handed to all three appliers. That
is what makes quality a coherent per-pawn property rather than three unrelated numbers — a
high-quality pawn is high-quality in skills *and* passions *and* trait count together. **Do not
"improve" this into a per-axis roll.**

| Quantity | Rolled |
|---|---|
| Quality | **once per pawn** |
| Skill baseline | once per pawn, `Lerp(shiftMin, shiftMax, q)` |
| Skill noise | **once per SKILL** — 12 draws, inside the loop in `SkillVarianceApplier.Shift` |
| Passion budget | once per pawn |
| Trait count jitter | once per pawn, ±0.25 |

### The mod displaces vanilla's roll, it does not author the pawn

`SkillVarianceApplier.cs:47` is `RoundToInt(record.levelInt + shift)` — the shift is applied **on top
of** whatever vanilla generated from backstory, age and `PawnKindDef`. Consequence: **two pawns at
identical quality are still completely different pawns.** Even with true-zero noise they would
differ; the shift moves the whole pawn up or down, it does not decide what the pawn is. This is the
correct mental model for the whole mod, and it is easy to lose when reading the envelope maths,
which talks only about shifts and budgets.

### The growth moment rolls a FRESH quality

`GrowUpVariance.cs:58` calls `RollQuality` again. A pawn generated at `q = 0.20` can grow up at
`q = 0.85`. The two rolls are independent and nothing carries over — a child is **not** "the same
pawn's quality, re-applied." Deliberate, but it means growth-moment outcomes cannot be predicted
from the pawn's original generation.

### The noise sliders now mean literally zero at zero

`MinMagnitudeFloor` and `PassionBudgetSpreadMin` used to be **floors, not zeros** (0.5 and 0.25), so
a slider reading `0.00` still delivered ±0.5 levels per skill and still varied the passion budget
enough to change how many passions a pawn got. Both are `0f` now.

> [!CAUTION]
> **Both constants are Lerp low endpoints**, so moving them rescaled magnitude at *every* noise
> setting, not just at zero — proportionally hardest at the quiet end, where every preset except
> Wildcard lives:
>
> | `skillNoise` | magnitude before | after | change |
> |---|---|---|---|
> | 0.00 | 0.50 | 0.00 | −100% |
> | 0.20 (`Faithful`) | 1.60 | 1.20 | −25% |
> | 0.35 (`Distinct`) | 2.43 | 2.10 | −13% |
> | 0.85 (`Wildcard`) | 5.18 | 5.10 | −1.4% |
>
> Absolute dispersion fell everywhere, but the *ratio* between profiles widened — Wildcard went from
> 3.23× Faithful's per-skill sd to **4.25×**. The composite reads neither constant, so
> `envelope_check.py` still passes and `EnvelopeFigures.g.cs` is byte-unchanged. **Any dispersion
> figure predating this change is dead** — re-measure rather than diffing against it.

## Range semantics — two kinds, and they are NOT uniform

Settled after a two-agent design review. **Do not "unify" these without re-reading this.**

A range's handles are mapped by quality — `Lerp(min, max, q)` picks the pawn's target. What happens
*next* differs per control, deliberately:

| Control | Kind | Mechanism |
|---|---|---|
| **Skill shift** | **Target** | `baseline + (tri·2−1)·magnitude`, `clampToRange: false`. Exceeds both handles. |
| **Passion budget** | **Target** | `mean + Clamp(Gaussian(0,σ), ±4σ)`. Exceeds above; floored at 1 below unless `min = 0`. |
| **Trait count** | **Hard limit** | `Clamp(Round(lerp + jitter), min, max)`. Jitter is only ±0.25 and there is no trait noise knob. |
| **Child shift at 13** | **Hard limit** | Same formula as Skill shift but `clampToRange: true`. |

**Why targets, not limits, on the two big axes** — the alternative (ranges as hard bounds, noise
reshaping the distribution inside them) was considered and rejected:

1. **The range would have to do two jobs at once.** Today `min/max` maps quality onto an outcome and
   the noise scalar sets dispersion — two concepts, two controls, tunable independently. Make the
   range a bound and it becomes the quality mapping *and* the outlier limit, which pull opposite
   ways: allowing a rare exceptional pawn forces the typical pawn up too.
2. **`Faithful` could no longer do its job.** Vanilla's budget is `5 + Clamp(Gaussian(0,1), −4, 4)` —
   a mean plus noise unbounded by any range, with no min/max concept at all. A hard-bounded
   `Faithful` cannot reproduce that shape, and reproducing it is the profile's entire purpose.
3. **`envelope_check.py` could not verify the change.** It reads `Lerp(min,max,q)` — the mean —
   never a roll, so it would report PASS throughout while every generated pawn changed.

**A third option was also evaluated and rejected: `lerp → noise → clamp`** (what `ApplyGrowUp`
already does, applied everywhere). It fails for its own reason: **clamping a symmetric distribution
against a wall does not remove the tail, it stacks the tail into a spike on the wall.** Simulated at
400k pawn-skills per preset against each one's real Beta quality:

| Preset | magnitude | range width | % skills pinned to a handle |
|---|---|---|---|
| Faithful | 1.60 | 6.00 | 0.7% |
| Distinct | 2.42 | 9.80 | 3.1% |
| Sovereign | 1.82 | 3.85 | 5.1% |
| Wildcard | 5.17 | 12.90 | 5.4% |

Harmless on the shipped presets — that is *not* the argument. Two things kill it:

1. **The noise slider inverts.** The pin rate is driven by `magnitude ÷ range width`, which no UI
   surfaces. On a custom profile with a narrow band (range `1.0–3.0`), pinning goes 18% at
   `skillNoise` 0.20 → **49% at 0.50** → **70% at 1.00**, split evenly between the handles. Past
   roughly half travel, *raising* the variance knob makes pawns **more alike**: a 12-skill pawn ends
   with ~4 skills at exactly the min shift and ~4 at exactly the max. A control that reverses
   direction halfway along is worse than one whose range is a soft target.
2. **It flattens exactly the pawns worth having.** Wildcard pin rate by the pawn's own quality:
   `q 0.0–0.2 → 29.1%`, `q 0.4–0.6 → 0.0%`, `q 0.8–1.0 → 29.2%`. Clamping does nothing to the average
   pawn and hits the top and bottom deciles almost exclusively — so the exceptional pawn, which is
   the entire point of a variance preset, arrives *flatter* than an average one.

It also gives up an identity that is currently **exact**: because the noise is symmetric with mean
zero, `E[shift] = Lerp(min, max, q)` precisely, which is *why* `CalculateCompositeScore` reading
`Lerp` is correct rather than approximate. Clamping breaks that, and `envelope_check.py` computes
`Lerp`, so it could never measure the gap.

**If the guarantee is ever wanted, do not clamp — scale the noise by headroom:**
`effective = min(magnitude, min(baseline − min, max − baseline))`. That bounds the roll with no point
mass at either end, keeps `E[shift] = Lerp` exact, and preserves the slider's monotonic meaning.
Cost: noise does less work at extreme quality. Rule 5 consultation item, not decided.

**Why the age-13 path is the exception:** at generation the pawn's levels were just rolled, so
straying past a handle costs nothing. At 13 they represent twelve years of play, so a minimum of `0`
has to genuinely mean "never subtracts." Rationale is on `SkillVarianceApplier.cs:14-22`.

> [!WARNING]
> **`skillShiftMin` means two different things in two code paths** — a soft target in `Apply`, a hard
> floor in `ApplyGrowUp`. Intentional, but a real trap: do not reuse one path's helper in the other
> assuming shared semantics. The design review called this the most likely future bug in this area.
> The ambiguity is mitigated in code — `Shift` is private and reached only through `ShiftAroundBand`
> (generation) and `ShiftWithinBounds` (age-13) — and at the UI layer: **every range tooltip states
> which kind of range it is.** If you add a range control, its tooltip must say which kind it is.

## Why a clamp is the WRONG TOOL on the skill downside

A floor on the skill-shift downside was proposed, accepted, implemented and reverted:
`shift = Mathf.Max(shift, skillShiftMin - 2f)` on the generation path. Two independent reasons it
was wrong, both of which apply to *any* variant of the idea:

1. **A clamp converts a spread into a spike.** Every roll that would have landed below the line
   instead lands *exactly on* it — a probability mass point at an endpoint, precisely the artifact
   the soft-band design exists to avoid.
2. **The invariant people think is missing is already enforced.** `Shift` ends with
   `record.Level = Mathf.Clamp(newLevel, 0, 20)`. A skill cannot go negative and never could.

> [!CAUTION]
> **The "13.8 levels below vanilla" figure that motivated the fix was misleading**, and it was
> written into this document by the agent that then acted on it. On `Wildcard`, `skillShiftMin` −8.7
> plus a 5.1 magnitude does compute to −13.8 — but that shift is applied to a level that is then
> clamped to 0. The pawn does not end up at −13. **The real effect is that a share of skills pin at
> 0**, which is itself an endpoint pile; a second clamp above it would have created a second pile.

**Stated honestly:** on a low-quality `Wildcard` pawn, many skills pin at 0. That is a tuning
outcome, not a safety hole, and the levers are `skillShiftMin` and `skillNoise`. If pinning is judged
too aggressive, **narrow the band or the noise — do not add a clamp.**

## Why the passion budget is not clamped to capacity

"Clamp realized budget to eligible capacity" was accepted, then broken by the owner's question:
*how can Wildcard even reach full Major on all skills?*

**It effectively cannot.** With `PassionBudgetSpreadMin = 0`, Wildcard's budget is
`Lerp(1.2, 9.8, q) + clamp(N(0, 3.4), ±13.6)`, and the clamp window is exactly 4σ.

- Below **q = 0.372** an 18-pip budget is **arithmetically impossible** — even a maxed 4σ roll cannot
  reach it.
- At q = 0.874 it needs a 2.73σ roll (**p ≈ 0.3%**) — a *conditional* figure for an already
  exceptional pawn, not a population rate.
- Reaching q ≥ 0.874 at all, under `Beta(2.96, 5.04)` (mean 0.37, k=8), is itself ~3.1 sd out.
- **And 18 pips still is not all-Major.** 12 Majors costs exactly 18, so every coin flip must come up
  Major: `0.6¹² ≈ 0.2%`.

Compounded, an all-Major Wildcard pawn is on the order of **1 in 10⁷**. The instinct that it would be
too strong is right; the premise that it happens is not.

> [!CAUTION]
> **The clamp is a nerf, not a cleanup — and it would fire far more often than the 2.7σ tail
> suggests.** Capacity is `eligible.Count × 1.5`, and `eligible` excludes conflicting passions
> (Brawler vs Shooting), `TotallyDisabled` skills and `DropAll` genes — so it is routinely well under
> 12. For a pawn with 6 eligible skills, capacity is 9 pips, which a mid-quality Wildcard roll clears
> roughly **20%** of the time.
>
> And clamping is **not** outcome-neutral. The budget is converted to Major/Minor *counts* by the
> spend loop before anything is handed out, and Majors go first. Lowering the budget lowers the Major
> count:
>
> | | budget | rolled | 6 eligible skills receive |
> |---|---|---|---|
> | today | 12 pips | ~5 Major + ~4 Minor | **5 Major + 1 Minor** |
> | clamped | 9 pips | ~4 Major + ~3 Minor | **4 Major + 2 Minor** |
>
> So the surplus is not "silently discarded" in any sense that clamping recovers — discarding it is
> what currently lets a restricted-skill pawn max out.

**The three options, and the call:**

1. ✅ **Leave it.** A pawn with few eligible skills gets the best passions those skills can hold.
   Defensible on its own terms, and the "problem" it would fix is a 1-in-10⁷ event.
2. **Clamp the rolled counts, not the budget** — `majorPassions = Min(majorPassions, eligible.Count)`
   after the spend loop. **Genuinely outcome-neutral**; only tidies the trace. Available if the
   unspent-pip trace line ever becomes annoying.
3. **Clamp the budget** — a deliberate nerf to restricted-skill pawns across every profile, not a
   Wildcard tail fix. **Do not do this by accident.**

Implementation note if 2 or 3 is ever revisited: `budget` is rolled at `PassionVarianceApplier.cs:42`
but `eligible` is not built until ~`:79`, so either needs a reorder.

## Settled and not to be relitigated

| Question | Resolution |
|---|---|
| User-facing derivation write-up in the settings UI | **No.** If wanted, it belongs in the mod's About/description or `docs/`, not a tooltip. |
| Exposing the exchange rate `R` as a player setting | **Rejected.** A control that changes nothing (the score is display-only) while visibly breaking the ±35% envelope the mod advertises. |
| Making the Best-of-N integration midpoint-correct | **Rejected — carried permanently.** Both implementations share the slip so it cancels in every displayed figure, `N=1` is exact, and fixing it repastes every table for a difference no player can see. Argument in full under "Why the integration slip is carried". |

---

# 📖 VANILLA REFERENCE (decompiled)

`Assembly-CSharp.dll` decompiled with `ilspycmd` (installed as a global dotnet tool). Everything here
is quoted from the real assembly, not from memory or an earlier agent's summary. **Re-read this
before arguing about what vanilla does.**

**`PawnGenerator.GenerateSkills` (`:1846-1955`):**

1. Budget = `5f + Mathf.Clamp(Rand.Gaussian(), -4f, 4f)` → **mean 5, range [1, 9]**.
   `Rand.Gaussian(centerX, widthFactor)` scales a standard normal, so `widthFactor` *is* the sd and
   vanilla's clamp is exactly 4σ — which is what `PassionBudgetClampFactor` mirrors.
2. Spend: Major 1.5 / Minor 1, coin flip `Rand.Bool` (exactly 50%).
3. Forced-trait pass, skills in **def order**.
4. **`if (AgeBiologicalYears < 13) return;`** — a child never receives a rolled budget. Its passions
   come from forced traits plus growth birthdays at ages **7, 10, 13** (`GrowthUtility`), granting
   0–4 passions each by growth-point tier, applied as `IncrementPassion()` (None→Minor→Major).
5. Level-ordered walk, descending `GetLevel(includeAptitudes: false)`, skipping disabled /
   trait-conflicting / gene-`DropAll`.

Also confirmed: `SkillRecord.LearnRateFactor` is **None 0.35 / Minor 1.0 / Major 1.5**, and the skill
count has **no vanilla constant** — it is `DefDatabase<SkillDef>.AllDefsListForReading`, which
`Pawn_SkillTracker`'s ctor uses to build one `SkillRecord` per def.

Our child guard matches vanilla exactly — `AgeBiologicalYears < Constants.VanillaAdultPassionAge`,
ungated. It must **not** be `ModsConfig.BiotechActive && DevelopmentalStage != Adult`: that agrees
for humans on Biotech but **disagrees for any race declaring a Child life stage past 13 — HAR races
do** — and does not run at all without Biotech.

### Three places we deliberately do NOT copy vanilla

Each is argued in place in `PassionVarianceApplier.cs`:

| # | Vanilla's implementation | Why it is not worth copying |
|---|---|---|
| 1 | Forced-trait pass checks only `TotallyDisabled` — not `conflictingPassions`, not `DropAll` genes | It will force a Brawler a Shooting passion, the exact outcome vanilla's own walk prevents 20 lines later. Unreachable in shipped vanilla content, reachable the moment a mod adds such a trait. |
| 2 | Its inner trait loop has **no `break`** | Two traits forcing one skill charge the budget twice and overwrite the same `SkillRecord` — budget burned for nothing. |
| 3 | The level walk **overwrites skills that already hold a passion** | A Tortured Artist ends up with *fewer* distinct passions than an identical pawn without the trait, because part of the budget was spent twice on Artistic. Plainly not the intent of a trait whose purpose is to grant a passion. |

All three would break the "one unit of budget buys one passion" assumption that the composite score,
`envelope_check.py` and `GrowUpVariance`'s pip accounting all rely on. **"Mirrors vanilla" in this
mod means mirrors its intended algorithm.**

Note also that the `PassionVarianceApplier` "ran out of skills" guard **is reachable and must not be
deleted**: at `MaxPassionPips = 18` an all-Major budget buys exactly 12 Majors for exactly 12 skills
(dead even), but a Minor costs 1 pip so 18 pips buys up to 18 passions, and `eligible` is often
smaller than 12 (conflicting passions, disabled skills, `DropAll` genes).

---

# 🎭 PROFILES

**Naming:** the C# variable name and the player-facing name differ. Always refer to profiles by their
**display name** in discussion; the variable name only matters when editing `VarianceProfile.cs`.

| Display name | ID const | C# variable | Kind | Represents |
|---|---|---|---|---|
| **Faithful** | `FaithfulId` | `VanillaLike` | baseline | Closest to unmodded RimWorld. **The reference all envelope maths is measured against.** |
| **Desperate** | `DesperateId` | `Hardscrabble` | power tier (lowest) | Scraped-together survivors. Low skills, few passions, poor rolls common. |
| **Scavenger** | `ScavengerId` | `Scavenger` | power tier (low) | Wasteland survivors, pirates. Lower baseline skills, tough survival rolls. |
| **Specialist** | `SpecialistId` | `Specialist` | power tier | Engineered single-domain specialists (Genies, Hussars). Focused skill spikes. |
| **Elite** | `EliteId` | `Elite` | power tier (high) | Imperial nobility, high-born. Consistently high capability. |
| **Sovereign** | `SovereignId` | `Sovereign` | power tier (top) | Archite lords, Sanguophages, supreme leaders. |
| **Distinct** | `DistinctId` | `BalancedVariance` | **variance** | The mod's signature tuning. Strong individual strengths *and* weaknesses, fair colony average. |
| **Wildcard** | `WildcardId` | `WildSpread` | **variance** | Maximum variation. 0–8 traits, zero-to-many passions, wide skill swings. |

**Power tier vs variance preset is a load-bearing distinction.** Power tiers must obey Rule 2.
Variance presets are tuned for *dispersion* around a roughly baseline mean, so they cross `Faithful`
as N rises and are exempt from ordering — but NOT from the ±35% envelope.

`Sovereign`'s skill range is deliberately untouched by retunes — `skillShiftMin` stays `0` so its
mean band sits at or above the vanilla baseline, and the whole gain is passion budget. Translating it
instead would have left 0.5pp of N=1 headroom. **That `0` bounds the band, not each skill:** the
unclamped noise term means an individual skill on a low-quality roll can still land below vanilla.

### Which profiles the default config uses

Player-visible **out of the box** and must stay calibrated. Changing one is a Rule 5 consultation
item.

| Assignment | Profile | Source |
|---|---|---|
| Active profile (colonists) | **Faithful** | `activeProfileId` default |
| Hostile fallback | **Distinct** | `hostileProfileId` default |
| Empire | **Elite** (Highest) | `RestoreDefaultFactionOverrides` |
| Ancients / AncientsHostile | **Sovereign** (High) | " |
| Pirate / PirateSavage | **Scavenger** (Normal) | " |
| OutlanderCivil / OutlanderRough | **Faithful** (Low) | " |
| TribeCivil / TribeRough / TribeSavage | **Desperate** (Low) | " |
| Sanguophage | **Sovereign** (Highest) | `RestoreDefaultXenotypeOverrides` |
| Highmate | **Elite** (High) | " |
| Genie / Hussar / Dirtmole | **Specialist** (High/Normal) | " |
| Waster / Pigskin | **Scavenger** (Normal) | " |
| Neanderthal / Yttakin | **Distinct** (Normal) | " |
| Impid | **Wildcard** (Normal) | " |

**Race overrides ship with ZERO defaults.** There is no `RestoreDefaultRaceOverrides` and the section
has one Delete button, not the delete/restore pair the other two have — the installed race list is
mod-dependent and unknowable at compile time, so there is nothing sensible to seed.

---

# 🧩 OVERRIDE RESOLUTION

Three sources — faction, race, xenotype — resolved by a **total order**, not pairwise rules, because
three sources compared pairwise can cycle with no winner.

- Priority buckets: `Lowest (0)`, `Low (1)`, `Normal (2)`, `High (3)`, `Highest (4)`.
- **Priority level is compared first and always wins.** Source rank breaks exact ties only.
- `RankOf` gives `Faction > Race > Xenotype` when `factionOverridesTakePrecedence` is `true` (the
  default), and `Race > Xenotype > Faction` when it is `false`. **Race beats Xenotype at equal
  priority in both states.**
- No override matches → `Hostile Profile` (if applicable) → `Default Active Profile`.
- The xenotype source is skipped entirely without Biotech. **Race and faction are not.**

> [!IMPORTANT]
> **`DrawRaceOverridesSection` is NOT gated on `ModsConfig.BiotechActive`.** Only the xenotype
> section is. HAR race mods do not depend on Biotech; gating race there silently disables the entire
> feature for the users it was built for. This is the highest-risk invariant in the overrides area —
> re-check it after any edit to `ValuesFor` or the Overrides tab.

**Any override beats the Active Colony Profile**, including a race override at Normal priority. This
was surprising enough in practice that the General tab now carries a caption under the picker:
*"Overrides on a pawn's faction, race or xenotype take precedence over this."* Considered and
rejected: computing and displaying what a colonist *actually* resolves to — more useful, but it has
to stay correct as `ValuesFor` evolves.

Because the shipped xenotype defaults sit at High/Highest (Sanguophage, Highmate, Genie, Hussar), a
race override at the default Normal loses to them automatically; the tie rule only fires against the
Normal-tier xenotypes.

### The Add-menu filter has two halves and both are load-bearing

`SelectableRaces()` returns humanlike races referenced by at least one `PawnKindDef`.

- **`Humanlike`** excludes the ~37 mechanoid alien races HAR mods ship (`Wolfein_Mechanoid_*`,
  `Milian_Mechanoid_*`, `Milira_Drone*`, `*_FloatUnit_*`). Without it the menu is unusable.
- **The `PawnKindDef` traversal** excludes abstract and unreferenced race defs.

If someone "simplifies" this to `DefDatabase<ThingDef>.AllDefs.Where(d => d.race != null)`, the menu
floods.

**`CreepJoiner` (Anomaly) reaches the menu and stays.** The filter rule is "humanlike races something
spawns" and it qualifies; excluding it would mean a hardcoded defName special case that every future
DLC would need extending. It labels as "Human", which is why the duplicate-label grouping renders
`Human (Human)` and `Human (CreepJoiner)` — **that is load-bearing, not cosmetic.**

**Stale-override scrubbing** goes through `PawnVarianceSettings.ScrubStaleOverrides(overrides,
priorities, deletedId)` (internal). The hazard it exists for is not duplication — it is a future
fourth override axis remembering one of its two parallel dictionaries and not the other.

---

# 🖼️ PROFILE EDITOR — LAYOUT INVARIANTS

Spec: [`docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md`](docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md) ·
Plan: [`docs/superpowers/plans/2026-08-03-profile-editor-layout.md`](docs/superpowers/plans/2026-08-03-profile-editor-layout.md)

Drawing lives in `Source/ProfileEditorTab.cs` (`partial class PawnVarianceSettings`), with
`Source/Dialog_RenameProfile.cs` for the rename modal.

- **Pinned 162px header** (`DrawProfileEditorHeader`), does not scroll: profile picker + 5-button
  action strip (`+ New`, `Duplicate`, `Rename`, `Reset`, `Delete`) / one-line description / quality
  slider with `{tier} ({power})` readout / Best-of-25 readout row / full-width distribution curve.
  Rows: 28 + 4 + 20 + 2 + 28 + 2 + 20 + 4 + 54 = 162. **The individual row sums are arithmetic and
  have only been verified in aggregate — if you change one, re-measure rather than re-adding.**
- **The editor has its own selection state.** `editorProfileId` is separate and non-persisted;
  it must never read or write `activeProfileId`, or the editor hijacks the colony profile. Delete
  also clears the deleted id from the colony profile, the hostile profile and all override maps.
- **The curve is never greyed**, even on read-only presets. It is a readout, not a control; greying
  it would break comparing presets by cycling the picker. Only the quality *slider* is disabled.
  Do not "fix" this.
- **`+ New` and `Duplicate` stay enabled on presets.** A new user lands on `Faithful`, which is
  read-only — these two buttons are the only way off it. Greying them creates a dead end.
- **`Widgets.IntRange` is FORBIDDEN on the four min/max pairs.** `passionCountMin`/`Max` hold
  fractional calibrated values (`1.4`, `2.5`, `6.2`, …); `IntRange` truncates them and would silently
  recalibrate a Rule 5 governed value. Use `FloatRange`, no `roundTo`. Passion counts display to one
  decimal (`:F1`) — display only.
- **Row 2 saves and restores three pieces of global draw state** — `Text.Font`, `GUI.color`,
  `Text.WordWrap`. `WordWrap = false` is what structurally guarantees the fixed 20px row stays one
  line and cannot overlap the quality slider. Keep all three restores.
- **Scroll heights are floors, not caps.** `PawnVarianceSettings.cs:614` is
  `Math.Max(overridesViewHeight, 1000f)` and `:656` recomputes `listing.CurHeight + 40f` each frame,
  so extra sections expand the view rather than clipping. The editor body carries a `580f` minimum so
  the scrollbar is always active and lower controls stay reachable.
- **Best-of-25, not Best-of-50, and no `N` slider** — it is a lens, not a setting. At N=50 `Wildcard`
  displays near the envelope limit, and a UI that advertises how close a preset sits to the limit
  invites players to treat the limit as a target.
- **The distribution curve stays a SINGLE line.** A second, ghosted Best-of-N curve was considered
  and rejected — it doubles the ink for a quantity the two header anchors already state numerically.
- **If you touch `FormatPowerPercent`, the baseline must be measured at the same N as the score.**
  Comparing a Best-of-25 score against Faithful's N=1 baseline once put every figure ~36pp too high
  and flipped `Desperate`/`Scavenger` positive — inverting the exact fact the second anchor exists to
  convey.
- The Best-of-25 readout mirrors `envelope_check.py` at 1024 integration nodes. **If you change one,
  change both** — and the `Verify Best-of-N` debug action now enforces that mechanically.

### `countProtectedTraits` is `true` and that is deliberate

Trait count means **total traits on the pawn**, not traits this mod adds. A Hussar with 2 forced
traits on a 2–4 profile rolls ~1 extra and lands at 3, the same total as a Baseliner on that profile.
The accepted cost is that forced-trait pawns get less rolled personality.

> [!CAUTION]
> **Mechanism worth remembering, because it made the flip invisible.** `Scribe_Values.Look` omits a
> value from the written XML when it equals the default. Any settings file saved while the default
> was `false`, by a user who had it `false`, therefore has **no `countProtectedTraits` key at all** —
> and loads as `true`, changing behaviour without the player touching anything. This is a general
> hazard of changing a `Scribe_` default, not a one-off.

`VarianceProfile` clamps `passionCountMin`/`Max` to `Constants.MaxPassionPips` in the normalise path,
deliberately: the slider bound only guards new input, while old saves and `SettingsTransfer` imports
reach those fields without passing a widget.

---

# 🛠️ FEATURE SUMMARY & ARCHITECTURE

1. **Three-source override priority system** — see "Override resolution" above.

2. **Unlimited dynamic custom profiles** — `CustomProfile` instances in `customProfiles` with string
   ids (`"custom_1"`, `"custom_2"`), created/renamed/duplicated/reset/deleted from the Profile
   Editor. `CustomProfile` implements `IRenameable`.

3. **Settings import/export** (`Source/SettingsTransfer.cs`) — structural clipboard transfer of
   custom profiles, override maps, priorities and General toggles. Pre-validates XML via
   `XmlDocument.LoadXml` before calling `Scribe_Deep.Look`, to prevent a Scribe exception blocking.

4. **⚠️ Traits are generated from TWO independent call sites** — any future trait work must handle
   both:
   - `TraitVarianceApplier.cs:72` — `GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false)`
   - `GrowUpVariance.cs:209` — `GenerateTraitsFor(pawn, requested, null, growthMomentTrait: true)`

   Two consequences that are easy to miss:
   - The growth-moment call passes **`request: null`**, so every vanilla check that reads the request
     is skipped — `kindDef.disallowedTraits`, `disallowedTraitsWithDegree`, `requiredWorkTags`,
     `ProhibitedTraits`, and the hostile-spawn `allowOnHostileSpawn` gate (verified in decompiled
     `PawnGenerator.GenerateTraitsFor`).
   - The growth-moment trait pass is **add-only by design** (`GrowUpVariance.cs:70-79`). **Anything
     granted at 13 is permanent**; no later pass revisits it.

5. **Age-13 growth-moment deferral pipeline** — children aging to 13 defer mod application while a
   `ChoiceLetter_GrowthMoment` is pending (`GrowUpPendingComponent`); the mod then applies strictly
   add-only trait/passion increments once the player resolves the letter.

6. **Non-spam faction handling** — `Faction.OfPlayerSilentFail` instead of `Faction.OfPlayer` across
   call sites, to eliminate world-gen log errors.

---

# 🚀 BUILD & DEPLOYMENT LOOP

```powershell
tasklist /FI "IMAGENAME eq RimWorldWin64.exe"   # must show no running instance
dotnet build Source/PawnVarianceMod.csproj
Copy-Item Assemblies/PawnVarianceMod.dll, Assemblies/PawnVarianceMod.pdb "C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/" -Force
```

- **Guard**: check for a running RimWorld before copying, or the DLL copy fails on a file lock.
- **Verification**: `dotnet build` must return `0 Error(s), 0 Warning(s)`.

---

# 🧪 VERIFICATION HARNESS

**There is no unit-test project, and that is a decision rather than a gap.** The interesting code is
`Pawn`-coupled — the Harmony postfix, `ValuesFor` resolution and all three appliers only mean
anything against a real generated pawn — so an out-of-game test double would be testing a *copy* of
the logic instead of the logic. The harness is two dev-mode debug actions in `Source/DebugActions.cs`
under the **`Varied Pawns`** category, plus two offline tools. All debug actions are invisible to
normal players by construction (RimWorld gates the debug menu behind `Prefs.DevMode`) and runnable
through GABS via `rimworld/execute_debug_action`.

### 1. `Verify Best-of-N against envelope_check.py`

Diffs the mod's live 1024-node integrator against the 20000-node reference in
`Source/EnvelopeFigures.g.cs` for all 8 presets × N = 1, 5, 25, 50.

**Tolerance is 0.5 *percentage points on the displayed quantity*, not 0.5% on raw scores** — and that
distinction is the whole point. The two implementations share a first-order-accurate right-edge CDF,
so 1024 and 20000 nodes genuinely do not converge to the same raw number (up to ~0.9% apart at N=50).
That gap cancels in the ratio to `Faithful`, so it moves no digit on screen. An earlier version of
this gate compared raw scores and produced 15 false failures alongside 1 real defect, which made the
real one indistinguishable. A deliberately wide 3% raw guard is kept so gross divergence still fails.

**Why it exists:** the mod and `envelope_check.py` contain two implementations of the same integral
(custom profiles need a live figure no precomputed table can cover), and the only thing holding them
together used to be a comment saying *"if you change one, change both."* That contract has already
failed. It also compares the scoring constants against a snapshot taken at generation time, so
"changed a constant, forgot to re-run the tool" is caught — otherwise a stale table passes by being
merely self-consistent.

### 2. `Roll pawns and dump distribution`

Generates 50 / 200 / 1000 colonists through the real `PawnGenerator.GeneratePawn` path and dumps
mean / sd / min / p10 / median / p90 / max for per-skill level, per-pawn mean skill, passion pips and
trait count, plus a histogram and the passionless-pawn rate.

**This is the only place dispersion can be *observed* rather than derived.** Hold the reported
per-skill sd against the `per-skill sd` column from `envelope_check.py`: **observed should sit above
predicted**, since the tool models the noise term only while the observed figure also carries the
spread of the quality roll itself. If observed comes in *below* predicted, the noise term is not
reaching pawns and something upstream is clamping it.

Passion pips are priced as the spend loop prices them (Major 1.5, Minor 1) — counting passions
instead would understate any Major-biased profile by a third. Verbose logging is suppressed for the
batch and restored in a `finally`, and throwaway pawns are `Discard`ed so they cannot leak into the
world pawn pool. Expect one vanilla `Tried to discard <pawn> whose state is -1.` warning per pawn:
harmless, but at 200 pawns it floods the log and each warning is a candidate for GABS's attention
gate.

> [!NOTE]
> It samples player-faction colonists, so it exercises the **active profile only** — the faction, race
> and xenotype override paths are not covered. Nothing in this repo exercises override resolution at
> runtime; the closest thing is the Python mirror `zzz-Do-Not-Commit/test_race_resolution.py`
> (19 cases), which validates the rule table but not the C# that implements it.

### 3. Diagnostic dumps

- **`Dump Add-menu race list`** — prints what `SelectableRaces()` actually returns, by calling the
  real method, so a regression cannot pass it.
- **`Dump override resolution matrix`** — generates a real pawn per case and calls
  `PawnVarianceSettings.ValuesFor(pawn, request)`, the same call the Harmony postfix makes, under
  both `factionOverridesTakePrecedence` states. **It reports rather than asserts**, deliberately:
  re-deriving the expected winner in the harness would be a second copy of the rule, and a copy
  agreeing with itself proves nothing.

  > **If you add cases here, force the xenotype.** A sweep once returned a third profile entirely
  > because the Empire pawnkind randomly rolled a Genie, whose xenotype override sits at High and
  > outranked both Normal candidates. Correct behaviour, wrong experiment. Force `Baseliner` so the
  > comparison under test is the only live one.

### 4. Offline

- `python docs/tools/envelope_check.py` — the envelope gate; also regenerates
  `Source/EnvelopeFigures.g.cs`.
- `python zzz-Do-Not-Commit/test_race_resolution.py` — 19-case resolver mirror.

  > It passed **14/14 while never asserting a `Lowest`-priority override winning as the sole match** —
  > a case the requirement named explicitly. The gate was green and not watching. All five added
  > assertions passed on the first run, so the resolver was already correct, but **a passing test
  > suite said nothing about the requirement it was built to protect.**

---

# 📡 AUTOMATION & BRIDGE NOTES

- **RimBridgeServer & GABS** are installed and configured (`rimbridge/list_logs`,
  `rimworld/execute_debug_action`). All mod logs are prefixed `[PawnVarianceMod]`. Key traces:
  `Trait assignment (...) for X (quality Q, profile P)`, the growth-moment deferral line, and
  `Growth moment resolved for … after N ticks`.
- **The mod logs nothing at startup by design**, so for a load-success check (e.g. verifying the race
  section is not Biotech-gated) a **silent log is the pass condition**.
- **`FloatMenu`-based UI cannot be driven by the bridge.** A synthetic click activates the button but
  no float menu survives to the next frame to be read. This affects every Add button in the Overrides
  tab, not just the race one. Adding an override row is a by-hand check.
- **`get_ui_layout`'s `disabled` field does not capture ambient `GUI.enabled`.** Every button reports
  `disabled: false`, including Rename/Delete while a read-only preset is selected, where the code
  demonstrably sets `GUI.enabled = false`. Do not read a greying regression out of that field; it
  cannot see one.
- **`update_mod_settings` rejects dictionary-index paths**, so it is not a way into the override maps.

> [!NOTE]
> **A no-Biotech run produces a large error wall, and none of it is this mod.** Every entry belongs to
> **Milira Race**: its *Milian mechanoid* content binds to Biotech defs that do not exist when Biotech
> is off — `MechBandwidth`, `MechControlGroups`, `MechRepairSpeed`, `MechFormingSpeed`,
> `WorkSpeedGlobalOffsetMech`, the `LightMechanoid`/`LightMechanoidKind` parent nodes,
> `MainButtonDef Mechs`, `PawnColumnDef Overseer`/`ControlGroup`, `Milian_Gestator`,
> `Milian_Recharger` and the `Milian_NamePlate_*` family — cascading into `Milira_Scenarios` config
> errors. A pre-existing Milira-without-Biotech compatibility problem. (Duplicate-`packageId` errors
> for `CETeam.CombatExtended` and `NozoMe.MapModeFramework` are duplicate workshop installs, also
> unrelated.) **Do not read this wall as a regression.**

---

# 🐞 LESSONS THAT KEEP COSTING TIME

### Debug action visibility is the opposite of what it looks like

| declared | current state | visible? |
|---|---|---|
| `Entry \| Playing` (3) | `PlayingOnMap` (6) | no |
| `Entry \| PlayingOnMap` (7) | `PlayingOnMap` (6) | no |
| `PlayingOnMap` (6) | `PlayingOnMap` (6) | **yes** |

The gate is `(current & declared) == declared` — the declared set must be a **SUBSET** of the current
state. **ORing in another state makes an action LESS visible, not more**, and "visible at the main
menu AND on a map" cannot be expressed in a single attribute. If you add a debug action and it never
shows up, this is why. Declare the single state you actually need.

### Unit errors survive review when a normalizer changes underneath them

Every numerical defect this project has shipped has the same shape: **a quantity whose UNITS changed
while an expression built on the old units stayed behind.** The passion budget's denominator moved
from 12 (a skill *count*) to 18 (a *pip* ceiling) and a count-unit premium factor stayed in the
numerator, inflating the passion axis by up to +25% and scaling with `passionMajorBias` — a slider
meant to change *which* passions a pawn gets, not how many pips it spends. The same era left a `24`
in a slider ceiling and a `24` in a comment that had become an argument for deleting a live guard.
**When you change a normalizer, grep for everything built on the old one.**

### Per-task review and whole-branch review catch different things; execution catches a third thing

Two Best-of-N integrator defects survived a clean build, `envelope_check.py` and every *per-task*
review, and were found by running the real assembly. But when the batch's whole-branch review was
finally dispatched, it re-derived **both** of them statically from the diff alone. So:

- *Per-task* review does not catch **cross-task** numerical defects.
- **"Reviewed and builds clean" says nothing about numerical code** — `envelope_check.py` never
  executes the C#.
- **Neither mode substitutes for the other, and neither substitutes for running it.** Do not cite the
  in-game finds as a reason to skip a static review — that argument nearly won once.

### Cite-check every review verdict

Of a recent Gemini review's findings, three did not survive verification against the source: a
finding citing a line range that was unrelated UI code, a claimed race between a static cache and
async pawn generation (the call graph shows the path is main-thread UI and debug only), and a
visibility bug flagged on the **correctly-declared** action while its genuinely broken sibling was
missed. **Two of the three would have sent someone editing the wrong code.** Verify line citations
before relaying a review.

### When an agent must edit a file the owner is holding

**Stage a filtered patch to the index — never `stash`/`checkout`/`reset` the file out from under
them.** `git apply --cached` of a filtered patch commits one hunk while leaving the owner's in-flight
lines dirty in the working tree.

> [!CAUTION]
> **The owner edits files while agents run.** In one session three files were dirty at start; minutes
> later two had been reverted by hand and the third re-edited to different values. **Read the working
> tree immediately before any `stash`/`checkout`/`reset`** — a `git status` from the top of a long
> session is not evidence about the tree now.

---

# 📋 CODE REVIEW STATUS (file-by-file)

Files marked `DONE (REVIEWED)` are protected by Rule 8 — no modification without explicit permission.

- [x] `Source/VarianceProfile.cs` — legacy enum/comment cleanup, `IExposable` parameterless
  `ExposeData()`, `distributionParamsDirty` cache, `MakeValues()`.
- [x] `Source/PawnVarianceSettings.cs` — Overrides tab UX safety, button colors & dialogs, dynamic
  scroll view height, explicit Normal priority handling, percentage readout vs Faithful.
- [x] `Source/ProfileEditorTab.cs` — layout redesign, partial class, pinned header, delete cascade,
  Best-of-25 readout math, Beta curve plotting.
- [x] `Source/Dialog_RenameProfile.cs` — rename modal subclassing `Dialog_Rename<CustomProfile>`.
- [x] `Source/SettingsTransfer.cs` — Scribe export/import, `ForceStop` safety, `XmlDocument`
  pre-validation, atomic `CopyFrom` swap.
- [x] `Source/QualityRoller.cs` — `Beta(a,b)` via Gamma variates (Marsaglia-Tsang, Stuart's theorem,
  Box-Muller), 0/0 NaN underflow guard.
- [x] `Source/SkillVarianceApplier.cs` — baseline lerp + triangular noise, generation vs age-13 split,
  Biotech gene aptitude fix reading `levelInt` directly.
- [x] `Source/TraitProtection.cs` — Biotech gene DNA protection, `ScenForced`, multi-source forced
  trait capture, relationship-aware sexuality protection.
- [ ] `Source/TraitVarianceApplier.cs` — **NEXT UP**
- [ ] `Source/TraitAgeCap.cs`
- [ ] `Source/TraitTrace.cs`
- [ ] `Source/PassionVarianceApplier.cs`
- [ ] `Source/GrowUpVariance.cs`
- [ ] `Source/GrowthUpPatch.cs`
- [ ] `Source/GrowUpPendingComponent.cs`
- [ ] `Source/HarmonyPatches.cs`
- [ ] `Source/PawnVarianceMod.cs`
- [ ] `Source/Constants.cs`

---

# 🔮 NEXT PROJECTS (after this mod)

1. **Guest Room Mod** — designate a room as a guest room. Low room stats satisfy traders poorly
   (drops relations, but lowers perceived wealth → easier raids). High room stats increase trade
   relations and trader frequency, but increase perceived wealth → harder raids.
2. **Perceived Wealth Mod** — decouple storyteller raid scaling from actual stockpile value via a
   dynamic rumor system. Perceived wealth fluctuates on direct observations by escaping raiders,
   visiting traders and radio broadcasts, with rumor decay and suspicion floors for dark zones.
