# Pawn Variance Mod — Design

## Overview

A RimWorld mod that adds configurable, randomized variance to a pawn's skill levels, trait count/quality, and passion assignment at generation time. Each pawn rolls one hidden continuous "quality" value (0-1, Beta-distributed) that smoothly biases all three systems in the same probabilistic direction, without ever guaranteeing that a high-quality roll produces the best possible outcome across all three. There are no discrete tiers in the mechanism — "Incompetent/Standard/Specialist/Prodigy" exist only as computed display labels for a given quality value, never as boundaries that change behavior. Each system also has its own on/off toggle, defaulting on, so users can keep vanilla behavior for any subset of the three, plus its own independent "noise" control governing how loosely that system tracks the shared quality value.

## Goals

- Let users control the distribution via a small, direct settings surface: one "average pawn quality" slider plus one independent "noise" slider per system (skill/trait/passion) — no per-tier fields, no hidden brackets.
- Loosely correlate skill/trait/passion outcomes per pawn without hard-coupling them — a high-quality roll should *usually* skew good everywhere but must retain a real chance of a mediocre or bad outcome in any individual system, and there must be no discontinuity ("seam") between two pawns whose underlying quality rolls are nearly identical.
- Work safely with mods that add/change traits and skills, without hand-maintained knowledge of specific mods' content.
- Be safe to add or remove mid-save with zero save-file corruption risk.
- Default to applying to all humanlike pawns (colonists, prisoners, wanderers, quest pawns, raiders/hostiles) with a toggle to exclude hostile-faction pawns.
- Let users see at a glance, in a pawn's own bio, an approximate color-coded tier readout (Incompetent/Standard/Specialist/Prodigy) — cosmetic only, never affects mechanics.

## Non-goals

- No automated unit test harness (impractical for a Harmony-patched RimWorld mod; verification is manual/in-game, see Testing).
- No custom UI beyond the standard vanilla Mod Settings window and the one bio-text tier label described below (no new dialogs/panels/inspect-string patches beyond that single append).
- No multi-RimWorld-version support (targets current stable 1.5/1.6 only).
- No retroactive re-rolling of pawns generated before the mod was added or while a toggle was off.
- No new TraitDefs, HediffDefs, or other content Defs — the mod only reweights selection among already-loaded Defs (vanilla + whatever mods are active).

## Architecture

Standard Harmony-patched RimWorld mod, single patch point:

- `PawnVarianceMod : Mod` — entry point; loads settings, draws the Mod Settings window.
- `PawnVarianceSettings : ModSettings` — all configurable values; `ExposeData` for persistence in mod config (not the save file).
- `HarmonyPatches` — one postfix on `PawnGenerator.GeneratePawn`.
- `QualityRoller` — rolls one continuous `quality ∈ [0,1]` per pawn from a Beta distribution parameterized by the "average pawn quality" slider and a fixed internal population-spread constant (recomputed only when the mean slider changes, not per pawn).
- `SkillVarianceApplier` — for each skill, computes a smooth quality-derived baseline shift plus an independent per-skill noise offset (scaled by the skill noise slider), additive on top of vanilla-generated levels, clamped to [0, 20].
- `TraitVarianceApplier` — computes trait count as a smooth function of quality; selects traits via continuous weighted sampling against a quality-derived target desirability score, with the sampling `spread` parameter widened/narrowed by the trait noise slider; respects vanilla trait conflict/exclusion checks.
- `PassionVarianceApplier` — computes passion count as a smooth function of quality; places passions weighted by each skill's *final absolute level* (post-variance if skills enabled, vanilla level if not, so it has no dependency on the skill toggle's state), with weighting "peakedness" controlled by the passion noise slider.
- `TraitDesirabilityCache` — `[StaticConstructorOnStartup]` static cache: for every loaded `TraitDef`+degree, computes a heuristic desirability score once from its actual effects (skill offsets, stat offsets/factors, work-tag disables, social effects) — no hand-curated per-trait table, works identically for vanilla and modded traits, requires zero maintenance as trait mods are added/updated/removed.
- `TierLabelPatch` — separate, small Harmony postfix appending a color-coded tier label to the pawn's bio/description text (see "Tier bio label" below). Deliberately a second, independent patch rather than folded into `HarmonyPatches`' `GeneratePawn` postfix, since it fires on a different method at a different time (description text is generated on-demand when the character info panel is opened, not at pawn-generation time) and has nothing to do with mutating pawn stats.

### Per-pawn flow

1. Vanilla `GeneratePawn` completes fully (all vanilla skills/traits/passions generated normally). **Unverified assumption, flagged for implementation-time confirmation** (requires checking the target RimWorld version's decompiled `PawnGenerator`, not just this spec): (a) `GeneratePawn` is known historically to internally retry/regenerate a candidate pawn multiple times before returning a single validated result — if still true, the postfix (and quality roll/RNG draws) may fire more than once per logical pawn, which only matters for RNG-draw-count bookkeeping, not correctness, since discarded candidates are simply discarded; (b) whether quest-reward/trader-redress pawn flows route back through this same method or a separate path that skips it is unconfirmed — if the latter, some of the pawn categories Goals promises variance for (quest pawns, traders) could silently bypass the patch and need a second patch point.
2. Postfix fires. Early-exit immediately if: pawn is not `RaceProps.Humanlike`, or all three toggles are off, or (`applyToHostilePawns` is off and `pawn.Faction?.HostileTo(Faction.OfPlayer) == true`).
3. `QualityRoller` rolls one continuous `quality` value for this pawn.
4. Each enabled applier (skill/trait/passion) runs independently, using the shared `quality` value as a smooth bias input but rolling its own independent noise (scaled by its own noise slider) — no system's outcome determines another's, and no hard boundary exists anywhere in the mapping from quality to output.
5. Entire postfix body wrapped in try/catch; any exception is logged and vanilla generation is left untouched (fail-safe to vanilla, never to a broken/partial state). Logging uses `Log.ErrorOnce` keyed by exception type+stack trace (not a single global "log the very first failure ever" flag) — a *systematic* bug (e.g. a null-ref triggered by one specific modded trait) must keep surfacing distinctly rather than silently going quiet after pawn #1, which would otherwise let the Testing Plan's manual distribution check pass by misreading "no variance happening" as "noise slider is just high." A `verboseLogging` dev-mode setting (default off) additionally rethrows instead of catching, for use during manual testing.

## Core Algorithms

### Quality roll
Roll one `quality ∈ [0,1]` per pawn from a Beta distribution. The Beta distribution's shape parameters (`α`, `β`) are derived from the "average pawn quality" slider (target mean) and a fixed internal population-spread constant (not user-exposed — see Settings Schema for rationale). Recomputing `α`/`β` happens only when the mean slider changes, not per pawn. Beta is chosen over a clamped normal distribution specifically because it's naturally bounded to [0,1] with no clamping artifacts distorting the tails.

**Sampling implementation note**: there is no built-in Beta sampler in .NET or RimWorld's `Rand`. Standard construction: `Beta(α,β) = X/(X+Y)` where `X~Gamma(α,1)`, `Y~Gamma(β,1)`, using the Marsaglia-Tsang method for Gamma sampling (shape ≥ 1) with the Ahrens-Dieter boost correction (sample at shape+1, then multiply by `U^(1/shape)`) for the shape < 1 case — which will occur routinely here, since an extreme average-quality slider position combined with the fixed population-spread constant can push `α` or `β` below 1. This is a small, well-documented numerical routine, not a one-line `Rand` call — flagging explicitly so it isn't underestimated during implementation.

There is no tier lookup anywhere — `quality` flows directly into the three appliers below as a continuous number.

### Trait desirability scoring (`TraitDesirabilityCache`)
For each loaded `TraitDef`+degree, compute a single scalar score by summing independently-normalized contributions from each effect category, so incommensurable units (skill points, stat percentages, boolean work-tag disables, social multipliers) don't get combined as raw numbers:
1. **Skill offsets**: sum the trait's skill-level offsets, normalized by dividing by a fixed reference magnitude (e.g. ±6, RimWorld's largest vanilla skill offset) so this contribution lands roughly in [-1, 1].
2. **Stat offsets/factors**: for each affected stat, convert a factor to a percent-deviation-from-1.0 (`factor - 1`) and treat offsets as already percent-like; sum, normalize by a fixed reference magnitude (e.g. ±1.0) to land roughly in [-1, 1].
3. **Work-tag disables**: a small fixed negative penalty per disabled work tag (disabling capability is close to universally negative for a colonist).
4. **Social effects**: normalize opinion/social-fight-chance modifiers the same percent-deviation way as stats.
Sum the four normalized contributions (unweighted sum — no category is treated as inherently more important than another, since a modded trait mix could concentrate its whole effect in just one category and still needs a meaningful score). This produces an unbounded-but-typically-small real number per trait; `observedMinScore`/`observedMaxScore` (Settings Schema) are simply the min/max of this raw score across all currently-loaded traits, so absolute scale doesn't matter — only the relative ordering and spread across whatever's loaded.

### Skill variance (if enabled)
For each skill independently:
1. Compute a smooth baseline shift: `baseline = lerp(globalMinShift, globalMaxShift, quality)` across the mod's configured global shift range (e.g. -4 to +8).
2. Add an independent per-skill noise offset, rolled from a triangular distribution (average of two uniform rolls, so it clusters near 0 rather than being flat — same "gradual, not boxed" rationale as the rest of the design) over `[-magnitude, +magnitude]`, where `magnitude = lerp(minMagnitudeFloor, maxMagnitude, skillNoiseSlider)` with `minMagnitudeFloor` a small nonzero constant (not exactly 0, for the same reason trait `spread` and passion `temperature` have nonzero floors — see below).
3. Add the result to the vanilla-generated level, clamp to [0, 20].

Store final per-skill levels for use by passion placement. No tier brackets exist, so there is no discontinuity between any two quality values, however close.

### Trait variance (if enabled)
1. Compute trait count as a smooth function of quality (e.g. `round(lerp(minCount, maxCount, quality) + smallRandomJitter)`), clamped to sane bounds.
2. Clear vanilla-generated traits.
3. Compute a quality-derived target desirability score: `target = lerp(observedMinScore, observedMaxScore, quality)`, where the observed bounds come from `TraitDesirabilityCache`'s startup scan of currently-loaded traits (see Settings Schema) — not a fixed constant.
4. Repeatedly sample from all eligible loaded traits using continuous weighted sampling: `weight = exp(-(score - target)² / spread)`, where `spread = lerp(minSpreadFloor, maxSpread, traitNoiseSlider)` with `minSpreadFloor > 0` (never exactly 0 — see Trait/Passion noise-floor setting below; a zero spread would produce a NaN/degenerate weight array and collapse to a hard argmax cutoff, which would violate the "no hard cutoffs" pillar). Only the **mod-list-dependent** parts are cached: the sorted list of `(TraitDef, score)` pairs and `observedMinScore`/`observedMaxScore`, rebuilt when the loaded mod list changes. `target` and the resulting weight vector/cumulative sum are **per-pawn** (since `target` depends on that pawn's own `quality` roll) and are recomputed fresh for each pawn — this is a cheap O(number of loaded traits) pass, not something that can be precomputed once.
5. Skip any candidate that conflicts with already-picked traits. This is reimplemented against `TraitDef`'s public `conflictingTraits`/`exclusionTags` fields and `TraitSet.HasTrait`, mirroring vanilla's structural conflict rules directly rather than reflecting into vanilla's private generator method (which isn't a stable public API to depend on). If eligible candidates run out before the rolled count is filled, stop early with a `Log.Message` (expected with small trait pools, not an error).

This weighting is deliberately probabilistic, not a hard cutoff — a high-quality pawn can still roll a mediocre or bad trait, just less often than a low-quality pawn would, and the trait noise slider lets users directly control how often that happens. The per-pawn weight-array cost scales with the number of currently-loaded traits (low hundreds to low thousands of `TraitDef`s even with large trait-expansion modlists) — expected to stay cheap relative to the rest of pawn generation, but noted here since it isn't a fixed O(1) cost.

### Passion variance (if enabled)
1. Compute passion count as a smooth function of quality, same pattern as trait count.
2. Build a weighted list of the pawn's skills using each skill's final absolute level (from the skill applier if enabled, vanilla level otherwise — chosen specifically so passion placement never needs a fallback branch for the skill-toggle-off case).
3. Weight each skill via softmax: `weight_i = exp(level_i / temperature)`, where `temperature = lerp(minTemperatureFloor, maxTemperature, passionNoiseSlider)` with `minTemperatureFloor > 0` (same non-zero-floor rationale as trait `spread` above — a zero temperature would collapse to a hard "always pick the single best skill" cutoff). Low temperature sharply favors the pawn's highest-level skills; high temperature approaches uniform weighting regardless of level.
4. Sample without replacement for placement; assign Minor/Major passion using vanilla's own ratio logic.

### Tier bio label
Purely cosmetic — has zero influence on any mechanic above. `TierLabelPatch` postfixes the description-generation method to append `"\n\n<color=#RRGGBB>{TierName}</color>"` to a pawn's bio text. Default color mapping: Incompetent = red (`#B33A3A`), Standard = white/default (no color tag), Specialist = blue (`#3A7CB3`), Prodigy = gold (`#D4AF37`) — not user-configurable in v1 (YAGNI; colors can become settings later if requested).

**Storage note, consistent with the Save-Game Safety rule**: the original per-pawn `quality` roll is never stored anywhere (no new field on `Pawn`, no `ThingComp` — that would violate "zero custom save-file footprint"). Instead, `TierLabelPatch` computes a **display-only "effective quality" approximation live, every time the description is requested**, by reading the pawn's *current* already-vanilla-saved data (e.g. average skill level relative to the configured shift range, average trait desirability score of the pawn's actual current traits) rather than recalling the original roll. Two consequences worth being explicit about, both acceptable: (1) nothing new is ever written to save data, by construction — the label is a pure read-time UI computation, not stored state; (2) the displayed tier reflects the pawn's *current* stats, so if a player later edits the pawn via dev tools or another mod changes their skills, the label updates accordingly rather than staying frozen to a historical roll — this is a reasonable, arguably more honest semantic ("this is how good this pawn currently is") rather than a bug.

**Implementation-time verification flagged**: the exact method that generates a pawn's bio-tab description text needs confirming against the target RimWorld version's decompiled source before implementation (naming it precisely here would be asserting unverified certainty, consistent with the similar caveat already flagged for `GeneratePawn`'s call sites above).

## Settings Schema

Persisted via `PawnVarianceSettings.ExposeData()` in mod config (never in save files):

- **Average pawn quality** slider (0-1, default 0.5): the mean of the shared Beta-distributed quality roll. This controls the *population's average outcome*, not correlation strength — cross-system correlation comes from all three appliers consuming the same per-pawn `quality` roll (see Per-pawn flow), which happens regardless of where this slider is set. Correlation *strength* is instead controlled by the three noise sliders below (lower noise = tighter correlation to the shared roll).
- **Skill noise** slider (0-1, default 0.35): maps to the per-skill triangular-noise `magnitude` via `lerp(minMagnitudeFloor, maxMagnitude, slider)`, same named-constant/nonzero-floor pattern as trait and passion noise. Nonzero by default so quality is a *bias*, never a deterministic outcome — this is what guarantees a high-quality pawn can still roll a mediocre skill.
- **Trait noise** slider (0-1, default 0.35): maps to the trait weighted-sampling `spread` via `lerp(minSpreadFloor, maxSpread, slider)`. `minSpreadFloor` and `maxSpread` are named constants with sane defaults (not separately user-exposed — see rationale below). Nonzero-floor by default so trait quality is never a deterministic function of `target`.
- **Passion noise** slider (0-1, default 0.35): maps to the passion-weighting `temperature` via `lerp(minTemperatureFloor, maxTemperature, slider)`, same named-constant/nonzero-floor pattern as trait noise.
- **Skill shift range** (min/max, default -4 to +8): what quality=0 and quality=1 map to as the skill baseline shift. User-configurable — this is the direct replacement for the old per-tier shift brackets, now expressed as the single global range the continuous curve spans.
- **Trait count range** (min/max, default 1 to 6): what quality=0 and quality=1 map to as trait count. The `smallRandomJitter` added on top (Core Algorithms) is a small fixed-magnitude named constant, not separately user-exposed — it only smooths the count roll's rounding, it isn't a tunable "feel" knob the way the noise sliders are.
- **Passion count range** (min/max, default matching vanilla's typical 0-3): what quality=0 and quality=1 map to as passion count. Same jitter-constant note as trait count.
- **Trait target score range**: *not* a user-editable setting — auto-derived at startup as the observed min/max desirability score across all currently-loaded `TraitDef`s (via `TraitDesirabilityCache`). This keeps quality=0/1 meaningfully calibrated to whatever trait mods are actually loaded, rather than a fixed constant that could be miscalibrated for an unusually mild or extreme modded trait pool. Directly serves the "works safely with any trait mod" goal.
- The Beta distribution's population-spread parameter (how much pawns vary in underlying quality), and the `minMagnitudeFloor`/`maxMagnitude`/`minSpreadFloor`/`maxSpread`/`minTemperatureFloor`/`maxTemperature`/`smallRandomJitter` constants above, are **fixed internal constants, not user-exposed** — deliberately deferred (YAGNI) since the settings above already cover the tuning power most users will reach for; exposing any of them as additional sliders is a plausible future addition if requested.
- `enableTraitVariance`, `enableSkillVariance`, `enablePassionVariance` — default on.
- `applyToHostilePawns` — default on.
- Tier display thresholds (effective-quality-approximation cutoffs for "reads as Incompetent/Standard/Specialist/Prodigy") — cosmetic only, never affect mechanics. Used in two places: (1) a live readout inside the Mod Settings window itself (e.g. "Average pawn currently reads as: Specialist" next to the average-quality slider), and (2) the color-coded tier label `TierLabelPatch` appends to a pawn's bio text (see Core Algorithms > Tier bio label). This is the one deliberate, narrowly-scoped exception to "no custom UI beyond Mod Settings" (see updated Non-goals).

## Edge Cases

1. Average quality slider or any noise slider set to an out-of-range value (e.g. hand-edited config XML) → clamped to [0,1] on load rather than throwing.
2. Trait/passion sampler runs out of eligible candidates → under-fill gracefully, log once at message verbosity.
3. All three toggles off → early-exit before the quality roll even happens; pawn is 100% vanilla.
4. Non-humanlike pawns → always skipped, unconditionally.
5. Hostile-pawn check uses `pawn.Faction?.HostileTo(Faction.OfPlayer)`, null-guarded (faction-less pawns default to "apply").
6. Mod list changes between saves (trait mod added/removed) → desirability cache and trait weight tables are rebuilt fresh at each startup from currently-loaded Defs; nothing stale persists.
7. Harmony patch throws for any reason → caught, logged via `Log.ErrorOnce` keyed by exception type+stack (so a recurring systematic bug keeps surfacing rather than going silent after the first pawn), vanilla pawn stands untouched. `verboseLogging` dev setting rethrows instead, for manual testing.
8. Noise slider at 0 for a system → its `spread`/`temperature` sits at the nonzero floor constant, not exactly 0 (see Core Algorithms) — output is close to deterministic-by-quality but never a hard cutoff.
9. A min/max range setting (skill shift, trait count, passion count) is hand-edited so min > max → swapped on load rather than silently inverting the quality→outcome relationship.
10. Settings change detection: `PawnVarianceSettings` marks derived caches (Beta `α`/`β`, trait score bounds) dirty whenever `Write()` fires from the Mod Settings window (RimWorld's settings UI updates live, with no explicit "Apply" step) — caches are lazily rebuilt on next access after being marked dirty, not on a fixed schedule, so slider changes take effect without requiring a restart.

## Save-Game Safety

Hard architectural rule: **the mod adds nothing to the save file** — no `GameComponent`, `WorldComponent`, `ThingComp`, or custom `Scribe`-saved types, and no new Defs. Settings live in mod config XML, not the save. Per-pawn effects are written through vanilla-typed fields (`pawn.story.traits`, `pawn.skills`, passion fields) that RimWorld already saves regardless of this mod's presence.

Consequences, documented as intended behavior:
- **Add mid-save**: only pawns generated after this point get variance; existing pawns are untouched (no retroactive rerolling).
- **Remove mid-save**: the Harmony patch stops applying; new pawns generate as pure vanilla; existing pawns' already-saved traits/skills/passions remain intact with no Scribe errors, since none of that data ever depended on this mod's assembly.

This rule constrains all future feature work on this mod: no persistent custom save-file state, ever.

## Testing Plan

Manual/in-game verification (no automated harness — impractical for a Harmony-patched game mod):

1. **Distribution smoke test**: generate ~30-50 pawns across starting pawns/wanderers/prisoners, confirm quality/skill/trait/passion distributions roughly track the configured average-quality slider, with no visible clustering/discontinuity around any particular quality value.
2. **Noise slider sweep**: for each of the 3 noise sliders independently, verify 0 produces a purely deterministic quality-driven outcome and higher values visibly loosen that system's correlation with the shared quality roll.
3. **Toggle matrix**: manually verify all 16 combinations of the 4 toggles (skill/trait/passion/`applyToHostilePawns`), especially passion placement when skill variance is off and hostile-pawn generation when `applyToHostilePawns` is off (spawn a raid, confirm raiders stay vanilla).
4. **Mid-save add/remove + mod-list change**: save with mod active → remove mod → reload (confirm no Scribe errors, existing pawns intact) → re-add (confirm new pawns get variance again) → separately, save with a trait mod loaded, remove that trait mod, reload, generate new pawns (confirm desirability cache rebuilds cleanly against the smaller trait pool with no stale references).
5. **Extreme settings**: average quality at 0 and 1, all noise sliders at 0 and 1 — confirm no crashes or infinite sampling loops.
6. **Compatibility smoke test**: run with a popular trait-expansion mod loaded; confirm modded traits enter the weighted pool with sane-looking desirability scores.
7. **Config tampering**: hand-edit the mod's config XML with out-of-range slider values and an inverted min/max range; confirm both are clamped/swapped on load rather than throwing.
8. **Non-humanlike check**: generate/spawn animals; confirm they're untouched regardless of settings.
9. **Tier bio label**: open several pawns' bio/character tab, confirm the color-coded label appears, matches roughly what their actual stats suggest, and never appears for non-humanlike pawns; edit a pawn's skills via dev tools and reopen the bio tab, confirm the label updates to reflect current stats rather than staying frozen.
