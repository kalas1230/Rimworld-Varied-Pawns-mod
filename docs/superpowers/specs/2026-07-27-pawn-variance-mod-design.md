# Pawn Variance Mod — Design

## Overview

A RimWorld mod that adds configurable, randomized variance to a pawn's skill levels, trait count/quality, and passion assignment at generation time. Each pawn rolls one hidden "archetype tier" (Incompetent, Standard, Specialist, Prodigy — weights and ranges fully user-configurable) that biases all three systems in the same probabilistic direction, without ever guaranteeing that a high-tier roll produces the best possible outcome across all three. Each system also has its own on/off toggle, defaulting on, so users can keep vanilla behavior for any subset of the three.

## Goals

- Let users reconfigure the default archetype table (tier weights, trait count ranges, skill shift ranges) via both raw per-tier fields and a quick "rarity" slider.
- Loosely correlate skill/trait/passion outcomes per pawn without hard-coupling them — a top-tier roll should *usually* skew good everywhere but must retain a real chance of a mediocre or bad outcome in any individual system.
- Work safely with mods that add/change traits and skills, without hand-maintained knowledge of specific mods' content.
- Be safe to add or remove mid-save with zero save-file corruption risk.
- Default to applying to all humanlike pawns (colonists, prisoners, wanderers, quest pawns, raiders/hostiles) with a toggle to exclude hostile-faction pawns.

## Non-goals

- No automated unit test harness (impractical for a Harmony-patched RimWorld mod; verification is manual/in-game, see Testing).
- No custom UI beyond the standard vanilla Mod Settings window.
- No multi-RimWorld-version support (targets current stable 1.5/1.6 only).
- No retroactive re-rolling of pawns generated before the mod was added or while a toggle was off.
- No new TraitDefs, HediffDefs, or other content Defs — the mod only reweights selection among already-loaded Defs (vanilla + whatever mods are active).

## Architecture

Standard Harmony-patched RimWorld mod, single patch point:

- `PawnVarianceMod : Mod` — entry point; loads settings, draws the Mod Settings window.
- `PawnVarianceSettings : ModSettings` — all configurable values; `ExposeData` for persistence in mod config (not the save file).
- `HarmonyPatches` — one postfix on `PawnGenerator.GeneratePawn`.
- `ArchetypeRoller` — rolls a tier for a pawn using precomputed cumulative weight thresholds (recomputed only when settings change, not per pawn).
- `SkillVarianceApplier` — applies independent per-skill jitter (triangular distribution) within the rolled tier's shift range, additive on top of vanilla-generated levels, clamped to [0, 20].
- `TraitVarianceApplier` — rolls trait count within the tier's range; selects traits via continuous weighted sampling against a startup-computed desirability score, respecting vanilla trait conflict/exclusion checks.
- `PassionVarianceApplier` — rolls passion count within the tier's range; places passions weighted by each skill's *final absolute level* (post-variance if skills enabled, vanilla level if not), so it has no dependency on the skill toggle's state.
- `TraitDesirabilityCache` — `[StaticConstructorOnStartup]` static cache: for every loaded `TraitDef`+degree, computes a heuristic desirability score once from its actual effects (skill offsets, stat offsets/factors, work-tag disables, social effects) — no hand-curated per-trait table, works identically for vanilla and modded traits, requires zero maintenance as trait mods are added/updated/removed.

### Per-pawn flow

1. Vanilla `GeneratePawn` completes fully (all vanilla skills/traits/passions generated normally).
2. Postfix fires. Early-exit immediately if: pawn is not `RaceProps.Humanlike`, or all three toggles are off, or (`applyToHostilePawns` is off and `pawn.Faction?.HostileTo(Faction.OfPlayer) == true`).
3. `ArchetypeRoller` rolls one tier for this pawn.
4. Each enabled applier (skill/trait/passion) runs independently, using the shared tier as a bias input but rolling its own independent randomness — no system's outcome determines another's.
5. Entire postfix body wrapped in try/catch; any exception is logged once and vanilla generation is left untouched (fail-safe to vanilla, never to a broken/partial state).

## Core Algorithms

### Archetype roll
Given 4 tier weights (normalized to sum to 100 on settings load/save) and the rarity-slider-adjusted thresholds, roll one `float` in [0,100) against a precomputed cumulative-threshold array. Result: one tier + its skill shift range, trait count range, passion count range.

### Skill variance (if enabled)
For each skill, roll one independent value within the tier's shift range using a triangular distribution (average of two uniform rolls) so results cluster near the tier midpoint rather than being flat — same min/max settings fields as a uniform roll would need, better-feeling distribution. Add to vanilla-generated level, clamp to [0, 20]. Store final per-skill levels for use by passion placement.

### Trait variance (if enabled)
Roll trait count within the tier's range. Clear vanilla-generated traits. Repeatedly sample from all eligible loaded traits using continuous weighted sampling: `weight = exp(-(score - tierTarget)² / spread)`, precomputed once per tier as a cumulative array whenever settings or the loaded mod list change (not per pawn), sampled per pick via binary search. Skip any candidate that conflicts with already-picked traits via vanilla's own conflict/exclusion checks. If eligible candidates run out before the rolled count is filled, stop early with a `Log.Message` (not a warning — expected with small trait pools) rather than looping or erroring. This weighting is deliberately probabilistic, not a hard cutoff — a Prodigy-tier pawn can still roll a mediocre or bad trait, just less often than a Standard-tier pawn would.

### Passion variance (if enabled)
Roll passion count within the tier's range. Build a weighted list of the pawn's skills using each skill's final absolute level (from step above if skill variance is enabled, vanilla level otherwise — this basis is chosen specifically so passion placement never needs a fallback branch for the skill-toggle-off case). Sample without replacement for placement; assign Minor/Major passion using vanilla's own ratio logic.

## Settings Schema

Persisted via `PawnVarianceSettings.ExposeData()` in mod config (never in save files):

- Per tier (Incompetent / Standard / Specialist / Prodigy): weight %, trait count min/max, skill shift min/max.
- Rarity slider (0-100): quick-adjusts top-tier weights, bidirectionally kept in sync with the raw per-tier % fields.
- `enableTraitVariance`, `enableSkillVariance`, `enablePassionVariance` — default on.
- `applyToHostilePawns` — default on.

## Edge Cases

1. Tier weights don't sum to 100 → normalized proportionally on load/save, never an error.
2. Trait/passion sampler runs out of eligible candidates → under-fill gracefully, log once at message verbosity.
3. All three toggles off → early-exit before archetype roll; pawn is 100% vanilla.
4. Non-humanlike pawns → always skipped, unconditionally.
5. Hostile-pawn check uses `pawn.Faction?.HostileTo(Faction.OfPlayer)`, null-guarded (faction-less pawns default to "apply").
6. Mod list changes between saves (trait mod added/removed) → desirability cache and weight thresholds are rebuilt fresh at each startup from currently-loaded Defs; nothing stale persists.
7. Harmony patch throws for any reason → caught, logged once, vanilla pawn stands untouched.
8. Malformed/out-of-range settings (e.g. hand-edited config XML) → clamped/normalized on load rather than throwing.

## Save-Game Safety

Hard architectural rule: **the mod adds nothing to the save file** — no `GameComponent`, `WorldComponent`, `ThingComp`, or custom `Scribe`-saved types, and no new Defs. Settings live in mod config XML, not the save. Per-pawn effects are written through vanilla-typed fields (`pawn.story.traits`, `pawn.skills`, passion fields) that RimWorld already saves regardless of this mod's presence.

Consequences, documented as intended behavior:
- **Add mid-save**: only pawns generated after this point get variance; existing pawns are untouched (no retroactive rerolling).
- **Remove mid-save**: the Harmony patch stops applying; new pawns generate as pure vanilla; existing pawns' already-saved traits/skills/passions remain intact with no Scribe errors, since none of that data ever depended on this mod's assembly.

This rule constrains all future feature work on this mod: no persistent custom save-file state, ever.

## Testing Plan

Manual/in-game verification (no automated harness — impractical for a Harmony-patched game mod):

1. **Distribution smoke test**: generate ~30-50 pawns across starting pawns/wanderers/prisoners, confirm tier/skill/trait/passion distributions roughly track configured weights.
2. **Toggle matrix**: manually verify all 8 combinations of the 3 toggles, especially passion placement when skill variance is off.
3. **Mid-save add/remove**: save with mod active → remove mod → reload (confirm no Scribe errors, existing pawns intact) → re-add (confirm new pawns get variance again).
4. **Extreme settings**: rarity slider at 0 and 100, a tier weight at 0%, trait count range at its max — confirm no crashes or infinite sampling loops.
5. **Compatibility smoke test**: run with a popular trait-expansion mod loaded; confirm modded traits enter the weighted pool with sane-looking desirability scores.
