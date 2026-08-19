# Mod-Wide Variance Toggles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add three mod-wide master switches (skill / trait / passion variance) that AND with the existing per-profile flags, and fix the missing per-section greying in the Profile Editor.

**Architecture:** Three `true`-defaulted bools on `PawnVarianceSettings`, read only through three accessor methods that return the strict AND with the resolved profile's own flag. Nothing writes the ANDed value into a `VarianceProfileValues` — custom profiles are returned from `Resolve` as a live reference, so a write would persist to the player's saved profile. The same three bools are drawn on the General tab and in each Profile Editor section header, and `SectionHeader` becomes the single owner of `GUI.enabled` for its section body.

**Tech Stack:** C# / .NET Framework 4.7.2, RimWorld 1.6, Harmony, Unity IMGUI (`Verse.Widgets`, `Listing_Standard`), RimWorld `Scribe` serialisation.

**Spec:** `docs/superpowers/specs/2026-08-19-mod-wide-variance-toggles-design.md`

## Global Constraints

- **This mod is published.** A player who updates and touches no settings must get bit-identical pawn generation. Every new persisted field defaults to `true`.
- **The field initialiser and the Scribe default must both be `true`** for all three new fields. Both load paths are live on `PawnVarianceSettings`: a first run never calls `ExposeData` (so the initialiser applies), an existing settings file hits `Scribe_Values.Look` with an absent node (so the default applies). `VarianceProfile.cs:30-45` documents a live case where the two drifted apart.
- **`SettingsTransfer.ConfigVersion` is NOT bumped.** Its comment (`SettingsTransfer.cs:20-22`) reserves it for incompatible payload shape changes; this change is purely additive with safe defaults.
- **No field is added to `VarianceProfileValues`.** `CustomProfile` serialisation must stay unchanged so existing custom profiles load untouched.
- **There is no unit-test project in this repo, by design.** The TDD cycle in the standard plan template does not apply. Each task's verification is: `dotnet build` clean, `tools/check-translation-keys.ps1` green where strings changed, and named in-game checks. This is stated so an implementer does not go and invent a test harness.
- **Translation keys ship in the same commit as their first use.** `check-translation-keys.ps1` reports both MISSING and ORPHAN; adding keys ahead of the code that uses them turns the gate red.
- **Build command:** `dotnet build Source/PawnVarianceMod.csproj -c Release`. Success criterion is `0 Error(s)`. MSB3245 reference-resolution warnings mean the RimWorld install path in the csproj is not resolving — fix that before reading the result, do not work around it.

---

## File Structure

| File | Responsibility in this change |
|---|---|
| `Source/PawnVarianceSettings.cs` | Owns the three new fields, their persistence, their transfer, the three accessors, and the General tab UI. |
| `Source/VarianceProfile.cs` | Comment only — points the three per-profile flags at their accessors. |
| `Source/HarmonyPatches.cs` | Four generation-path gates switched to accessors. |
| `Source/GrowthUpPatch.cs` | One growth-path early-return gate switched to accessors. |
| `Source/GrowUpVariance.cs` | Three growth-path gates switched to accessors. |
| `Source/ProfileEditorTab.cs` | `SectionHeader` gains the mod-wide checkbox and becomes the owner of section-body `GUI.enabled`. |
| `Languages/English/Keyed/VariedPawns.xml` | Nine new keys, added alongside the code that uses them. |
| `About/About.xml` | `<modVersion>` bump. |
| `HANDOVER.md` | Release note for the one visible behaviour change. |

---

### Task 1: Settings fields, persistence, and the accessors

No behaviour changes in this task — nothing reads the accessors yet. That is deliberate: it makes the persistence change reviewable on its own.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs` (fields near `:48`, accessors near `:319`, `ExposeData` near `:433`, `CopyFrom` near `:595`)
- Modify: `Source/VarianceProfile.cs:88-90` (comment only)

**Interfaces:**
- Consumes: nothing.
- Produces: `PawnVarianceSettings.enableSkillVarianceModWide`, `.enableTraitVarianceModWide`, `.enablePassionVarianceModWide` (all `public bool`, default `true`); `bool SkillVarianceActive(VarianceProfileValues v)`, `bool TraitVarianceActive(VarianceProfileValues v)`, `bool PassionVarianceActive(VarianceProfileValues v)` — all `public`, instance methods on `PawnVarianceSettings`, null-`v`-safe (return `false`).

- [ ] **Step 1: Add the three fields**

In `Source/PawnVarianceSettings.cs`, immediately after `public bool verboseLogging = false;` (`:48`):

```csharp
        // Mod-wide masters for the three variance dimensions. Strict AND with the per-profile flags
        // of the same name on VarianceProfileValues, authoritative in one direction only: off here
        // is a hard stop for every pawn and every profile; on here defers entirely to the profile.
        // A per-profile flag can never re-enable what these switch off.
        //
        // These exist because the per-profile flags are UNREACHABLE on the eight presets. The
        // Profile Editor body is drawn inside GUI.enabled = ... && EditingCustom, and no preset
        // sets the flags, so every preset inherits `= true` from the field initialiser. Without a
        // mod-wide switch, disabling one dimension means abandoning presets entirely and rebuilding
        // every faction/race/xenotype assignment as a custom profile.
        //
        // THE INITIALISER AND THE SCRIBE DEFAULT MUST BOTH BE `true`, and that is not pedantry.
        // Both load paths are live for this class: a first run never calls ExposeData at all
        // (LoadedModManager.ReadModSettings returns a fresh instance), so the initialiser applies;
        // an existing settings file predating this feature hits Scribe_Values.Look with an absent
        // node, so the Scribe default applies. Set either to false and every existing player
        // silently loses a whole dimension on update. The mod is published -- VarianceProfile.cs
        // :30-45 documents a live case where these two drifted apart, harmless there only because
        // that ctor is reachable by Scribe alone.
        public bool enableSkillVarianceModWide = true;
        public bool enableTraitVarianceModWide = true;
        public bool enablePassionVarianceModWide = true;
```

- [ ] **Step 2: Add the three accessors**

In `Source/PawnVarianceSettings.cs`, immediately above `public VarianceProfileValues ValuesFor(Pawn pawn) => ValuesFor(pawn, null);` (`:319`):

```csharp
        // The ONLY correct way to ask whether a dimension applies to a pawn: the strict AND of the
        // mod-wide master and the resolved profile's own flag. Reading either half alone is a bug.
        //
        // Deliberately NOT implemented by writing the ANDed value into the resolved
        // VarianceProfileValues. ValuesFor does not hand back a throwaway object: Resolve clones
        // presets (preset.MakeValues(), :246) but returns CUSTOM profiles as a LIVE REFERENCE
        // (:254, which carries its own comment forbidding the "fix"), and Active/Hostile are
        // session-cached clones (:550-553). An ANDed write would therefore persist a mod-wide
        // toggle into the player's saved custom profile on disk, and corrupt the cached
        // Active/Hostile values for the rest of the session. These read; they never write.
        //
        // Null-safe on v because a caller that failed to resolve a profile must not silently get
        // variance applied from a half-built state.
        public bool SkillVarianceActive(VarianceProfileValues v)
            => enableSkillVarianceModWide && v != null && v.enableSkillVariance;

        public bool TraitVarianceActive(VarianceProfileValues v)
            => enableTraitVarianceModWide && v != null && v.enableTraitVariance;

        public bool PassionVarianceActive(VarianceProfileValues v)
            => enablePassionVarianceModWide && v != null && v.enablePassionVariance;

```

- [ ] **Step 3: Persist them**

In `ExposeData`, immediately after `Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);` (`:433`):

```csharp
            // Defaults MUST be true -- see the comment on the fields. An existing settings file
            // written before this feature has no node here, and this default is what it gets.
            Scribe_Values.Look(ref enableSkillVarianceModWide, "enableSkillVarianceModWide", true);
            Scribe_Values.Look(ref enableTraitVarianceModWide, "enableTraitVarianceModWide", true);
            Scribe_Values.Look(ref enablePassionVarianceModWide, "enablePassionVarianceModWide", true);
```

- [ ] **Step 4: Carry them through settings import/export**

In `CopyFrom`, immediately after `verboseLogging = other.verboseLogging;` (`:595`):

```csharp
            // A config exported with a dimension disabled must import that way. Omitting these
            // would silently re-enable it on import.
            enableSkillVarianceModWide = other.enableSkillVarianceModWide;
            enableTraitVarianceModWide = other.enableTraitVarianceModWide;
            enablePassionVarianceModWide = other.enablePassionVarianceModWide;
```

- [ ] **Step 5: Point the per-profile flags at their accessors**

In `Source/VarianceProfile.cs`, immediately above `public bool enableSkillVariance = true;` (`:88`):

```csharp
        // DO NOT READ THESE DIRECTLY to decide whether a dimension applies -- they are only half
        // the answer. The effective value is the strict AND with the mod-wide master on
        // PawnVarianceSettings; go through SkillVarianceActive / TraitVarianceActive /
        // PassionVarianceActive there. Reading the field alone ignores the mod-wide switch.
```

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj -c Release`
Expected: `0 Error(s)`. Warnings about unused members are not expected — the accessors are `public`.

- [ ] **Step 7: Commit**

```bash
git add Source/PawnVarianceSettings.cs Source/VarianceProfile.cs
git commit -m "feat(settings): mod-wide skill/trait/passion masters and their accessors

Three true-defaulted bools plus SkillVarianceActive/TraitVarianceActive/
PassionVarianceActive, which return the strict AND with the resolved profile's
own flag. Nothing reads them yet -- behaviour is unchanged.

The accessors read and never write: Resolve returns custom profiles as a live
reference and Active/Hostile are session-cached clones, so ANDing into the
resolved values would persist a mod-wide toggle into a saved profile on disk."
```

---

### Task 2: Wire the eight gates

**Files:**
- Modify: `Source/HarmonyPatches.cs:51`, `:58-60`
- Modify: `Source/GrowthUpPatch.cs:140`
- Modify: `Source/GrowUpVariance.cs:139-141`

**Interfaces:**
- Consumes: `settings.SkillVarianceActive(v)`, `settings.TraitVarianceActive(v)`, `settings.PassionVarianceActive(v)` from Task 1.
- Produces: nothing new.

In all three files a local named `settings` is already in scope at the edit site, and a local named `v` (the resolved `VarianceProfileValues`) is already in scope. Do not introduce new locals.

- [ ] **Step 1: Generation path — the all-three early return**

In `Source/HarmonyPatches.cs`, replace line 51:

```csharp
            if (!v.enableSkillVariance && !v.enableTraitVariance && !v.enablePassionVariance) return;
```

with:

```csharp
            // Accessors, not the raw flags. Left reading the raw flags, a pawn with every dimension
            // disabled mod-wide would still pay for QualityRoller.RollQuality and the full profile
            // resolution walk below, on every single generation.
            if (!settings.SkillVarianceActive(v) && !settings.TraitVarianceActive(v) && !settings.PassionVarianceActive(v)) return;
```

- [ ] **Step 2: Generation path — the three per-dimension gates**

In `Source/HarmonyPatches.cs`, replace lines 58-60:

```csharp
                if (v.enableTraitVariance) TraitVarianceApplier.Apply(pawn, quality, request, v);
                if (v.enableSkillVariance) SkillVarianceApplier.Apply(pawn, quality, v);
                if (v.enablePassionVariance) PassionVarianceApplier.Apply(pawn, quality, v);
```

with:

```csharp
                if (settings.TraitVarianceActive(v)) TraitVarianceApplier.Apply(pawn, quality, request, v);
                if (settings.SkillVarianceActive(v)) SkillVarianceApplier.Apply(pawn, quality, v);
                if (settings.PassionVarianceActive(v)) PassionVarianceApplier.Apply(pawn, quality, v);
```

- [ ] **Step 3: Growth path — the all-three early return**

In `Source/GrowthUpPatch.cs`, replace line 140:

```csharp
            if (!v.enableSkillVariance && !v.enableTraitVariance && !v.enablePassionVariance) return;
```

with:

```csharp
            // Accessors, not the raw flags -- same reason as HarmonyPatches.cs.
            if (!settings.SkillVarianceActive(v) && !settings.TraitVarianceActive(v) && !settings.PassionVarianceActive(v)) return;
```

- [ ] **Step 4: Growth path — the three per-dimension gates**

In `Source/GrowUpVariance.cs`, replace lines 139-141:

```csharp
                if (v.enableTraitVariance) ApplyTraitGrowthUp(pawn, quality, triggerPath, v);
                if (v.enableSkillVariance) ApplySkillGrowthUp(pawn, quality, v);
                if (v.enablePassionVariance) ApplyPassionGrowthUp(pawn, quality, v);
```

with:

```csharp
                if (settings.TraitVarianceActive(v)) ApplyTraitGrowthUp(pawn, quality, triggerPath, v);
                if (settings.SkillVarianceActive(v)) ApplySkillGrowthUp(pawn, quality, v);
                if (settings.PassionVarianceActive(v)) ApplyPassionGrowthUp(pawn, quality, v);
```

- [ ] **Step 5: Confirm no raw reads remain on a decision path**

Run: `grep -n "\.enableSkillVariance\|\.enableTraitVariance\|\.enablePassionVariance" Source/*.cs`

Expected: matches ONLY in `PawnVarianceSettings.cs` (inside the three accessors), `VarianceProfile.cs` (declarations, `ExposeData`, and any preset object initialisers), and `ProfileEditorTab.cs` (the `SectionHeader` call sites, which bind the UI to the field and must keep reading it directly). Any match in `HarmonyPatches.cs`, `GrowUpVariance.cs` or `GrowthUpPatch.cs` is a missed gate — fix it before continuing.

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj -c Release`
Expected: `0 Error(s)`.

- [ ] **Step 7: In-game — confirm nothing changed at defaults**

This is the back-compat regression check from spec §7.7, and it is the step that would catch an inverted default. Launch RimWorld through GABS (`games_start` then `games_connect` — GABS must launch the game itself or the bridge handshake fails), then run the debug action **`Varied Pawns > Roll pawns and dump distribution`** at 1000 pawns.

Expected: the model-vs-delivered delta is inside tolerance and reports OK, exactly as on the pre-change build. Record the delivered pips/pawn figure in the commit message so the next task can compare against it.

- [ ] **Step 8: Commit**

```bash
git add Source/HarmonyPatches.cs Source/GrowthUpPatch.cs Source/GrowUpVariance.cs
git commit -m "feat(variance): gate all eight apply sites on the mod-wide masters

Generation (HarmonyPatches) and growth-up (GrowthUpPatch, GrowUpVariance) now
ask the accessors instead of reading the per-profile flags directly. The two
all-three early returns are included deliberately: left on the raw flags, a
pawn with everything disabled mod-wide would still pay for RollQuality and the
full profile resolution walk.

At default settings the accessors are identity functions over the per-profile
flags, so generation is unchanged; verified with the 1000-pawn distribution
dump."
```

---

### Task 3: General tab UI

**Files:**
- Modify: `Source/PawnVarianceSettings.cs` — `DrawGlobalSettings`, immediately before `DrawShareSettingsSection(listing);` (`:1360`)
- Modify: `Languages/English/Keyed/VariedPawns.xml` — six keys, in the General block after `VP_VerboseLoggingTip` (`:172`)

**Interfaces:**
- Consumes: the three fields from Task 1; the existing private helpers `Section(Listing_Standard, string)` (`:1227`) and `Caption(Listing_Standard, string)` (`:1237`).
- Produces: translation keys `VP_Section_VarianceTypes`, `VP_VarianceTypesCaption`, `VP_ModWideSkill`, `VP_ModWideTrait`, `VP_ModWidePassion`, `VP_ModWideTip`.

- [ ] **Step 1: Add the six keys**

In `Languages/English/Keyed/VariedPawns.xml`, immediately after the `VP_VerboseLoggingTip` line (`:172`):

```xml

  <VP_Section_VarianceTypes>Variance types</VP_Section_VarianceTypes>
  <VP_VarianceTypesCaption>Master switches for the three kinds of variance this mod applies.</VP_VarianceTypesCaption>

  <VP_ModWideSkill>Skill variance</VP_ModWideSkill>
  <VP_ModWideTrait>Trait variance</VP_ModWideTrait>
  <VP_ModWidePassion>Passion variance</VP_ModWidePassion>
  <VP_ModWideTip>Applies to every pawn, whichever profile or override matches them.\n\nTurning this off here overrides the matching setting inside every profile, and no profile can turn it back on. Turning it on hands the decision back to each profile.\n\nUse this to let another mod own a dimension. With trait variance off, a mod such as More Trait Slots decides trait counts and Varied Pawns leaves them alone.</VP_ModWideTip>
```

- [ ] **Step 2: Draw the section**

In `Source/PawnVarianceSettings.cs`, in `DrawGlobalSettings`, immediately before the line `DrawShareSettingsSection(listing);` (`:1360`):

```csharp
            // Mirrored in the Profile Editor's section headers -- same three fields, drawn twice on
            // purpose. Discoverable here alongside the other every-profile toggles; adjustable
            // there, where the greying makes the consequence visible.
            Section(listing, "VP_Section_VarianceTypes".Translate());
            Caption(listing, "VP_VarianceTypesCaption".Translate());

            listing.CheckboxLabeled(
                "VP_ModWideSkill".Translate(),
                ref enableSkillVarianceModWide,
                "VP_ModWideTip".Translate());
            listing.CheckboxLabeled(
                "VP_ModWideTrait".Translate(),
                ref enableTraitVarianceModWide,
                "VP_ModWideTip".Translate());
            listing.CheckboxLabeled(
                "VP_ModWidePassion".Translate(),
                ref enablePassionVarianceModWide,
                "VP_ModWideTip".Translate());

```

- [ ] **Step 3: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj -c Release`
Expected: `0 Error(s)`.

- [ ] **Step 4: Check translation keys**

Run (PowerShell): `.\tools\check-translation-keys.ps1`
Expected: no MISSING and no ORPHAN entries. An ORPHAN here means a key was added that Step 2 does not actually reference — fix the code, not the XML.

- [ ] **Step 5: In-game — see the section and confirm it persists**

Open **Options > Mod settings > Varied Pawns > General**, scroll to "Variance types". Confirm all three boxes are ticked on first sight (this is the default asserting itself). Untick "Trait variance", close the settings window, reopen it, and confirm it is still unticked.

- [ ] **Step 6: Commit**

```bash
git add Source/PawnVarianceSettings.cs Languages/English/Keyed/VariedPawns.xml
git commit -m "feat(ui): Variance types master switches on the General tab

Three checkboxes bound to the mod-wide fields, with a shared tooltip whose job
is the precedence story: they beat the per-profile setting, which cannot turn
them back on. Names the More Trait Slots case, since handing a dimension to
another mod is the use players will arrive looking for."
```

---

### Task 4: Profile Editor — mirrored switches and the greying rule

This task also fixes the pre-existing bug where a section unticked on a custom profile left its sliders draggable. Both flags feed one rule, so they are fixed together.

**Files:**
- Modify: `Source/ProfileEditorTab.cs` — `SectionHeader` (`:51-73`), `DrawProfileEditorTab` (`:100-103`), `DrawGenerationSettings` (`:337`) and its three `SectionHeader` call sites (`:381`, `:401`, and the Skills one at `:340`)
- Modify: `Languages/English/Keyed/VariedPawns.xml` — three keys, after `VP_Section_TraitsTip` (`:115`)

**Interfaces:**
- Consumes: the three fields from Task 1; the existing key `VP_Enable` (`:67` in the XML); `PawnVarianceMod.Settings.EditingCustom`.
- Produces: `SectionHeader(Listing_Standard listing, string title, bool outerEnabled, ref bool enabled, ref bool modWide, string tooltip = null)` — signature change, all three call sites updated. Translation keys `VP_ModWide`, `VP_ModWideOffHint`, `VP_ProfileOffHint`.

- [ ] **Step 1: Add the three keys**

In `Languages/English/Keyed/VariedPawns.xml`, immediately after the `VP_Section_TraitsTip` line (`:115`):

```xml
  <VP_ModWide>Mod-wide</VP_ModWide>
  <VP_ModWideOffHint>Turned off mod-wide under General. This profile's setting is ignored.</VP_ModWideOffHint>
  <VP_ProfileOffHint>Turned off for this profile.</VP_ProfileOffHint>
```

- [ ] **Step 2: Move the EditingCustom narrowing out of the caller**

`SectionHeader` must draw its mod-wide checkbox at the TRUE outer enabled state, because that box is a mod-wide setting and has to stay clickable while viewing a preset — the one case the caller's blanket narrow forbids. So the caller stops narrowing and hands the state down instead.

In `Source/ProfileEditorTab.cs`, replace lines 100-103:

```csharp
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;
```

with:

```csharp
            // GUI.enabled is deliberately NOT narrowed by EditingCustom here any more.
            // DrawGenerationSettings and SectionHeader own it now: SectionHeader has to draw its
            // mod-wide checkbox at the true outer state, since that box is a MOD-WIDE setting and
            // must stay clickable while a preset is being viewed -- which is exactly what a blanket
            // narrow here forbade. Every control below a header is still greyed on a preset;
            // SectionHeader re-applies EditingCustom itself as part of the greying rule.
            bool wasEnabled = GUI.enabled;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;
```

- [ ] **Step 3: Rewrite SectionHeader**

In `Source/ProfileEditorTab.cs`, replace the whole of `SectionHeader` (`:51-73`) with:

```csharp
        // Draws a section title, the two enable checkboxes, the reason-for-greying caption, and
        // then SETS GUI.enabled FOR EVERYTHING BELOW IT until the next header. That last part is
        // the point of this method: before it existed, no GUI.enabled write in this file consulted
        // the section enable flags at all, so unticking Traits on a custom profile left the trait
        // sliders fully draggable while doing nothing.
        //
        // outerEnabled is the caller's true GUI.enabled, captured BEFORE any narrowing.
        private static void SectionHeader(
            Listing_Standard listing, string title, bool outerEnabled,
            ref bool enabled, ref bool modWide, string tooltip = null)
        {
            GUI.enabled = outerEnabled;

            listing.Gap(SectionGap);
            listing.GapLine(0f);

            Rect row = listing.GetRect(30f);
            Rect titleRect = row.LeftPart(0.44f);
            Rect modWideRect = new Rect(row.x + row.width * 0.46f, row.y, row.width * 0.26f, row.height);
            Rect boxRect = row.RightPart(0.26f);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            // BOTH checkboxes sit outside the grey they control, for two different reasons.
            // The mod-wide box: it is a mod-wide setting, so it must stay clickable while the body
            // is greyed for a preset -- otherwise it is dead on exactly the eight profiles this
            // feature exists to rescue. It is therefore NOT gated on EditingCustom.
            bool modWideVal = modWide;
            Widgets.CheckboxLabeled(modWideRect, "VP_ModWide".Translate(), ref modWideVal);
            modWide = modWideVal;

            // The per-profile box: if it lived inside the grey it controls, unticking it would lock
            // the player out of re-ticking it. Still gated on EditingCustom, since it edits profile
            // data. GUI.enabled AND the write guard, matching every other editable control here.
            bool editingCustom = PawnVarianceMod.Settings.EditingCustom;
            GUI.enabled = outerEnabled && editingCustom;
            bool val = enabled;
            Widgets.CheckboxLabeled(boxRect, "VP_Enable".Translate(), ref val);
            if (editingCustom) enabled = val;

            GUI.enabled = outerEnabled;
            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(4f);

            // Why the body below is greyed, in precedence order. Mod-wide wins the message because
            // it wins the behaviour: when it is off, the profile flag genuinely is ignored. Drawn
            // greyed itself, matching the VP_EnableOverridesHint pattern on the Overrides tab.
            if (!modWide || !enabled)
            {
                GUI.enabled = false;
                Caption(listing, !modWide
                    ? "VP_ModWideOffHint".Translate()
                    : "VP_ProfileOffHint".Translate());
            }

            // THE GREYING RULE. EditingCustom is re-applied here because Step 2 removed it from the
            // caller -- drop it and every slider becomes editable on a preset, silently mutating
            // the resolved values a preset hands out.
            GUI.enabled = outerEnabled && editingCustom && modWide && enabled;
        }
```

- [ ] **Step 4: Update the three call sites**

In `Source/ProfileEditorTab.cs`, `DrawGenerationSettings` (`:337`), replace:

```csharp
            var v = Editing;

            SectionHeader(listing, "VP_Section_Skills".Translate(), ref v.enableSkillVariance,
                "VP_Section_SkillsTip".Translate());
```

with:

```csharp
            var v = Editing;
            // Captured before any header narrows it, and restored at the end of this method.
            bool outerEnabled = GUI.enabled;

            SectionHeader(listing, "VP_Section_Skills".Translate(), outerEnabled,
                ref v.enableSkillVariance, ref enableSkillVarianceModWide,
                "VP_Section_SkillsTip".Translate());
```

Replace (`:381`):

```csharp
            SectionHeader(listing, "VP_Section_Traits".Translate(), ref v.enableTraitVariance,
                "VP_Section_TraitsTip".Translate());
```

with:

```csharp
            SectionHeader(listing, "VP_Section_Traits".Translate(), outerEnabled,
                ref v.enableTraitVariance, ref enableTraitVarianceModWide,
                "VP_Section_TraitsTip".Translate());
```

Replace (`:401`):

```csharp
            SectionHeader(listing, "VP_Section_Passions".Translate(), ref v.enablePassionVariance,
                "VP_Section_PassionsTip".Translate());
```

with:

```csharp
            SectionHeader(listing, "VP_Section_Passions".Translate(), outerEnabled,
                ref v.enablePassionVariance, ref enablePassionVarianceModWide,
                "VP_Section_PassionsTip".Translate());
```

- [ ] **Step 5: Restore GUI.enabled at the end of DrawGenerationSettings**

The last `SectionHeader` leaves `GUI.enabled` narrowed, and `Listing_Standard` does not restore it. At the very end of `DrawGenerationSettings`, immediately after the final `Caption(listing, v.passionCountMin > 0f ? ... : ...);` and before the closing brace of the method:

```csharp

            // The Passions header left GUI.enabled narrowed; nothing else restores it, and leaving
            // it narrowed leaks into whatever the caller draws next.
            GUI.enabled = outerEnabled;
```

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj -c Release`
Expected: `0 Error(s)`. A CS1620 ("argument must be passed with the ref keyword") means a call site in Step 4 was missed.

- [ ] **Step 7: Check translation keys**

Run (PowerShell): `.\tools\check-translation-keys.ps1`
Expected: no MISSING and no ORPHAN entries.

- [ ] **Step 8: In-game — the greying matrix**

**This cannot be automated.** `ProfileEditorTab.cs:245-246` records that GABS's `get_ui_layout` cannot observe ambient `GUI.enabled`, so no layout probe can see a greying regression in this file. Check by eye or screenshot.

Open **Mod settings > Varied Pawns > Profile editor** and confirm each row:

| Profile shown | Mod-wide | Per-profile | Expected |
|---|---|---|---|
| Any preset | on | on (forced) | Body greyed as today. Mod-wide box **clickable**. Enable box greyed. No hint caption. |
| Any preset | off | on (forced) | Body greyed. Mod-wide box clickable. Hint reads "Turned off mod-wide…". |
| Custom | on | on | Body fully editable, as today. No hint caption. |
| Custom | on | off | **Body greyed** — this is the bug fix. Hint reads "Turned off for this profile." Both checkboxes still clickable. |
| Custom | off | on | Body greyed. Hint reads "Turned off mod-wide…". Both checkboxes still clickable. |

Also confirm: after unticking a section's per-profile box, you can tick it straight back on. If you cannot, the box was drawn inside its own grey.

- [ ] **Step 9: In-game — confirm the switches actually mirror**

Untick "Trait variance" on the **General** tab, switch to the **Profile editor** tab, and confirm the Traits section's Mod-wide box is now unticked and its body greyed. Tick it back on from the Profile editor, return to General, and confirm the General checkbox followed.

- [ ] **Step 10: Commit**

```bash
git add Source/ProfileEditorTab.cs Languages/English/Keyed/VariedPawns.xml
git commit -m "feat(ui): mirror the mod-wide masters into the profile editor, and grey each section

SectionHeader now draws a Mod-wide checkbox beside the per-profile Enable box
and owns GUI.enabled for its section body. The EditingCustom narrowing moves
out of DrawProfileEditorTab and into SectionHeader, because the mod-wide box
must stay clickable while a preset is being viewed -- the exact case the
blanket narrow forbade.

Fixes a pre-existing bug found while designing this: no GUI.enabled write in
this file consulted the section enable flags, so unticking a section on a
custom profile left its sliders draggable while doing nothing."
```

---

### Task 5: Release metadata and the back-compat regression pass

**Files:**
- Modify: `About/About.xml` (`<modVersion>`)
- Modify: `HANDOVER.md`

**Interfaces:**
- Consumes: everything from Tasks 1-4.
- Produces: nothing consumed by later tasks.

- [ ] **Step 1: Back up the live settings file**

Before touching anything, copy the current settings file aside so the regression check runs against a genuine pre-change config:

```bash
cp "$LOCALAPPDATA/../LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/Mod_kalas.pawnvariance_PawnVarianceMod.xml" zzz-Do-Not-Commit/settings-pre-modwide.xml
```

If the filename differs, find it with `ls "$LOCALAPPDATA/../LocalLow/Ludeon Studios/RimWorld by Ludeon Studios/Config/" | grep -i variance`. Do not proceed without a copy — this file is the evidence for Step 2.

- [ ] **Step 2: Verify the untouched-settings guarantee**

Confirm the backed-up file contains **no** `enableSkillVarianceModWide` / `enableTraitVarianceModWide` / `enablePassionVarianceModWide` nodes:

```bash
grep -c "ModWide" zzz-Do-Not-Commit/settings-pre-modwide.xml
```

Expected: `0`. Then launch the game with that file in place and open the General tab. Expected: all three "Variance types" boxes are **ticked**. That is the Scribe default proving itself against a real pre-change file — the single check that would catch an inverted default before it reaches players.

- [ ] **Step 3: Regression — same settings, same distribution**

Run **`Varied Pawns > Roll pawns and dump distribution`** at 1000 pawns with the restored pre-change settings and all masters left at their defaults.

Expected: the delivered pips/pawn figure matches the one recorded in Task 2 Step 7 to within the action's own reported tolerance, and the check reports OK. A difference here means an accessor is not the identity function it is supposed to be at defaults.

- [ ] **Step 4: Regression — settings round-trip**

In the settings window: untick "Trait variance", use **Share settings > Copy** to export, tick it back on, then **Share settings > Paste** to import. Expected: "Trait variance" is unticked again after the import. This exercises the `CopyFrom` change from Task 1 Step 4.

- [ ] **Step 5: Compatibility — hand traits to More Trait Slots**

With **More Trait Slots** (`Arkymn.MoreTraitSlots`) active and "Trait variance" off mod-wide, generate pawns and confirm trait counts follow More Trait Slots' own min/max sliders rather than the profile's trait count range. This is the case the whole feature exists for, and it is not covered by any of the checks above.

- [ ] **Step 6: Bump the mod version**

In `About/About.xml`, change `<modVersion>1.0.0</modVersion>` to `<modVersion>1.1.0</modVersion>`. `tools/build-release.ps1` reads this to name the release zip.

- [ ] **Step 7: Record the one visible change**

Add to `HANDOVER.md`, under a `## Changes in 1.1.0` heading (create it if absent):

```markdown
## Changes in 1.1.0

**Mod-wide variance toggles.** Options > Mod settings > Varied Pawns > General now carries
master switches for skill, trait and passion variance. Off is a hard stop for every pawn and
every profile; on defers to each profile's own setting, which can never re-enable what the
master switched off. The same three switches are mirrored into each Profile Editor section
header. Use them to hand a dimension to another mod — with trait variance off, More Trait Slots
decides trait counts and this mod leaves them alone.

**Fixed: Profile Editor sections did not grey out.** Unticking a section on a custom profile
left its sliders draggable even though the setting was already being honoured at generation.
No generated pawn changes; the UI was lying about it. Players with a section unticked will see
those sliders greyed after updating.

**Upgrading is safe.** All three master switches default to on, so a player who updates and
changes nothing gets identical pawn generation. Nothing is written into colony saves.
```

- [ ] **Step 8: Commit**

```bash
git add About/About.xml HANDOVER.md
git commit -m "chore(release): bump to 1.1.0 and record the mod-wide toggles

Verified against a real pre-change settings file with no ModWide nodes: all
three masters read as on, the 1000-pawn distribution dump is unchanged, and a
settings export/import round-trips a disabled dimension."
```

---

## Post-Plan Notes

**The upload itself is the owner's.** The Steam Workshop changelog is posted at upload time through RimWorld's own uploader; nothing in this plan can do it. Step 7's `HANDOVER.md` text is written to be pasted straight into that box.

**What no offline check here can catch.** This project's own history is that clean builds, `envelope_check.py` and agent review have all passed while the generator disagreed with every model. The checks that carry real weight in this plan are Task 2 Step 7, Task 4 Step 8, and Task 5 Steps 2, 3 and 5 — all of which require the game running. Do not report this plan complete on a green build.
