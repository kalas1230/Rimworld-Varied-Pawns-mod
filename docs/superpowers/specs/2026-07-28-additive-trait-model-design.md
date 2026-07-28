# Additive Trait Model — Design

Date: 2026-07-28
Status: approved, not yet implemented
Supersedes the trait-generation portion of `2026-07-27-pawn-variance-mod-design.md`.

## Problem

`TraitVarianceApplier.Apply` calls `pawn.story.traits.allTraits.Clear()` and rebuilds the
pawn's trait list from scratch. Every trait bug fixed in the last two sessions traces to
that one decision:

1. `PawnKindDef.forcedTraits` wiped with nothing to restore them.
2. Gene-forced traits regranted without `sourceGene`, orphaning them against later gene removal.
3. `BackstoryDef.forcedTraits` (Childhood + Adulthood) wiped.
4. `PawnGenerationRequest.ForcedTraits` wiped.
5. Vanilla's sexuality-trait roll wiped.
6. Vanilla Traits Expanded's `VTE_SlowWorkSpeed` hediff orphaned, causing an intermittent
   `Collection was modified` throw that aborts pawn spawning (see below).
7. Trait-granted abilities leaked, because `Clear()` bypasses the `RemoveAbility` loop in
   `TraitSet.RemoveTrait`.

Reconstruction is unwinnable. It requires enumerating every system in vanilla **and every
loaded mod** that attaches state to a trait. We found four forced-trait sources and believed
we were done; VTE then demonstrated a fifth category living in another assembly, which we
cannot enumerate even in principle.

Per `superpowers:systematic-debugging`, three or more fixes each revealing a new problem in
a different place indicates a wrong architecture rather than a failed hypothesis. We are at
seven.

### The VTE crash, in full

`Pawn_HealthTracker.Notify_Spawned()` enumerates without copying:

```csharp
foreach (Hediff hediff in hediffSet.hediffs)
    hediff.Notify_Spawned();
```

VTE's `Hediff_ForcedWork` overrides `Notify_Spawned()` to call `RecacheData()`, which ends:

```csharp
else {
    base.pawn.health.RemoveHediff((Hediff)(object)this);   // Hediff_ForcedWork.cs:137
}
```

That branch fires when the pawn has neither `VTE_Submissive` nor `VTE_Rebel`.

Chain: VTE's `GainTrait_Patch` postfixes `TraitSet.GainTrait`, so when vanilla's
`GenerateTraits` grants `VTE_Rebel`, VTE attaches `VTE_SlowWorkSpeed`. Our `Clear()` then
removes `VTE_Rebel` **without** routing through `TraitSet.RemoveTrait`, starving VTE's
`RemoveTrait_Patch` (which would have called `RecacheData()` and dropped the hediff safely).
If our reroll does not reproduce Submissive or Rebel, the pawn carries an orphaned hediff
until it spawns — at which point the hediff removes itself mid-enumeration and throws.

This explains every observed property: intermittent (needs the roll-then-lose sequence),
sometimes prevents raid arrival (throw aborts `Pawn.SpawnSetup` under `GenSpawn.Spawn`), and
disappears when VTE is disabled (user-confirmed).

Note: a prior handover recorded `RemoveTrait_Patch` as "ruled out as a contributing factor."
That was backwards — it is precisely the cleanup our raw `Clear()` starves.

## Approach

Reconcile the pawn's existing trait list in place. Never clear it.

Two alternatives were considered and rejected:

**Patch vanilla's own trait count** — philosophically the best fit for this mod ("use vanilla's
systems, change the randomness values"), but structurally impossible. From the decompile:

```csharp
private static readonly IntRange TraitsCountRange = new IntRange(1, 3);                      // line 53
int num = Mathf.Min(GrowthUtility.GrowthMomentAges.Length, TraitsCountRange.RandomInRange);   // line 1496
for (int k = 3; k <= ageBiologicalYears; k++)                                                 // line 1506
    if (GrowthUtility.IsGrowthBirthday(k)) { /* grants exactly one trait */ }
```

The range is `private static readonly`; even overwritten, `Mathf.Min` clamps to
`GrowthMomentAges.Length`, and the loop grants at most one trait per growth birthday. Vanilla
cannot produce more than 3 traits. Reaching the mod's max of 6 would require transpiling one
of the most heavily-patched methods in the game — the exact mod-conflict exposure the
`GenerateNewPawnInternal` retarget was chosen to avoid.

**Scoped reroll** (remove all removable, re-add to target) — more churn and more mod-hook
firing per pawn for no benefit over reconciliation. Rejected by the user.

Trait *selection* continues to delegate to the public `PawnGenerator.GenerateTraitsFor`.
Calling it directly is what allows exceeding 3 traits, since it bypasses the growth-birthday
loop entirely.

## Protected traits

Never removed. These rules are dictated by correctness, not preference.

| Rule | Why |
|---|---|
| `t.sourceGene != null` | `TraitSet.RemoveTrait` calls `pawn.genes.RemoveGene(trait.sourceGene)` — removing the trait deletes the gene. |
| `PawnKindDef.forcedTraits` | Defines the pawn kind. |
| `BackstoryDef.forcedTraits` (Childhood + Adulthood) | Granted directly by vanilla `GenerateTraits`. |
| `PawnGenerationRequest.ForcedTraits` | Caller (quest/scenario/other mod) guaranteed it for this specific call. |
| `t.ScenForced` | Scenario-forced via `ScenPart_ForcedTrait`. |
| `Gay`, **only when** the pawn has a same-gender love or ex-love partner | Vanilla hard-grants this (`GenerateTraits` ~line 1521); removing it leaves an incoherent relationship state. |

Everything else is removable, including modded traits and freely-rolled sexuality traits.

### Sexuality traits are NOT guaranteed by vanilla

An earlier session recorded that vanilla "guarantees every eligible pawn ends up with exactly
one sexuality trait." **This is false**, and the comment asserting it in
`TraitVarianceApplier.cs` (~lines 102-113) must be corrected. `TryGenerateSexualityTraitFor`
builds a weighted table whose first entry is `null`:

```csharp
float second = DefDatabase<TraitDef>.AllDefsListForReading
    .Where(x => !pawn.story.traits.HasTrait(x) && x != Gay && x != Asexual && x != Bisexual)
    .Sum(x => x.GetGenderSpecificCommonality(pawn.gender));
tmpTraitChances.Add(new Pair<TraitDef, float>(null, second));
...
if (tmpTraitChances.TryRandomElementByWeight(x => x.Second, out var result) && result.First != null)
```

The `null` candidate carries the summed commonality of every other trait in the game and
dominates the three sexuality entries. When it wins, the `result.First != null` guard grants
nothing. Heterosexual (no sexuality trait) is the common, correct outcome.

Consequence: the existing call to `PawnGenerator.TryGenerateSexualityTraitFor`
(`TraitVarianceApplier.cs` lines 120-123) **must be deleted**. It is idempotent only when
`HasSexualityTrait` is already true. Today it is roughly harmless because `Clear()` leaves the
pawn with no sexuality trait, so it supplies the single roll. Under the additive model
vanilla's own roll survives, so keeping the call would give every straight pawn a *second*
independent roll and measurably skew the population's sexuality distribution. The
`AllowGay` same-gender-partner block (lines 114-119) is genuinely idempotent but becomes dead
code, since vanilla already granted that trait and the protection rule above preserves it.
Both blocks are removed.

## Algorithm

```
quality      = existing quality roll
protected    = traits matching any protected rule
rolledTarget = clamp(round(lerp(traitCountMin, traitCountMax, quality) + jitter),
                     traitCountMin, traitCountMax)

if pawn has not passed all growth moments:
    rolledTarget = min(rolledTarget, growth birthdays passed)

desiredTotal = countProtectedTraits ? max(protected.Count, rolledTarget)
                                    : protected.Count + rolledTarget

delta = desiredTotal - allTraits.Count
  delta > 0 → GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false), GainTrait each
  delta < 0 → RemoveTrait() |delta| times, chosen uniformly at random from removable only
  delta = 0 → no mutation at all
```

Key properties:

- **`protected.Count` is a hard floor.** At `traitCount = 0` an Yttakin keeps Psychically Dull
  and ends with exactly 1 trait.
- **`delta = 0` touches nothing.** The common case for default settings performs no mutation,
  where today every pawn is torn down and rebuilt.
- **Age gating mirrors vanilla.** A pawn holds at most one trait per growth birthday it has
  passed, counted as `GrowthUtility.GrowthMomentAges.Count(a => a <= AgeBiologicalYears)`. The
  cap stops binding once the pawn has passed *all* growth moments (i.e. `AgeBiologicalYears >=
  GrowthUtility.GrowthMomentAges.Max()`), from which point the flat target applies in full.
  Derive these thresholds from `GrowthUtility.GrowthMomentAges` at runtime; do not hardcode
  ages, since Biotech content or mods may change them.

## Consequences for existing code

The `Capture*` helpers stop being *restoration* code and become *classification* code —
answering "is this trait protected?" instead of "how do I rebuild this trait?" This is a
strictly smaller job and retires a bug class: because no `Trait` object is ever reconstructed,
`FirstValidDegree` and its degree-guessing become unnecessary, so the `PsychicSensitivity`
degree-0 crash cannot recur.

`TraitDesirabilityCache` stays — `TierUtility` still uses it for the cosmetic quality-tier
tooltip.

Fixed as a direct consequence of the pivot:
- VTE orphaned-hediff crash (removal routes through `RemoveTrait`, firing VTE's own cleanup).
- Leaked trait-granted abilities (`RemoveTrait` runs vanilla's `RemoveAbility` loop).
- All four forced-trait-source bugs become structurally impossible rather than individually
  patched.

## Settings

| Setting | Change |
|---|---|
| `countProtectedTraits` | New bool, default `false`. Off: sliders mean "traits this mod rolls" (protected traits are extra). On: sliders mean "total traits on the pawn". |
| `traitCountMin` | Slider floor drops from 1 to 0 so the zero-rolled-traits case is reachable. |
| Trait slider labels | Reworded to match whichever meaning the checkbox selects. |

`ExposeData`, `ResetToDefaults`, and the defaults constants block must all be updated together
— they are the single source of truth for what "default" means, and a stale saved config
silently defaults a new bool to `false` (which here is the intended default anyway).

## Edge cases

- **Fewer removable traits than the drop requires** — remove all removable and stop. The
  protected floor wins and the pawn exceeds target. This generalizes the Yttakin-at-0 case.
- **`GenerateTraitsFor` returns fewer than requested** — it gives up after 500 attempts when
  conflicts exhaust the pool. Accept the shortfall; vanilla logs its own warning.
- **Growth-up path** — add-only, never remove. A pawn reaching adulthood must not silently
  lose a trait the player built a colony role around. `GrowthUpPatch` already never removes,
  so it inherits the age-scaled cap and otherwise stays as-is.
- **Redressed pawns** — unaffected. The `GenerateNewPawnInternal` retarget already keeps this
  mod off the redress path.

## Non-goals

- No "never remove modded traits" escape hatch. `TraitSet.RemoveTrait` is the correct API; a
  mod attaching state on `GainTrait` without handling `RemoveTrait` is buggy on its own terms.
  Special-casing hypothetical broken mods would make trait counts unpredictable whenever a
  large trait pack is loaded, and the new behaviour is strictly better than `Clear()`, which
  fires no hooks at all.
- No quality-driven trait *selection*. Vanilla's picker has no concept of a "better" trait;
  trait variance remains purely about how many traits a pawn gets.
- No retroactive re-rolling of existing pawns.

## Testing

No test harness exists in this repo and RimWorld types cannot be instantiated outside the
game, so verification is in-game.

1. Dev-console raids with VTE enabled — the `Notify_Spawned` "Collection was modified" error
   is gone and raids arrive reliably across repeated spawns.
2. Yttakin at `traitCount = 0` → exactly 1 trait (Psychically Dull).
3. Yttakin at `traitCount = 4` → 5 traits with the checkbox off, 4 with it on.
4. Child pawn → trait count capped by growth birthdays passed, not the flat target.
5. Sexuality distribution across ~20 generated pawns looks vanilla-ish — predominantly
   straight, with no visible excess of Gay/Bisexual/Asexual.
6. A pawn with an ability-granting trait that gets removed → the ability is gone too.
