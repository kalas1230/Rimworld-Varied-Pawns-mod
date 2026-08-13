# Handover — Varied Pawns Mod

Repo: <https://github.com/kalas1230/Rimworld-Pawn-variance-mod> · Branch `main`. Paths in this
document are relative to the repo root; the RimWorld install is written as `…\RimWorld\`.

**What this document is.** The durable reference for the mod: the scoring model and where its
numbers come from, the invariants that are easy to break by accident, the decisions that have
already been argued out, and what is still open. **It is not a changelog** — git holds the history
of what changed when. Nothing here should be phrased as "on date X we did Y"; if a fact only
matters as history, it belongs in a commit message.

The mod is **unreleased**. There are no existing users and no backward-compatibility obligation —
do not add migration shims. If a saved config breaks, the fix is to reset it.

---

# 🔴 OPEN WORK

## 0. Dispersion-aware scoring — DONE

**Plan:** [`docs/superpowers/plans/2026-08-07-dispersion-aware-scoring.md`](docs/superpowers/plans/2026-08-07-dispersion-aware-scoring.md)
**Spec:** [`docs/superpowers/specs/2026-08-07-dispersion-aware-scoring-design.md`](docs/superpowers/specs/2026-08-07-dispersion-aware-scoring-design.md)

Best-of-N's composite score used to read six profile fields (`averageQuality`, `skillShiftMin/Max`,
`passionCountMin/Max`, `passionMajorBias`) and was blind to noise. Best-of-N is a **maximum**
statistic and maxima reward dispersion, so a metric blind to spread systematically understated the
one preset whose noise sits far off the pack — `Wildcard` breached the ±35% envelope (Rule 1) while
the gate read green, because the gate only proved the two implementations agreed with each other,
not that either measured the real quantity. The rest of this document now describes the fixed state;
the two findings that shaped the fix are worth carrying forward because they generalize:

- **Skill noise is nearly free on the envelope; passion noise is the whole story.** Per-skill noise
  is drawn 12 times independently and averaged, so it enters Best-of-N diluted by `√12` and then gets
  censored by `Clamp(0,20)`. The passion budget is a single per-pawn draw and reaches Best-of-N in
  full undiluted. A retune that only narrows the skill axis leaves the preset's real dispersion
  almost untouched.
- **A metric can pass every offline check and still be wrong in the one place that matters: the
  realised, clamped population.** `envelope_check.py` and the composite score both read
  `Lerp(skillShiftMin, skillShiftMax, q)` — the mean band — never a rolled, clamped pawn. See
  "Left-censoring DESTROYS dispersion" below for the mechanism and the retune history it forced.

Model: a deterministic quadrature (`Source/DispersionModel.cs`, mirrored in
`grid_moments`/`make_grid_score` in `envelope_check.py`) treats the composite as
`Normal(μ(q), σ(q))` conditional on the Beta-distributed quality roll `q`, and integrates that
mixture to get `F(x)` for Best-of-N. An independent Monte Carlo ground truth
(`docs/tools/dispersion_mc.py`) validates the quadrature offline; it cannot ship in the gate itself
because the gate needs both sides deterministic. See "What the model cannot see" below for where the
approximation is known to drift.

**In-game verification, run by the controller, not predicted:**
- `Verify Best-of-N against envelope_check.py` passes **32/32** against the shipped build, worst
  displayed divergence 0.01pp.
- A 1000-pawn `Wildcard` dump confirmed the shipped band resolves correctly
  (`ACTUALLY RESOLVED TO: Wildcard x1000`).
- The noise-field rename (`skillNoise`/`passionNoise` → `skillSpread`/`passionSpread`, now real
  units) moved no distribution figure: per-skill sd 3.47 vs 3.51 pre-rename, per-pawn sd 1.26 vs
  1.30 — run-to-run noise on an unseeded dump, against which a dropped `√6` conversion would have
  read ~2.4× narrower. It did not.
- Zeroing both spread fields moved `Best of 25` from +23% to +9%, proving the readout now actually
  responds to dispersion rather than only to the mean band.
- **The generator/model assertion is measured, not argued.** `Roll pawns and dump distribution` at
  1000 pawns, against a build deployed from the current tree, reports for the resolved profile
  (`Custom 1 x1000`): `model predicts 4.548 pips/pawn (sd 1.234)`, `pawns delivered 4.553`,
  `delta +0.004 against tolerance 0.156` — `OK — the model describes the pawns being rolled`.
  `DumpDistribution` groups eligible pawns by their RESOLVED profile and prints one
  `GENERATOR vs MODEL` block per group (minimum 30 eligible pawns per group; a smaller group is
  reported, not asserted), so a colony that resolves several profiles at once is asserted per
  profile rather than skipped outright. A delta beyond tolerance on a group means a generator
  branch has no mirror.
- **The multi-group path is now measured too.** A 1000-pawn run resolved
  `Custom 1 x504 (50.4%), Wildcard x255 (25.5%), Distinct x241 (24.1%)`, printed the
  `MIXED SAMPLE` warning, and emitted three independent `GENERATOR vs MODEL` blocks — all three
  `OK` (deltas `+0.008/0.220`, `−0.211/0.589`, `−0.068/0.420`). The grouping branch has been seen
  working.

  > **Getting there needed a fixture, and the reason is worth keeping.** This document used to say
  > a run "with race/xenotype overrides active" would close the item. That is wrong and cost a
  > cycle: within one dump run the sampler draws a single `PawnKindDef` (`Colonist`) from a single
  > faction (the player's), so faction is constant and race is always `Human`, leaving xenotype as
  > the only axis `ValuesFor` could split on — and vanilla's `PlayerColony` FactionDef has **no
  > `xenotypeSet`**, so every sampled pawn generates Baseliner. **No override configuration can
  > produce a mixed sample**; an override just relabels all 1000 pawns at once. The sample itself
  > has to be made heterogeneous. The fixture that does it — a `PatchOperationAdd` giving
  > `PlayerColony` a 25% Impid / 25% Neanderthal xenotypeSet, dropped into the *deployed* mod's
  > `Patches/` folder only and deleted afterwards — is kept at
  > `zzz-Do-Not-Commit/TestOnly_PlayerColonyXenotypes.xml`. It must never ship: that directory is
  > git-excluded, and the deploy loop copies only the `.dll` and `.pdb`.

**Affordance worth knowing:** the profile editor can be opened directly via GABS with
`rimworld/open_mod_settings`, `modId: mod-settings:kalas.pawnvariance:28ba19877e53c641` — far faster
than clicking through Options when verifying a UI change in game.

## 1. Carried items — known, quantified, not fixed

The in-game `Varied Pawns > Verify Best-of-N against envelope_check.py` gate passes **32/32** against
the shipped build: worst displayed divergence 0.01pp against the 0.50pp tolerance, worst raw 0.01%
against the **0.1%** guard (tightened from 3%; re-measured under the new guard, not carried over),
every `N=1` row bit-identical. The gate also asserts that both sides ran the same quadrature grid —
see "The integration slip is GONE" for that check and the teeth test that proved it. It also reports
`pip prediction matches Moments
at q=0.10/0.50/0.90, and ExpectedPassionPips' Beta integral matches its own per-q reconstruction, on
all presets` (invariant 3). `envelope_check.py` PASSes Rule 1 and Rule 2 at N = 1, 5, 25, 50 and
reports `EnvelopeFigures.g.cs: unchanged`.

**Re-running both after any scoring change is Rule 6**, and the tuning constraints that used to sit
here have moved to "Tuning constraints" below — they govern every future retune.

| Item | Why it is carried |
|---|---|
| **Milians are unreachable by race override, and that is now a closed limitation rather than an open question.** `Milian_Race` does not appear in the Add menu: the only `PawnKindDef` naming it, `Milian_Base`, is `Abstract="True"` with zero concrete children, so nothing spawns it through a kind def and `SelectableRaces()`' traversal drops it. Measured across all 1376 installed workshop mods, not just Milira's own — no mod supplies a concrete child, in either the mod's `1.5/` or `1.6/` defs. | Milians **are** real humanlike pawns (`Race_Milian.xml`: `intelligence: Humanlike`), produced in **code** — the Milira assembly carries `Milian_Race` as a string literal alongside `CompHumanizeMilian`, `JobDriver_HumanizeMilian` and `CompMilianGestateInfo`, i.e. gestated/humanized from the mechanoid Milians rather than rolled from a kind def. So the traversal would need a second source to reach them, and that source is **all humanlike `ThingDef`s** — which would also re-admit every abstract and unreferenced race def the current filter deliberately drops. **Not worth it for one mod's edge case; the filter stays as specified.** Not verified: whether a humanized Milian routes through `GenerateNewPawnInternal` at all, i.e. whether the mod even applies variance to them. |
| **Init-vs-`Scribe` default mismatch on skill and trait fields** — on `VarianceProfileValues`, `skillShift` initialises −4/6 against `Scribe`'s −3/3, `traitCount` 1/6 against 2/3, and `skillSpread` `0.857321f` (`Distinct`'s value) against `0.50f` (`Faithful`'s). `averageQuality`, `passionSpread` and `passionMajorBias` do agree. | Unreachable either way — every creation path passes explicit values, and the parameterless ctor is reached only by `Scribe`, whose `ExposeData` overwrites all of it on load. The in-file comment used to claim all four defaults matched `Faithful`; **that claim is now corrected in place**, field by field, with an explicit "do not chase the numbers" note. Nothing further to do here. |
| **`CopyFrom` does not validate imported profile ids** (T5-M1, Minor) — **resolved in effect, cosmetic remainder only.** | The harmful half is gone. P-14 deleted the `customProfiles[0]` fallback, so a dangling id no longer generates pawns from an arbitrary unrelated profile under the requested profile's name; `Resolve` now returns pristine `Faithful` values and emits a `Log.WarningOnce` naming the id. An unvalidated id can still *arrive* through import, which is all that is left. P-14 says so explicitly: *"T5-M1 should be reclassified as resolved-in-effect."* |
| **Single-slot cache in `CalculateBestOfNScore`** (Minor) — **no longer thrashes anywhere live.** | Both eviction paths are already handled: `FaithfulBestOfNBaseline` has its own separate cache slot, and the verify gate precomputes the whole Faithful baseline array up front (`DebugActions.cs`, above the profile loop) specifically so it does not alternate against the slot. The one remaining caller, `ProfileEditorTab`, makes a single call per frame and always hits. Kept in the table only so nobody "fixes" a problem that has already been designed around. |
| Five further Minor findings | In `.superpowers/sdd/progress.md`, all marked `CARRIED`: T1-M1, T1-M2, T1-M3, T2-M1, T2-M2. **T2-M1 is the one worth knowing** — a real 2-vs-2 mirror split in the passion-*disabled* fallback, inert today only because `VanillaPassionBudget` (5.0) sits far below capacity (~12 pips), so the missing cap is a no-op. It agrees by luck of the current constants, not because the formulas match. |

> [!NOTE]
> `.superpowers/sdd/progress.md` is **gitignored and gets overwritten in place** by each batch, so
> the T-numbers in it always refer to the *current* batch. The 2026-08-04 batch's own per-task
> findings are genuinely gone: this document used to say they survive in
> `git show fb1d8a8:HANDOVER.md`, and **they do not** — that revision contains no `T*-M*` marker at
> all, and no revision of this file ever carried more than two. Do not go looking. What *does*
> survive of that batch is the reasoning that was promoted into
> `docs/AUDIT-2026-08-06-problem-register.md` (P-14 is where T5-M1 ended up).

---

# 🎚️ TUNING CONSTRAINTS

Settled properties of the code that a retune has to work with. **Tuning without knowing these will
fight the implementation** — each one has bitten at least once.

- **No downside floor on skills, and do not add one.** `skillShiftMin` and `skillSpread` are the
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
- **Two baselines, and they are not interchangeable.** `Faithful` has two reference numbers and
  quoting the wrong one is audit finding Q-03.
  | | value | what it is | used by |
  |---|---|---|---|
  | **readout** | `0.2422` | `Faithful`'s **dispersion-aware** typical — noise integrated through the clamps | `FaithfulBaseline()`; the "Typical" row's denominator; the curve's centre line |
  | **mean-band** | `0.2507` | what a **zero-variance** vanilla pawn scores | the both-axes-off invariant (Q-16); the `0.2500` argument below |

  They agreed to six decimals until 2026-08-09 and now differ by ~3.4%, because `E[spent]` is a
  staircase and the mean band lands just above one of its jumps. **The readout must divide like by
  like** — that is the whole of Q-03. The Best-of-N row always did this correctly, via
  `FaithfulBestOfNBaseline`, which is why only the "Typical" row was wrong.
- **The `Faithful` mean-band baseline is `0.2507`, and an exactly-`0.2500` baseline was rejected.**
  `Faithful`'s budget midpoint is `5.0` to match **vanilla's own flat budget**, which is what the
  vanilla-like preset should have carried all along. Chasing a round `0.2500` reference instead
  would have needed a `4.79`-pip midpoint — a number picked to make a readout tidy rather than to
  match the game. The round number is cosmetic and nothing depends on it. Measured at 1000 pawns,
  `Faithful`'s realised budget is `4.59` pips mean over a `1.0–9.0` range, which **is** vanilla's
  `5 + Clamp(Gaussian(0,1), ±4)` range exactly.
  > The baseline read `0.2571` until 2026-08-09 and the near-miss on a round `0.2500` is now a
  > near-miss from the other side. It moved because the score started spending the budget through
  > the generator's own loop instead of continuously (finding Q-14) — `5.0` pips targeted, `4.81`
  > delivered at the mean band. **The `4.59` measurement above did not change and was never
  > wrong**; what changed is that the model finally agrees with it. That figure was sitting in this
  > document as evidence of vanilla parity while the score assumed `5.001`, which is exactly how
  > the defect survived. Do not "restore" `0.2571`.
- **Every preset's passion band carries the same `+1` pip offset**, not just `Faithful`'s. Raising
  the reference alone put `Faithful` *above* `Specialist` (a Rule 2 violation) and left `Desperate`
  1.3pp inside the envelope. A uniform shift preserves every relative difference; it is what widened
  the tightest margin to 10.3pp — since re-measured at **8.7pp** (`Sovereign` @ N=1, after the
  2026-08-12 `Wildcard` retune), see below. **If `Faithful` moves again, move all eight.**

**Two hard gates on any retune:** `envelope_check.py` must still PASS Rule 1 and Rule 2 at
N = 1, 5, 25, 50; and if any figure moves, `Source/EnvelopeFigures.g.cs` **and** every pasted table
in this document must be regenerated together. The tool prints
`Source/EnvelopeFigures.g.cs: unchanged` when nothing moved — trust that line, not memory.

> [!CAUTION]
> **Retuning is where this project has shipped its worst defects.** Both Best-of-N integrator bugs
> and the ~36pp Best-of-25 inversion were introduced during retune-adjacent work and survived clean
> builds and static review. Run the in-game `Verify Best-of-N` action afterwards.

## 1. Retunements (Pre-Shipping)

- ~~**Wildcard profile balance ($N=1$ vs $N=25$)**~~ — **DONE 2026-08-12.** `Wildcard` now reads
  **−19.9% at N=1 / +23.2% at N=50** (was −2.5% / +26.5%), a slope of +43.1 against `Distinct`'s
  +20.4 — the "proportionately severe single-draw downside" this item asked for. It is no longer
  the strongest preset at colony scale, and no longer the tightest envelope margin (11.8pp;
  `Sovereign` @ N=1 is tightest again at 8.7pp).

  **The lever is `passionCountMin` 2.2 → 0.3, not `averageQuality`.** Two `averageQuality`-based
  builds were deployed and dumped first, and both FAILED in game while `envelope_check.py`
  reported PASS: lowering quality buys the N=1 penalty by crushing skills into `Clamp(0,20)`,
  which flattens pawn-to-pawn spread at the same time (per-pawn sd 1.30 → **1.07**, i.e. Wildcard
  less varied than `Faithful`). Passion pips have no equivalent collapse, so the floor drop buys
  the same penalty for free. Shipped values verified over two 1000-pawn dumps: per-pawn sd
  1.24/1.22 and per-skill sd 3.44/3.46, both above `Faithful`'s 1.16–1.20 and 3.41–3.43, median
  2.6/2.7 uncensored. Full reasoning and the failed-build table are in `VarianceProfile.cs`.

---

# 🔒 MANDATORY ARCHITECTURAL RULES

1. **Mean-power envelope (±35%)** — every preset MUST stay within ±35% of `Faithful` **at every
   batch size** (N = 1, 5, 25, 50), not only at Best-of-1. "Mean-power" is a legacy name for the
   scope limit: the enforcing metric is now dispersion-aware (see §0), so the rule also bounds the
   part of a preset's power that comes from spread, not literally just its mean. It still does not
   constrain dispersion *as its own axis* — there is no Rule 1 equivalent for spread, see "The
   dispersion axis" below.
   > **`Wildcard` breached this once, at −20.8%/+22.3% under the old mean-only metric measuring
   > +49.0% at N=50 with dispersion accounted for — the gate could not see the breach because it
   > read a metric blind to the axis that caused it.** Fixed by making the metric dispersion-aware
   > and retuning the preset against it (§0). Do not weaken the rule to accommodate a variance
   > preset if this recurs on a future profile — retune the preset instead.
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
3. `composite = (0.8·skillNorm + 1.5·passionNorm) / 2.3`, where `skillNorm = (5 + skillShift)/20`
   and `passionNorm` is the passion axis below. The weights are
   `Constants.CompositeSkillWeight` / `CompositePassionWeight` and the divisor is their sum — **do
   not hardcode either here again.** This line read `1.4 / 2.2` for a while after `wP` moved to
   `1.5`, which is exactly the stale-normalizer shape described under "Unit errors survive review".
4. Compare each profile to `Faithful` **at the same N**.

## The passion axis

`passionNorm = E[ min(spend(budget), capacity) ] / MaxPassionPips · PassionPipEfficiency(majorBias)`.

Three terms, each with a reason:

**The spend loop** — `spend(budget)` is how many pips a pawn *actually receives*, which is not the
budget. `PassionVarianceApplier` buys whole passions at 1.5 / 1.0 pips until it cannot afford a
Minor and **discards the remainder**, so a 4.8-pip budget delivers 4. Every model side scored the
continuous budget until 2026-08-09, i.e. scored a pawn richer than any that gets rolled — `Faithful`
assumed `5.001` and delivers `4.551`, which is the same ~0.45-pip gap the 1000-pawn dump recorded as
`4.59` (audit finding Q-14). The loss is near-identical on every profile, so most of it cancels in
the ratio to `Faithful`; what does *not* cancel is signed by tier, pushing each preset further from
`Faithful` in the direction it already sits.

`Source/PassionSpend.cs` computes the loop's outcome distribution **exactly** rather than sampling
it — every branch tests the remaining budget against one of the two costs, so the distribution is
constant between consecutive thresholds and a table indexed by threshold is the answer, not an
approximation of it. Delivered pips always lie in `(budget − 1, budget]`, so at most three outcomes
carry mass and the capacity cap can be applied per outcome, which keeps the *variance* exact too.
**Order matters: spend first, cap second** — the generator spends the whole budget into whole
passions and only discards the surplus when it runs out of eligible skills.

Mirrored in `make_spend` (`envelope_check.py`) and executed for real, sampled, in `dispersion_mc.py`
— which is the only place the loop actually runs, and therefore the only thing that can catch the
table being wrong. **Change one, change all three.**

**Capacity cap** — `skills × (MinorCost + (MajorCost − MinorCost) · bias)` = 12 / 15 / 18 pips at
bias 0 / 0.5 / 1. A *low* Major bias saturates *early* (12 Minors fill all 12 skills for 12 pips),
which is the opposite of what the old formula assumed. **The cap binds no shipped preset** — the
widest is `Wildcard` at 10.5 against a 14.1 capacity (bias moved 0.6 → 0.35 on 2026-08-07, which
also moved the capacity from 15.6) — so it changes nothing today and is correct for custom profiles,
which can reach 18.

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

## A disabled axis scores as VANILLA, at full weight — it is not dropped

`enableSkillVariance` / `enablePassionVariance` are per-profile checkboxes. When one is off the mod
leaves that axis alone, so **the pawn keeps vanilla's skills or vanilla's passions — it does not
have none.** The composite therefore substitutes vanilla's own value on that axis and **keeps the
weight**:

| Axis off | Contributes | Value |
|---|---|---|
| Skills | `AssumedVanillaSkillBaseline / AssumedMaxSkillLevel` | 0.2500 |
| Passions | `VanillaPassionBudget × PassionPipEfficiency(VanillaMajorBias) / MaxPassionPips` | 0.2609 |

In the dispersion model the same value is used **with zero variance** — a disabled axis is a
constant, so it contributes nothing to σ.

> [!CAUTION]
> **Do not "simplify" this by zeroing the weight of a disabled axis.** That is what the code did
> until 2026-08-09 (`wS = v.enableSkillVariance ? CompositeSkillWeight : 0f`), and it made both
> fallbacks above dead code — `VanillaPassionBudget` and `VanillaMajorBias` had no live consumer at
> all. It also rescales the composite so the surviving axis is 100% of it, while the result is still
> divided by the same `Faithful` baseline: two scales, one reference.
>
> **The invariant that catches this:** with both axes off the mod changes nothing, so the score must
> be exactly vanilla's own, i.e. the **mean-band** composite of the vanilla-like profile:
> `(0.8 × 0.25 + 1.5 × 0.251087) / 2.3 = 0.250709`. The old code returned `q` there. `Faithful` must
> also score `0.250709` in **all four** flag combinations, being the vanilla-mimicking preset. Both
> are checked by the in-game verify action.
>
> ⚠️ **This is no longer `FaithfulBaseline()`.** It was until 2026-08-09; that function now returns
> the **dispersion-aware** typical, `0.2422`, because the readout it feeds is dispersion-aware and
> was dividing one estimator by another (Q-03). Two different quantities, both correct: a disabled
> axis is a zero-variance constant, while `Faithful` with its axes on is a profile with real spread,
> and the composite is not linear across it. **Do not reconcile them.** See "Two baselines" below.
>
> The passion term is `0.251087`, not the `0.260870` this line carried until 2026-08-09, because
> vanilla's 5-pip budget is now spent through vanilla's own discretizing loop like every other
> budget (Q-14): `4.8125` pips delivered, not `5.0`. **Both sides of the invariant moved together,
> which is why it still holds exactly** — and that is the point, since a fix that discretized the
> live branch but not the fallback would have put the two on different scales and this equality is
> what would have caught it.
>
> **No preset exercises this** — all eight leave both flags `true`, so `envelope_check.py`'s 32/32
> and `EnvelopeFigures.g.cs` are blind to it by construction, and the toggle gate is a set of
> standalone invariants rather than a comparison against the table. Four sites must agree:
> `CalculateCompositeScore`, `DispersionModel.Moments`, `envelope_check.py`'s `make_composite` and
> its `grid_moments`.

## The skill ↔ passion exchange rate (`R`)

**`R(bias) = (20 / MaxPassionPips) · (wP / wS) · PassionPipEfficiency(bias)`
`= (20/18) · (1.5/0.8) · eff(bias)` skill levels per passion pip.**

> [!IMPORTANT]
> **`R` is a function, not a scalar, and every quoted figure is anchored at vanilla's 0.5 bias.**
> The pip-efficiency term made it bias-dependent; above the capacity cap it is also piecewise, since
> the marginal pip there is worth **zero**. The cap binds no shipped preset but is reachable on
> custom profiles — which is exactly where a live `R` would be used.
>
> | Major bias | 0.00 | 0.35 (`Wildcard`) | 0.50 (vanilla, `Faithful`) | 0.70 (`Sovereign`) | 1.00 |
> |---|---|---|---|---|---|
> | `R` | 1.77 | **1.91** | **1.96** | 2.01 | 2.08 |

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
dispersion model self-check (zero noise vs analytic): 4.10e-04
mean-band consistency (zero spread, pointwise): 2.78e-16  worst at Desperate @ q=0.900
generator/mirror checklist: 5 branches, 19 declarations, all present
wS=0.8  wP=1.5  pips/18  skill/20  K=8
Exchange rate R(bias) = (20/18) * (1.5/0.8) * eff(bias)
  R = 1.96 skill levels per passion pip at vanilla bias 0.5   (range 1.77 at bias 0 .. 2.08 at bias 1)
Faithful baseline @ q=0.50: 0.2422 readout (dispersion-aware), 0.2507 mean-band (both-axes-off invariant)

profile                     N=1                N=5               N=25               N=50
Faithful        0.2418   +0.0%     0.3041   +0.0%     0.3455   +0.0%     0.3595   +0.0% 
Distinct        0.2207   -8.7%     0.3117   +2.5%     0.3783   +9.5%     0.4015  +11.7%   (variance)
Wildcard        0.1936  -19.9%     0.3140   +3.2%     0.4089  +18.4%     0.4429  +23.2%   (variance)
Desperate       0.1884  -22.1%     0.2445  -19.6%     0.2840  -17.8%     0.2978  -17.2% 
Elite           0.2954  +22.2%     0.3541  +16.4%     0.3931  +13.8%     0.4065  +13.1% 
Sovereign       0.3053  +26.3%     0.3651  +20.1%     0.4046  +17.1%     0.4181  +16.3% 
Specialist      0.2644   +9.3%     0.3261   +7.2%     0.3673   +6.3%     0.3813   +6.1% 
Scavenger       0.2077  -14.1%     0.2661  -12.5%     0.3062  -11.4%     0.3200  -11.0% 

Rule 2 - power-tier ordering at the same N:
  N=1   Desperate(0.188) < Scavenger(0.208) < Faithful(0.242) < Specialist(0.264) < Elite(0.295) < Sovereign(0.305)   OK
  N=5   Desperate(0.245) < Scavenger(0.266) < Faithful(0.304) < Specialist(0.326) < Elite(0.354) < Sovereign(0.365)   OK
  N=25  Desperate(0.284) < Scavenger(0.306) < Faithful(0.345) < Specialist(0.367) < Elite(0.393) < Sovereign(0.405)   OK
  N=50  Desperate(0.298) < Scavenger(0.320) < Faithful(0.360) < Specialist(0.381) < Elite(0.406) < Sovereign(0.418)   OK

Tightest envelope margins:
  Sovereign @ N=1: +26.3%  (8.7pp of headroom)
  Wildcard @ N=50: +23.2%  (11.8pp of headroom)
  Elite @ N=1: +22.2%  (12.8pp of headroom)

Within-pawn dispersion (REPORTED, NOT ENFORCED -- invisible to every % above):
  profile     skillSpread   per-skill sd  vs Faithful passionSpread   budget sd
  Faithful           0.50        0.50 lv        1.00x          1.00     1.00 pips
  Distinct           0.86        0.86 lv        1.71x          1.40     1.40 pips
  Wildcard           2.08        2.08 lv        4.16x          2.60     2.60 pips
  Desperate          0.61        0.61 lv        1.22x          1.00     1.00 pips
  Elite              0.54        0.54 lv        1.08x          1.00     1.00 pips
  Sovereign          0.59        0.59 lv        1.18x          1.00     1.00 pips
  Specialist         0.61        0.61 lv        1.22x          1.00     1.00 pips
  Scavenger          0.61        0.61 lv        1.22x          1.00     1.00 pips
  A profile can be flat in the table above and 3x wider here. Wildcard is exactly
  that case: its 2026-08-04 retune narrowed skillShift (the mean band), not skillSpread.
```

## The integration slip is GONE — do not go looking for it

**What `N` is.** The player does not keep the first pawn they are offered — they reroll starts, pick
from quest pawns, accept or refuse captures. So `N` is simply **how many pawns were looked at before
one was kept**. `N=1` is "took the first". `N=25` is "looked at 25, kept the best". Nothing more.

**What the slip was, and why this section is now a tombstone.** Both integrators used to accumulate
a running CDF with `run += v * dq` *before* appending, counting each slice half a slice too big;
the two sides ran at different resolutions (a 20000-node reference against a 1024-node integrator)
and the gap reached ~0.9% at `N=50`. It was quantified, argued out, and deliberately carried.

**The dispersion-aware rewrite retired all of it, and this document was the last place still saying
otherwise.** As it stands now:

- `CalculateBestOfNScoreCore` is a one-line delegate to `DispersionModel.BestOfN`, whose `BuildCdf`
  forms each `F[j]` as a **direct weighted sum** over the q-nodes with renormalised weights. A grep
  for `run +=` across `Source/` returns nothing.
- The Python `run +=` survives only in `beta_grid`, which now feeds the tool's **zero-noise analytic
  self-check** — not the reference `Scores`, which come from `make_grid_score`.
- **The two sides no longer differ in resolution at all**: `QNodes`/`XNodes`/`TriNodes`/`GaussNodes`
  = 256/512/65/65 in `DispersionModel`, and `QGRID`/`XGRID`/`TGRID`/`GGRID` are the same four
  numbers in `envelope_check.py`. `GRID = 20000` and `Constants.BestOfNIntegrationNodes = 1024`
  belong to the retired scheme; the latter is dead and annotated as such at its definition.

So the expected raw disagreement between tool and mod is **float-precision-scale** (float32 vs
float64, plus `MathUtil.NormalCdf`'s ~1.5e-7 `Erf`), not ~0.9%. This is audit finding **Q-09** in
[`docs/AUDIT-2026-08-09-problem-register.md`](docs/AUDIT-2026-08-09-problem-register.md), and both
`DebugActions.cs`'s tolerance note and `EnvelopeFigures.g.cs`'s generated header now state it.

**Both loose ends the retirement left behind are now closed, and closed on measurements:**

- **The raw tolerance is `0.1%`, down from `3%`.** The `3%` was sized for the retired slip and then
  held on the explicit condition that the post-rewrite gap was *predicted, not measured*. That
  condition was met — the gate ran 32/32 against the shipped build with a worst raw deviation of
  **0.01%** — so the number was simply stale. `0.1%` is 10× the observed worst and ~30× tighter than
  what it replaces. **Do not push it below ~0.05%:** C# `float32` against Python `float64` means
  some daylight is structural. If it ever trips, ask whether the two integrators diverged in
  *method* before reaching for a wider number.
- **The shared grid is asserted, not assumed.** `VerifyBestOfN` now compares
  `DispersionModel.QNodes/XNodes` against `EnvelopeFigures.ReferenceQNodes/ReferenceXNodes` and
  fails on any mismatch, naming the remedy (re-run the tool). Retuning the node counts on one side
  only used to reintroduce Q-09's resolution gap silently; it now cannot.

**Both were then verified in game, and the teeth test justified the assertion better than the
argument for it did.**

- **Under the tightened guard the gate still passes 32/32.** Header reads
  `dispersion grid 256q x 512x on both sides; readout tolerance 0.50pp, raw 0.10%`; worst raw is
  `Desperate @ N=50, 0.01%` and every other row is `0.00%`. The `0.01%` figure is now measured
  *under* `0.1%`, not carried over from the `3%` era.
- **Teeth test: `DispersionModel.QNodes` 256 → 255, rebuilt, redeployed.** The gate logged
  `GRID MISMATCH: DispersionModel.QNodes is 255 but the reference figures were integrated at 256`
  and `FAIL: 1 mismatch(es)` at error level. Restored to 256 and re-run: `PASS`, output
  bit-identical to the pre-test run.

> [!IMPORTANT]
> **The one-node drift stayed INSIDE both numeric tolerances.** With the grid off by a single node,
> the worst raw deviation was **0.03%** (`Elite @ N=25`) against the `0.1%` guard, and the worst
> displayed divergence was **0.04pp** against the `0.50pp` guard. **Every row passed both
> thresholds.** The only thing that caught a desynchronised quadrature was the grid assertion
> itself — tightening the raw tolerance 30× would *not* have been enough, and neither would
> tightening it further, since the drift is well inside the structural float32/float64 floor. This
> is the concrete case for why the check is an equality on the inputs rather than a threshold on
> the outputs.

> [!NOTE]
> The teeth test also exposed a real defect **introduced by the assertion itself**: the
> `^^ Constants.cs has moved since the reference was generated.` advisory keyed off the shared
> `failures` counter, so a grid mismatch was reported as constant drift — pointing the next reader
> at the wrong file. Fixed by snapshotting the counter before the constants block
> (`failuresBeforeConstants`) so the advisory fires only for failures that block produced, and so
> any check inserted there later cannot recreate it. Confirmed absent on the final passing run.

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
> - **`skillSpread`, `passionSpread`** — now scoring inputs, not just reported dispersion. This is
>   the single most important line in this list: they used to be exempt precisely because the
>   composite could not read them, which is the defect the dispersion-aware scoring work fixed.
>   Changing either one now moves every figure, and omitting them from this list would recreate the
>   same silent-staleness trap Rule 7 was corrected to close.
>
> The tool **parses `Source/Constants.cs` and `Source/VarianceProfile.cs` directly** rather than
> hardcoding values, so it cannot drift from what ships. Deterministic integration, not sampling.
> No third-party dependencies. It exits non-zero on a Rule 1 or Rule 2 violation, so it can gate a
> commit, and it regenerates `Source/EnvelopeFigures.g.cs` (checked in, auto-generated, **never**
> hand-edited). If `git status` shows that file dirty after a run, the shipped figures were stale —
> commit it.
>
> **Why this matters more than it looks:** the tightest preset has **8.7pp** of headroom
> (`Sovereign` @ N=1 — `Wildcard` @ N=50 is second-tightest at 11.8pp). A change that *feels*
> cosmetic — nudging one preset's `averageQuality` by 0.02, or "tidying" a normalizer — can breach
> the envelope without touching the preset that breaks, because the weights are shared.

## The dispersion axis — `skillSpread` and `passionSpread` — and what the model still cannot see

**The envelope is now dispersion-aware.** Both `CalculateCompositeScore`/`DispersionModel` and
`envelope_check.py`'s `grid_moments` treat the composite as `Normal(μ(q), σ(q))` conditional on the
Beta-distributed quality roll `q`, where `σ(q)` is built from `skillSpread` and `passionSpread`, and
integrate that mixture into Best-of-N. Every percentage in the envelope table now responds to both
fields — this used to be false and was the whole reason `Wildcard` breached Rule 1 invisibly; see
§0 above for that history.

> [!CAUTION]
> **The vanilla passion floor is written in FOUR places and they must stay identical.**
> `PassionVarianceApplier` (the generator), `DispersionModel.Moments`, `envelope_check.py`'s
> `grid_moments`, and `dispersion_mc.py`'s `simulate`. The condition is
> `budget < 1 && passionCountMin > 0` — the generator adds `alreadyCommittedPips <= 0` because
> the growth-up path tops up a pawn who already has passions; the three models are
> generation-only and do not need that term.
>
> **The three model sides once gated it behind `spread > 0` and the generator did not.** All
> three agreed with each other, so the in-game gate stayed green while every one of them
> disagreed with the pawns actually being rolled. A custom profile at `passionCountMin/Max = 0.5`
> with `passionSpread = 0` — two slider moves, both values inside the shipped slider bounds —
> scored a budget of `0.5` against a delivered `1.0`, worth **6.6pp** on the readout, or ~13× the
> 0.5pp tolerance the gate is built to catch. No shipped preset reached it (all have
> `passionCountMin ≥ 2.2`), which is why `EnvelopeFigures.g.cs` was byte-unchanged by the fix.
>
> **This is the §0 failure recurring in miniature: "both implementations agree" is not "the
> metric is right."** The gate can only ever prove the model sides consistent with each other.
> Anything the *generator* does that no model mirrors is invisible to it. When touching any
> budget-shaping branch, diff all four sites by hand.
>
> **Verified in game 2026-08-08.** `Verify Best-of-N` 32/32 PASS, worst shown 0.01pp — the fix
> disturbs no shipped preset, as predicted. The fix itself was checked on the one configuration
> that reaches the branch: a custom profile at `passionCountMin/Max = 0.5`, `passionSpread = 0`
> reads **`Typical −53% (0.12)` / `Best of 25 −57% (0.15)`**, matching the model exactly. The
> pre-fix build read **−60%** at the same config. **Note that 32/32 does NOT test this fix** — no
> shipped preset has `passionCountMin < 1`, so the custom profile is the only instrument that
> does.

**The mechanism worth keeping in mind when reading the numbers, because it still shapes them:**
`skillSpread` drives the per-skill excursion in `SkillVarianceApplier.Shift` —
`magnitude = Lerp(0, 6, SkillNoiseScalar)`, so up to **±6 levels per skill**
(`Constants.MaxMagnitude`) — but it is drawn **12 times independently, once per skill**, and the
pawn's Best-of-N-relevant quantity is the *average* over those 12 draws. Averaging divides the
variance by 12, so per-skill spread reaches the composite diluted by `√12` — a `skillSpread` swing
that looks dramatic per-skill barely moves the pawn's aggregate. `passionSpread` is a single
per-pawn draw with no such averaging, so it reaches Best-of-N in full. **This is why the Wildcard
retune spent its budget on `passionSpread`, not `skillSpread`** — moving `skillSpread` to 0 shifts
N=50 by only 0.3pp.

**Do not read dispersion out of the mean band.** `skillShiftMin/Max` set where a profile's *average*
skill sits; `skillSpread` sets how far an individual pawn strays from that average. "Narrowing a
profile's dispersion" by narrowing `skillShift` alone changes the mean band, not the noise — and,
as "Left-censoring DESTROYS dispersion" below documents in detail, narrowing the band from the wrong
side (the floor, against the `Clamp(0,20)` wall) can *reduce* realised spread while every offline
number suggests the opposite.

**Reported, not enforced.** `envelope_check.py`'s dispersion table (per-skill sd, budget sd) is
diagnostic, not a Rule 1 axis — there is no percentage-envelope equivalent for spread and none is
wanted. Observed (as opposed to derived) dispersion comes from the `Roll pawns and dump distribution`
debug action, which is the only place censoring against the `Clamp(0,20)` floor is visible at all.

### What the model cannot see — three limits, read before trusting a custom profile

1. **The `Normal(μ(q), σ(q))` approximation degrades at high `passionSpread`.** Validated against
   `docs/tools/dispersion_mc.py`'s independent Monte Carlo: **0.08pp** of drift at the shipped
   `Wildcard` value, but **1.5pp** at `PassionNoiseScalar` 0.85 (Wildcard's pre-retune spread,
   before `passionSpread` moved 3.4 → 2.0 pips) — heavy `±4σ` clamping and floor-forcing pull the
   real distribution away from a clean Gaussian. **A custom profile with a very high `passionSpread`
   will drift from what pawns actually roll while `Verify Best-of-N` stays green**, because the
   in-game gate only checks that the C# quadrature agrees with the Python quadrature — both share
   the identical approximation, so they agree with each other to ~0.000pp while both sit up to
   ~1.5pp from reality at the extreme.
2. **The Monte Carlo is an independent numerical *method*, not independent *verification*.**
   `dispersion_mc.py`, `DispersionModel.Moments` and `grid_moments` all substitute a flat
   `Constants.AssumedVanillaSkillBaseline` for every skill's real vanilla level rather than each
   skill's actual generated value — a shared modeling assumption, not a coincidence of
   implementation. Agreement between the Monte Carlo and the quadrature rules out an *integration*
   bug; it proves nothing about whether the flat-baseline assumption itself is accurate.
3. **`DispersionModel.OutcomeDensity` (the header curve) has no Python counterpart at all**, unlike
   `Moments` and `BestOfN`, which are both mirrored in `envelope_check.py` and cross-checked by the
   in-game gate. It is UI-only — nothing consumes it outside `ProfileEditorTab.cs` — but that also
   means nothing cross-checks it. It sits outside the "two implementations of one integral" contract
   the 0.5pp gate enforces; a bug there would not be caught by any existing verification.

**The scope limit is stated to the player too.** The Row 3 power readout's tooltip says the figure
is *"Based on starting skill levels and the passion budget only. It does not include traits, and it
does not show how much pawns differ from each other, so two profiles with the same figure can still
play very differently."* That second sentence is load-bearing: without it a player reads `Distinct`'s
−10% as "weaker" and picks against the profile for the exact reason it exists. **If you reword that
tooltip, keep the exclusion clause.**

## Trait count is NOT a quality axis — more traits is *worse*

**Quality sets how many traits a pawn gets; it never influences which traits — selection is
vanilla's, and it is quality-blind.** Trait *selection* is delegated entirely to
`PawnGenerator.GenerateTraitsFor`, which never sees `q`. Scaling trait count with quality does not buy better traits — it buys **more
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

**Asked 2026-08-08 — "if traits don't correlate with quality, why is count derived from quality?"**
Answered: they are consistent once *count* and *selection* are kept apart, so only the wording was
fixed (above, and at "HOW A PAWN IS ACTUALLY ROLLED"). The mechanism at
`TraitVarianceApplier.cs:43-48` stays. The real tension — `q` is a positive dial everywhere except
traits, where extra draws quietly raise hazard exposure — is the inversion already mitigated by
removing the trait term from the composite and narrowing preset spreads. The one untried alternative
is **decoupling count from `q`** (roll it from its own independent draw in `[min, max]`): ~3 lines,
needs no envelope re-tune since the composite doesn't read trait count, and kills the inversion at
any spread. **Rejected** because it costs the single-roll design — trait count becomes pure noise,
unconnected to the rest of the pawn — and it would shift the observable distribution of every
existing profile, for a defect already measured as small. Reopen only if the one-dial property is
being dropped for other reasons anyway.

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

**Quality is rolled ONCE per pawn** (`HarmonyPatches.cs:55`) and handed to all three appliers. That
is what makes quality a coherent per-pawn property rather than three unrelated numbers — a
high-quality pawn is shifted up in skills *and* passions *and* trait **count** together. Note the
last word: **quality sets how many traits a pawn gets; it never influences which traits — selection
is vanilla's, and it is quality-blind.** More traits is not better traits (see "Trait count is NOT a
quality axis"). **Do not "improve" the single roll into a per-axis roll.**

| Quantity | Rolled |
|---|---|
| Quality | **once per pawn** |
| Skill baseline | once per pawn, `Lerp(shiftMin, shiftMax, q)` |
| Skill noise | **once per SKILL** — 12 draws, inside the loop in `SkillVarianceApplier.Shift` |
| Passion budget | once per pawn |
| Trait count jitter | once per pawn, ±0.25 |

### The mod displaces vanilla's roll, it does not author the pawn

`SkillVarianceApplier.cs:73` is `RoundToInt(record.levelInt + shift)` — the shift is applied **on top
of** whatever vanilla generated from backstory, age and `PawnKindDef`. Consequence: **two pawns at
identical quality are still completely different pawns.** Even with true-zero noise they would
differ; the shift moves the whole pawn up or down, it does not decide what the pawn is. This is the
correct mental model for the whole mod, and it is easy to lose when reading the envelope maths,
which talks only about shifts and budgets.

### The growth moment rolls a FRESH quality

`GrowUpVariance.cs:92` calls `RollQuality` again. A pawn generated at `q = 0.20` can grow up at
`q = 0.85`. The two rolls are independent and nothing carries over — a child is **not** "the same
pawn's quality, re-applied." Deliberate, but it means growth-moment outcomes cannot be predicted
from the pawn's original generation.

### The noise sliders now mean literally zero at zero

`MagnitudeLerpLow` (named `MinMagnitudeFloor` until P-11 renamed it — the old name greps to nothing)
and `PassionBudgetSpreadMin` used to be **floors, not zeros** (0.5 and 0.25), so
a slider reading `0.00` still delivered ±0.5 levels per skill and still varied the passion budget
enough to change how many passions a pawn got. Both are `0f` now.

> [!CAUTION]
> **Both constants are Lerp low endpoints**, so moving them rescaled magnitude at *every* noise
> setting, not just at zero — proportionally hardest at the quiet end, where every preset except
> Wildcard lives:
>
> The left column is the **pre-rename 0–1 scalar**, which is what the constants were lerped against
> at the time; today's field is `skillSpread` in real skill-level units and the scalar survives as
> the derived `SkillNoiseScalar`. The figures are quoted unconverted so they match the retune they
> record.
>
> | `skillNoise` (pre-rename scalar) | magnitude before | after | change |
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
   `skillSpread` 0.49 lv → **49% at 1.22 lv** → **70% at 2.45 lv**, split evenly between the
   handles (measured on the old 0–1 scalars at 0.20/0.50/1.00, converted here by `×√6`). Past
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
outcome, not a safety hole, and the levers are `skillShiftMin` and `skillSpread`. If pinning is
judged too aggressive, **narrow the band or the noise — do not add a clamp.**

## ⚠️ Left-censoring: a band below the floor DESTROYS dispersion, it does not create it

**Measured 2026-08-07**, `Roll pawns and dump distribution` at 1000 pawns, active profile only, no
overrides in play. This is the single most important empirical fact about the skill axis and it is
the opposite of what the preset names imply.

| | `Faithful` | `Wildcard` |
|---|---|---|
| per-skill level, mean | 3.37 | **1.42** |
| per-skill level, **median** | 3.0 | **0.0** |
| per-skill level, p90 | 8.0 | 5.0 |
| **per-pawn mean skill, sd** | **1.23** | **1.10** |
| passion pips, mean / sd | 3.59 / 1.24 | 4.34 / 2.96 |
| traits/pawn | 2.51 (2–3) | 2.89 (0–7) |

**`Wildcard`'s per-pawn skill dispersion is NARROWER than `Faithful`'s.** The preset whose entire
purpose is maximum variation produces *less* pawn-to-pawn variation in skills than the vanilla-like
baseline. Its median skill is **0**, and 506 of 1000 pawns averaged under 0.6 across all twelve
skills.

**The mechanism.** `skillShiftMin = -8.7` is applied on top of vanilla's own levels, which average
~3.4 (read `Faithful`'s column — it barely shifts anything, so it is effectively a vanilla readout).
Most rolls therefore land below zero, and `Shift` ends with `Mathf.Clamp(newLevel, 0, 20)`. Every one
of those rolls stacks onto the same floor. **You cannot have spread below a wall you are already
pressed against** — the clamp converts the entire lower half of the distribution into a point mass at
0, which is the exact "spread becomes a spike" failure documented for the rejected clamp proposal,
arriving through the band instead of through a new clamp.

> [!CAUTION]
> **The envelope cannot see this and never will.** `CalculateCompositeScore` reads
> `Lerp(skillShiftMin, skillShiftMax, q)` — the *mean band* — and `envelope_check.py` computes the
> same. Neither models the per-pawn `Clamp(0, 20)`, because neither generates a pawn. `Wildcard`
> passes Rule 1 comfortably at −20.8% while its actual population is censored. **A preset can be
> inside the envelope, inside the dispersion table, and still be broken in the only place it
> matters.** The dispersion table's `per-skill sd` column is derived from `skillSpread` alone and is
> equally blind — it reports `2.08 lv` for `Wildcard`, four times `Faithful`, which is true of the
> *intended* noise and false of the delivered population once the band censors it.

### The dump action now MEASURES the censoring — read this before eyeballing the median

`Roll pawns and dump distribution` prints a `CLAMP CENSORING` block, one row per **resolved**
profile (same grouping as the `GENERATOR vs MODEL` blocks, so a mixed sample gives one reading per
profile instead of a pooled figure that averages a censored band into a healthy one):

```
  CLAMP CENSORING against Shift's Clamp(0, 20), capable skills only:
    Faithful       at 0:   1999/10938 ( 18.3%)   at 20:      1/10938 (  0.0%)   median  3.0
```

**Baselines, measured at 1000 pawns each. A high pin rate is NOT by itself a defect:**

| profile | at 0 | at 20 | per-skill median |
|---|---|---|---|
| `Faithful` (the reference) | **18.3%** | 0.0% (1 skill) | 3.0 |
| `Custom 1` | 16.7% | 0.0% | 3.0 |
| `Wildcard` (shipped band) | **33.3%** | 0.0% (3 skills) | 2.0 |

Reproduced on the **Release** build (2026-08-13, staging from `7b3f401`, 200 pawns): `Faithful`
`at 0: 376/2200 (17.1%)`, `at 20: 1/2200 (0.0%)`, median `3.0` — the smaller sample's 17.1% against
the 18.3% baseline is sampling noise, and the readout behaves identically in the shipping binary.

**`Faithful` itself pins 18.3%, and the shipped `Wildcard` pins a third of all capable skills.**
Both are healthy. That is precisely why the alarm fires on **median 0**, the criterion rule 3 below
already commits to, and not on a percentage — any threshold low enough to look alarming would fire
on the reference preset. The percentages are reported as data for comparison, not as a gate.

Two properties of the readout that are load-bearing:

- **Capable skills only.** `SkillRecord.GetLevel` returns `0` for a `TotallyDisabled` skill and
  backstory incapability costs ~1.0–1.2 of 12 skills per pawn, so counting those would put a
  permanent ~13% floor on *every* profile — a warning that reads identically on a healthy band and a
  broken one. The denominator is the capable count and should track the run's own
  `skills disabled/pawn` line (`12000 − 10938 = 1062` against `mean 1.06`).
- **Raw learned level, aptitudes excluded** — that is exactly what `Shift` clamps, since
  `record.Level` writes `levelInt` while Biotech aptitude is added afterwards by the getter. So the
  figure reports *this mod's* censoring, not a gene's.

**Why this lives in the debug action and not in the profile editor.** An analytic pin-rate readout
would be a fifth model site, blind for the same reason the other four are — see the CAUTION above:
no model here reads a rolled, clamped pawn. It would also have no Python mirror, putting it in
`DispersionModel.OutcomeDensity`'s position (nothing cross-checks it). **Counting real pawns is the
only instrument that can see censoring at all.**

**Rules that follow — apply these before moving any `skillShiftMin`:**

1. **Keep `skillShiftMin` above roughly `−4`** unless censoring is the deliberate goal. Vanilla's
   base is ~3.4, so a band floor much past that guarantees a pile at zero rather than a wide preset.
2. **Widening a band downward past the floor REDUCES dispersion.** It looks like more variance in
   every number this project computes offline and delivers less in game. If the goal is spread, the
   levers are `skillSpread` and the *upper* handle.
3. **Never judge a skill band from `envelope_check.py` alone.** Run
   `Roll pawns and dump distribution` and read the **median** and the per-pawn sd, not the mean. A
   median of 0 means the band is under the floor.
4. This is a property of the *band*, not of noise. `Wildcard`'s `skillSpread` is not the cause and
   narrowing it would not fix it.

### The fix, and the trap that recurred through the CEILING this time

`skillShiftMin` first went `−8.7 → −5.0` (ceiling still `4.2`), which cleared most but not all of the
censoring — median rose from `0.0` to `1.0`, but per-pawn skill sd read `1.21`–`1.31` against
`Faithful`'s `1.19`–`1.23` across two 1000-pawn runs, comparable at best, not decisively wider. The
section's own rule 3 was the tool that caught it: read the median and the per-pawn sd, not the
envelope.

**The trap then recurred, even with this section already written, and arrived through the ceiling,
not the floor.** The dispersion-aware scoring work's own first retune proposal for `Wildcard` — part
of tightening the preset back inside the ±35% envelope once the metric could see dispersion — lowered
`skillShiftMax` (the *ceiling*) rather than the floor. **That failed in game for exactly this
section's reason, from the other wall**: dropping the ceiling while the floor still sat at `−5.0`
narrowed the band enough that most rolls landed near zero again, and per-skill median and p10 both
returned to `0.0`. A second, less aggressive ceiling change was tried and measured *worse*, not
better — narrowing a band removes quality-driven pawn-to-pawn spread faster than raising the floor
alone restores it, so the floor and ceiling both have to move together. Only the band that raised
**both** the floor (to `−4.0`) and the ceiling (kept at `4.2`, i.e. widened relative to the failed
proposals) cleared the censoring while landing wider than `Faithful` on both the per-skill and
per-pawn spread measures — the only one of three measured bands to do so.

**The full three-band comparison table, the exact figures, and the reasoning are on the `WildSpread`
preset in `Source/VarianceProfile.cs` — read it before touching this band again rather than
re-deriving it or trusting a summary, including this one.** In outline: the shipped band is
`skillShiftMin = −4.0`, `skillShiftMax = 4.2`, measured at 1000 pawns with per-skill median `2.0`,
per-skill sd `3.51` (vs `Faithful`'s `3.41`–`3.43`, now genuinely above), per-pawn mean-skill sd
`1.30` (vs `Faithful`'s `1.19`–`1.23`, also genuinely above). It bought that with envelope headroom —
`Wildcard` was then the **single tightest preset** in the mod at 8.5pp, ahead of `Sovereign`'s 8.7pp;
**the 2026-08-12 retune undid that, and Wildcard now sits at 11.8pp with the band unchanged** —
and it still sits below `Faithful` at N=1 (now `−19.9%`), preserving the documented crossing property,
because the passion axis was retuned in the same pass (`passionSpread` 3.4 → 2.0 pips,
`passionMajorBias` 0.6 → 0.35) rather than the skill band alone having to carry that constraint.

**Do not "fix" this by lowering `skillShiftMin` again**, and do not assume narrowing the ceiling is
safe just because it looks like the smaller move — that assumption is exactly what failed. **Neither
`envelope_check.py` nor the dispersion table can see censoring.** If you move this band, dump 1000
pawns and read the median and the per-pawn sd, on both the floor and the ceiling side.

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

Implementation note if 2 or 3 is ever revisited: `budget` is rolled at `PassionVarianceApplier.cs:64`
but `eligible` is not built until `:115`, so either needs a reorder. (Note `:42` is inside the
passion-wipe loop over `pawn.skills.skills` — reordering around *that* line edits the wipe, not the
budget roll.)

## Settled and not to be relitigated

| Question | Resolution |
|---|---|
| User-facing derivation write-up in the settings UI | **No.** If wanted, it belongs in the mod's About/description or `docs/`, not a tooltip. |
| Exposing the exchange rate `R` as a player setting | **Rejected.** A control that changes nothing (the score is display-only) while visibly breaking the ±35% envelope the mod advertises. |
| Making the Best-of-N integration midpoint-correct | **Rejected — carried permanently.** Both implementations share the slip so it cancels in every displayed figure, `N=1` is exact, and fixing it repastes every table for a difference no player can see. Argument in full under "Why the integration slip is carried". |
| **Collapsing `envelope_check.py` and the C# into one shared implementation** | **Rejected 2026-08-08 — the redundancy is load-bearing.** Plan written, costed and then shelved. See below. |
| **Whether the debug tools ship in the released DLL** | **They ship. Decided, not drifted into.** `DebugActions.cs` stays compiled into the release build, gated by `Prefs.DevMode` as it is today. Reasoning below. |
| **How much of this repo is public** | **All of it, as it stands.** `HANDOVER.md`, `TRAIT-DESIRABILITY-RESEARCH.md` and everything under `docs/` stay tracked and public. The internal record — including the full account of every defect this project shipped — is published deliberately. |
| **Translations for 1.0** | **Keys, not hardcoded English.** UI strings are extracted to a `Languages/English/Keyed/` file so a translator can contribute without a refactor. Not "English-only on purpose". |

### Why the debug tools ship

The exposure argument is not the interesting one — `Prefs.DevMode` already hides them from any
normal player, and DLL size is irrelevant. The reason they stay is that **this project's entire
defect history is bugs that only running the real assembly could see.** Four times: both Best-of-N
integrator bugs, the `Wildcard` band retune, and the sig-gated passion floor. Clean builds, a
passing `envelope_check.py` and multiple agent reviews were green for every one of them.

The debug tools *are* the instrument that caught them. If they compiled out of the release build,
the artifact a player runs would be one nobody can ever measure, and the artifact this project
verifies would be a different binary. That gap is the exact shape of every defect listed above,
which makes stripping them the one change most likely to reintroduce the failure mode.

The concrete payoff is the modpack case. When another mod's postfix wins on
`PawnGenerator.GeneratePawn`, the symptom is not an error — it is pawns that quietly look vanilla.
The only way to diagnose that from a bug report is `Roll pawns and dump distribution` running
inside the reporter's own load order. Ship the tools and the published build is self-diagnosing;
cut them and that report cannot be answered at all.

**If they are ever cut, they must go behind a compile flag, never by deleting the file** — a
hand-deleted file means the shipped DLL and the verified DLL are different artifacts, which is
worse than any reason for cutting them.

### Why the C#/Python duplication STAYS — do not "fix" it

It looks like an obvious smell: two implementations of one integral, kept in step by an
`IF YOU CHANGE ONE, CHANGE BOTH` comment and policed by a 0.5pp gate. A plan to collapse them into
one platform-free source folder compiled into both the mod and a C# harness was written in full
(`docs/superpowers/plans/2026-08-08-single-implementation-scoring.md`) and **rejected before any
code was written.** The reasoning, so nobody re-derives the wrong answer:

**Count what the mirror has actually done.** It has caught **two** real defects — both Best-of-N
integrator bugs, which "survived clean builds and static review" and were caught by the in-game
`Verify Best-of-N` action, i.e. by the C# disagreeing with the Python. It has caused **zero**.

**Now count the defects this project HAS shipped, and check which failure mode each one was:**

| Defect | Failure mode |
|---|---|
| Both Best-of-N integrator bugs | one side wrong → **the mirror caught them** |
| The sig-gated passion floor (`52602f7`) | all three model sides agreed, all disagreed with the generator |
| The `Wildcard` band retune | every offline instrument green, the realised population censored |
| The two "24-pip era" recurrences | correct arithmetic on the wrong vanilla scale |

**Every shipped defect except the integrator bugs is "both sides jointly wrong relative to the
game" — never "the two sides disagree."** Mirror drift is the one failure mode that has never
happened here. Collapsing to a single implementation would retire the defence with the proven
record in order to eliminate a risk that has never materialised.

**The corollary, which is the useful part:** effort aimed at correctness here should go at the
**model-vs-generator** gap — the thing that predicts versus the thing that rolls — not at the
C#-vs-Python gap. Nothing offline can close that gap, because the model never generates a pawn.
The cheap instrument is an in-game action that rolls N pawns and prints their realised mean
composite beside `DispersionModel`'s prediction. That is worth building; the refactor is not.

**What the shelved plan does still contain, if any of it is ever wanted separately:** the exact
`Mathf`-semantics trap table (`Mathf.Lerp` clamps `t`; `Mathf.RoundToInt` is banker's rounding;
`Mathf.Exp` and friends round back to `float` immediately). Anyone writing float code that has to
agree with Unity's should read that table.

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

**Only the race menu is filtered this way.** The faction and xenotype menus are straight
`DefDatabase<T>.AllDefs` sweeps (`PawnVarianceSettings.cs:1167-1171`) — sorted and label-disambiguated,
but not narrowed. They are still much shorter than a count of the defs installed on disk, because
`DefDatabase` holds only the defs of **mods active in the current run** and never abstract ones. That
is expected and was confirmed in game under the 1376-mod modpack (pre-publish item 6); it is not a
filter and there is nothing to fix.

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
- **NEVER begin a `Listing_Standard` with the rect you passed to `BeginScrollView`.** This is the
  single most expensive UI trap in the mod and it is not obvious from reading the code, because the
  broken form is the tidier-looking one.

  `Listing_Standard` **column-wraps** when its content passes the height it was begun with: it does
  not overflow downward, it starts a new column to the **right**. Passing `viewRect` to both
  `BeginScrollView` and `listing.Begin` therefore fails twice over, and the second failure is what
  makes it permanent:

  1. The overflow is drawn one column width outside the viewport — *beside* the scroll range, not
     below it — so no amount of scrolling reaches it.
  2. After a wrap, `listing.CurHeight` returns only the **last column's** height. Feed that back into
     `viewHeight` and the next frame is sized just as short, wraps again, and re-measures the same
     wrong number. **It never recovers** — not by adding rows, not by restoring defaults. Only a
     restart clears it, and then only because the field's initialiser happens to exceed the content.

  Measured on the Overrides tab with the shipped default overrides (10 faction + 10 xenotype): real
  content **1227px**, `viewRect` pinned at the old `1000f` floor, Race Overrides displaced to
  **x=837, y=92** in a second column, and `overridesViewHeight` latched at **251** (the last
  column's 211 + 40) across every subsequent frame.

  **The shape that is correct:** size the scroll content from the measured height, and begin the
  listing with a rect tall enough that it can never wrap
  (`PawnVarianceSettings.UnboundedListingHeight`, `100000f`, beside the vertical-rhythm constants).
  RimWorld itself begins a 9999-tall group in
  `Dialog_ModSettings`, so an oversized group rect is an accepted idiom — `Widgets.BeginGroup` only
  clips, and the enclosing scroll view is what bounds what the player sees. `DrawOverridesTab` is the
  worked example; copy it rather than re-deriving.

- **Scroll heights are floors, not caps — and the floor must be the viewport, never a constant.**
  Each tab recomputes `listing.CurHeight + 40f` every frame, so extra sections expand the view rather
  than clipping. **A hardcoded floor has to be guessed, and a wrong guess is what puts the listing
  into the wrapping regime above**: the Overrides tab's `1000f` sat *below* its real 1227px content.
  `Math.Max(measured, outRect.height)` is correct by construction — content shorter than the viewport
  simply does not scroll.
  > **The General and Profile Editor tabs still pass `viewRect` to `listing.Begin`.** They do not
  > trip this today only because their content (545 and 510) stays under their floors (600 and 580).
  > They are one added control away from the identical latch. Deliberately left alone; fix them the
  > moment either tab grows.
- **Best-of-25, not Best-of-50, and no `N` slider** — it is a lens, not a setting. At N=50 `Wildcard`
  displays near the envelope limit, and a UI that advertises how close a preset sits to the limit
  invites players to treat the limit as a target.
- **The distribution curve stays a SINGLE line.** A second, ghosted Best-of-N curve was considered
  and rejected — it doubles the ink for a quantity the two header anchors already state numerically.
- **If you touch `FormatPowerPercent`, the baseline must be measured at the same N as the score.**
  Comparing a Best-of-25 score against Faithful's N=1 baseline once put every figure ~36pp too high
  and flipped `Desperate`/`Scavenger` positive — inverting the exact fact the second anchor exists to
  convey.
- The Best-of-25 readout mirrors `envelope_check.py` on the shared `256/512/65/65` quadrature grid
  (**not** the retired 1024-node scheme this line used to name). **If you change one, change both** —
  and the `Verify Best-of-N` debug action now enforces that mechanically, by asserting the node
  counts are equal rather than by thresholding the outputs.

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
   - `TraitVarianceApplier.cs:76` — `GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false)`
   - `GrowUpVariance.cs:245` — `GenerateTraitsFor(pawn, requested, null, growthMomentTrait: true)`

   Two consequences that are easy to miss:
   - The growth-moment call passes **`request: null`**, so every vanilla check that reads the request
     is skipped — `kindDef.disallowedTraits`, `disallowedTraitsWithDegree`, `requiredWorkTags`,
     `ProhibitedTraits`, and the hostile-spawn `allowOnHostileSpawn` gate (verified in decompiled
     `PawnGenerator.GenerateTraitsFor`).
   - The growth-moment trait pass is **add-only by design** (`GrowUpVariance.cs:107-113`). **Anything
     granted at 13 is permanent**; no later pass revisits it.

5. **Age-13 growth-moment deferral pipeline** — children aging to 13 defer mod application while a
   `ChoiceLetter_GrowthMoment` is outstanding; the mod then applies strictly add-only trait/passion
   increments once the letter resolves. **The deferral holds no state** — see "The deferral is
   derived, not stored" below.

6. **Non-spam faction handling** — `Faction.OfPlayerSilentFail` instead of `Faction.OfPlayer` across
   call sites, to eliminate world-gen log errors.

## The deferral is DERIVED, not stored — and the mod writes nothing to the save

> [!IMPORTANT]
> **THE MOD HAS ZERO COLONY-SAVE FOOTPRINT, AND THAT IS AN INVARIANT, NOT AN ACCIDENT.**
> Do not add a `GameComponent`, `WorldComponent`, `MapComponent`, `ThingComp`, `HediffComp`, or any
> other mod-owned `IExposable` reachable from a saved object. `VarianceProfileValues` and
> `CustomProfile` are `IExposable` but hang off `PawnVarianceSettings : ModSettings`, which writes
> to `Config/Mod_PawnVarianceMod_PawnVarianceMod.xml` — **not** the save.

**Why the rule exists.** `Game.ExposeData` deep-saves the component list by concrete type name
(`Game.cs:443`, `Scribe_Collections.Look(ref components, "components", LookMode.Deep, this)`), and
`Game.FillComponents` populates that list by reflecting over
`typeof(GameComponent).AllSubclassesNonAbstract()`. **There is no opt-out** — no attribute, no flag,
no alternate registry. Owning one type therefore stamps its name into every save, and removing the
mod then logs two errors per load:

```
Could not find class PawnVarianceMod.GrowUpPendingComponent while resolving node li.
  Trying to use Verse.GameComponent instead. Full node: <li Class="..."><pendingGrowUpPawns /> ...
SaveableFromNode exception: System.ArgumentException: Can't load abstract class Verse.GameComponent
```

**Emptying the component's `ExposeData` does not help, and the old save proves it**: the node that
produced those two errors had `<pendingGrowUpPawns />` and `<pendingGrowUpSinceTicks />` both empty.
Zero data, same two errors. The trigger is the type existing, not what it holds. **The only fix is
not to own the type**, which is why `GrowUpPendingComponent` was deleted rather than slimmed.

**How the deferral works with no state.** The pending condition — *"this pawn has a growth-moment
letter still waiting"* — is recomputed from the letter stack by
`GrowUpVariance.HasUnresolvedGrowthLetter`. Vanilla scribes letters itself, so the condition survives
save/load for free. The old scribed list was only ever written when that method already returned
true (its single `Register` call site gated on it), i.e. it was a cache of a question the letter
stack can always answer. Three triggers reach `GrowUpVariance.Apply`, none of them stateful:

| Trigger | Site | Fires when |
|---|---|---|
| Life-stage change | `DevelopmentalStage_Postfix` | Pawn becomes Adult with **no** outstanding *adulthood* letter — applies immediately. With one outstanding, it just `return`s; that bare return **is** the deferral. |
| Letter resolved | `GrowthMomentMakeChoices_Postfix` | The player chose, **on the `ChildToAdult` letter**. Ages 7 and 10 are ignored. |
| Letter left the stack | `LetterStackRemoveLetter_Postfix` | The **`ChildToAdult`** letter timed out unresolved. Replaces the old 2500-tick `GameComponentTick` sweep. |

All three read "is this the adulthood letter?" from `GrowUpVariance.IsAdulthoodGrowthLetter`, never
by re-expressing the test locally — see the per-PAWN guard below.

> [!CAUTION]
> **Once-only is now a guard, not a structure — do not weaken it.** The deleted list gave
> apply-once for free, and it did so on **two** axes at once: one entry *per pawn*, consumed by
> `Deregister`. Replacing it takes **two independent guards**, and treating either as sufficient on
> its own is exactly how this broke once already.
>
> **Once per LETTER** — a **Harmony prefix/postfix pair** on `MakeChoices` that snapshots
> `choiceMade` and acts only on the false → true transition. That transition happens exactly once
> per letter (`MakeChoices` returns early on `ArchiveView`, which is true once `choiceMade` is set)
> and is scribed with the letter, so it survives save/load. The case it exists for is real: an
> archived letter re-opened from the History tab calls `MakeChoices` again, and reading `choiceMade`
> in the postfix alone cannot distinguish that from a genuine resolution.
>
> **Once per PAWN** — `GrowUpVariance.IsAdulthoodGrowthLetter`, which requires
> `letter.def == LetterDefOf.ChildToAdult`. A pawn has three growth letters in its life (ages 7, 10,
> 13) and only the last is an adulthood transition. See the section below for the vanilla source and
> the measured failure this fixes.
>
> `GrowUpVariance` is add-only and permanent, so a double application silently gives one pawn two
> full grow-up passes and cannot be undone in that save.
>
> `LetterStackRemoveLetter_Postfix` carries **both** guards too: it bails on `growth.choiceMade`
> (the removal that *follows* a normal choice would otherwise apply a second time) and on a
> non-adulthood letter def (a stale age-7 letter timing out after its pawn turned 13).

**The sweep is gone and does not need replacing.** Its stated main job was *"cleaning up a pawn that
died or was otherwise lost while pending"* — with no list, there is nothing to leak and a lost pawn
simply stops satisfying the derived condition. Only the timeout case remained, and that is an event,
so it gets an event hook instead of a poll.

**Verified in game against the deployed build:** a real colony round-tripped — the freshly written
save contains **no** `PawnVarianceMod` type at all (the components node runs straight from
`GameComponent_PsychicRitualManager` to `CombatExtended.*`; the only `kalas.pawnvariance` string left
is the `modIds` header every active mod appears in), and reloading that save produced zero errors.
A quick-test colony with `applyVarianceToChildren` on logged zero errors and zero warnings across
pawn generation, forced birthdays and a life-stage progression.

**Verified live 2026-08-11 (`Dump growth-moment state`, quick-test colony).** A colonist spawned at
age 3 and walked to 13 with `T: Force Birthday` crossed Child → Adult **with a letter outstanding**
and correctly deferred — `WOULD DEFER (letter outstanding)`, nothing applied, nothing stored. That is
the exact branch that used to call `Register`. The derived predicate answered correctly at ages 7,
12 and 13.

> [!IMPORTANT]
> ## 🟢 FIXED AND RE-MEASURED IN GAME — once-only per-PAWN
>
> **The defect.** The deleted design had this right and its first replacement did not. The pending
> list held one entry *per pawn*; the first `Deregister` consumed it, so any further growth letters
> for that pawn found nothing and did nothing. The `choiceMade` false → true guard is *per letter*,
> so **N unresolved letters for one pawn produced N calls to `GrowUpVariance.Apply`.**
>
> **Measured, not theorised.** A pawn reaching 13 with its age-7 and age-10 letters never clicked
> has three letters queued. Resolving all three logged three separate
> `Growth moment resolved for Svejgaard` lines (03:39:58, 03:40:01, 03:40:05), each rolling a fresh
> quality — `0.68`, `0.37`, `0.44`.
>
> **Damage in that run was zero, and understanding why is the whole point.** Both appliers are
> top-up passes that recompute against the pawn's *current* state, so passes 2 and 3 were no-ops:
> traits read `already at or above target (4 >= 2) — add-only path, nothing removed`, and passions
> read `committed pips 4.00 ... rolled 0.52 -> 0 Major + 0 Minor`. **That was the configuration being
> lucky, not the guard working.** A later pass that rolls a *higher* quality than the first gets a
> higher target and will genuinely add — q=0.2 then q=0.9 crosses the 2 → 3 trait boundary. Fresh
> quality per pass also means the pawn is effectively rolled best-of-N on trait count, which is not
> what a single-roll design is supposed to do. Reachable in normal play by any pawn whose 7 or 10
> letter sits unclicked until after 13; letters pile up during a raid and get cleared in a batch.
>
> ### The fix: `GrowUpVariance.IsAdulthoodGrowthLetter`
>
> `Pawn_AgeTracker.BirthdayBiological` is the **only** site in `Assembly-CSharp` that constructs a
> growth letter, and it labels the adulthood one itself:
>
> ```csharp
> bool flag = (float)birthdayAge == AdultMinAge;
> ...
> LetterDef negativeEvent = (flag ? LetterDefOf.ChildToAdult : LetterDefOf.ChildBirthday);
> ```
>
> So `letter.def == LetterDefOf.ChildToAdult` **is** "this is the adulthood moment", and ages 7 and
> 10 never carry it. `Letter.def` is scribed by vanilla (`Scribe_Defs.Look(ref def, "def")`), so the
> guard survives save/load **with nothing stored by us** — the constraint that forced the
> `GameComponent`'s deletion. `AdultMinAge` is race-driven, so it stays correct for HAR races moving
> the adult threshold, same as the `DevelopmentalStage` checks. `ChildToAdult` is a Biotech
> `LetterDef` with `letterClass ChoiceLetter_GrowthMoment`
> (`Data/Biotech/Defs/Misc/CustomNotificationLetters.xml:32`) and `[MayRequireBiotech]`, hence null
> without Biotech — which needs no guard, since reference equality never matches null and vanilla
> issues no growth letters there anyway.
>
> **Applied at three sites, and all three are load-bearing.** `GrowthMomentMakeChoices_Postfix` and
> `LetterStackRemoveLetter_Postfix` are the obvious two. The third is
> **`HasUnresolvedGrowthLetter`**, and narrowing it is not cosmetic symmetry — it is what stops the
> fix creating a worse bug: had only the appliers been gated, a pawn turning 13 while holding a
> stale age-7 letter would make the life-stage hook **defer on a letter neither applier will ever
> act on**, and that pawn would silently receive *no* variance at all. The deferral condition and
> the thing that ends the deferral must name the same letter.
>
> **The rejected lead, recorded so it is not retried.** The previous handover nominated
> `pawn.ageTracker.growthPoints` / `canGainGrowthPoints`. It does not work: `MakeChoices` ends with
> `growthPoints = 0f; canGainGrowthPoints = true;` at **every** growth moment — 7, 10 and 13 alike —
> so the post-resolution state of an age-7 letter is identical to that of the age-13 letter and
> neither field can answer "has this pawn already had its adult pass?". `canGainGrowthPoints` is
> merely a letter-outstanding flag (`BirthdayBiological` sets it false when it sends one). A
> session-only `HashSet` remains insufficient for the original reason: it loses the guard across a
> reload, which is exactly when stacked letters get cleared.
>
> **Known residual, accepted.** A pawn de-aged below `AdultMinAge` and re-aged (growth vat, dev
> mode) crosses adulthood twice and would get a second pass. The deleted pending list would have
> blocked it; blocking it now needs persistent per-pawn state, which is the constraint we are not
> reopening.
>
> ### Re-measured live 2026-08-11 (GABS-driven quick-test colony, deployed build)
>
> Build `0 Error(s), 0 Warning(s)`. A `HumanlikeChild` colonist (**Bamsefar**) spawned at age 3 and
> walked to 13 with nine forced birthdays, game unpaused, **all three letters left unclicked**.
> `Dump growth-moment state` at 13 showed the setup and the discriminator directly:
>
> ```
> growth letters on the stack: 3
>   Bamsefar  choiceMade=False  timeoutPassed=False  tier=0  def=ChildBirthday  (childhood -- ignored)
>   Bamsefar  choiceMade=False  timeoutPassed=False  tier=0  def=ChildBirthday  (childhood -- ignored)
>   Bamsefar  choiceMade=False  timeoutPassed=False  tier=0  def=ChildToAdult   <- ADULTHOOD (actionable)
>   Bamsefar  13  Adult  Adult  yes  WOULD DEFER (letter outstanding)
> ```
>
> Resolving all three in order (age 7 → age 10 → adulthood) produced **exactly one**
> `Growth moment resolved for Bamsefar` and exactly one grow-up pass, on the `ChildToAdult` letter.
> **The two childhood letters produced no mod log line at all** — under the old guard each was a full
> `Apply`. The measured before/after is **3 → 1**.
>
> Vanilla was unaffected: its own age-7 and age-10 grants still landed (`VTE_Wanderlust`,
> `VTE_ThickSkinned` both appear in the grow-up trace's `incoming (5)`), and the deferral line fired
> once at the Child → Adult transition. No error or warning entries appeared in the bridge log
> journal across the run.
>
> Settings were toggled **in memory only** (`wroteSettings: false`) and restored to `false/false`;
> the on-disk config was never written.
>
> ### Save/load round trip — also measured 2026-08-11, and this is the one that matters most
>
> The whole argument for `letter.def` is that vanilla scribes it, so that claim was measured rather
> than assumed. Second colony, colonist **Chen**, same setup: age 3 → 13, all three letters
> outstanding, nothing applied (`WOULD DEFER`). Saved, **reloaded**, and resolved all three letters
> *after* the reload. The dump on the far side was byte-identical in the fields that matter — three
> letters, both `ChildBirthday` still `(childhood -- ignored)`, `ChildToAdult` still
> `<- ADULTHOOD (actionable)` — and resolving all three produced **exactly one**
> `Growth moment resolved for Chen`. The guard survives save/load with nothing stored by the mod.
>
> Note the `observed-as` column reads `Adult` after the reload: `LastKnownStage` is cleared by
> `Game.LoadGame` and re-recorded from the post-load resync. That is correct and is *why* the
> deferral has to be derived from the letter rather than from session state — the session baseline is
> gone by the time a stacked letter gets clicked.
>
> **Save footprint re-confirmed on this build.** The freshly written save contains **zero**
> `PawnVarianceMod` occurrences; the only `kalas.pawnvariance` string is the `<modIds>` header every
> active mod appears in. The About.xml / workshop "safe to add and remove" claim still holds. The
> test save was deleted afterwards.
>
> **Automation trap worth remembering:** the debug-spawned child's name is randomised per colony, and
> `execute_debug_action` with a `pawnName` that matches nobody still reports `success: true` inside
> `run_script`. A ten-birthday loop against a stale name silently did nothing and the script reported
> a clean run — only the state dump caught it. Always read the pawn name from the colony, and always
> confirm the setup with `Dump growth-moment state` before trusting it.

> [!WARNING]
> **Still unverified.** ~~Letter pending across save/load~~ (measured 2026-08-11, see above);
> archived letter re-opened from the History tab (the per-letter guard's original purpose); timeout
> path via `LetterStackRemoveLetter_Postfix`; `applyVarianceToChildren` off. The remaining three all
> run through lines edited by the 2026-08-11 per-pawn fix, so they are not merely inherited gaps.
> Mitigation for all of it: that setting is `false` by default, so the whole path is opt-in.
>
> **Old dev saves carry the stale node** and log those two errors once each until re-saved.
> Self-healing, and irrelevant after release since no shipped save ever contained the node.

> [!TIP]
> **How to reproduce any of this** — the method cost a session to find. Spawn
> `Actions > Spawn Pawn With Lifestage... > Colonist > HumanlikeChild` (it spawns around age 3;
> the generation trace prints the pre-conversion adult age, which is misleading), then
> `T: Force Birthday` repeatedly. **The game must be UNPAUSED**: `LastKnownStage` is only recorded
> from `AgeTickInterval`, so on a paused game no baseline exists, the Adult transition takes the
> `!hadBaseline` return, and the mod correctly does nothing while looking completely broken.
> `Dump growth-moment state` shows exactly that as `NOT ACTIONABLE -- no observed baseline`.

---

# 🚀 BUILD & DEPLOYMENT LOOP

```powershell
tasklist /FI "IMAGENAME eq RimWorldWin64.exe"   # must show no running instance
dotnet build Source/PawnVarianceMod.csproj
$dep = "C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod"
Copy-Item Assemblies/PawnVarianceMod.dll, Assemblies/PawnVarianceMod.pdb "$dep/Assemblies/" -Force
Copy-Item About/About.xml, About/LoadFolders.xml, About/Preview.png, About/ModIcon.png "$dep/About/" -Force
New-Item -ItemType Directory -Force "$dep/Languages/English/Keyed" | Out-Null
Copy-Item Languages/English/Keyed/VariedPawns.xml "$dep/Languages/English/Keyed/" -Force
```

- **Guard**: check for a running RimWorld before copying, or the DLL copy fails on a file lock.
- **Verification**: `dotnet build` must return `0 Error(s), 0 Warning(s)`.
- **Copy `About/` too.** This block copied only `Assemblies/` for a long time, so the deployed mod
  carried a stale `About.xml` and neither image, and the logo and title card never appeared in game
  no matter how often they were regenerated. `tools/build-release.ps1` always packaged `About/`
  correctly, so only the dev deploy was ever affected. **`About/` changes need a full game
  restart** — there is no reload path for mod metadata.
- **Copy `Languages/` too, and this is the same bug a second time.** Item 9 added a fourth shipped
  directory in `dcf71a1` and this block was not updated, so **every dev deploy between then and
  2026-08-13 ran without any translation file at all.** The symptom is not an error: RimWorld
  renders the raw key text, so every tab shows `VP_...` where a label should be, and the two startup
  verifiers (`VerifyPresetKeys`/`VerifyPriorityKeys`) fire `Log.Error` for keys that are in fact
  perfectly fine in the repo. **Anyone checking translated strings against the dev copy is testing a
  build that has no translations** — which would read as "the extraction is broken" when nothing is.
  `tools/build-release.ps1` was never affected: it stages from `Get-ExpectedShipMap`, which has
  always included `Languages/`, and `-Check` is what caught the folder missing from staging in the
  first place (item 17).

  > **The pattern is now twice-confirmed and worth generalising: this block is a hand-maintained
  > duplicate of the ship list, and it goes stale silently every time the ship list grows.** The
  > shipped set is 7 files across 4 directories; if a fifth is ever added, it must be added here in
  > the same commit. The durable fix is to deploy via `Get-ExpectedShipMap` rather than by hand —
  > not done, but it is the reason this note keeps needing to be written.

---

# 📦 PUBLISHING TO THE WORKSHOP

`tools/build-release.ps1 -Build -Zip` produces the uploadable folder (`Release\Varied Pawns\`) and
the GitHub Release zip. What it cannot do is the Steam side. That is this section.

## The sequence

0. **Re-stage, or at minimum run `.\tools\build-release.ps1 -Check`, and do not proceed until it
   passes.** Staging has an indefinite shelf life and no freshness guarantee of its own; this is
   the only thing standing between you and uploading a build older than the repo. See item 17 for
   what that failure looked like when it actually happened.
1. Copy the staged folder into `…\RimWorld\Mods\` so the game can see it, then launch RimWorld and
   open **Mods**.
2. Select *Varied Pawns* → **Upload to Steam Workshop**. RimWorld fills the item from `About.xml`:
   the name, the `<description>`, and `Preview.png` as the thumbnail. Nothing else is uploaded for
   you.
3. **Steam keeps a brand-new item hidden until the Workshop Legal Agreement is accepted on the
   item's own page. Do that first** — otherwise the item exists, looks published from the game's
   side, and nobody can see it.
4. Replace the auto-filled description by pasting **`Release\upload\description-paste.txt`**, which
   `-Build` generates from `docs/workshop-description.txt` (everything below the `====` divider,
   UTF-8, no BOM) and `-Check` verifies. Paste that file rather than hand-copying out of the source,
   which is how the copy that used to live there acquired 22 double-encoded em dashes. **The two
   descriptions are meant to differ**: `About.xml`'s is the short in-game mod-list text, this one is
   the full listing. If what the mod does changes, change both.
5. Add the remaining listing images in the order fixed in `docs/workshop/STATUS.md`.
6. Set visibility to Public.

## Two things that are easy to get wrong

- **The payoff shot's caption must not describe it as what a preset gives you.** Those three
  colonists were rolled on the custom `Showcase` profile, not on a shipped preset; the image's own
  footer says so, and a caption that contradicts it misrepresents what picking `Distinct` from the
  dropdown actually gets a player. `docs/workshop/STATUS.md` carries the profile's values and the
  full constraint.
- **After publishing, remove the local `Mods\PawnVarianceMod\` copy** (or give the dev copy a
  different `packageId`). Subscribing to your own Workshop item while the local folder is still
  there means two installs of `kalas.pawnvariance` — a duplicate-`packageId` error for you, and the
  same error for any player who ends up with both. The wall of duplicate-`packageId` noise this
  project has already seen from `CETeam.CombatExtended` and `NozoMe.MapModeFramework` is exactly
  this, in someone else's mod.

**Bump `<modVersion>` in `About.xml` before every re-upload.** RimWorld ignores it; mod managers
display it and `build-release.ps1` names the zip from it.

## Pre-publish checks

Open work, listed so it is decided deliberately rather than discovered by a player. The envelope
gate and the in-game `Verify Best-of-N` are *not* on this list because they are already green
against the staged build — these are the things those gates structurally cannot see.

**Numbering is stable and append-only: struck-through items keep their number and their outcome
rather than being deleted,** because commits and notes elsewhere refer to items by number. New
findings get the next free number wherever they belong topically — 17 sits with the blocking items,
not at the end.

Closed so far: 1, 3, 4, 5, 6, 7, 8, 11, 15, 16, 17, 18, 19, 20 (done), 2, 14 (decided, moved to
*Settled and not to be relitigated*), and **12 (closed by decision with the risk accepted, not by
measurement — read the item, it is not a benchmark)**. **Item 9 is done except for a cosmetic
wrap/clip pass**; **item 13 is done bar one marked `TODO` line** that needs the Workshop URL.

**Only 10 is still fully open, and it cannot be done until the item is published.** Nothing on this
list now requires loading the modpack again.

**The verification that used to be outstanding here — distinguishing a translated preset label from
its `devName` fallback — is DONE and passed.** See item 20 for the marked-key recipe and the
two-line comparison that discriminates them; it is worth keeping because it is the only instrument
that can.

**Every item closed here that was not purely mechanical found a defect the existing gates could not
see:** item 1's audit found staging shipping a build older than the code (→ 17), item 9 found the
verification harness matching presets on a translatable label and then, on its in-game pass, found
the harness crying wolf on every run (→ 19), and four `Log.Error`s in every player's log that the
in-game harness structurally cannot see (→ 20), and item 4 found a false claim in the listing. Items
17, 18, 19 and 20 exist because of items 1 and 9. **The rate is six defects from seven
non-mechanical items, and item 20 was found only because the owner read `Player.log` after this
document already called the work complete.** Treat the remaining open items as likely to behave the
same way — and see item 20 for why "the in-game gate is green" is not the same claim as "the game
logged nothing".

> **Items 5 and 6 are the first non-mechanical closures to find nothing, and that is worth stating
> rather than quietly folding into the rate.** Both were run under the 1376-mod modpack, the
> environment furthest from anything this project had tested in, and both came back clean. Item 6
> produced one *clarification* — the menu is shorter than the on-disk faction count, correctly —
> but no defect. The pattern above is a warning about where to look, not a law that every check
> must yield something.

> **Both gates are run against the actual upload artifact, not a dev build.** The staged DLL is a
> **Release** build and in-game results recorded before 2026-08-12 came from the **Debug** dev copy
> — a different binary that nothing had ever gated. Re-run both against the staged folder, not the
> dev copy, before uploading.
>
> **Last run 2026-08-13, against the staging from commit `7b3f401` (Release, 132,096 bytes),
> deployed into `Mods\` with the dev copy moved aside so no duplicate `packageId` existed:**
> - `Verify Best-of-N` **32/32 PASS**. Worst shown **0.01pp** (`Distinct` @ N=50), worst raw
>   **0.01%** (`Desperate` @ N=50), grid assertion silent, constants clean, skill count 12, per-axis
>   toggles OK, `cross-branch OK ... live = fallback = 0.250709 (delta 8.94E-008)`. **This is the
>   run that matters for the regenerated `EnvelopeFigures.g.cs`** — `Faithful`'s reference row reads
>   the new `0.304099 / 0.345485 / 0.359545`, confirming the gate compared against the regenerated
>   table rather than a stale one.
> - `Roll pawns and dump distribution` at 200 pawns: `GENERATOR vs MODEL [Faithful]` **OK**,
>   model 4.550 pips/pawn (sd 1.234), delta **−0.063** against tolerance 0.349. Passionless pawns
>   **0.0%**, passion pips mean 4.49 — consistent with the ~4.55 the model targets and with the
>   `4.59` vanilla-parity figure recorded under "Tuning constraints".

### Blocking — do before the item goes public

1. **Read what is actually in the release folder, file by file.** Done once, and it must be redone
   against whatever is staged at upload time — the audit is of an artifact, not of the script.
   Every file was accounted for and all are legitimate: `LICENSE` (MIT, identical to repo),
   `About\LoadFolders.xml` (1.6 only, agreeing with `supportedVersions`), `About\ModIcon.png`
   (256×256), `About\Preview.png` (1024×576, 105 KB — Steam's cap is 1 MB), `About\About.xml`, and
   one DLL. **`Assemblies\` contains exactly one DLL — Harmony is referenced but not bundled**,
   which is the check that matters there, because bundling it breaks other mods. The count is now
   **7**, not the 6 recorded when this was first audited: `Languages\English\Keyed\VariedPawns.xml`
   joined the ship list with item 9.

   **What the audit actually caught, and the reason to keep doing it: staged content goes stale
   silently.** The staged DLL was 142,848 bytes built at 09:29 while the repo's was 150,016 built
   at 10:01, after the last `Source\` edit — so the staging, and `Release\VariedPawns-1.0.0.zip`
   beside it, were a build older than the code. `build-release.ps1`'s stale-build check compares
   the *repo* DLL against `Source\` and only runs when you re-stage; **nothing invalidates staging
   that already exists.** Treat the staging folder as a build output with no freshness guarantee:
   re-run the script immediately before uploading, never upload what happens to be sitting there.
2. ~~**Decide whether the debug tools ship.**~~ **SETTLED — they ship.** Moved to "Settled and not
   to be relitigated"; the reasoning is under *Why the debug tools ship*. Do not reopen this as an
   open question.
3. ~~**Put the GitHub URL in `About.xml`.**~~ **DONE.** It is now in two places: a `<url>` element,
   which RimWorld renders as a clickable link in the mod info panel (verified as a real field
   against the 1388 installed workshop mods — 360 use it), and a closing line in `<description>`,
   because mod managers and modpack listings show the text and not the field. The description line
   also names GitHub Issues as the bug destination, which closes item 11.
4. ~~**Have an AI read the published artifact, not the repo.**~~ **DONE, and it found a false claim
   in the listing — the exact category this item existed to catch.**

   **The finding.** Both `About.xml` and `docs/workshop-description.txt` claimed, in bold, *"It
   changes how many traits a pawn gets — never which ones. Trait selection stays entirely
   vanilla's."* **The second half is false.** `TraitVarianceApplier.cs:106` removes traits with
   `removable.RandomElement()` whenever the rolled target is below the pawn's current count, which
   is the mod choosing which trait a pawn loses. The code's own comment at line 93 says so:
   *"Uniform random choice: vanilla's picker has no concept of a 'better' trait."* Only the
   ADDITION path delegates to vanilla (`GenerateTraitsFor`, line 76).

   The claim's *intent* — that nothing is scored or favoured by quality — is true and worth
   keeping; the absolute was the defect. Reworded in all three copies to say new traits come from
   vanilla's picker, over-target pawns drop one at random, and forced traits are never removed.

   **Why it survived everything else:** it is a claim about the code made in a text file. No build,
   no envelope gate, no in-game action and no diff review can see it, because nothing compares
   prose to behaviour. **This is the only review this project has run that reads the artifact
   instead of the diff, and it found something on the first pass — re-run it before any future
   listing change.**

   Load-bearing claims that were checked and DO hold: add mid-save, remove mid-save (no
   `GameComponent`/`WorldComponent`/`MapComponent`/`ThingComp`/`HediffDef` anywhere; the settings
   are a config file, not save data), children off by default, eight presets all reachable, Biotech
   optional, the `18` pip figure, 1.6-only consistency across `About.xml`/`LoadFolders.xml`/listing,
   and every `{0}` placeholder count against its `.Translate(...)` call site.

   **Three of the five findings did not survive cite-checking** — two named a file the reviewer was
   never given, and one misread a `Count > 0` guard as missing. Cite-check every verdict; that rule
   is in this document for a reason and it paid again here.

   **Hazard found while fixing it: the listing text existed in THREE places** — `About.xml`
   (short, in-game), `docs/workshop-description.txt` (the source of truth per the publish
   sequence), and `Release/upload/description-paste.txt` (a gitignored paste-ready duplicate). All
   three were corrected at the time. **Change a claim in one, change it in the other.**

   > **The third copy is no longer hand-maintained, because leaving it that way cost exactly what
   > this note predicted.** `build-release.ps1` now *derives* `description-paste.txt` from
   > `docs/workshop-description.txt` on every `-Build` (everything below the `====` divider,
   > written UTF-8 **without** a BOM), and `-Check` fails when the two disagree.
   >
   > What the hand-maintained copy had drifted into by 2026-08-13, found only because someone
   > diffed it: **22 mojibake sequences against 1 surviving clean em dash** — every `—`
   > double-encoded to `â€"` by a UTF-8 file being written back as Windows-1252 — plus a BOM that
   > pastes into Steam's description box as an invisible leading character. Item 4's corrected
   > trait paragraph *was* present, so the claim was right and the punctuation was wreckage.
   > **Nothing would have caught this**: it is a gitignored file, no gate read it, and it looks
   > fine in an editor that guesses the encoding.
   >
   > **Two copies remain, and they still need manual agreement** — `About.xml`'s short description
   > and `docs/workshop-description.txt`. Only the derived third one is now safe.

17. ~~**Close the stale-staging hole.**~~ **DONE — `build-release.ps1 -Check` now exists.**

    **The hole.** `Release\Varied Pawns\` could ship code older than the repo with nothing to
    catch it. Found by item 1's audit: the staged DLL was 142,848 bytes built at 09:29 while the
    repo's was 150,016 built at 10:01, after the last `Source\` edit — so the staging folder, and
    `Release\VariedPawns-1.0.0.zip` beside it, were a build older than the code. `Release\` is
    gitignored, so nothing in git tracked it either. The old stale-build check compared the
    **repo** DLL against `Source\*.cs` and ran **only when you re-staged**: it made staging correct
    at the moment of creation and said nothing afterwards, while staging sat indefinitely in the
    exact folder the Steam uploader is pointed at.

    **The fix.** `-Check` validates the existing staging against the current repo without
    rebuilding or restaging anything, and reports STALE / MISSING / UNEXPECTED per file plus the
    repo's own DLL-vs-source staleness. It compares **every shipped input by SHA-256**, not just
    the DLL — and that breadth is the load-bearing part, because the old check could only ever see
    `Source\`. On its first run against the then-current staging it correctly caught three things:
    a stale `About.xml`, a stale DLL, and a **completely missing `Languages\` folder** — a shipped
    input that did not exist when the DLL-only check was written, and which no amount of
    DLL-watching would ever have found.

    Both the stager and `-Check` read one `Get-ExpectedShipMap`, so the file set cannot drift
    between what is copied and what is verified.

    **`Release\staging.stamp.json`** records commit, dirty flag, mod version, file count and DLL
    hash. It lives in `Release\`, deliberately **outside** `Release\Varied Pawns\`, because
    everything inside the staging folder gets uploaded and a build stamp is not player content.
    **Do not tidy it into the mod folder.** The hash comparison does not depend on it — `-Check`
    re-derives everything from the repo — so a missing stamp is a warning, not a blocker.

    **The process rule still stands, and is now enforceable: run `-Check` immediately before every
    upload, or just re-stage.** Never upload what happens to be sitting there. The failure is
    silent and expensive — a Workshop item running code that matches no commit and no gate result
    is unfalsifiable from a bug report.

### Compatibility — the modpack pass

5. ~~**Run the mod inside the Progression Modpack.**~~ **DONE — clean.** The mod was run under the
   large collection installed on this machine (1376 workshop items), which is the first time it has
   been exercised against anything but a modest active list. **No load errors, no def conflicts, and
   no sign of a Harmony collision.**

   The collision surface is the generation postfix: any other mod patching
   `PawnGenerator.GeneratePawn` or `GenerateSkills` can silently win, and **the symptom is pawns
   that look vanilla rather than an error** — which is why the instrument is
   `Roll pawns and dump distribution` rather than the log. The shipped presets' measured spread did
   **not** collapse toward `Faithful` under the modpack, so nothing upstream is overwriting this
   mod's work.

   **Re-run this if the postfix or its patch target ever changes**, and read the dump, not the log:
   a mod that wins the patch produces a clean startup and vanilla-looking colonists, which is
   exactly the shape of defect every gate in this document is blind to.
6. ~~**Click the Add Faction Override button under that modpack and watch what the menu does.**~~
   **DONE — opened by hand at modpack scale. It is usable and it does not clip**, which is what the
   item existed to establish: the sort and de-duplication landed on 2026-08-12 *without the menu
   ever being opened in game* — a `FloatMenu` does not survive a synthetic click, so the bridge
   cannot verify it and the change rested on the tab drawing without exceptions. `Verse.FloatMenu`
   columning and scrolling past `MaxScreenHeightPercent` (0.9) has now been watched happening
   rather than read out of the shipped assembly's metadata. The searchable-`Window` fallback this
   item held in reserve is **not needed**.

   **The one surprise, and it is correct behaviour: far fewer factions appear in the menu than the
   ~499 figure above implies.** That number was counted **on disk**, across all 1376 installed
   workshop items. The menu is built from `DefDatabase<FactionDef>.AllDefs`
   (`PawnVarianceSettings.cs:1168`), and `DefDatabase` holds only the defs of **mods active in the
   current run**, never the abstract ones (35 of the 534 tags). An installed-but-inactive mod's
   factions are therefore absent by construction. **Expect the on-disk count and the menu length to
   disagree, and do not "fix" it** — an override keyed to a def that is not loaded could not
   resolve against anything anyway, and `LabelForKey` already keeps such a row rendering and
   removable if the mod is later disabled.

   > Note the faction menu applies **no pawn-spawning filter** — it is a straight `DefDatabase`
   > sweep. The "humanlike races something spawns" traversal is the **race** menu's, and only the
   > race menu's; see *The Add-menu filter has two halves* above. The two menus narrow their lists
   > for different reasons and should not be reasoned about together.

### Worth checking, lower stakes

7. ~~**Install it the way a player does, from the zip, with nothing else but Harmony.**~~ **DONE.**
   Ran from `Release\VariedPawns-1.0.0.zip` extracted straight into `Mods\`, with the dev copy
   removed, `ModsConfig.xml` cut to Harmony + core + the five DLCs, and
   `Config\Mod_PawnVarianceMod_PawnVarianceMod.xml` **deleted** so the first-run path actually ran.
   Mod loads as `Varied Pawns` from the zip folder. **`rimbridge/list_logs` reported a clean
   startup and that reading was WRONG — see item 20.** Read `Player.log`, which showed four
   `No active language!` errors this check should have caught. Defaults
   seeded correctly with no config present: `activeProfileId preset_faithful`,
   `hostileProfileId preset_distinct`, `hasInitializedDefaultOverrides true`, 10 faction and 10
   xenotype overrides, 0 race overrides (correct — the race list ships empty on purpose), and
   `applyVarianceToChildren false`, so **Rule 4's default holds on a clean install.**

   **Deviation, stated rather than hidden:** `brrainz.rimbridgeserver` was left active, because it
   is the only way to observe anything. It patches nothing in pawn generation. Everything else was
   absent.

   **Finding, and it is a non-issue on purpose:** the settings file is **never written** on a
   first run — not on settings-window close, not on quit. It does not matter, and the reason is
   worth keeping so nobody "fixes" it: `PopulateDefaultOverrides` early-returns on
   `hasInitializedDefaultOverrides` and otherwise re-seeds the same deterministic default set every
   launch (`PawnVarianceSettings.cs:173-181`), so an absent file produces byte-identical behaviour
   to a written one. RimWorld writes the file the first time the player closes the settings window
   through its own path. Not chased further: the bridge's `close_window` and `games_stop` both
   bypass RimWorld's normal `PreClose`/quit write, so this is an artifact of how the check was
   driven, not a mod defect — every other mod on this machine has its `Mod_*.xml`.
8. ~~**Verify the two removal claims, because the listing makes them in bold.**~~ **DONE, both
   directions, end to end on the shipping zip build.**

   **"The mod writes nothing into your save" — verified structurally, not just behaviourally.**
   A colony was generated with the mod active, saved, and the 5.9 MB `.rws` searched: **zero**
   occurrences of `PawnVarianceMod`, `VariedPawns` or `pawnvariance` anywhere, and exactly one
   `kalas` — inside vanilla's own `<meta><modIds>` load-order manifest, which every active mod
   appears in. Nothing in the save body belongs to this mod.

   **Remove mid-save:** with `kalas.pawnvariance` taken out of `ModsConfig.xml`, that save loaded to
   a playable map with **zero errors**. RimWorld reported the missing mod in its own compatibility
   check, which is vanilla behaviour for any removed mod, not orphaned data.

   **Add mid-save:** the mod-free colony was re-saved (recording 8 mods, no `kalas.pawnvariance`),
   the mod re-enabled, and that save loaded to a playable map with **zero errors**.
9. **Translations — DECIDED, extraction PART DONE.** The decision is *keys, not hardcoded
   English*: 1.0 ships with UI strings in `Languages/English/Keyed/VariedPawns.xml`, so a
   translator can contribute without a refactor. Doing it before release rather than after is the
   whole point — adding keys rewrites every UI string at once.

   **The code work is DONE.** 138 keys across `VarianceProfile.cs`, `ProfileEditorTab.cs` and
   `PawnVarianceSettings.cs`; builds clean with zero warnings and the checker green.
   `GrowUpVariance.cs` and `GrowthUpPatch.cs` were checked and contain no player-facing text at
   all, only `TraitTrace` diagnostics; `Dialog_RenameProfile.cs` has no literals.
   **What remains is the in-game pass, and only that.**

   **Scope rules, so the second half matches the first:** `DebugActions.cs` is excluded (DevMode
   only, and it is the harness, not player content). `Scribe` node names and defNames are never
   touched — renaming those orphans saved configs. `SettingsCategory()` stays untranslated because
   it is the mod's name and must agree with `About.xml`'s `<name>`.

   **Three traps this work has already hit, all of which recur in the second half:**
   - **Never call `.Translate()` in a static field initializer.** Every preset is a
     `static readonly` field, which initialises at type-init, potentially before
     `LanguageDatabase` loads — that bakes the raw key text in permanently. Presets now expose
     `label`/`description` as properties that translate on *access*.
   - **A display label is not an identity.** `DebugActions` looked presets up with
     `x.label == "Faithful"`, and `EnvelopeFigures.Profiles` is a generated table keyed by the
     same English names. Translating `label` would have stopped the entire verification harness
     from finding any preset — no exception, clean build, the gate simply never fires. Identity
     now lives in the never-translated `devName`; `label` is presentation only. **If you add a
     preset lookup, match on `devName` or `stringId`, never on `label`.**
   - **A double hyphen is illegal inside an XML comment,** and this repo's C# uses `--` as a dash
     constantly. One copied into `VariedPawns.xml` makes the whole file unparseable, which means
     every key in it silently goes missing at once. This was hit on the first run.
   - **Player-visible text does not have to be a string literal.** The `OverridePriority` button
     and its menu rendered `Highest`/`High`/`Normal`/`Low` through plain `.ToString()` on the
     enum — real UI text with no literal anywhere to grep for, and it was nearly missed for
     exactly that reason. It now goes through `OverridePriority.TranslatedLabel()`, with
     `VerifyPriorityKeys()` asserting a key exists for every member. **If any other enum is ever
     drawn with `.ToString()`, it has the same defect.**

   **The gate:** `tools\check-translation-keys.ps1` cross-checks keys used in code against keys
   defined in XML, both directions, plus empties and duplicates. It exists because a missing key
   does not throw and does not fail the build — RimWorld just renders the raw key text where the
   label should be, invisible until someone opens that specific tab. Run it after touching either
   side. It cannot see keys built by concatenation, so the derived `VP_Preset_*` keys are asserted
   at startup by `VarianceProfiles.VerifyPresetKeys()` instead.

   **The in-game pass is DONE at the mechanism level, and it found a defect — item 19.** What was
   established against the shipping zip build, and how, because "open every tab and look" turned out
   to be the weaker half of this check:

   - **Every player-facing key is now accounted for by something that runs.** There are exactly
     **three** `.Translate()` call sites in the mod whose key is not a string literal —
     `("VP_Priority_" + p)` (`PawnVarianceSettings.cs:28`) and `LabelKey`/`DescriptionKey`
     (`VarianceProfile.cs:246-247`). Both families are asserted by `VerifyPriorityKeys()` and
     `VerifyPresetKeys()`, which run from `PawnVarianceStartupChecks` and `Log.Error` on a miss.
     Both passed, so `VariedPawns.xml` parsed (the double-hyphen trap would have taken every key
     down at once) and `Languages\` shipped. **Confirm that in `Player.log`, not through the
     bridge.** This pass originally asserted it from an empty `rimbridge/list_logs`, which is blind
     to the startup window and was concealing four real errors — see item 20. Everything else is a
     literal, and
     `tools\check-translation-keys.ps1` covers those both ways: 117 used, 138 defined, none missing,
     empty or duplicated.
   - **What that leaves is genuinely only cosmetic:** whether any translated string *wraps badly or
     clips* in RimWorld's narrow widgets. That needs eyes on the rendered tabs and could not be done
     in this pass — another fullscreen game held the OS foreground, and RimWorld only renders while
     focused, so `take_screenshot` returns a silently stale frame. **This is the last remaining
     piece of item 9.** Note that `get_ui_layout` and `click_ui_target` *do* work unfocused; it is
     only screenshots and hover tooltips that do not.

19. **DONE — the raw-key trap recurred one layer up, in a SNAPSHOT rather than a static
    initializer. Found by item 9's in-game pass, which is the only thing that could have found it.**

    **The symptom.** `Roll pawns and dump distribution` printed
    `ACTUALLY RESOLVED TO: VP_Preset_Faithful x1000` — the raw key — while the line directly above
    it printed `configured active profile: Faithful` correctly, in the same log entry.

    **The mechanism.** `profileLabel` is a snapshot taken by `RefreshResolved()`. Two of its callers
    run from `GetSettings<>()` inside the `Mod` constructor — the parameterless ctor
    (`PawnVarianceSettings.cs:161`) and `ExposeData`'s load branch (`:544`) — which is **before
    `LanguageDatabase` is populated**, so `LabelFor` → `preset.label` → `.Translate()` returned the
    key and baked it in permanently. Making the presets' `label`/`description` lazy properties fixed
    the static-initializer version of this trap; it did not fix a value copied out of them too early.

    **Why it mattered despite no player ever seeing it.** Every consumer of `profileLabel` is a
    diagnostic — `DebugActions`, `TraitTrace`, the passion trace — so there is no player-facing
    impact, and that is *not* the reason to fix it. `DumpDistribution` compares its resolved label
    against `LabelFor(activeProfileId)`, computed live and therefore translated. Raw key vs
    translated label can never be equal, so the action printed
    `^^ NOT the configured active profile. An override ... outranked it` on **every run**, including
    runs where nothing overrode anything. That warning exists because a real override once went
    unnoticed for two consecutive 1000-pawn runs (see the comment above `resolved` in
    `DumpDistribution`). **An always-on false alarm trains the reader to ignore precisely the
    warning it was added to raise** — it had turned the project's only generator-vs-reality
    instrument into one that cries wolf.

    Custom profiles were never affected: `LabelFor` returns `custom.name` for them, which is not a
    key. **That is why the runs recorded in this document look clean** — they resolved to
    `Custom 1`. Presets were the broken case, and no preset-resolved dump had been run since the
    translation extraction landed.

    **The fix:** `RefreshResolvedLabels()` re-snapshots the two labels from
    `PawnVarianceStartupChecks`, which is `[StaticConstructorOnStartup]` and therefore runs after
    language data is loaded — the same correctly-timed hook the two key verifiers already use, and
    whose comment states this exact invariant. Only the labels are re-taken; the resolved *values*
    are untouched.

    **Verified:** the dump now reads `ACTUALLY RESOLVED TO: Faithful x1000 (100.0%)` with no false
    override warning, `GENERATOR vs MODEL [Faithful]` OK (delta +0.057 against tolerance 0.156), and
    `Verify Best-of-N` re-run afterwards is **32/32 PASS with output bit-identical to the pre-fix
    run**, confirming the change touches nothing in the scoring path.

    **The reusable lesson:** the translation pass's own documented rule — *never call `.Translate()`
    in a static field initializer* — is too narrow. The real rule is **never store the result of
    `.Translate()` in a field that outlives the load sequence.** Grep for assignments of a
    translated value, not just for static initializers. **Item 20 then showed even that is not the
    whole rule** — the call itself is a defect at that point in the load, regardless of where the
    result goes.

20. **DONE — four `Log.Error`s in every player's log on every startup, and the in-game harness
    could not see them. Found by the owner reading `Player.log` after item 19 was called complete.**

    **The defect.** `PawnVarianceSettings`' ctor (`:161`) and `ExposeData` (`:544`) both run from
    `GetSettings<>()` in the `Mod` constructor, and both reach `LabelFor` → `VarianceProfile.label`
    → `LabelKey.Translate()`. At that point **no language is loaded at all**, and RimWorld's
    `Translator.TryTranslate` does not fail quietly — it calls
    `Log.Error("No active language! Cannot translate from key ...")`. Result: four red errors
    (`VP_Preset_Faithful` and `VP_Preset_Distinct`, once from the ctor and once from `ExposeData`)
    in `Player.log` on **every single launch**, for every player.

    **Item 19's fix did not address this and was never going to.** Re-snapshotting the label later
    fixes the *stale value*; it does not stop the *early call*. Both defects came from the same
    lazy-property assumption, and fixing the visible half first is what made the other half look
    handled. Verified the hard way: the errors were still present, all eight lines, in the
    `Player.log` written by the item-19 build.

    **The fix.** `VarianceProfile.label` and `.description` now guard on
    `LanguageDatabase.activeLanguage == null` and return `devName` / `""` in that state, so nothing
    calls `.Translate()` before a language exists. `devName` is the right stand-in rather than a
    placeholder: it *is* the preset's untranslated English name. `RefreshResolvedLabels()` (item 19)
    then re-snapshots the real translated label at `[StaticConstructorOnStartup]`. The two fixes are
    complementary — keep both. **Verified: `Player.log` now contains zero `No active language`
    lines and zero `PawnVariance` entries across a full 18-mod startup.**

    > **A test that cannot fail is not a test, and this one could not.** `devName` and the English
    > label are the *same string* ("Faithful"), so **no ordinary English run can distinguish "the
    > guard fell back to devName" from "the translation resolved".**
    >
    > **NOW DONE, with a discriminating instrument, on the Release build — and it PASSES.**
    > `VP_Preset_Faithful` and `VP_Preset_Distinct` were temporarily edited to `Faithful [XLATED]`
    > / `Distinct [XLATED]` **in the deployed copy only** (never the repo — the same discipline as
    > the xenotype fixture), and `Roll pawns and dump distribution` was run at 200 pawns. That
    > action is the right instrument because it prints the two labels **by different paths**:
    >
    > | line | source | printed |
    > |---|---|---|
    > | `configured active profile:` | `LabelFor(...)`, computed **live** | `Faithful [XLATED]` |
    > | `ACTUALLY RESOLVED TO:` | `profileLabel`, the **snapshot** | `Faithful [XLATED] x200 (100.0%)` |
    >
    > Both carry the marker, so translation resolves **and** `RefreshResolvedLabels()` genuinely
    > re-takes the snapshot after the language database is up. Had the guard been stuck on
    > `devName`, the second line would have read plain `Faithful` while the first did not — the
    > exact split this instrument exists to expose. No false override warning was emitted, and
    > `Player.log` contained **zero** `No active language` lines across the run.
    >
    > **Keep this recipe.** It is the only way to test the guard, and it re-runs in about two
    > minutes: mark the two keys in the deployed copy, run the dump, compare the two lines, then
    > restore the deployed folder from staging.

    **The instrument failure is the more important half, because it will recur.**
    `rimbridge/list_logs` captures only from RimBridge's own `logs.initialize`, which its
    `STARTUP_TIMING` puts at ~14 s into launch — **after** `LoadedModManager.CreateModClasses()`.
    Every error this mod emits during settings load is therefore **structurally invisible** to it,
    and the tool reports an empty list rather than an unknown one. Item 7 and item 9 were both
    signed off on that empty list.

    > **Rule: for anything that happens during mod construction or settings load, read
    > `%LOCALAPPDATA%Low\Ludeon Studios\RimWorld by Ludeon Studios\Player.log` directly. Never
    > conclude "clean startup" from `rimbridge/list_logs`.** The bridge is fine for anything after
    > the main menu — debug actions, pawn dumps, the verify gate — which is most of what this
    > project uses it for, and is why the gap went unnoticed. It is the *startup* window that is
    > unobserved. This is the same shape as every other defect in this document: an instrument that
    > answers a narrower question than the one being asked, and an answer read as if it were the
    > broader one.
10. **Look at the item page as a stranger.** Preview image actually rendering (Steam caches
    aggressively), description BBCode not broken mid-tag, images in the intended order, tags set.
11. ~~**Plan where bug reports land.**~~ **DONE via item 3.** GitHub Issues is the destination, and
    it is now reachable from the in-game mod list — both as the clickable `<url>` and as a closing
    line of `<description>` that asks for RimWorld version, mod list, and expected vs actual. The
    Workshop description already carried the same ask.
12. ~~**Measure what the mod costs at generation time under a heavy load order.**~~ **CLOSED BY
    DECISION, not by measurement — and the distinction is the point of writing it this way.** The
    owner's call, taken 2026-08-13 with the residual risk below stated and accepted. No timing
    instrument was built and none exists: there is no `Stopwatch` anywhere in `Source\`.

    **What the decision rests on.** Item 5's `Roll pawns and dump distribution` run drove **1000
    real generations through the postfix under the 1376-item modpack** and completed without
    anything the owner noticed as a cost. That is a genuine datum and it is the right order of
    magnitude: the postfix is arithmetic on one pawn, with no allocation-heavy or def-scanning work
    in it.

    **What it does NOT establish, so nobody later mistakes this entry for a benchmark:**
    - **It was never timed.** "Not that bad" is a wall-clock impression of a bulk loop, not a
      per-pawn figure, and nothing recorded a number.
    - **It cannot isolate this mod's share.** Under a 1376-mod list every other pawn-generation
      patch runs in the same span, so a cost here would be buried in a much larger total.
    - **Neither case item 12 actually named was run** — a 40-pawn raid and a settlement-map load.
      Those are *bursts*, and a burst is what a player feels as a hitch; a steady 1000-pawn loop is
      the case most likely to look fine when a burst does not.

    **Reopen if a player reports a hitch on raid spawn or map load** — that is the symptom this
    would have caught, and it is cheap to diagnose once reported. Reopen also if the postfix ever
    stops being pure arithmetic: a def lookup, a `DefDatabase` scan or an allocation in that path
    changes the shape of the argument above, which is the only thing holding this item shut.
13. **Rewrite `README.md` for the person the Workshop link sends there — DONE except for one line.**
    The restructure did not actually need the Workshop item to exist; only the link does. Resolved
    the way the item suggested: a player-facing header above the developer material rather than
    moving the developer material into `docs/`. The first screen now carries what the mod does,
    `About/Preview.png`, how to install without Steam, requirements, where bugs go, and the licence.
    Everything below `# Developer documentation` is unchanged in purpose.

    **The one outstanding line is a marked `TODO` in the *Installing* section: paste the Workshop
    URL there after the first upload.** That is all that is left of this item.

    Three things in the developer half were **wrong** by the time this was done, all invalidated by
    the work above it, and all now corrected: the scratch-file convention still described
    `.git/info/exclude` (item 16 moved it), the shipped file list still said `About/`, one DLL and
    `LICENSE` (item 9 added `Languages/`, making it 7 files), and the release section predated
    `-Check` (item 17). **Worth noting as a pattern: `README.md` describes the repo's mechanics, so
    changing the mechanics silently ages it, and nothing checks that.**
14. ~~**Decide how much of this repo should be public.**~~ **SETTLED — all of it, as it stands.**
    `HANDOVER.md`, `TRAIT-DESIRABILITY-RESEARCH.md` and all 52 files under `docs/` stay tracked and
    public. The thing being chosen deliberately rather than drifted into: **the internal record is
    a genuine asset and also a full account of every mistake this project made, and it is published
    on purpose.** Recorded in "Settled and not to be relitigated". Ignored, and staying ignored:
    `Assemblies/`, `Release/`, `temp/`, `*.log`, IDE files, `zzz-Do-Not-Commit/`, `__pycache__/`.
15. ~~**Relativise the absolute paths in tracked files.**~~ **DONE.** `HANDOVER.md` line 3 now names
    the repo URL and states that paths here are repo-relative; the four `docs/superpowers/` plans
    and specs had 18 `file:///C:/Users/gokal/…` links rewritten to `../../../Source/…`, which has
    the side benefit that they now actually resolve as links on GitHub, which the `file:///` form
    never did. `git grep -i gokal` over tracked files returns nothing.
16. ~~**Move the `zzz-Do-Not-Commit/` rule into `.gitignore`.**~~ **DONE.** It and `__pycache__/`
    are now in the tracked `.gitignore` with a comment saying why they live there, and
    `.git/info/exclude` has been reduced to a note pointing at it. The rule was previously local to
    this one working copy, so a fresh clone had no protection over exactly the files that must
    never ship (`TestOnly_PlayerColonyXenotypes.xml` among them). Verified with
    `git check-ignore -v`, which now reports `.gitignore` as the source.

18. **DONE, as part of item 9 — the strings were edited on the way into `Languages\`, not copied.**
    The rules below were applied to all 138 keys; what follows stands as the standard for any new
    string. Three changes worth knowing about, because they are content and not length:
    - **The tooltips referred to controls that do not exist.** They said "Skill noise" and
      "Passion noise" throughout, while the sliders on screen are labelled "Skill spread" and
      "Passion spread". Unified on *spread*, matching what the player actually sees.
    - **The confirmation dialogs were roughly halved.** "Are you sure you want to delete all
      faction overrides? This will clear all custom faction profile assignments." became "Delete
      all faction overrides? Every custom faction assignment will be cleared."
    - **"Reset this profile to Faithful?" now substitutes the preset name** from
      `VarianceProfiles.VanillaLike` rather than hardcoding it, so the sentence cannot drift from
      the profile the code actually resets to, and a translator gets the localised name free.

    The original item, kept because the rules are the reusable part:

    **Edit the UI strings for length while they move into `Languages\`.** The extraction in item 9
    rewrites every player-visible string in the mod exactly once, and that is the moment to fix
    the text itself rather than transcribing it. **Do not copy strings across verbatim.** Much of
    the current text was written as explanation-in-place — it argues the scoring model at the
    player inside a checkbox tooltip — and RimWorld's settings widgets are narrow, so long labels
    wrap badly or clip. Rules being applied:

    - A label names the thing. The tooltip explains it. Anything in a label past about five words
      is usually a sentence that belongs in the tooltip.
    - Cut text that describes the mod's internals rather than the player's choice. "Best-of-N",
      "envelope", "composite" and similar are this document's vocabulary, not a player's.
    - Say what the setting does to pawns, not what the code does.
    - Keep every warning that is load-bearing — the precedence rules and the "overrides beat this"
      caption exist because their absence caused real confusion (see item 9's own notes and the
      General-tab caption added 2026-08-06). **Shorten those; do not drop them.**

    Translation cost is a real reason to do this now: every word kept here is a word every future
    translator pays for, forever. Where a rewrite changes meaning rather than length, it is a
    content decision — note it rather than slipping it in with the mechanical edit.

### `About\PublishedFileId.txt` — the trap that splits a mod in two

**Steam identity does not live in `About.xml`.** RimWorld records the Workshop item id in
`About\PublishedFileId.txt` inside the folder it uploaded, and every subscribed mod on this machine
carries one (verified across the installed workshop content). That file is what makes the *second*
upload an **update** instead of a **new item**.

Two ways this project loses it, both live right now:

- **`build-release.ps1` wipes and rebuilds `Release\Varied Pawns\` on every run**, and rebuilds it
  from the repo's `About\`, which has no `PublishedFileId.txt`. Publish, re-stage, publish again,
  and the second upload creates a second Workshop item.
- **Uploading from `Mods\PawnVarianceMod\`** writes the file *there*, outside the repo entirely,
  where the next deploy or a reinstall drops it.

So after the first successful upload: **copy `PublishedFileId.txt` into the repo's `About\` and
commit it.** It then flows into every future staging automatically, and the allowlist already ships
all of `About\`. A duplicate Workshop item cannot be merged with the original — subscribers,
ratings and comments stay on whichever one they found.

### Tie the upload to a commit

`build-release.ps1` warns when the tree is dirty, because an upload that matches no commit cannot
be checked out later. Go further and **tag the commit you build 1.0.0 from** (`git tag v1.0.0`), so
a bug report naming a version maps to source. Nothing enforces this; the tag is the only link
between "what a player is running" and "what the repo says", and `Assemblies\` is gitignored, so
the built DLL is not recoverable from the repo alone — keep `Release\VariedPawns-1.0.0.zip`
somewhere durable, or attach it to a GitHub Release, which does both.

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

Diffs the mod's live integrator against the reference in `Source/EnvelopeFigures.g.cs` for all 8
presets × N = 1, 5, 25, 50, and asserts both sides ran the **same quadrature grid**.

**Tolerance is 0.50 *percentage points on the displayed quantity*, plus a 0.10% raw guard.** The
displayed-quantity framing is the important half: an earlier version of this gate compared raw
scores and produced 15 false failures alongside 1 real defect, which made the real one
indistinguishable.

> **This section described the RETIRED scheme until 2026-08-11** — "the mod's live 1024-node
> integrator against the 20000-node reference", "up to ~0.9% apart at N=50", "a deliberately wide 3%
> raw guard". Every one of those was superseded by the dispersion-aware rewrite and contradicted by
> this document's own tombstone (see "The integration slip is GONE"), which is the authoritative
> section: both sides now run the identical `256/512/65/65` grid, so the expected raw disagreement is
> **float-precision-scale**, not ~0.9%, and the raw guard was tightened `3%` → `0.1%` after being
> re-measured at a worst observed `0.01%`. **Do not restore the old numbers.** `GRID = 20000` and
> `Constants.BestOfNIntegrationNodes = 1024` belong to the retired scheme; the latter is dead code,
> annotated as such at its definition.
>
> The grid equality is asserted rather than inferred from a threshold, and the teeth test is why: a
> one-node drift (`QNodes` 256 → 255) stayed **inside both numeric tolerances** — worst raw 0.03%
> against the 0.1% guard — and was caught only by the equality check.

**Why it exists:** the mod and `envelope_check.py` contain two implementations of the same integral
(custom profiles need a live figure no precomputed table can cover), and the only thing holding them
together used to be a comment saying *"if you change one, change both."* That contract has already
failed. It also compares the scoring constants against a snapshot taken at generation time, so
"changed a constant, forgot to re-run the tool" is caught — otherwise a stale table passes by being
merely self-consistent.

### 2. `Roll pawns and dump distribution`

Generates 50 / 200 / 1000 colonists through the real `PawnGenerator.GeneratePawn` path and dumps
mean / sd / min / p10 / median / p90 / max for per-skill level, per-pawn mean skill, passion pips and
trait count, plus a histogram, the passionless-pawn rate, and a per-resolved-profile `CLAMP
CENSORING` block (share of capable skills pinned at 0 and at 20 — see "The dump action now MEASURES
the censoring" for the baselines and for why the alarm keys off the median rather than the share).

**This is the only place dispersion can be *observed* rather than derived**, and the only place
`Clamp(0, 20)` censoring is visible at all. Hold the reported
per-skill sd against the `per-skill sd` column from `envelope_check.py`: **observed should sit above
predicted**, since the tool models the noise term only while the observed figure also carries the
spread of the quality roll itself. If observed comes in *below* predicted, the noise term is not
reaching pawns and something upstream is clamping it.

Passion pips are priced as the spend loop prices them (Major 1.5, Minor 1) — counting passions
instead would understate any Major-biased profile by a third. Verbose logging is suppressed for the
batch and restored in a `finally`, and throwaway pawns are cleaned up through
`DiscardThrowawayPawn`.

> **This document used to say to expect one vanilla `Tried to discard <pawn> whose state is -1.`
> warning per pawn and to treat it as harmless. Both halves were wrong.** That warning is
> `Verse.Thing.Discard` *refusing to run*: it bails out unless the thing is already destroyed, so
> the bare `Discard(true)` all three of these debug actions ended in was a **no-op** — the cleanup
> the code claimed to perform never happened, and the "harmless" reading is what let it sit. The
> fix is a three-step sequence, and the middle step matters: destroying first clears that refusal
> but hands the pawn to the **world pawn pool**, where `Pawn.Discard` refuses it again ("Tried to
> discard a world pawn"), so destroy-alone is *worse* than the original bug — it converts
> throwaway pawns into retained world pawns, exactly the leak the code was guarding against. See
> the comment on `DiscardThrowawayPawn` for the full sequence. **A clean 1000-pawn run now emits
> neither warning**, verified in game; if either reappears, the cleanup has regressed.

> [!IMPORTANT]
> **Read the `ACTUALLY RESOLVED TO:` line, not the configured one.** It samples player-faction
> colonists, but that does **not** mean it samples the Active Colony Profile — any override on the
> pawn's faction, race or xenotype outranks it. Each pawn is passed through
> `settings.ValuesFor(pawn, request)` (the same call the generation postfix makes) and the resolved
> labels are tallied:
>
> ```
>   configured active profile: Wildcard   hostile profile: Distinct
>   ACTUALLY RESOLVED TO: Faithful x50 (100.0%)
>   ^^ NOT the configured active profile. An override on faction, race or xenotype
>      outranked it, so these figures are Faithful's.
> ```
>
> A **`MIXED SAMPLE`** warning fires instead when more than one profile resolves — the figures then
> average across different profiles and are not a valid reading of any of them.
>
> This exists because the header previously asserted *"overrides are not exercised here"* and that
> assertion was false: on 2026-08-07 a `Human` race override at Normal priority outranked the Active
> Colony Profile, and two consecutive 1000-pawn runs reported `Wildcard` while generating `Faithful`
> pawns. The figures looked plausible; only cross-checking `Wildcard`'s declared 0–8 trait range
> against a reported min 2 / max 3 exposed it. **Both the agreement and the override-wins branches
> are verified in game.** The `MIXED SAMPLE` branch is not — it reads the same tally.

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
  section is not Biotech-gated) a silent log is the intended pass condition — **but `list_logs` is
  not an instrument that can establish silence.** See the startup-blindness box below before
  claiming a clean load.

**Driving the debug actions through the bridge — the working recipe, because two of the obvious
routes fail:**

1. **All six actions declare `PlayingOnMap`, so a map must exist first.** From the main menu they do
   not resolve. `rimworld/start_debug_game_ready` brings up the quick-test colony in ~5 s.
2. **The path is `Actions\<label>`, and the category is NOT in it.** `Varied Pawns/<label>` returns
   *"Could not find debug action"*. Category is metadata on the node; every one of these lives
   directly under the `Actions` tab.
3. **Do not use `rimworld/search_debug_actions`.** It walks the whole tree, and enumerating vanilla's
   incident nodes throws `NullReferenceException` inside
   `Verse.DebugActionsIncidents.RitualSiegeWithSpecifics` — **vanilla's code, nothing to do with this
   mod** — which then raises a *blocking* GABS attention item that must be acknowledged with
   `games_ack_attention` before any further call succeeds. Use
   `rimworld/list_debug_action_children` on `Actions` and filter locally instead.
4. **`Roll pawns and dump distribution` opens a `Dialog_DebugOptionListLister`** (50/200/1000), which
   is a real `Window` and **does** survive `get_ui_layout` + `click_ui_target` — unlike a `FloatMenu`,
   which does not. Grab the layout, click the `button` element at the wanted row.
5. **Read the output from `Player.log`, not from the tool result.** The verify action returns its
   whole report in `effects.logs`, but the dump's does not come back that way.

> [!CAUTION]
> ## Startup is INVISIBLE to both log instruments on this machine — never call a load "clean"
>
> **`rimbridge/list_logs` cannot see the mod-load window at all.** RimBridge's own `logs.initialize`
> lands ~14s into launch, *after* `LoadedModManager.CreateModClasses()`, so anything logged from the
> `Mod` constructor, settings load or a static constructor is structurally absent. An empty result
> means "not observed", not "nothing happened" — it once concealed four `Log.Error`s per launch
> (`No active language! Cannot translate from key VP_Preset_Faithful`, etc.) while the run was
> reported as error-free.
>
> **`Player.log` is the documented fallback, and Prepatcher silences it too.** With
> `zetrith.prepatcher` active — it is in the active mod list — the log stops dead at
> `Prepatcher: Restarted with the patched assembly, going silent` and never grows again, while the
> game goes on to reach the main menu normally. Observed: the file froze at 5,979 bytes with the
> game fully loaded. So the usual advice ("read `Player.log` instead") does **not** hold here.
>
> **Consequences worth internalising:**
> - Do not infer load progress from `Player.log`'s size or mtime, and do not infer it from process
>   RAM or CPU either — a fully-loaded RimWorld sitting at the menu looks identical to a stalled one.
>   Ask the owner, or check for a window title / the bridge port.
> - State which instrument you used and what window it covers, rather than "startup was clean".
> - `Player-prev.log` holds the *previous* run and is often the larger, more complete file — do not
>   mistake it for the current one.
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

# 🔮 NEXT PROJECTS (after this mod)

1. **Guest Room Mod** — designate a room as a guest room. Low room stats satisfy traders poorly
   (drops relations, but lowers perceived wealth → easier raids). High room stats increase trade
   relations and trader frequency, but increase perceived wealth → harder raids.
2. **Perceived Wealth Mod** — decouple storyteller raid scaling from actual stockpile value via a
   dynamic rumor system. Perceived wealth fluctuates on direct observations by escaping raiders,
   visiting traders and radio broadcasts, with rumor decay and suspicion floors for dark zones.
