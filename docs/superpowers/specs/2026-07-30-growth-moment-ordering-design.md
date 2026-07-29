# Growth-moment ordering — design

Date: 2026-07-30
Status: approved, not yet implemented
Supersedes: the grow-up ordering behaviour described in
`2026-07-28-additive-trait-model-design.md` (that spec's trait model is otherwise unchanged)

## Problem

At age 13 a child hits their last growth moment and becomes an adult in the same birthday. Vanilla
grants a trait and 0–4 passion increments for that growth moment. This mod's grow-up variance pass
runs **before** that grant lands, so it can neither count it nor leave room for it.

Tick ordering, from decompiled `Pawn_AgeTracker.AgeTickInterval`:

```
1. if (lifeStageChange) PostResolveLifeStageChange()   <- our grow-up pass
2. TickBiologicalAge(delta)
...
4. if (year rolled over) BirthdayBiological(...)       <- sets lifeStageChange, sends the letter
```

On the tick the pawn turns 13, step 4 sends the growth-moment letter and sets the flag. Our pass
runs at step 1 of the **next** interval. The player resolves the letter later still, via
`ChoiceLetter_GrowthMoment.MakeChoices`.

Result: every child who grows up ends at *our full budget + 1 growth-moment trait + 1–2 passion
increments*. `alreadyCommittedPips` reads 0 for the age-13 grant, and the trait target never
accounts for its trait. The sliders systematically under-describe grown-up children by about one
trait.

Confirmed empirically in `temp/temp_log_output_child_test.md` (15 grow-ups, 2026-07-29).

## Decision

The trait/passion sliders describe **what the pawn ends up with**, and we get there by
**observing** — running our pass after the growth moment resolves, so we see exactly what it gave
rather than predicting it.

Rejected alternatives:

- **Predict and reserve** — ask `TryChildGrowthMoment` what the grant will be and subtract it from
  the budget up front. No ordering dependency and no new persistence, but it predicts: the player
  may pick the "no trait" option (`ChanceForNoTraitOption = 0.35f` when `traitChoiceCount > 2`),
  leaving the pawn one under target.
- **Document and accept** — treat the growth moment as a vanilla bonus on top, the way forced and
  xenotype traits already are. Zero risk, but leaves the sliders misleading.

## Constraints established by investigation

Each of these was verified against decompiled source or a scan of the installed mod set, not
assumed.

1. **`ChoiceLetter_GrowthMoment.MakeChoices` is the single resolution point.** It is public,
   increments the chosen passions, calls `GainTrait` plus
   `TraitUtility.ApplySkillGainFromTrait`, and at exactly age 13 also runs
   `PawnGenerator.TryGenerateSexualityTraitFor(pawn, allowGay: true)`.

2. **Nothing installed touches the letter.** A scan of all 512 mod assemblies found zero
   references to `ChoiceLetter_GrowthMoment` or `MakeChoices`. The hook is uncontested.
   (Method names live in the UTF-8 `#Strings` metadata heap, not the UTF-16 `#US` literal heap —
   scan accordingly, or the result is a false clean.)

3. **`RemoveTrait` is the real collision surface.** Vanilla Traits Expanded patches it, VEF
   references it, WantsAndQuirks calls it from a reward worker. The 2026-07-28 spawn crash came
   from `allTraits.Clear()` *bypassing* `RemoveTrait` and orphaning VTE's `VTE_SlowWorkSpeed`
   hediff — calling it is the safe path, skipping it was the bug. This design stays add-only and
   therefore never engages that surface at all.

4. **Vanilla forces the choice at the deadline.** The letter carries `StartTimeout(120000)` —
   2 in-game days — but `LetterWithTimeout.ShouldAutomaticallyOpenLetter => LastTickBeforeTimeout`
   force-opens the dialog on the last tick, with `forcePause` and `absorbInputAroundWindow` set.
   `Dialog_GrowthMomentChoices` refuses "Later" at that point
   (`MessageCannotPostponeGrowthMoment`) and sets `closeOnAccept`/`closeOnCancel` to false until
   every choice is made. So "player ignores the letter forever" is not a reachable state. The
   fallback below is edge-case insurance — chiefly pawn death and third-party letter removal —
   not the primary path.

5. **Some pawns never get a letter.** In `BirthdayBiological`, pawns failing
   `ShouldSendNotificationAbout`, or not of `Faction.OfPlayer`, or `IsQuestLodger`, get the grant
   auto-applied silently and inline. A player colonist at a low growth tier with no new work types
   and no passion/trait options gets neither letter nor grant.

6. **Incidental:** WantsAndQuirks patches `SkillRecord.Level`. This independently vindicates the
   passion queue ordering by `GetLevel(includeAptitudes: false)` — it dodges a third-party
   modifier as well as Biotech aptitude genes. No action needed; recorded so a future reader does
   not "simplify" that call back to `.Level`.

## Design

### Component 1: `GrowUpVariance` — the shared apply path

Extract the body currently inline in `DevelopmentalStage_Postfix.Postfix` into a single entry
point, so all three trigger paths converge on identical behaviour:

```
GrowUpVariance.Apply(Pawn pawn)
  roll quality once
  if enableTraitVariance   -> ApplyTraitGrowthUp(pawn, quality)
  if enableSkillVariance   -> ApplySkillGrowthUp(pawn, quality)
  if enablePassionVariance -> ApplyPassionGrowthUp(pawn, quality)
```

Ordering (trait → skill → passion) is unchanged and still load-bearing: trait variance can disable
work tags, which passion placement's `TotallyDisabled` exclusion depends on.

The **whole** pass defers together rather than only traits and passions. `MakeChoices` calls
`TraitUtility.ApplySkillGainFromTrait`, so a chosen growth trait can grant skill levels; running
our additive shift afterwards builds on the real post-growth base and keeps one quality roll and
one trace per pawn.

### Component 2: `GrowUpPendingComponent` — the pending set

A `GameComponent` holding the set of pawns that have become adult but whose growth moment has not
yet resolved.

- **Scribed**, unlike the session-only `LastKnownStage` dictionary. A letter survives save/load and
  can sit for two in-game days, so the pending state must too. A `GameComponent` is per-save, so
  the cross-save `thingIDNumber` collision hazard that forced `LastKnownStage` to clear on load
  does not apply.
- Stores pawn references plus the tick each pawn was registered. Quality is rolled at apply time, so
  nothing else needs persisting; the timestamp exists only to serve Component 7's "how long the pawn
  sat pending" diagnostic, and must survive save/load for the same reason the pawn reference does.
- `GameComponentTick` sweeps every 2500 ticks. For each pending pawn:
  - dead, destroyed or null → deregister, do not apply
  - no unresolved growth letter remains for it → apply, deregister
  - otherwise → leave pending

The sweep's real job is **cleanup when the pawn dies or is otherwise lost** while pending. The
"letter vanished unresolved" case is near-unreachable, since vanilla force-opens the dialog at the
deadline and refuses to let it close unchosen (constraint 4) — it is covered by a one-line
condition rather than dedicated machinery, and is not worth designing around further.

"No unresolved growth letter remains" means: no `ChoiceLetter_GrowthMoment` in
`Find.LetterStack.LettersListForReading` with `letter.pawn == pawn && !letter.choiceMade &&
!letter.TimeoutPassed`.

### Component 3: registration at the life-stage change

`DevelopmentalStage_Postfix` keeps its existing genuine-transition detection unchanged — the
`LastKnownStage` NotAdult→Adult check that fixed the reload corruption stays exactly as it is.
Only what happens *after* detection changes:

```
if a ChoiceLetter_GrowthMoment for this pawn is outstanding
    register pending
else
    GrowUpVariance.Apply(pawn)      // silent auto-apply path, or no grant at all
```

The silent path is safe to apply immediately because vanilla's grant already landed inline during
`BirthdayBiological` on the previous tick — so the existing trigger was, by accident, already
correctly ordered for those pawns. This makes that correct by construction.

### Component 4: resolution hook

Harmony postfix on `ChoiceLetter_GrowthMoment.MakeChoices`. If the pawn is pending, apply and
deregister. Isolated in its own patch class, following this mod's existing per-class patch
isolation, so a wrong target cannot take down the rest of the mod.

### Component 5: reconciliation rule — unchanged, add-only

Target is computed as today, then filled only up to the remainder. If the growth moment already
pushed the pawn to or past target, add nothing. **Never remove.**

This is what keeps the design clear of constraint 3's collision surface, and it means the mod
never takes back a trait the player just chose in the dialog. Passions follow the same rule via
`alreadyCommittedPips`, which will now finally see the age-13 grant — and the existing
negative-budget branch already handles a pawn arriving over budget (verified in play:
`committed pips 5, rolled -2.04 -> 0 Major + 0 Minor`, no crash, nothing wiped).

### Component 6: settings

Replace `applyVarianceOnGrowUp` with a single General-section toggle governing whether the mod
touches children at all. Checked = the mod intervenes; unchecked = vanilla behaviour, the growth
moment plays out untouched and no grow-up pass runs.

- Field and scribe node both renamed to **`applyVarianceToChildren`**. The old name described the
  implementation trigger (a life-stage change) rather than what the setting means to a player, and
  it no longer even describes when the pass runs.
- Checkbox label: **"Apply variance to children growing up"**.
- Hover tooltip, via `CheckboxLabeled`'s third argument, as the other non-obvious toggles already
  do:

  > When a child turns 13 they become an adult and get their last growth moment — a trait and one
  > or more passions of your choosing. With this on, the mod waits for that choice, then tops the
  > pawn up to your trait and passion ranges, counting what the growth moment already gave. With
  > it off, children grow up exactly as in vanilla and this mod never touches them.

- Default checked.
- Not tied to a profile — it sits with `applyToHostilePawns` and the other housekeeping toggles,
  so switching profiles never silently changes it.
- One toggle, not two. A second checkbox also meaning "do we touch children" would be ambiguous.
- **Renaming the node drops the old saved value.** Both old and new defaults are `true`, so anyone
  who left it alone sees no change; only a player who deliberately turned it off gets it back on
  once. That is worth a readable name, and is the one deliberate exception to the
  no-migration-needed approach the profile system otherwise follows.

### Component 7: diagnostics

Extend the existing trait trace, gated on `verboseLogging` as before:

- the trigger path taken — `letter resolved`, `no letter (silent grant)`, or `fallback sweep`
- what the growth moment granted, as observed: trait and passion increments
- how long the pawn sat pending, in ticks, so a deferral that never resolves is visible

## Testing

The pending-set machinery is the new risk, so it carries the coverage. All runs with verbose
logging on.

| # | Scenario | How to trigger | Expect |
|---|----------|----------------|--------|
| 1 | Letter resolved normally | Age a 12-year-old colonist to 13, unpause, click the letter, choose | One grow-up trace, at click time, path `letter resolved`. Final trait count matches target *including* the growth trait |
| 2 | Deadline force-open | Age up, then let 2 in-game days pass without clicking | Dialog force-opens; "Later" refused; on OK, one grow-up trace |
| 3 | Save/reload while pending | Age up, do not click, save, quit, reload, then click | Exactly one grow-up trace, after the click. Pending state survived the reload |
| 4 | Pawn dies while pending | Age up, do not click, kill the pawn | Deregistered, no trace, no error |
| 5 | Silent path | Age up a non-player-faction child | Immediate trace, path `no letter (silent grant)` |
| 6 | Toggle off | Uncheck the child toggle, age up | No trace at all; growth moment behaves exactly as vanilla |
| 7 | Reload idempotency | After any successful grow-up: save, reload 2–3× | Zero further grow-up traces; traits and skills byte-identical each time |
| 8 | `countProtectedTraits` on | Xenotype child (Yttakin) with the toggle on | `desired total max(P,R)`, forced traits never removed |
| 9 | `applyToHostilePawns` off | Raid with the toggle off | Zero traces for raiders |
| 10 | No Biotech | Disable the DLC, generate pawns | No grow-up path reachable; no errors |

Items 7–10 are carried over from `HANDOVER.md` and are independent of this change.

Note for tests 1–5: the grow-up patch needs a baseline firing before it can detect a transition,
so **let the game run unpaused for a few seconds after loading** before aging anyone up. See
`2026-07-29-child-growthup-test-plan.md`.

## Out of scope

- **Variance for children at generation time.** Children are skipped entirely
  (`HarmonyPatches.cs:27`) and stay that way. Revisiting it would give `TraitAgeCap` real work —
  it is currently unreachable, since generation skips children and grow-up runs at 13, which is
  already `GrowthMomentAges.Max()`. Recorded as a known dead branch, not fixed here.
- **Growth moments at ages 7 and 10.** Only the age-13 moment collides with the grow-up pass.
- Removing traits on grow-up, for the reasons in constraints 3 and component 5.
