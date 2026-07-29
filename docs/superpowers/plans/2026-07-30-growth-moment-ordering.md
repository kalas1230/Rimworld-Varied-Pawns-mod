# Growth-Moment Ordering Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Defer the grow-up variance pass until the age-13 growth moment has resolved, so the mod counts what vanilla granted instead of stacking on top of it.

**Architecture:** Extract the existing grow-up apply logic into one shared entry point, then reach it from three triggers: immediately when no growth letter exists, from a `MakeChoices` postfix when the player resolves the letter, and from a `GameComponent` sweep that cleans up pawns lost while pending. The reconciliation rule stays add-only, so no trait is ever removed from a live pawn.

**Tech Stack:** C# 9 / net472, Harmony 2.x, RimWorld 1.6 Assembly-CSharp, MSBuild via `dotnet build`.

## Global Constraints

- **Spec:** `docs/superpowers/specs/2026-07-30-growth-moment-ordering-design.md`. Read it before starting.
- **No test framework exists, and none is being added.** This code depends on RimWorld statics (`Find.LetterStack`, `DefDatabase`, `Rand`, `Current.Game`) that cannot be instantiated outside the running game. The verification gate for every task is: `dotnet build` reports **0 Warning(s), 0 Error(s)**, then deploy and confirm the specific in-game observation named in that task. Do not claim a task passes on a clean build alone — the build only proves it compiles.
- **Build command:** `dotnet build Source/PawnVarianceMod.csproj` from the repo root.
- **Deploy:** copy `Assemblies/PawnVarianceMod.dll` and `.pdb` to `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\PawnVarianceMod\Assemblies\`. **The copy fails silently-ish if RimWorld is running** (DLL locked) — check with `tasklist //FI "IMAGENAME eq RimWorldWin64.exe"` first.
- **Commits:** this repo is committed **only when the user explicitly asks** (standing rule, `HANDOVER.md`). Each task ends with a commit step; run it only if the user has given the go-ahead for that task, otherwise leave the work in place and carry on.
- **Patch registration:** new Harmony patch classes must be added to `PawnVarianceMod.cs`'s constructor via `PatchIndividually`, never `PatchAll` — one bad target must not disable the other patches.
- **In-game verification requires `verboseLogging` ON** (Options → Mod settings → Pawn Variance → General).
- **Grow-up testing setup:** after loading a save, let the game run unpaused for a few seconds before aging anyone up. The patch needs a baseline firing to detect a transition. See `docs/superpowers/2026-07-29-child-growthup-test-plan.md`.
- Child→Adult is the **13th** birthday, not the 18th.

---

### Task 1: Extract the shared apply path

Pure refactor. No behaviour change — this is what makes Tasks 3 and 4 possible, and keeps them small.

**Files:**
- Create: `Source/GrowUpVariance.cs`
- Modify: `Source/GrowthUpPatch.cs` (remove the three `Apply*GrowthUp` methods and the try/catch body from `Postfix`)

**Interfaces:**
- Consumes: `QualityRoller.RollQuality()`, `TraitVarianceApplier`, `SkillVarianceApplier`, `PassionVarianceApplier`, `TraitTrace`, `TraitProtection`, `TraitAgeCap`.
- Produces: `public static void GrowUpVariance.Apply(Pawn pawn, string triggerPath)` — the single entry point every trigger calls. `triggerPath` is a short human-readable label that appears in the trace header.

- [ ] **Step 1: Create `Source/GrowUpVariance.cs` with the moved logic**

Move `ApplySkillGrowthUp`, `ApplyTraitGrowthUp` and `ApplyPassionGrowthUp` **verbatim** out of `GrowthUpPatch.cs`, changing only their containing class and the trace mode string. The try/catch moves here too, so all three future triggers are equally protected.

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // The single entry point for grow-up variance. Three triggers reach it — the life-stage change
    // when no growth letter is outstanding, the growth-moment letter being resolved, and the
    // fallback sweep — and all three must behave identically, which is why this lives in one place
    // rather than being duplicated per trigger.
    public static class GrowUpVariance
    {
        // triggerPath is purely diagnostic: it names which of the three routes got here, so a
        // verbose trace says why the pass ran at the moment it did.
        public static void Apply(Pawn pawn, string triggerPath)
        {
            var settings = PawnVarianceMod.Settings;

            try
            {
                float quality = QualityRoller.RollQuality();

                // Ordering matches the main postfix (HarmonyPatches.cs): trait, then skill, then
                // passion — trait variance can disable work tags, which passion placement's
                // TotallyDisabled exclusion depends on.
                if (settings.enableTraitVariance) ApplyTraitGrowthUp(pawn, quality, triggerPath);
                if (settings.enableSkillVariance) ApplySkillGrowthUp(pawn, quality);
                if (settings.enablePassionVariance) ApplyPassionGrowthUp(pawn, quality);
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception applying growth-up variance to {pawn.LabelShort}: {ex}");
            }
        }

        private static void ApplySkillGrowthUp(Pawn pawn, float quality)
        {
            SkillVarianceApplier.Apply(pawn, quality); // identical logic to generation-time; additive, so safe on accumulated childhood levels
        }

        private static void ApplyPassionGrowthUp(Pawn pawn, float quality)
        {
            // Pips, not distinct skills — matches AssignPassions' budget semantic (Minor=1, Major=2),
            // so existing growth-moment passions are weighed on the same scale as the rolled budget.
            int existingPips = pawn.skills.skills.Sum(r => r.passion == Passion.Major ? 2 : r.passion == Passion.Minor ? 1 : 0);
            PassionVarianceApplier.AssignPassions(pawn, quality, existingPips);
        }

        private static void ApplyTraitGrowthUp(Pawn pawn, float quality, string triggerPath)
        {
            // BODY MOVED VERBATIM from GrowthUpPatch.ApplyTraitGrowthUp, with exactly one change:
            // the TraitTrace.Begin mode argument becomes $"grow-up: {triggerPath}" instead of the
            // literal "grow-up". Do not otherwise edit this method in this task — behaviour changes
            // belong to Task 4.
        }
    }
}
```

For `ApplyTraitGrowthUp`, copy the current body from `Source/GrowthUpPatch.cs` exactly as it stands, changing only this one line:

```csharp
// was:
var trace = TraitTrace.Begin(pawn, quality, "grow-up");
// becomes:
var trace = TraitTrace.Begin(pawn, quality, $"grow-up: {triggerPath}");
```

- [ ] **Step 2: Reduce `DevelopmentalStage_Postfix.Postfix` to call the new entry point**

In `Source/GrowthUpPatch.cs`, replace the `try { ... } catch { ... }` block at the end of `Postfix` with a single call, and delete the three now-moved private methods from that file.

```csharp
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

            GrowUpVariance.Apply(___pawn, "life-stage change");
        }
```

Remove any `using` directives in `GrowthUpPatch.cs` that are now unused. The build will warn about none of these (C# does not warn on unused usings by default), so check by eye: `System`, `System.Linq` and `UnityEngine` are likely no longer needed there once the three methods leave.

- [ ] **Step 3: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

- [ ] **Step 4: Deploy and verify no behaviour changed**

Check RimWorld is closed, deploy, then in-game: age a 12-year-old to 13 with verbose logging on.

Expected: exactly the same grow-up trait and passion traces as before this task, with one cosmetic difference — the trait trace header now reads `Trait assignment (grow-up: life-stage change)` instead of `(grow-up)`. Trait counts, passion counts and the `age cap`/`target` lines must be unchanged in form.

- [ ] **Step 5: Commit** (only if the user has given the go-ahead — see Global Constraints)

```bash
git add Source/GrowUpVariance.cs Source/GrowthUpPatch.cs
git commit -m "refactor: extract grow-up variance into a single entry point"
```

---

### Task 2: Rename the child toggle and give it a tooltip

Independent of the rest. Small enough to review on its own, and it makes the setting readable before any behaviour depends on it.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:36` (field), `:71` (scribe), `:320-321` (checkbox)
- Modify: `Source/GrowthUpPatch.cs:44` (the one read site)

**Interfaces:**
- Produces: `PawnVarianceSettings.applyVarianceToChildren` (bool, default `true`), replacing `applyVarianceOnGrowUp`.

- [ ] **Step 1: Rename the field**

In `Source/PawnVarianceSettings.cs`, in the housekeeping block:

```csharp
        // Housekeeping preferences: deliberately outside the profile system, so switching profiles
        // never silently re-enables logging or changes whether raiders get variance.
        public bool applyToHostilePawns = true;
        public bool applyVarianceToChildren = true;
        public bool verboseLogging = false;
        public bool showQualityTier = true;
```

- [ ] **Step 2: Rename the scribe node**

```csharp
            Scribe_Values.Look(ref applyVarianceToChildren, "applyVarianceToChildren", true);
```

This deliberately drops the value saved under the old `applyVarianceOnGrowUp` key. Both defaults are `true`, so only a player who had explicitly turned it off is affected, and they get it back on once. This is the intended trade for a readable name — see the spec's Component 6.

- [ ] **Step 3: Update the checkbox with a label and tooltip**

Replace the existing grow-up checkbox in `DrawGlobalSettings`:

```csharp
            if (ModsConfig.BiotechActive)
                listing.CheckboxLabeled(
                    "Apply variance to children growing up",
                    ref applyVarianceToChildren,
                    "When a child turns 13 they become an adult and get their last growth moment — a trait and one or more passions of your choosing. With this on, the mod waits for that choice, then tops the pawn up to your trait and passion ranges, counting what the growth moment already gave. With it off, children grow up exactly as in vanilla and this mod never touches them.");
```

- [ ] **Step 4: Update `ResetToDefaults`**

```csharp
            applyVarianceToChildren = true;
```

- [ ] **Step 5: Update the read site**

In `Source/GrowthUpPatch.cs`, in `DevelopmentalStage_Postfix.Postfix`:

```csharp
            if (!settings.applyVarianceToChildren) return;
```

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`. A leftover reference to `applyVarianceOnGrowUp` shows up here as CS1061 — if you get one, you missed a read site.

- [ ] **Step 7: Deploy and verify in the settings UI**

Expected: the checkbox reads "Apply variance to children growing up", is checked by default, and hovering it shows the tooltip text. Toggle it off, close settings, reopen — it stays off. Quit to menu and relaunch — still off (proving the new scribe key round-trips).

- [ ] **Step 8: Commit** (only if the user has given the go-ahead)

```bash
git add Source/PawnVarianceSettings.cs Source/GrowthUpPatch.cs
git commit -m "feat: rename the child variance toggle and add an explanatory tooltip"
```

---

### Task 3: The pending-set GameComponent

**Files:**
- Create: `Source/GrowUpPendingComponent.cs`

**Interfaces:**
- Consumes: `GrowUpVariance.Apply(Pawn, string)` from Task 1.
- Produces:
  - `GrowUpPendingComponent.Instance` → `GrowUpPendingComponent` (null when no game is loaded)
  - `void Register(Pawn pawn)`
  - `bool Deregister(Pawn pawn, out int ticksPending)` — true if the pawn was pending
  - `static bool HasUnresolvedGrowthLetter(Pawn pawn)`

- [ ] **Step 1: Create the component**

RimWorld discovers `GameComponent` subclasses automatically through `Game.FillComponents()`, so no XML registration is needed — but the `(Game game)` constructor is mandatory or instantiation throws.

```csharp
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Tracks pawns that have become adult but whose age-13 growth moment has not resolved yet, so
    // the grow-up pass can run afterwards and count what vanilla granted.
    //
    // Unlike DevelopmentalStage_Postfix's session-only LastKnownStage dictionary, this IS scribed:
    // the growth-moment letter survives save/load and can sit for two in-game days, so the pending
    // state has to survive with it. A GameComponent is per-save, so the cross-save thingIDNumber
    // collision hazard that forces LastKnownStage to clear on load does not apply here.
    public class GrowUpPendingComponent : GameComponent
    {
        private const int SweepIntervalTicks = 2500;

        // Parallel lists rather than a Dictionary<Pawn, int>: Scribe resolves Pawn references AFTER
        // collections are rebuilt, so a dictionary keyed by pawn hashes its keys before they point
        // at anything. Parallel lists are the standard RimWorld idiom for exactly this reason.
        private List<Pawn> pendingPawns = new List<Pawn>();
        private List<int> pendingSinceTicks = new List<int>();

        public GrowUpPendingComponent(Game game)
        {
        }

        public static GrowUpPendingComponent Instance =>
            Verse.Current.Game?.GetComponent<GrowUpPendingComponent>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingPawns, "pendingGrowUpPawns", LookMode.Reference);
            Scribe_Collections.Look(ref pendingSinceTicks, "pendingGrowUpSinceTicks", LookMode.Value);

            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

            if (pendingPawns == null) pendingPawns = new List<Pawn>();
            if (pendingSinceTicks == null) pendingSinceTicks = new List<int>();

            // A null entry means the referenced pawn no longer exists in the save. Drop those, and
            // drop everything if the two lists ever disagree in length rather than risk pairing a
            // pawn with another pawn's timestamp.
            if (pendingPawns.Count != pendingSinceTicks.Count)
            {
                Log.Warning($"[PawnVarianceMod] Pending grow-up lists out of sync ({pendingPawns.Count} vs {pendingSinceTicks.Count}); clearing.");
                pendingPawns.Clear();
                pendingSinceTicks.Clear();
                return;
            }

            for (int i = pendingPawns.Count - 1; i >= 0; i--)
            {
                if (pendingPawns[i] != null) continue;
                pendingPawns.RemoveAt(i);
                pendingSinceTicks.RemoveAt(i);
            }
        }

        public void Register(Pawn pawn)
        {
            if (pawn == null || pendingPawns.Contains(pawn)) return;
            pendingPawns.Add(pawn);
            pendingSinceTicks.Add(Find.TickManager.TicksGame);
        }

        public bool Deregister(Pawn pawn, out int ticksPending)
        {
            ticksPending = 0;
            int index = pendingPawns.IndexOf(pawn);
            if (index < 0) return false;

            ticksPending = Find.TickManager.TicksGame - pendingSinceTicks[index];
            pendingPawns.RemoveAt(index);
            pendingSinceTicks.RemoveAt(index);
            return true;
        }

        // The sweep's real job is cleaning up a pawn that died or was otherwise lost while pending.
        // The "letter vanished unresolved" case is near-unreachable — vanilla force-opens the dialog
        // on the last tick before timeout and refuses to let it close unchosen — so it is covered by
        // the condition below rather than by dedicated machinery.
        public override void GameComponentTick()
        {
            if (pendingPawns.Count == 0) return;
            if (Find.TickManager.TicksGame % SweepIntervalTicks != 0) return;

            for (int i = pendingPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = pendingPawns[i];

                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    pendingPawns.RemoveAt(i);
                    pendingSinceTicks.RemoveAt(i);
                    continue;
                }

                if (HasUnresolvedGrowthLetter(pawn)) continue;

                int ticksPending = Find.TickManager.TicksGame - pendingSinceTicks[i];
                pendingPawns.RemoveAt(i);
                pendingSinceTicks.RemoveAt(i);
                GrowUpVariance.Apply(pawn, $"fallback sweep after {ticksPending} ticks pending");
            }
        }

        public static bool HasUnresolvedGrowthLetter(Pawn pawn)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null) return false;

            for (int i = 0; i < letters.Count; i++)
            {
                if (letters[i] is ChoiceLetter_GrowthMoment growthLetter
                    && growthLetter.pawn == pawn
                    && !growthLetter.choiceMade
                    && !growthLetter.TimeoutPassed)
                    return true;
            }
            return false;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`.

If `TimeoutPassed` or `choiceMade` fails to resolve, re-check against the decompile at `zzz-Do-Not-Commit/decompile/ChoiceLetter_GrowthMoment.cs` — both are public there (`choiceMade` is a public field, `TimeoutPassed` a public property on `LetterWithTimeout`).

- [ ] **Step 3: Deploy and verify the component loads**

Nothing calls `Register` yet, so the only thing to confirm is that the component instantiates without error.

Expected: load a save, check the log for **no** `Could not instantiate GameComponent` or `GrowUpPendingComponent` exception. Save and reload once — still no error (this exercises `ExposeData` on empty lists).

- [ ] **Step 4: Commit** (only if the user has given the go-ahead)

```bash
git add Source/GrowUpPendingComponent.cs
git commit -m "feat: add a scribed pending set for deferred grow-up variance"
```

---

### Task 4: Wire up deferral — registration, resolution hook, and trace

The behaviour change. Everything before this was scaffolding.

**Files:**
- Modify: `Source/GrowthUpPatch.cs` (registration branch in `Postfix`; new patch class)
- Modify: `Source/PawnVarianceMod.cs:24-29` (register the new patch)

**Interfaces:**
- Consumes: `GrowUpPendingComponent.Instance`, `.Register`, `.Deregister`, `.HasUnresolvedGrowthLetter` (Task 3); `GrowUpVariance.Apply(Pawn, string)` (Task 1); `PawnVarianceSettings.applyVarianceToChildren` (Task 2).

- [ ] **Step 1: Replace the immediate apply with the registration branch**

In `Source/GrowthUpPatch.cs`, `DevelopmentalStage_Postfix.Postfix`, swap the single `GrowUpVariance.Apply` call from Task 1 for:

```csharp
            // The age-13 growth moment grants a trait and one or more passions, and it resolves
            // AFTER this point: BirthdayBiological sends its letter on the tick before
            // PostResolveLifeStageChange fires, and the player clicks it whenever they like. Applying
            // now would stack our full budget on top of that grant. So if a letter is outstanding,
            // wait for it — GrowthMomentMakeChoices_Postfix or the sweep will finish the job.
            //
            // No letter means one of two things, and both are safe to apply immediately: either the
            // pawn took vanilla's silent auto-apply path (non-player faction, not
            // notification-worthy, or a quest lodger — the grant already landed inline last tick), or
            // the growth tier offered nothing at all.
            var pending = GrowUpPendingComponent.Instance;
            if (pending != null && GrowUpPendingComponent.HasUnresolvedGrowthLetter(___pawn))
            {
                pending.Register(___pawn);
                if (settings.verboseLogging)
                    Log.Message($"[PawnVarianceMod] {___pawn.LabelShortCap} became adult with a growth-moment letter outstanding — deferring variance until it resolves.");
                return;
            }

            GrowUpVariance.Apply(___pawn, "no letter (silent grant)");
```

- [ ] **Step 2: Add the resolution hook**

Append this patch class to `Source/GrowthUpPatch.cs`, alongside the existing `Game_LoadGame_Postfix` and `Game_InitNewGame_Postfix`:

```csharp
    // The single point at which a growth moment's choices are actually applied: MakeChoices
    // increments the chosen passions, calls GainTrait plus TraitUtility.ApplySkillGainFromTrait, and
    // at exactly age 13 also runs PawnGenerator.TryGenerateSexualityTraitFor. Running our pass in a
    // postfix here means we observe the real grant instead of predicting it.
    //
    // Verified uncontested: a scan of all 512 installed mod assemblies found zero references to
    // ChoiceLetter_GrowthMoment or MakeChoices, so there is no patch-ordering conflict to manage.
    // Isolated as its own patch class per this mod's per-class patch isolation.
    [HarmonyPatch(typeof(ChoiceLetter_GrowthMoment), nameof(ChoiceLetter_GrowthMoment.MakeChoices))]
    public static class GrowthMomentMakeChoices_Postfix
    {
        public static void Postfix(ChoiceLetter_GrowthMoment __instance)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null) return;

            var pending = GrowUpPendingComponent.Instance;
            if (pending == null) return;
            if (!pending.Deregister(pawn, out int ticksPending)) return; // not one of ours — a growth moment at age 7 or 10

            if (PawnVarianceMod.Settings.verboseLogging)
            {
                string grantedTrait = __instance.chosenTrait != null && __instance.chosenTrait != ChoiceLetter_GrowthMoment.NoTrait
                    ? TraitTrace.Describe(__instance.chosenTrait)
                    : "none";
                string grantedPassions = __instance.chosenPassions.NullOrEmpty()
                    ? "none"
                    : string.Join(", ", __instance.chosenPassions.Select(s => s.defName));
                Log.Message($"[PawnVarianceMod] Growth moment resolved for {pawn.LabelShortCap} after {ticksPending} ticks: trait {grantedTrait}, passion increments {grantedPassions}");
            }

            GrowUpVariance.Apply(pawn, $"letter resolved after {ticksPending} ticks pending");
        }
    }
```

`TraitTrace` is `internal` and `GrowthMomentMakeChoices_Postfix` is in the same assembly, so `TraitTrace.Describe` is reachable. `GrowthUpPatch.cs` already has `using System.Linq;` and `using RimWorld;`; confirm both are still present after Task 1's cleanup, since `Select` and `ChoiceLetter_GrowthMoment` need them.

- [ ] **Step 3: Register the patch**

In `Source/PawnVarianceMod.cs`, add to the constructor's patch list:

```csharp
            PatchIndividually(harmony, typeof(GrowthMomentMakeChoices_Postfix));
```

- [ ] **Step 4: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Warning(s)`, `0 Error(s)`.

- [ ] **Step 5: Deploy and run the core scenario**

Age a 12-year-old colonist to 13, unpause, and **do not click the letter yet**.

Expected at the transition: the deferral line `became adult with a growth-moment letter outstanding — deferring variance until it resolves`, and **no** trait or passion trace.

Then click the letter and make choices.

Expected on resolution: the `Growth moment resolved for … trait X, passion increments Y` line, then a trait trace headed `Trait assignment (grow-up: letter resolved after N ticks pending)`, then a passion trace. The trait trace's `incoming` list must already include the growth-moment trait, and the passion trace's `committed pips` must already include the growth-moment passions.

- [ ] **Step 6: Run the remaining scenarios from the spec's test table**

Work through rows 2–6 of the table in `docs/superpowers/specs/2026-07-30-growth-moment-ordering-design.md`. In particular:

- **Row 3 (save/reload while pending):** age up, don't click, save, quit to menu, reload, then click. Expected: exactly one grow-up trace, after the click. This is the one that proves `ExposeData` works — if the pending set didn't persist, `Deregister` returns false and **nothing happens at all**, which is the failure mode to watch for.
- **Row 4 (pawn dies while pending):** age up, don't click, kill the pawn. Expected: no trace, no error, and the pending entry is gone (verify by saving and reloading — no warning about out-of-sync lists).
- **Row 6 (toggle off):** uncheck the new setting, age up. Expected: no deferral line, no trace, growth moment behaves as vanilla.

- [ ] **Step 7: Commit** (only if the user has given the go-ahead)

```bash
git add Source/GrowthUpPatch.cs Source/PawnVarianceMod.cs
git commit -m "feat: defer grow-up variance until the growth moment resolves"
```

---

## Self-review notes

**Spec coverage.** Component 1 → Task 1. Component 2 → Task 3. Components 3 and 4 → Task 4. Component 5 (add-only reconciliation) needs no code: it is already how `ApplyTraitGrowthUp` behaves, and Task 1 moves it verbatim. Component 6 → Task 2. Component 7 (diagnostics) is split across Task 1 (trigger path in the trace header) and Task 4 (the deferral and resolution log lines, including pending duration). Test table rows 1–6 → Task 4 Steps 5–6; rows 7–10 are pre-existing carryover items independent of this change and are not tasks here.

**Known gap, deliberately left:** the spec's out-of-scope section keeps `TraitAgeCap` unreachable. No task touches it.
