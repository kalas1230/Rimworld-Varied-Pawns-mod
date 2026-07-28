# Additive Trait Model Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `TraitVarianceApplier`'s destroy-and-rebuild trait generation with in-place reconciliation, so the mod never wipes trait state belonging to vanilla or other mods.

**Architecture:** Vanilla generates traits normally. Our postfix then classifies the pawn's traits into protected vs removable, computes a target count, and either adds traits (via the public `PawnGenerator.GenerateTraitsFor`) or removes them (via `TraitSet.RemoveTrait`) until the count matches. `allTraits.Clear()` is deleted outright. Protection classification moves into its own file; the applier keeps only the reconciliation logic.

**Tech Stack:** C# targeting net472, RimWorld 1.6.4871 `Assembly-CSharp.dll`, Harmony. Built with `dotnet build`.

**Spec:** `docs/superpowers/specs/2026-07-28-additive-trait-model-design.md`

## Global Constraints

- **No test harness exists and none can be added.** RimWorld types (`Pawn`, `TraitSet`, `SkillRecord`) cannot be instantiated outside a running game — they depend on `DefDatabase` and global game state. Per-task verification is therefore `dotnet build` (must report **0 Warnings, 0 Errors**) plus the in-game checks listed in the final task. Do not scaffold a test project.
- **Language level:** `KeyValuePair<TKey,TValue>` deconstruction is NOT available in this project's net472 BCL. Iterate `KeyValuePair` explicitly via `.Key` / `.Value`.
- **Build command:** `dotnet build Source/PawnVarianceMod.csproj`
- **Deploy command:** `cp -r Assemblies "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/"` — fails silently with "Permission denied" if RimWorld is running. Check with `tasklist //FI "IMAGENAME eq RimWorldWin64.exe"` first.
- **Do not commit `Source/*` changes.** This repo's standing rule is that mod code is committed only when the user explicitly asks. Commit steps in this plan cover documentation only; leave code changes in the working tree and tell the user.
- **Verified vanilla facts** (do not re-derive, do not assume otherwise):
  - `SkillRecord.Level` getter is `Mathf.Clamp(levelInt + Aptitude, 0, 20)`; setter writes raw `levelInt`.
  - `TraitSet.RemoveTrait(Trait, bool unsuppressConflicts = false)` calls `pawn.genes.RemoveGene(trait.sourceGene)` when `sourceGene != null`.
  - `GrowthUtility.GrowthMomentAges` is `public static readonly int[] { 7, 10, 13 }`.
  - `PawnGenerator.GenerateTraitsFor(Pawn, int, PawnGenerationRequest?, bool)` is public static and returns candidates the caller must `GainTrait` itself.
  - **`Trait` lives in the `RimWorld` namespace, not `Verse`.** (`ilspycmd -t Verse.Trait` fails with "Could not find type definition".)
  - **`Trait.ScenForced` exists** as `public bool ScenForced => scenForced;`. Despite the name it does NOT mean "scenario-forced only" — the constructor `Trait(TraitDef def, int degree = 0, bool forced = false)` assigns `scenForced = forced`, so it is true for *anything* vanilla constructed with `forced: true`: `PawnKindDef.forcedTraits`, `PawnGenerationRequest.ForcedTraits`, and `ScenPart_ForcedTrait`. It is **false** for backstory-forced traits, which vanilla builds as a plain `new Trait(te.def, te.degree)`, and false for normally rolled traits. This is why the `Capture*` sets are still required alongside it — `ScenForced` alone would not protect a backstory-forced trait.

---

## File Structure

| File | Responsibility |
|---|---|
| `Source/TraitProtection.cs` | **New.** Pure classification: given a pawn and request, decide whether a `Trait` is protected from removal. No mutation. |
| `Source/TraitVarianceApplier.cs` | **Modify.** Reconciliation only — compute target, add or remove. Loses `Clear()` and the sexuality blocks; keeps the `Capture*`/`FirstValidDegree` helpers that `GrowthUpPatch` still consumes. |
| `Source/TraitAgeCap.cs` | **New.** Single shared helper for vanilla's growth-birthday trait cap, used by both the generation and growth-up paths. |
| `Source/PawnVarianceSettings.cs` | **Modify.** Add `countProtectedTraits` bool; relabel trait sliders. |
| `Source/GrowthUpPatch.cs` | **Modify.** Apply the same age cap and `countProtectedTraits` semantics to its target count. Stays add-only. |

---

### Task 1: Add the `countProtectedTraits` setting

**Files:**
- Modify: `Source/PawnVarianceSettings.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PawnVarianceMod.Settings.countProtectedTraits` (bool, default `false`), read by Tasks 4 and 6.

- [ ] **Step 1: Add the field**

In the field block near the top (after `public bool showQualityTier = true;`), add:

```csharp
        // When false (default), the trait count sliders mean "traits this mod rolls" and protected
        // traits (gene/xenotype, kindDef-, backstory-, request- and scenario-forced) sit on top as a
        // floor. When true, the sliders mean "total traits on the pawn" and protected traits are
        // counted against that total. Either way protected traits are never removed, so a pawn can
        // still exceed the target when its protected set alone is larger.
        public bool countProtectedTraits = false;
```

- [ ] **Step 2: Add the default constant**

After `private const float DefaultPassionMajorBias = 0.8f;` add:

```csharp
        private const bool DefaultCountProtectedTraits = false;
```

- [ ] **Step 3: Persist it**

In `ExposeData()`, after the `showQualityTier` line, add:

```csharp
            Scribe_Values.Look(ref countProtectedTraits, "countProtectedTraits", false);
```

- [ ] **Step 4: Reset it**

In `ResetToDefaults()`, after `verboseLogging = false;` add:

```csharp
            countProtectedTraits = DefaultCountProtectedTraits;
```

- [ ] **Step 5: Add the UI control and relabel the sliders**

Replace the trait block in `DoWindowContents` (currently lines ~128-131):

```csharp
            listing.Gap();
            listing.CheckboxLabeled("Enable trait variance", ref enableTraitVariance);
            listing.Label($"Trait count range: {traitCountMin:F0} to {traitCountMax:F0}");
            traitCountMin = listing.Slider(traitCountMin, 0f, 15f);
            traitCountMax = listing.Slider(traitCountMax, 0f, 15f);
```

with:

```csharp
            listing.Gap();
            listing.CheckboxLabeled("Enable trait variance", ref enableTraitVariance);
            listing.CheckboxLabeled(
                "Count xenotype/forced traits toward the trait count",
                ref countProtectedTraits,
                "When off, the range below counts only traits this mod rolls, and traits forced by a xenotype, gene, backstory or scenario are added on top. When on, the range counts every trait the pawn has. Forced traits are never removed either way.");
            listing.Label(countProtectedTraits
                ? $"Total trait count: {traitCountMin:F0} to {traitCountMax:F0}"
                : $"Rolled trait count (forced traits extra): {traitCountMin:F0} to {traitCountMax:F0}");
            traitCountMin = listing.Slider(traitCountMin, 0f, 15f);
            traitCountMax = listing.Slider(traitCountMax, 0f, 15f);
```

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

---

### Task 2: Create the age cap helper

**Files:**
- Create: `Source/TraitAgeCap.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `TraitAgeCap.MaxRolledTraitsFor(Pawn pawn)` returning `int`. Used by Tasks 4 and 6.

- [ ] **Step 1: Create the file**

```csharp
using System.Linq;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Vanilla gates how many traits a pawn can accumulate on growth birthdays, not on a flat number:
    // PawnGenerator.GenerateTraits walks `for (int k = 3; k <= ageBiologicalYears; k++)` and grants at
    // most one trait per GrowthUtility.IsGrowthBirthday(k). With GrowthMomentAges = { 7, 10, 13 } that
    // means 0 rolled traits below age 7, 1 at 7-9, 2 at 10-12 and 3 from 13 on. Without this cap the
    // mod would hand a five-year-old a full adult trait load, which vanilla structurally never does.
    // Thresholds are read from GrowthUtility at runtime rather than hardcoded, since Biotech content
    // or another mod may change them.
    public static class TraitAgeCap
    {
        public static int MaxRolledTraitsFor(Pawn pawn)
        {
            if (pawn?.ageTracker == null) return int.MaxValue;

            int[] momentAges = GrowthUtility.GrowthMomentAges;
            if (momentAges == null || momentAges.Length == 0) return int.MaxValue;

            int age = pawn.ageTracker.AgeBiologicalYears;

            // Past the last growth moment the pawn is fully grown and the cap stops binding, so the
            // mod's own target applies in full (this is what lets adults exceed vanilla's max of 3).
            if (age >= momentAges.Max()) return int.MaxValue;

            return momentAges.Count(a => a <= age);
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

---

### Task 3: Create the trait protection classifier

**Files:**
- Create: `Source/TraitProtection.cs`

**Interfaces:**
- Consumes: `TraitVarianceApplier.CaptureForcedTraits(Pawn)`, `TraitVarianceApplier.CaptureBackstoryForcedTraits(Pawn)`, `TraitVarianceApplier.CaptureRequestForcedTraits(PawnGenerationRequest)` — all existing, all returning `Dictionary<TraitDef, int>`.
- Produces: `TraitProtection.Build(Pawn pawn, PawnGenerationRequest? request)` returning `TraitProtection`, with instance method `bool IsProtected(Trait trait)`. Used by Tasks 4 and 6.

- [ ] **Step 1: Create the file**

```csharp
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Decides which of a pawn's traits must never be removed. Every rule here is required for
    // correctness, not preference — see the spec's "Protected traits" table.
    //
    // Deliberately NOT protected: freely-rolled sexuality traits. An earlier version of this mod
    // believed vanilla guarantees every pawn exactly one of Gay/Bisexual/Asexual. That is false.
    // PawnGenerator.TryGenerateSexualityTraitFor builds a weighted table whose first entry is a null
    // candidate carrying the summed commonality of every other trait in the game; when that entry
    // wins, the `result.First != null` guard grants nothing. Heterosexual is the common, correct
    // outcome, so removing a rolled sexuality trait breaks no invariant. The one genuinely forced
    // case — a same-gender love/ex-love partner implying Gay — IS protected below.
    public class TraitProtection
    {
        private readonly HashSet<TraitDef> forcedDefs;
        private readonly bool protectGay;

        private TraitProtection(HashSet<TraitDef> forcedDefs, bool protectGay)
        {
            this.forcedDefs = forcedDefs;
            this.protectGay = protectGay;
        }

        public static TraitProtection Build(Pawn pawn, PawnGenerationRequest? request)
        {
            var defs = new HashSet<TraitDef>();

            void AddAll(Dictionary<TraitDef, int> source)
            {
                if (source == null) return;
                foreach (KeyValuePair<TraitDef, int> kvp in source) defs.Add(kvp.Key);
            }

            AddAll(TraitVarianceApplier.CaptureForcedTraits(pawn));
            AddAll(TraitVarianceApplier.CaptureBackstoryForcedTraits(pawn));
            if (request.HasValue)
                AddAll(TraitVarianceApplier.CaptureRequestForcedTraits(request.Value));

            // Vanilla hard-grants Gay when the pawn already has a same-gender love or ex-love partner
            // (PawnGenerator.GenerateTraits, ~line 1521). Removing it would leave the relationship in
            // an incoherent state, so this specific case is protected while a freely-rolled Gay is not.
            bool sameGenderPartner =
                LovePartnerRelationUtility.HasAnyLovePartnerOfTheSameGender(pawn)
                || LovePartnerRelationUtility.HasAnyExLovePartnerOfTheSameGender(pawn);

            return new TraitProtection(defs, sameGenderPartner);
        }

        public bool IsProtected(Trait trait)
        {
            if (trait == null) return true;

            // Mandatory: TraitSet.RemoveTrait calls pawn.genes.RemoveGene(trait.sourceGene) when this
            // is set, so removing a gene-sourced trait would delete the gene that granted it.
            if (trait.sourceGene != null) return true;

            // ScenForced is a misleading name: Trait's constructor assigns `scenForced = forced`, so
            // this is true for anything vanilla built with forced:true — kindDef-forced traits,
            // request-forced traits and ScenPart_ForcedTrait alike. It is false for backstory-forced
            // traits (vanilla builds those as a plain `new Trait(def, degree)`), which is exactly why
            // the forcedDefs set below is still needed rather than being redundant with this check.
            if (trait.ScenForced) return true;
            if (forcedDefs.Contains(trait.def)) return true;
            if (protectGay && trait.def == TraitDefOf.Gay) return true;

            return false;
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

`Trait.ScenForced` has been verified to exist against this RimWorld version's `Assembly-CSharp.dll` (see Global Constraints) — it should compile as written. If it somehow does not, re-derive the member name from the decompile rather than guessing or dropping the rule. Note the namespace is `RimWorld.Trait`, not `Verse.Trait`:

```bash
export PATH="$PATH:$HOME/.dotnet/tools"
R="/c/Program Files (x86)/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed"
ilspycmd -t RimWorld.Trait -r "$R" "$R/Assembly-CSharp.dll" | grep -in "scen"
```

---

### Task 4: Rewrite `TraitVarianceApplier.Apply` as in-place reconciliation

**Files:**
- Modify: `Source/TraitVarianceApplier.cs` (replace the body of `Apply`, lines ~21-124)

**Interfaces:**
- Consumes: `TraitProtection.Build`, `TraitProtection.IsProtected`, `TraitAgeCap.MaxRolledTraitsFor`, `PawnVarianceMod.Settings.countProtectedTraits`.
- Produces: `TraitVarianceApplier.Apply(Pawn, float, PawnGenerationRequest)` — signature unchanged, so `HarmonyPatches.cs` needs no edit.

**Do not touch** `CaptureForcedTraits`, `CaptureBackstoryForcedTraits`, `CaptureRequestForcedTraits`, `CaptureDisallowedTraits`, `FindForcingGene`, `FirstValidDegree`, or `JitterSample`. `GrowthUpPatch` and `TraitProtection` consume them.

- [ ] **Step 1: Replace the `Apply` method**

Replace everything from `public static void Apply(Pawn pawn, float quality, PawnGenerationRequest request)` through its closing brace (up to but NOT including the `// Returns TraitDef -> degree` comment) with:

```csharp
        public static void Apply(Pawn pawn, float quality, PawnGenerationRequest request)
        {
            var settings = PawnVarianceMod.Settings;

            var protection = TraitProtection.Build(pawn, request);
            List<Trait> current = pawn.story.traits.allTraits;

            int protectedCount = 0;
            var removable = new List<Trait>();
            foreach (Trait trait in current)
            {
                if (protection.IsProtected(trait)) protectedCount++;
                else removable.Add(trait);
            }

            int rolledTarget = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality) + JitterSample()),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            rolledTarget = Mathf.Min(rolledTarget, TraitAgeCap.MaxRolledTraitsFor(pawn));

            // Protected traits are a floor, never part of the budget: at a target of 0 an Yttakin still
            // keeps its xenotype-forced Psychically Dull and ends with exactly one trait.
            int desiredTotal = settings.countProtectedTraits
                ? Mathf.Max(protectedCount, rolledTarget)
                : protectedCount + rolledTarget;

            int delta = desiredTotal - current.Count;

            if (delta > 0)
            {
                // Vanilla's own commonality-weighted picker, with every real rejection rule applied.
                // Threading the real request through gives it the kindDef disallowed-traits,
                // required-work-tag and hostile-spawn checks it can only do with one.
                List<Trait> generated = PawnGenerator.GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false);
                foreach (Trait trait in generated)
                    pawn.story.traits.GainTrait(trait);
            }
            else if (delta < 0)
            {
                // Remove through vanilla's real TraitSet.RemoveTrait rather than clearing the backing
                // list. That runs vanilla's own ability cleanup and fires other mods' RemoveTrait
                // patches — notably Vanilla Traits Expanded's, which drops the VTE_SlowWorkSpeed hediff
                // it attached on GainTrait. Bypassing it (as allTraits.Clear() did) left that hediff
                // orphaned, and it then removed itself from inside Pawn_HealthTracker.Notify_Spawned's
                // foreach over hediffSet.hediffs, throwing "Collection was modified" and aborting the
                // pawn's spawn. Uniform random choice: vanilla's picker has no concept of a "better"
                // trait, so there is no quality axis to bias this by.
                int toRemove = Mathf.Min(-delta, removable.Count);
                for (int i = 0; i < toRemove; i++)
                {
                    Trait victim = removable.RandomElement();
                    removable.Remove(victim);
                    pawn.story.traits.RemoveTrait(victim);
                }
            }

            // No sexuality-trait call here, deliberately. Vanilla's own roll already ran during
            // generation and — because nothing is cleared any more — it survives untouched. Calling
            // PawnGenerator.TryGenerateSexualityTraitFor again would give every straight pawn a second
            // independent roll and skew the population's sexuality distribution away from vanilla's.
        }
```

- [ ] **Step 2: Fix the stale class comment**

The comment above the class (lines ~11-20) claims the mod's job is to "preserve forced traits that our own `Clear()` would otherwise destroy". Replace that sentence with:

```csharp
        // applied exactly as vanilla does it. This mod's only remaining job for traits is: (1) classify
        // which traits are protected from removal, and (2) decide how many total traits the pawn should
        // have, driven by quality/traitCountMin/Max. The trait list is reconciled in place — never
        // cleared — so no trait state belonging to vanilla or another mod is ever destroyed and rebuilt.
```

- [ ] **Step 3: Verify `Clear()` is gone**

Run: `grep -n "allTraits.Clear\|TryGenerateSexualityTraitFor" Source/TraitVarianceApplier.cs`
Expected: **no output**. Any match means a block survived the replacement — remove it.

- [ ] **Step 4: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

If `RandomElement()` fails to resolve, add `using System.Linq;` — it is a Verse extension on `IEnumerable<T>` and the file already imports `System.Linq`, so this should not occur.

---

### Task 5: Remove the now-unused `using` and confirm nothing else referenced the deleted code

**Files:**
- Modify: `Source/TraitVarianceApplier.cs`

- [ ] **Step 1: Check for orphaned references**

Run: `grep -rn "LovePartnerRelationUtility" Source/`
Expected: matches in `Source/TraitProtection.cs` only. If `TraitVarianceApplier.cs` still matches, a sexuality block survived — delete it.

- [ ] **Step 2: Confirm the retained helpers still have consumers**

Run: `grep -rn "CaptureForcedTraits\|FindForcingGene\|CaptureDisallowedTraits\|FirstValidDegree" Source/`
Expected: `CaptureForcedTraits` appears in `TraitProtection.cs` and `GrowthUpPatch.cs`; `FindForcingGene` and `CaptureDisallowedTraits` in `GrowthUpPatch.cs`; `FirstValidDegree` inside `TraitVarianceApplier.cs`. If any has zero consumers outside its own definition, leave it in place anyway and note it — do not delete, `GrowthUpPatch` is modified in Task 6.

- [ ] **Step 3: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

---

### Task 6: Apply the same target semantics to the growth-up path

**Files:**
- Modify: `Source/GrowthUpPatch.cs` (the target-count block, lines ~134-151)

**Interfaces:**
- Consumes: `TraitProtection.Build`, `TraitAgeCap.MaxRolledTraitsFor`, `PawnVarianceMod.Settings.countProtectedTraits`.
- Produces: nothing new.

`ApplyTraitGrowthUp` stays **add-only** — a pawn reaching adulthood must never silently lose a trait the player built a colony role around. Only the target calculation changes, so it matches generation-time semantics.

- [ ] **Step 1: Replace the target-count block**

Replace:

```csharp
            int currentCount = pawn.story.traits.allTraits.Count;
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality)),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            if (currentCount >= targetCount) return; // accepted limitation, see Growth-up variance closing paragraph
```

with:

```csharp
            int currentCount = pawn.story.traits.allTraits.Count;

            // Same target semantics as generation-time Apply, so a pawn's trait count means the same
            // thing however it was produced. No PawnGenerationRequest exists this late (the pawn
            // already exists, mid-game), so request-forced traits can't be classified here — that's
            // fine, they were applied at generation and this path only ever adds.
            var protection = TraitProtection.Build(pawn, null);
            int protectedCount = pawn.story.traits.allTraits.Count(t => protection.IsProtected(t));

            int rolledTarget = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality)),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            rolledTarget = Mathf.Min(rolledTarget, TraitAgeCap.MaxRolledTraitsFor(pawn));

            int targetCount = settings.countProtectedTraits
                ? Mathf.Max(protectedCount, rolledTarget)
                : protectedCount + rolledTarget;

            // Add-only by design: never remove on grow-up, even if the pawn is already above target.
            if (currentCount >= targetCount) return;
```

- [ ] **Step 2: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded.` with `0 Warning(s)` and `0 Error(s)`.

If `Count(...)` fails to resolve, confirm `using System.Linq;` is present at the top of `GrowthUpPatch.cs`.

---

### Task 7: Update the design spec's status and commit documentation

**Files:**
- Modify: `docs/superpowers/specs/2026-07-28-additive-trait-model-design.md`

- [ ] **Step 1: Update the status line**

Change `Status: approved, not yet implemented` to `Status: implemented, pending in-game verification`.

- [ ] **Step 2: Commit documentation only**

```bash
git add docs/superpowers/specs/2026-07-28-additive-trait-model-design.md docs/superpowers/plans/2026-07-28-additive-trait-model.md
git commit -m "docs: mark additive trait model spec implemented, add plan"
```

Do NOT `git add Source/` — see Global Constraints.

---

### Task 8: Deploy and verify in-game

**Files:** none — this task is verification only.

This is the only task that can detect a defect in the preceding ones. It cannot be automated; the user must run it.

- [ ] **Step 1: Confirm RimWorld is closed**

Run: `tasklist //FI "IMAGENAME eq RimWorldWin64.exe"`
Expected: `INFO: No tasks are running which match the specified criteria.`
If RimWorld is running, ask the user to close it — the copy fails silently otherwise.

- [ ] **Step 2: Build and deploy**

```bash
dotnet build Source/PawnVarianceMod.csproj
cp -r Assemblies "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/"
```

- [ ] **Step 3: Tell the user to click "Reset to Defaults"**

A config saved before this change has no `countProtectedTraits` key. It will default to `false`, which is the intended default, so this is precautionary rather than required — but stale configs have bitten this mod before.

- [ ] **Step 4: Ask the user to run these checks and report results**

1. **The VTE crash (the main event).** With Vanilla Traits Expanded enabled, spawn ~10 dev-console raids. Expected: no `Collection was modified` error in `Pawn_HealthTracker.Notify_Spawned()`, and every raid arrives.
2. **Protected floor at zero.** Set trait count 0-0, generate an Yttakin. Expected: exactly 1 trait (Psychically Dull).
3. **Checkbox off.** Set trait count 4-4, checkbox off, generate an Yttakin. Expected: 5 traits.
4. **Checkbox on.** Same settings, checkbox on. Expected: 4 traits.
5. **Age gating.** Generate a child under 7. Expected: forced/backstory traits only, no rolled ones.
6. **Sexuality distribution.** Generate ~20 pawns. Expected: predominantly straight, no obvious excess of Gay/Bisexual/Asexual. This is the check for the removed `TryGenerateSexualityTraitFor` call; a visible excess means it was left in or protection is wrong.
7. **Ability cleanup.** Find a pawn whose ability-granting trait got removed. Expected: the ability is gone too, not orphaned on the pawn.

- [ ] **Step 5: Do not mark this plan complete until the user reports results**

Per `superpowers:verification-before-completion`: evidence before assertions. A clean `dotnet build` proves only that the code compiles, not that any of the seven behaviours above are correct.

---

## Self-Review

**Spec coverage:**
- Problem / VTE crash → Task 4 (removal via `RemoveTrait`), verified in Task 8 check 1.
- Protected traits table → Task 3.
- Sexuality correction → Task 3 (comment + narrowed Gay rule), Task 4 (call deleted), Task 8 check 6.
- Algorithm → Task 4.
- Age gating → Task 2, applied in Tasks 4 and 6.
- Settings → Task 1. (`traitCountMin` needs no change; the spec records why.)
- Consequences for existing code → Task 5 confirms the retained helpers keep their consumers.
- Edge cases: fewer removable than needed → `Mathf.Min(-delta, removable.Count)` in Task 4. `GenerateTraitsFor` shortfall → accepted, no handling needed. Growth-up add-only → Task 6. Redressed pawns → no change required, already handled by the existing patch target.
- Non-goals → nothing implemented for them, correct by construction.
- Testing → Task 8.

**Placeholder scan:** No TBD/TODO. Every code step contains complete code. The two conditional branches (`Trait.ScenForced` in Task 3, `RandomElement()` in Task 4) give an exact diagnostic command rather than "handle appropriately".

**Type consistency:** `TraitProtection.Build(Pawn, PawnGenerationRequest?)` returns `TraitProtection`; called with a real request in Task 4 and `null` in Task 6, which the nullable parameter accepts. `IsProtected(Trait)` returns `bool`, used in both. `TraitAgeCap.MaxRolledTraitsFor(Pawn)` returns `int`, consumed by `Mathf.Min` in both. `countProtectedTraits` is spelled identically in Tasks 1, 4 and 6.
