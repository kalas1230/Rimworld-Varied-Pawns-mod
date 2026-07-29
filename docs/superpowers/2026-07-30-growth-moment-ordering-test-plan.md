# Growth-moment ordering — test plan

Date: 2026-07-30
Covers: `docs/superpowers/plans/2026-07-30-growth-moment-ordering.md` (Tasks 1–4, all implemented)
Spec: `docs/superpowers/specs/2026-07-30-growth-moment-ordering-design.md`

The code change is implemented and builds clean (0 Warning(s), 0 Error(s)). **Nothing below has been
verified in-game** — the build only proves it compiles. Every task in the plan named an in-game
observation as its real gate; those observations are collected here.

---

## What changed, in one paragraph

The mod's grow-up variance pass used to run on the life-stage change, which happens *before* the
age-13 growth moment resolves — so the mod stacked its full trait/passion budget on top of vanilla's
grant. Now the pass waits: if a `ChoiceLetter_GrowthMoment` is outstanding when the pawn becomes
adult, the pawn is put in a scribed pending set and nothing is applied; the pass runs from a postfix
on `ChoiceLetter_GrowthMoment.MakeChoices` when the player makes their choices, so it *observes*
what vanilla granted. Three trigger paths now exist, and each names itself in the trait trace.

## Setup

1. **Deploy.** `dotnet build Source/PawnVarianceMod.csproj`, then copy `Assemblies/PawnVarianceMod.dll`
   and `.pdb` to
   `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\PawnVarianceMod\Assemblies\`.
   **The copy fails silently-ish if RimWorld is running** (the DLL is locked) — close the game first,
   or check with `tasklist /FI "IMAGENAME eq RimWorldWin64.exe"`.
2. **Verbose logging ON**: Options → Mod settings → Pawn Variance → General. Nearly every expectation
   below is a log line, and they are all gated on this.
3. **Dev mode ON**, for the age debug action.
4. **Let the game run unpaused for a few seconds after loading, before aging anyone up.**
   `DevelopmentalStage_Postfix` only acts when *it has itself* observed the pawn as NotAdult on an
   earlier firing (`GrowthUpPatch.cs:45-52`). Load-and-immediately-age records no baseline, the patch
   correctly declines, and you see nothing — which looks exactly like a bug. This is still the most
   likely way to waste a session.
5. **Child→Adult is the 13th birthday**, not the 18th. The transition under test is 12 → 13.
6. Aging a pawn: Debug Actions menu → search `age` → pawn-targeted age action → set 13 in one step
   from 12.

## The strings to grep for

All prefixed `[PawnVarianceMod]`. These are the entire diagnostic surface of the change:

| String | Means |
|---|---|
| `became adult with a growth-moment letter outstanding — deferring variance until it resolves` | Registration: pawn is now pending, nothing applied |
| `Growth moment resolved for … after N ticks: trait X, passion increments Y` | Resolution hook fired; N is how long it sat pending; X/Y are what **vanilla** granted, observed |
| `Trait assignment (grow-up: letter resolved after N ticks pending)` | Deferred pass, normal path (spec row 1) |
| `Trait assignment (grow-up: no unresolved letter)` | Immediate pass — **three different situations share this label**, see scenario 5 |
| `Trait assignment (grow-up: fallback sweep after N ticks pending)` | Sweep found the letter gone; should be rare |
| `Suppressed grow-up variance for … (path): applyVarianceToChildren is off.` | Settings opt-out honoured, including for a pawn already pending |
| `Suppressed grow-up variance for … (path): hostile pawn and applyToHostilePawns is off.` | Hostile opt-out honoured |
| `Suppressed grow-up variance for … (path): pawn is dead or destroyed.` | The pass declined on a pawn that no longer exists |
| `Pending grow-up lists out of sync (A vs B); clearing.` | **Should never appear.** Save-data corruption in the pending set |
| `GrowUpPendingComponent.Instance was null while processing …` | **Should never appear.** Would mean the deferral silently degraded to the old stacking bug |
| `Exception resolving growth moment for …` | The new postfix's guard caught something. Any occurrence is a bug — send the stack trace |
| `Exception applying growth-up variance to …` | `GrowUpVariance.Apply`'s guard caught something. Same |

`Trait assignment (grow-up: life-stage change)` and `(grow-up: no letter (silent grant))` **must never
appear.** Both were intermediate labels during implementation and no longer exist in the code; seeing
either means the wrong build is deployed.

---

## Core scenarios

These are the spec's test table, rows 1–6. Rows 1, 3 and 4 are the ones that would catch a real
defect; 2, 5 and 6 are cheaper confirmations.

### 1. Letter resolved normally — the whole point of the change

**Do.** Load, unpause a few seconds, note the child's existing passions (skill + Minor/Major) and
trait count. Age 12 → 13. Unpause. **Do not click the letter yet.**

**Expect at the transition:** the `deferring variance until it resolves` line, and **no trait trace
and no passion trace at all.** A trace here means the deferral did not take and the old stacking bug
is still live.

**Then** click the letter and make your choices.

**Expect on resolution, in this order:**
1. `Growth moment resolved for … after N ticks: trait X, passion increments Y`
2. `Trait assignment (grow-up: letter resolved after N ticks pending)`
3. `Passion assignment for …`

**The two lines that prove the fix works:**
- The trait trace's `incoming` list **already contains** the growth-moment trait X.
- The passion trace's `committed pips` **already counts** the growth-moment passions Y
  (Minor = 1 pip, Major = 1.5 — the trace now prints the scale inline, and the value is fractional, so
  `committed pips 2.50` for one Minor + one Major is correct, not a rounding bug).

If either reads as though the grant had not happened, the ordering fix has failed even though the
deferral fired. That is the specific failure this test exists to catch.

**Also check:** final trait count matches the trace's `target` line, *including* the growth trait —
that is the slider finally meaning what it says. And the `age cap` line should read
`none (fully grown)`; a numeric cap here would be new information worth capturing verbatim
(see the 2026-07-29 plan's `TraitAgeCap` prediction).

### 2. Deadline force-open

**Do.** Age up, do not click, let 2 in-game days (120 000 ticks) pass. **Use dev-mode fast tick speed**
or this reads as a half-hour wait.

**Expect.** Vanilla force-opens the dialog on the last tick before timeout, with the game paused;
"Later" is refused. On OK: exactly one grow-up trace, path `letter resolved after N ticks pending`,
with N close to 120 000.

**Watch for** the `fallback sweep` path firing instead. That would mean the letter left the stack (or
its `TimeoutPassed`/`choiceMade` state read differently than expected) before `MakeChoices` ran. Not
fatal — the pass still happens — but it means the deferral is resolving through the insurance path
rather than the designed one, and is worth reporting.

### 3. Save / reload while pending — proves `ExposeData`

**Do.** Age up, do not click, **save, quit to menu, reload**, unpause a few seconds, then click the
letter.

**Expect.** Exactly one grow-up trace, after the click.

**The failure mode to watch for is silence.** If the pending set did not survive the reload,
`Deregister` returns false, the postfix treats the pawn as "not one of ours", and **nothing happens
at all** — no trace, no error, and the pawn silently never gets variance. So a clean log here is not
a pass; you must see the trace.

**Also:** no `Pending grow-up lists out of sync` warning at any point.

### 4. Pawn dies while pending

**Do.** Age up, do not click, kill the pawn (dev mode).

**Expect.** No trace, no error. The sweep (`GameComponentTick`, every 2500 ticks) drops the entry.
Let at least 2500 ticks pass unpaused before concluding.

**Verify the entry is really gone:** save, reload. No `out of sync` warning, and no grow-up trace
appears for the dead pawn. Note you cannot dismiss the letter by hand — `CanDismissWithRightClick` is
false — but once the pawn is destroyed `CanShowInLetterStack` goes false and the letter removes itself,
so it disappearing on its own is expected, not a symptom.

**Acceptable alternative outcome:** `Suppressed grow-up variance for … : pawn is dead or destroyed.`
That is the guard doing its job on a path the sweep had not yet reached; it is a pass, not a failure.

### 5. Immediate pass — the `no unresolved letter` label covers three situations

This label is emitted when the mod becomes adult-aware and finds no *unresolved* letter. Three
distinct situations reach it, and the log line alone does not tell them apart, so test them separately
and record which you were doing:

**5a — genuine silent grant.** Age up a **non-player-faction** child (or a quest lodger). Per the spec,
vanilla applies the grant silently and inline during `BirthdayBiological` for pawns failing
`ShouldSendNotificationAbout`, not of `Faction.OfPlayer`, or `IsQuestLodger`.

**Expect.** An **immediate** trace at the transition, path `no unresolved letter`, no deferral line.
This path is safe precisely because vanilla's grant already landed on the previous tick — so the trait
trace's `incoming` should still show the granted trait.

**Note the interaction with `applyToHostilePawns`:** if that is off and the pawn is hostile you now get
`Suppressed grow-up variance for … : hostile pawn`. Use a neutral faction's child to test 5a itself.

**5b — no grant offered at all.** A player colonist at a low growth tier with no new work types and no
passion or trait options gets neither letter nor grant. Same label, and the trait trace's `incoming`
shows nothing new. Hard to arrange deliberately; note it if you see it.

**5c — the letter was resolved before the mod's hook fired.** `Pawn_AgeTracker.AgeTickInterval` runs on
a multi-tick cadence, so there is a real window between the letter being sent and
`PostResolveLifeStageChange` firing. **Age the pawn up and click the letter immediately** — this is the
most natural thing a tester does, so cover it deliberately.

**Expect.** No deferral line and no `Growth moment resolved` line (the pawn was never pending, so the
resolution postfix correctly returns early), then one immediate trace labelled `no unresolved letter`.
**The outcome is still correct** — `incoming` and `committed pips` must show the growth-moment grant,
because the pass reads the pawn, not the letter. What to watch for is `incoming` *not* showing it,
which would mean the hook fired before the grant landed after all.

### 6. Toggle off

**Do.** Uncheck **"Apply variance to children growing up"** (Options → Mod settings → Pawn Variance →
General). Age up a colonist.

**Expect.** No deferral line, no trace, no registration. The growth moment behaves exactly as
vanilla.

**While you are there** — the setting was renamed this change (`applyVarianceOnGrowUp` →
`applyVarianceToChildren`), so also confirm:
- The label reads exactly **"Apply variance to children growing up"**, and hovering shows the
  tooltip about waiting for the choice.
- It is **checked by default** on a profile with no saved value.
- Toggle off → close settings → reopen: still off. Quit to menu → relaunch: **still off.** That last
  step is the one that proves the new scribe key `applyVarianceToChildren` round-trips.
- **Expected one-time regression:** if you had deliberately turned the old setting off, it comes back
  on once. The old scribe key was intentionally dropped (spec Component 6). Not a bug.

---

## New-machinery scenarios not in the spec's table

The pending set and the second patch target are new attack surface. These four are cheap and cover
what rows 1–6 do not.

### 7. Growth moments at ages 7 and 10 must not trigger anything

**Do.** With a 6- or 9-year-old, age to 7 or 10 and resolve the growth-moment letter normally.

**Expect.** The letter offers its normal choices and applying one works. **No grow-up trace, no
`Growth moment resolved` line, no deferral line.** The pawn was never pending, so
`GrowthMomentMakeChoices_Postfix` must return at its `Deregister` early-out.

This is the most likely place for the new postfix to misfire, because it fires on *every* growth
moment at every age — only the pending-set membership distinguishes them.

### 8. Toggle off *while a pawn is already pending*

Scenario 6 tests toggle-off-then-age-up, which cannot reach this path at all.

**Do.** With the setting ON, age up a colonist and **do not click the letter.** Confirm the deferral
line appeared. Now open Options → Mod settings and **uncheck** "Apply variance to children growing up".
Close settings. Then click the letter and make your choices.

**Expect.** `Suppressed grow-up variance for … (letter resolved after N ticks pending):
applyVarianceToChildren is off.` and **no trait or passion trace.** The growth moment itself applies
normally — that is vanilla's, not the mod's.

**Why this is worth a scenario:** the setting used to be read only at registration time, so a pawn
already pending would have received a full pass in defiance of the checkbox and its tooltip. The
suppression log line is what proves the guard now runs on the resolution path too.

**Same shape, worth one run if convenient:** `applyToHostilePawns` off, with a pending pawn whose
faction turns hostile. Expect the hostile suppression line.

### 9. Pawn destroyed (not merely killed) while pending

`Dead` and `Destroyed` are different code paths. Scenario 4 covers `Dead` (the sweep catches it);
destruction trips the letter's `ArchiveView` and reroutes through the dialog.

**Do.** Age up, do not click, then destroy the pawn (dev-mode destroy, or have them consumed/vaporised
rather than simply killed). Then open the **History** tab, find the archived growth-moment letter, open
it, and click OK.

**Expect.** No trait or passion trace. Either nothing at all (the postfix's post-`Deregister` guard
returns immediately) or `Suppressed grow-up variance for … : pawn is dead or destroyed.` Both are
passes. A full grow-up trace here is the failure — it would mean traits were written to a pawn that no
longer exists.

### 10. Pending pawn leaves the map

**Do.** Age up, do not click, then send the pawn out on a caravan (or let them be kidnapped). Come
back, then resolve the letter.

**Expect.** The letter survives and resolves normally with a full trace — `ArchiveView` only trips on
`DestroyedOrNull`, and a caravan pawn is neither. **The failure mode here is silence:** no trace and no
error at all would mean the pawn was stranded pending forever. This is untested reasoning, not
established behaviour, so record what actually happens either way.

### 11. Two children growing up at once

**Do.** Have two 12-year-olds. Age both to 13 in the same paused moment. Unpause. Resolve the two
letters **in the opposite order** to the order you aged them.

**Expect.** Two deferral lines, then two resolutions, each naming the right pawn, each with its own
`N ticks pending`. The pending set is two parallel lists (`List<Pawn>` + `List<int>`); a pawn paired
with another pawn's timestamp would show up here as a wildly wrong `N`, and a broken pairing would
show up as an `out of sync` warning after the next save/reload.

### 12. Reload idempotency after a successful grow-up (spec row 7)

**Do.** After any completed grow-up: save, reload, unpause, let it run. Two or three times.

**Expect.** **Zero** further grow-up traces for that now-adult pawn; traits, skills and passions
byte-identical each reload.

This is the regression the `LastKnownStage` dictionary exists to prevent — the old `HashSet` guard
could not tell vanilla's post-load life-stage resync from a real transition and re-shifted every
adult's skills on every load. Task 4 added its branch *after* that guard and did not touch it, but
this is the test that proves it.

### 13. No Biotech (spec row 10)

**Do.** Disable the Biotech DLC, generate pawns, play briefly.

**Expect.** No grow-up path reachable, no errors, and no `GrowUpPendingComponent` exception on load.
The child toggle's checkbox is inside a `ModsConfig.BiotechActive` guard, so it should not render.

---

## Carryover items (spec rows 8–9, independent of this change)

Run if you have the session budget; they are unchanged by this work.

- **`countProtectedTraits` ON** with a xenotype child (Yttakin carry forced traits): trace reads
  `countProtectedTraits ON (target is total traits)` and `desired total max(P,R)` rather than `P + R`.
  Forced traits are never removed to hit a lower target.
- **`applyToHostilePawns` off** during a raid: zero traces for raiders, full trace for any
  neutral/friendly pawn generated at the same time.

---

## What to send back

The session's `Player.log` is enough — every trace is self-contained and greppable by
`[PawnVarianceMod]`. Copy it to `temp/` (gitignored) as in previous sessions.

If something looks wrong, **the log line that looked wrong is far more useful than a description of
the symptom.** For scenario 1 specifically, the two lines worth quoting verbatim either way are the
trait trace's `incoming` line and the passion trace's `committed pips` line — those two are the fix.

## The pip-scale fix, and how to confirm it in-game

`GrowUpVariance.ApplyPassionGrowthUp` used to charge an existing **Major passion 2 pips** while
`PassionVarianceApplier`'s spend loop **prices a new Major at 1.5** (`PassionVarianceApplier.cs:59-67`)
— a 0.5 over-charge per Major. It was latent before this change (`alreadyCommittedPips` read 0 for the
age-13 grant) and this change is exactly what makes it bite, so it was fixed in the same pass:
`alreadyCommittedPips` is now a `float` and an existing Major counts **1.5**. The spend loop and all
generation-time passion tuning are unchanged.

**Confirm it in scenario 1**, on a child who already holds a Major passion from the age-10 growth
moment: the `committed pips` line should read a **fractional** value matching Minor×1 + Major×1.5 over
the passions you wrote down. A whole number where you expect `.5` means an older DLL is deployed.

## Known gaps, deliberately not tested

- **`TraitAgeCap` is still structurally unreachable** and no task touched it. Generation skips
  children; grow-up runs at 13, which is already `GrowthMomentAges.Max()`, so the cap is released at
  the exact moment it would apply. Recorded as a known dead branch (spec, Out of scope).
- **Growth moments at ages 7 and 10 are not deferred** — only the age-13 moment collides with the
  grow-up pass. Scenario 7 confirms they are left alone, which is the whole requirement.
- **Enabling variance *after* a pawn has already grown up never revisits them.** If all three
  `enable*Variance` toggles are off at the moment of the transition, the pawn is not registered and
  `LastKnownStage` records them as Adult permanently, so turning the toggles on later does nothing for
  that pawn. This is unchanged pre-existing behaviour, not something the new suppression paths
  introduced — noted so the new `Suppressed grow-up variance` log lines don't get blamed for it.
- **Downgrading the mod with a pawn pending is graceful but noisy.** `GrowUpPendingComponent` is
  scribed into the save. Loading a new save with an older mod build (or with this mod disabled) makes
  vanilla log an error about the unknown component type, then discard it; any pawn pending at save time
  silently never receives variance. Expected, not a new bug — noted so it is not reported as one.
  Upgrading is clean: `Game.FillComponents()` adds the component to pre-existing saves automatically, so
  old save → new mod needs no migration.
- **No unit tests, by design.** This code depends on RimWorld statics (`Find.LetterStack`,
  `Find.TickManager`, `DefDatabase`, `Rand`, `Current.Game`) that cannot be instantiated outside the
  running game. In-game observation is the only real gate, which is why this document exists.
