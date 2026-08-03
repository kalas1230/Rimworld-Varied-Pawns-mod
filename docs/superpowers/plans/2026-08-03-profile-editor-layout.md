# Profile Editor Layout Redesign — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the Profile Editor tab's 1600-2000px single scroll with a pinned 140px header over a ~500px compacted body, without changing any profile value.

**Architecture:** Split the tab rect once into two sibling rects — a fixed-height header drawn with explicit `Rect` maths, and a scrolling `Listing_Standard` body. Compact the body by replacing eight paired min/max sliders with four `Widgets.FloatRange` controls, folding enable checkboxes into section headers, and demoting static captions to tooltips.

**Tech Stack:** C# 9, .NET Framework 4.7.2, RimWorld 1.6 (`Assembly-CSharp`), Unity IMGUI (`Verse.Widgets`, `Verse.Listing_Standard`), Harmony 2.

**Spec:** [`docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md`](../specs/2026-08-03-profile-editor-layout-design.md)

## Global Constraints

- **Build must be clean.** `dotnet build Source/PawnVarianceMod.csproj` returns `0 Error(s), 0 Warning(s)`. Any warning is a task failure.
- **Presentation change only.** No task may alter a value in `VarianceProfileValues`, `CalculateCompositeScore`, `MapToCenteredX`, `TierForQuality`, or any preset in `VarianceProfile.cs`. HANDOVER Rule 5 requires the project owner's sign-off for those; this work has no such sign-off.
- **`Widgets.IntRange` is forbidden** on all four min/max pairs. `passionCountMin`/`Max` hold fractional calibrated values (`1.4`, `2.5`, `3.0`, `5.0`, `6.0`, `6.2`) and `IntRange` truncates them silently.
- **No `roundTo` quantisation** on any `Widgets.FloatRange` call.
- **Display precision:** trait counts `ToStringStyle.Integer`; passion counts `ToStringStyle.FloatOne` (approved display-only change, signed off 2026-08-03); skill shifts one decimal.
- **No new persisted state.** No new `Scribe_Values` / `Scribe_Deep` entries, no new fields on `CustomProfile` or `VarianceProfileValues`. Settings and clipboard payloads must round-trip unchanged.
- **The Overall Power number stays visible** in the header readout — not hidden in a tooltip.
- **Build/deploy loop** (from HANDOVER):
  ```powershell
  tasklist /FI "IMAGENAME eq RimWorldWin64.exe"   # must show no running instance first
  dotnet build Source/PawnVarianceMod.csproj
  Copy-Item Assemblies/PawnVarianceMod.dll, Assemblies/PawnVarianceMod.pdb "C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/" -Force
  ```

## ⚠️ On TDD — read this before Task 1

**This repository has no unit-test harness, and this plan does not add one.**

There is no test project, no xunit/nunit reference, and no `dotnet test` target — `Source/PawnVarianceMod.csproj` is a single library assembly referencing `Assembly-CSharp.dll` from the RimWorld install. Every symbol this plan touches (`Widgets`, `Listing_Standard`, `GUI`, `Text`, `TooltipHandler`, `Dialog_Rename`) is a Unity/Verse static that cannot be instantiated outside a running game process. The repo's established practice is manual in-game test plans (`docs/superpowers/2026-07-29-child-growthup-test-plan.md`, `docs/superpowers/2026-07-30-growth-moment-ordering-test-plan.md`).

Standing up an in-process test harness for IMGUI code is a larger project than this redesign and was not requested.

**So each task's verification is: (a) a clean build, and (b) named, specific in-game observations.** Where a step says "observe", it means launch RimWorld, open Mod Settings → Varied Pawns → Profile Editor, and check the stated condition. Do not report a task complete on a clean build alone — the build proves it compiles, not that the layout is right.

**Do not fabricate test files, and do not claim a task passed a test that was not run.** If you cannot launch RimWorld, say so and stop; report which tasks are build-verified only.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `Source/PawnVarianceSettings.cs` | Modify (shrinks ~460 lines) | Settings state, Scribe, override resolution, General + Overrides tabs, shared UI helpers |
| `Source/ProfileEditorTab.cs` | **Create** | `partial class PawnVarianceSettings` — everything that draws the Profile Editor tab: header, body, range helpers, distribution curve |
| `Source/Dialog_RenameProfile.cs` | **Create** | `Dialog_Rename` subclass for renaming a custom profile |
| `Source/VarianceProfile.cs` | Modify (Task 5 only, ~6 lines) | Adds `IRenameable` to `CustomProfile` so `Dialog_Rename<T>` accepts it. **No numeric field, preset value, or `ExposeData` body may change** — those are governed by HANDOVER Rule 5. |

`PawnVarianceSettings.cs` is 1255 lines. This work adds ~250 lines of UI code to it. Task 1 moves the Profile Editor drawing into its own partial-class file first so the later tasks edit a focused ~500-line file. The split is purely mechanical — no logic changes.

---

## Task 1: Mechanical split into a partial class

**Files:**
- Modify: `Source/PawnVarianceSettings.cs` (remove methods; add `partial` keyword at line 19)
- Create: `Source/ProfileEditorTab.cs`

**Interfaces:**
- Consumes: nothing (first task).
- Produces: `partial class PawnVarianceSettings` declared in two files. All moved methods keep their exact current names, signatures, and `private`/`private static` accessibility, so callers elsewhere in the class are unaffected.

**This task changes no behaviour.** If the tab looks even slightly different afterward, something was mistyped.

- [ ] **Step 1: Mark the class partial**

In `Source/PawnVarianceSettings.cs` line 19, change:

```csharp
    public class PawnVarianceSettings : ModSettings
```

to:

```csharp
    public partial class PawnVarianceSettings : ModSettings
```

- [ ] **Step 2: Create the new file with the moved methods**

Create `Source/ProfileEditorTab.cs`. Cut these methods **verbatim** from `PawnVarianceSettings.cs` and paste them in:

- `DrawProfileEditorTab` (line 490)
- `DrawProfileSelector` (line 848)
- `DrawNameField` (line 928)
- `DrawGenerationSettings` (line 940)
- `DrawChildSkillShift` (line 990)
- `DrawQualityDistributionCurve` (line 1187)
- `DrawTierBand` (follows `DrawQualityDistributionCurve`)
- `DrawVerticalTierMarker` (follows `DrawTierBand`)

The file skeleton:

```csharp
using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public partial class PawnVarianceSettings
    {
        // ... the eight methods, pasted verbatim ...
    }
}
```

Leave `Section`, `Caption`, `LabeledSlider`, `ProfileMenu`, `TierForQuality`, `CalculateCompositeScore`, and `MapToCenteredX` where they are — they are shared with the General and Overrides tabs.

- [ ] **Step 3: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

If you get `CS0111` (member defined twice), a method was copied instead of moved — delete it from `PawnVarianceSettings.cs`.

- [ ] **Step 4: Observe in-game that nothing changed**

Deploy and launch. Open Mod Settings → Varied Pawns → Profile Editor.
Expected: pixel-identical to before — same header, same four sections, same scroll length.

- [ ] **Step 5: Commit**

```bash
git add Source/PawnVarianceSettings.cs Source/ProfileEditorTab.cs
git commit -m "refactor: move Profile Editor drawing into a partial class file"
```

---

## Task 2: Replace the four min/max slider pairs with `Widgets.FloatRange`

**Files:**
- Modify: `Source/ProfileEditorTab.cs` (`DrawGenerationSettings`, `DrawChildSkillShift`)

**Interfaces:**
- Consumes: `partial class PawnVarianceSettings` from Task 1.
- Produces:
  ```csharp
  private const int SkillShiftRangeId      = 1001;
  private const int ChildSkillShiftRangeId = 1002;
  private const int TraitCountRangeId      = 1003;
  private const int PassionCountRangeId    = 1004;

  // Draws "label" on the left, a FloatRange on the right. Writes the handles back
  // into lo/hi. Consumes 42px of listing height (32px control + 10px ControlGap).
  private static void LabeledFloatRange(
      Listing_Standard listing, string label, int id,
      ref float lo, ref float hi, float min, float max,
      ToStringStyle style, string tooltip = null);
  ```

**Why `FloatRange` and never `IntRange`:** `passionCountMin`/`Max` hold fractional calibrated values across the shipped presets — `1.4`, `2.5`, `3.0`, `5.0`, `6.0`, `6.2` (`Source/VarianceProfile.cs:280-394`). `Widgets.IntRange` would truncate `6.2` → `6` the first time a user duplicated Elite and nudged the handle, silently recalibrating a value that HANDOVER's ±35% envelope is measured against. Trait counts happen to be integral, but they use `FloatRange` too, so the hazard cannot come back.

- [ ] **Step 1: Add the id constants and the helper**

At the top of the `PawnVarianceSettings` body in `Source/ProfileEditorTab.cs`:

```csharp
        // Stable, unique control ids. Widgets.FloatRange uses the caller-supplied id
        // against its own static drag-tracking state, NOT GUIUtility.GetControlID
        // auto-indexing -- so conditionally rendering the child-shift block cannot
        // shift these or hijack an in-progress drag. Never reuse or auto-generate them.
        private const int SkillShiftRangeId      = 1001;
        private const int ChildSkillShiftRangeId = 1002;
        private const int TraitCountRangeId      = 1003;
        private const int PassionCountRangeId    = 1004;

        private const float RangeRowHeight = 32f;
        private const float RangeLabelFrac = 0.42f;

        private static void LabeledFloatRange(
            Listing_Standard listing, string label, int id,
            ref float lo, ref float hi, float min, float max,
            ToStringStyle style, string tooltip = null)
        {
            Rect row = listing.GetRect(RangeRowHeight);
            Rect labelRect = row.LeftPart(RangeLabelFrac);
            Rect rangeRect = row.RightPart(1f - RangeLabelFrac - 0.02f);

            // Vertically centre the label against the range control.
            labelRect.y += 4f;
            Widgets.Label(labelRect, label);

            // Marshal into a local: an r-value struct cannot be passed by ref.
            var range = new FloatRange(lo, hi);
            Widgets.FloatRange(rangeRect, id, ref range, min, max, null, style);
            lo = range.min;
            hi = range.max;

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(ControlGap);
        }
```

Note the deliberate omissions: no `roundTo` argument, and `labelKey` is passed as `null`. A non-null `labelKey` is run through `.Translate()`, which this mod has no language file for.

- [ ] **Step 2: Convert the skill shift pair**

In `DrawGenerationSettings`, delete these three lines (currently `PawnVarianceSettings.cs:955-957`, now in `ProfileEditorTab.cs`):

```csharp
            Caption(listing, $"Skill shift range (applied on top of vanilla roll):  {v.skillShiftMin:F1} to {v.skillShiftMax:F1}");
            v.skillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {v.skillShiftMin:F1}", v.skillShiftMin, -20f, 20f);
            v.skillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {v.skillShiftMax:F1}", v.skillShiftMax, -20f, 20f);
```

Replace with:

```csharp
            LabeledFloatRange(listing, "Skill shift", SkillShiftRangeId,
                ref v.skillShiftMin, ref v.skillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                "Applied on top of the vanilla roll. The low handle is the shift for the lowest-quality pawn, the high handle for the highest-quality pawn.");
```

- [ ] **Step 3: Convert the child skill shift pair**

In `DrawChildSkillShift`, inside the `if (v.applyChildSkillShift)` block, delete:

```csharp
                Caption(listing, $"Skill shift at age 13 growth moment (hard limit per skill):  {v.childSkillShiftMin:F1} to {v.childSkillShiftMax:F1}");
                v.childSkillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {v.childSkillShiftMin:F1}", v.childSkillShiftMin, -20f, 20f);
                v.childSkillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {v.childSkillShiftMax:F1}", v.childSkillShiftMax, -20f, 20f);
```

Replace with:

```csharp
                LabeledFloatRange(listing, "Child shift at 13", ChildSkillShiftRangeId,
                    ref v.childSkillShiftMin, ref v.childSkillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                    "Hard limit per skill at the age-13 growth moment.");
```

Keep the `Caption` immediately after it — the one that reads `"The minimum is at or above zero…"` / `"…can lose up to N levels…"`. It is computed from live values, so it survives the Task 3 tooltip sweep.

- [ ] **Step 4: Convert the trait count pair**

In the Traits section, delete:

```csharp
            Caption(listing, v.countProtectedTraits
                ? $"Total traits on the pawn:  {v.traitCountMin:F0} to {v.traitCountMax:F0}"
                : $"Traits this mod rolls, forced traits added on top:  {v.traitCountMin:F0} to {v.traitCountMax:F0}");
            v.traitCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {v.traitCountMin:F0}", v.traitCountMin, 0f, 15f);
            v.traitCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {v.traitCountMax:F0}", v.traitCountMax, 0f, 15f);
```

Replace with:

```csharp
            LabeledFloatRange(listing, "Trait count", TraitCountRangeId,
                ref v.traitCountMin, ref v.traitCountMax, 0f, 15f, ToStringStyle.Integer,
                v.countProtectedTraits
                    ? "Total traits on the pawn, including xenotype and forced traits."
                    : "Traits this mod rolls. Xenotype, gene, backstory and scenario traits are added on top.");
```

The conditional text is preserved — it moved from a caption into the tooltip, so the meaning of the range still depends on the checkbox above it.

- [ ] **Step 5: Convert the passion count pair**

In the Passions section, delete:

```csharp
            Caption(listing, $"Total passion budget (Minor = 1, Major = 2):  {v.passionCountMin:F0} to {v.passionCountMax:F0}");
            v.passionCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {v.passionCountMin:F0}", v.passionCountMin, 0f, 24f);
            v.passionCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {v.passionCountMax:F0}", v.passionCountMax, 0f, 24f);
```

Replace with:

```csharp
            LabeledFloatRange(listing, "Passion budget", PassionCountRangeId,
                ref v.passionCountMin, ref v.passionCountMax, 0f, 24f, ToStringStyle.FloatOne,
                "Minor passion = 1, Major passion = 2. Presets use fractional budgets, so this reads to one decimal.");
```

`ToStringStyle.FloatOne` is the approved display change. It moves no stored value — `6.2` was always `6.2`, it merely rendered as `"6"` under the old `:F0`.

Keep the `Caption` after it (`"Rolls vary around these target values…"` / `"Minimum is 0, so pawns with no passions are possible."`) — value-derived, so it stays.

- [ ] **Step 6: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

If you get `CS1510` ("a ref or out value must be an assignable variable"), you passed `new FloatRange(...)` directly to `Widgets.FloatRange` instead of staging it in the `range` local.

- [ ] **Step 7: Observe — label duplication check**

Deploy and launch. Select a custom profile so the body is editable.

`Widgets.FloatRange` renders the two handle values itself. Look at each of the four rows and confirm the numbers appear **once**, not twice. If a value is duplicated, remove the value from our left-hand label (the labels in Steps 2-5 are already value-free, so this should not happen — but check, because it is the one detail of `Widgets.FloatRange`'s internals this plan is not certain of).

Expected: four rows reading `Skill shift  [-3.0 ◄══► 3.0]`, `Trait count  [2 ◄══► 3]`, `Passion budget  [2.0 ◄══► 6.0]`, and — with the child checkbox ticked — `Child shift at 13  [-1.0 ◄══► 2.0]`.

- [ ] **Step 8: Observe — the fractional-value regression test**

This is the most important check in the plan.

1. Select the `Elite` preset, click `Duplicate`.
2. On the duplicate, read the Passion budget row. Expected: **`2.5` to `6.2`** — not `2` to `6`.
3. Drag the high handle slightly. Expected: the label tracks in `0.1` steps.
4. Close settings, reopen. Expected: the value you set is still fractional.

If any of these shows a truncated integer, `IntRange` was used somewhere or a cast crept in. Stop and fix before continuing.

- [ ] **Step 9: Observe — no drag hijack**

With the child-shift checkbox, toggle it off and on several times while dragging the `Skill shift` handles. Expected: dragging one range never moves another. This validates the constant-id rule.

- [ ] **Step 10: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: replace paired min/max sliders with FloatRange controls

Eight LabeledSlider calls and four range captions become four range
widgets, saving ~330px of body height.

IntRange is deliberately not used: passionCountMin/Max hold fractional
calibrated preset values (1.4, 2.5, 6.2) that it would silently
truncate, recalibrating values governed by HANDOVER Rule 5.

Passion counts now display to one decimal, which is a display-only
change signed off on 2026-08-03."
```

---

## Task 3: Compact the section headers, captions, and the passion noise row

**Files:**
- Modify: `Source/ProfileEditorTab.cs` (`DrawGenerationSettings`, `DrawChildSkillShift`)

**Interfaces:**
- Consumes: `LabeledFloatRange` and the id constants from Task 2.
- Produces:
  ```csharp
  // GapLine + a single 30px row: Medium-font title left, enable checkbox right.
  // Consumes ~45px versus the old Section (62px) + separate checkbox row (34px).
  private static void SectionHeader(
      Listing_Standard listing, string title, ref bool enabled, string tooltip = null);
  ```

- [ ] **Step 1: Add the `SectionHeader` helper**

In `Source/ProfileEditorTab.cs`, next to `LabeledFloatRange`:

```csharp
        private static void SectionHeader(
            Listing_Standard listing, string title, ref bool enabled, string tooltip = null)
        {
            listing.Gap(SectionGap);
            listing.GapLine(0f);

            Rect row = listing.GetRect(30f);
            Rect titleRect = row.LeftPart(0.70f);
            Rect boxRect = row.RightPart(0.28f);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            Widgets.CheckboxLabeled(boxRect, "Enable", ref enabled);

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(4f);
        }
```

- [ ] **Step 2: Convert the Skills section header**

Replace:

```csharp
            Section(listing, "Skills");
            listing.CheckboxLabeled("Enable skill variance", ref v.enableSkillVariance);
            listing.Gap(ControlGap);
```

with:

```csharp
            SectionHeader(listing, "Skills", ref v.enableSkillVariance,
                "When off, this profile leaves vanilla skill levels untouched.");
```

- [ ] **Step 3: Convert the Traits section header**

Replace:

```csharp
            Section(listing, "Traits");
            listing.CheckboxLabeled("Enable trait variance", ref v.enableTraitVariance);
```

with:

```csharp
            SectionHeader(listing, "Traits", ref v.enableTraitVariance,
                "When off, this profile leaves vanilla trait generation untouched.");
```

- [ ] **Step 4: Convert the Passions section header**

Replace:

```csharp
            Section(listing, "Passions");
            listing.CheckboxLabeled("Enable passion variance", ref v.enablePassionVariance);
            listing.Gap(ControlGap);
```

with:

```csharp
            SectionHeader(listing, "Passions", ref v.enablePassionVariance,
                "When off, this profile leaves vanilla passion assignment untouched.");
```

- [ ] **Step 5: Shorten the skill noise label**

Replace:

```csharp
            v.skillNoise = LabeledSlider(listing, $"Skill noise (spread between a pawn's own skills):  {v.skillNoise:F2}", v.skillNoise, 0f, 1f);
```

with:

```csharp
            Rect noiseRow = listing.GetRect(28f);
            Widgets.Label(noiseRow.LeftPart(0.42f), $"Skill noise:  {v.skillNoise:F2}");
            v.skillNoise = Widgets.HorizontalSlider(noiseRow.RightPart(0.56f), v.skillNoise, 0f, 1f);
            TooltipHandler.TipRegion(noiseRow, "How widely a single pawn's own skills spread apart from each other.");
            listing.Gap(ControlGap);
```

- [ ] **Step 6: Pair the passion noise and major bias sliders on one row**

Replace:

```csharp
            v.passionNoise = LabeledSlider(listing, $"Passion noise (how much the total budget varies):  {v.passionNoise:F2}", v.passionNoise, 0f, 1f);
            v.passionMajorBias = LabeledSlider(listing, $"Major passion bias:  {v.passionMajorBias:F2}", v.passionMajorBias, 0f, 1f);
            Caption(listing, "How often the budget is spent on a Major passion instead of a Minor one. Majors always go to the pawn's best skills first.");
            listing.Gap(ControlGap);
```

with:

```csharp
            Rect passionRow = listing.GetRect(28f);
            Rect leftHalf = passionRow.LeftPart(0.48f);
            Rect rightHalf = passionRow.RightPart(0.48f);

            Widgets.Label(leftHalf.LeftPart(0.52f), $"Passion noise:  {v.passionNoise:F2}");
            v.passionNoise = Widgets.HorizontalSlider(leftHalf.RightPart(0.46f), v.passionNoise, 0f, 1f);
            TooltipHandler.TipRegion(leftHalf, "How much the total passion budget varies between pawns.");

            Widgets.Label(rightHalf.LeftPart(0.52f), $"Major bias:  {v.passionMajorBias:F2}");
            v.passionMajorBias = Widgets.HorizontalSlider(rightHalf.RightPart(0.46f), v.passionMajorBias, 0f, 1f);
            TooltipHandler.TipRegion(rightHalf, "How often the budget is spent on a Major passion instead of a Minor one. Majors always go to the pawn's best skills first.");

            listing.Gap(ControlGap);
```

- [ ] **Step 7: Demote the two remaining static captions to tooltips**

The `countProtectedTraits` checkbox already carries its long explanation as a `CheckboxLabeled` tooltip argument — leave it exactly as it is.

Delete this caption under the Skills section (it describes what the section does, not a live value):

```csharp
            Caption(listing, "Drives every roll below. Higher quality shifts a pawn toward the top of each range you set.");
```

It belonged to the "Overall quality" section, which Task 4 moves into the header — the text is re-homed there as the quality slider's tooltip.

**Keep every remaining `Caption`.** They are all computed from live values:
- the child-shift `"…can lose up to N levels…"` warning
- the passion `"Rolls vary around these target values…"` / `"Minimum is 0…"` note

- [ ] **Step 8: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

If `Section` is now unreferenced from this file, do **not** delete it — the Overrides tab still uses it.

- [ ] **Step 9: Observe**

Deploy and launch, custom profile selected. Check:
1. Three section headers, each with an `Enable` checkbox on the same row as the title.
2. Toggling a section's `Enable` still greys/ungreys nothing yet (that behaviour is unchanged by this task) but does persist when you reopen settings.
3. `Passion noise` and `Major bias` sit side by side on one row, with **no clipped text** in either half.
4. Hovering the skill noise row, each section title, and each half of the passion row shows a tooltip.

- [ ] **Step 10: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: compact section headers, captions and the passion noise row

Enable checkboxes move into their section header rows, static
explanatory captions become tooltips, and passionNoise/passionMajorBias
share one row. Value-derived captions are retained."
```

---

## Task 4: Pin the quality slider and distribution curve in a fixed header

**Files:**
- Modify: `Source/ProfileEditorTab.cs` (`DrawProfileEditorTab`, `DrawGenerationSettings`, `DrawQualityDistributionCurve`)

**Interfaces:**
- Consumes: everything from Tasks 1-3.
- Produces:
  ```csharp
  private const float HeaderHeight = 86f;   // Task 5 raises this to 118f, Task 6 to 140f
  private const float HeaderGutter = 8f;

  // Draws the pinned header into an explicit rect. No Listing_Standard.
  private void DrawProfileEditorHeader(Rect rect);

  // Same curve, but into a caller-supplied rect instead of a listing.
  private static void DrawQualityDistributionCurve(Rect rect, VarianceProfileValues v);
  ```

**The header must not scroll.** Split the tab rect *before* `Widgets.BeginScrollView` is called, so the header and the scroll view are siblings.

- [ ] **Step 1: Change the curve to take a rect**

`DrawQualityDistributionCurve` currently takes a `Listing_Standard` and calls `listing.GetRect(54f)` internally. Change the signature and drop the listing-specific lines:

```csharp
        private static void DrawQualityDistributionCurve(Rect rect, VarianceProfileValues v)
        {
            // Dark container background
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.09f, 0.11f, 0.85f));
            Widgets.DrawBox(rect, 1);
            // ... everything from "// Tier Background Bands" to the mean line, UNCHANGED ...
        }
```

Delete the first two lines of the old body (`listing.Gap(4f);` and `Rect rect = listing.GetRect(54f);`) and the trailing `listing.Gap(ControlGap);`. **Change nothing between them** — the 70-sample loop, the four `DrawTierBand` calls, the three `DrawVerticalTierMarker` calls, and the yellow mean line all stay exactly as they are.

- [ ] **Step 2: Add the header drawing method**

In `Source/ProfileEditorTab.cs`:

```csharp
        private const float HeaderHeight = 86f;
        private const float HeaderGutter = 8f;
        private const float CurveHeight = 54f;

        private void DrawProfileEditorHeader(Rect rect)
        {
            var v = Active;
            bool outerEnabled = GUI.enabled;

            // Row 3: quality slider + tier/power readout.
            Rect qualityRow = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect qLabel = qualityRow.LeftPart(0.26f);
            Rect qSlider = new Rect(qualityRow.x + rect.width * 0.27f, qualityRow.y + 3f, rect.width * 0.40f, 22f);
            Rect qReadout = qualityRow.RightPart(0.30f);

            Widgets.Label(qLabel, EditingCustom
                ? $"Average pawn quality:  {v.averageQuality:F2}"
                : $"Average pawn quality:  {v.averageQuality:F2}  (read-only)");

            GUI.enabled = outerEnabled && EditingCustom;
            v.averageQuality = Widgets.HorizontalSlider(qSlider, v.averageQuality, 0f, 1f);
            GUI.enabled = outerEnabled;

            TooltipHandler.TipRegion(qualityRow,
                "Drives every roll below. Higher quality shifts a pawn toward the top of each range you set.");

            // The readout is output, not input -- always full opacity, even on a
            // read-only preset, so presets stay comparable by cycling the picker.
            float meanComposite = CalculateCompositeScore(v.averageQuality, v);
            Widgets.Label(qReadout, $"→  {TierForQuality(meanComposite)} ({meanComposite:F2})");
            TooltipHandler.TipRegion(qReadout,
                "Overall Power: the composite score every profile is calibrated against. "
                + "Faithful's baseline is 0.31.");

            // Row 4: the distribution curve, full width, never greyed.
            Rect curveRect = new Rect(rect.x, qualityRow.yMax + 4f, rect.width, CurveHeight);
            DrawQualityDistributionCurve(curveRect, v);
        }
```

- [ ] **Step 3: Split the tab rect**

Replace the whole body of `DrawProfileEditorTab` with:

```csharp
        private void DrawProfileEditorTab(Rect outRect)
        {
            Rect headerRect = new Rect(outRect.x, outRect.y, outRect.width, HeaderHeight);
            DrawProfileEditorHeader(headerRect);

            Rect bodyRect = new Rect(
                outRect.x,
                headerRect.yMax + HeaderGutter,
                outRect.width,
                outRect.height - HeaderHeight - HeaderGutter);

            float viewHeight = Math.Max(profileEditorViewHeight, bodyRect.height);
            var viewRect = new Rect(0f, 0f, bodyRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(bodyRect, ref profileEditorScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            DrawProfileSelector(listing);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            profileEditorViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }
```

The `1000f` floor on `viewHeight` is replaced by `bodyRect.height` — with the body now ~500px, a 1000f floor would force a permanent scrollbar over empty space.

`DrawProfileSelector` still runs inside the body for now; Task 5 moves it into the header.

- [ ] **Step 4: Remove the quality block from the body**

In `DrawGenerationSettings`, delete these four lines — the slider, readout, and curve now live in the header:

```csharp
            Section(listing, "Overall quality");
            v.averageQuality = LabeledSlider(listing, $"Average pawn quality:  {v.averageQuality:F2}", v.averageQuality, 0f, 1f);
            float meanComposite = CalculateCompositeScore(v.averageQuality, v);
            Caption(listing, $"An average pawn currently reads as: {TierForQuality(meanComposite)} (Overall Power: {meanComposite:F2})");
            DrawQualityDistributionCurve(listing, v);
```

(The `Caption` describing what quality drives was already deleted in Task 3 Step 7.)

The method now opens directly with `var v = Active;` followed by the Skills `SectionHeader`.

- [ ] **Step 5: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

A `CS1503` on `DrawQualityDistributionCurve` means a caller still passes a `Listing_Standard` — there should be exactly one caller now, in `DrawProfileEditorHeader`.

- [ ] **Step 6: Observe — the pin actually holds**

Deploy and launch, custom profile selected.
1. Scroll the body to the bottom. Expected: the quality slider and curve **stay put** at the top.
2. Drag the quality slider. Expected: the curve and the `→ Standard (0.31)` readout update live while you drag.
3. Drag a `Trait count` handle at the bottom of the body. Expected: the curve is still visible.

- [ ] **Step 7: Observe — the curve keeps its width and the readout is right**

1. Confirm the curve spans the full tab width, with all four tier bands and three vertical markers distinct.
2. Select the `Faithful` preset. Expected readout: **`→ Standard (0.31)`**. If it reads `0.48`, the wrong value is being formatted.
3. Still on `Faithful` (a preset): the quality slider is greyed and its label ends in `(read-only)`, but the curve and readout are at **full opacity**.
4. Cycle through all nine presets. Expected: the curve visibly changes shape per preset — this is the preset-comparison behaviour the design depends on.

- [ ] **Step 8: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: pin the quality slider and distribution curve above the scroll

Splits the tab rect into a fixed header and a sibling scroll view, so
the curve stays visible while editing any slider below it. The curve
keeps full tab width and is never greyed -- it is a readout, and
greying it would break preset comparison."
```

---

## Task 5: Move profile selection and management into the header

**Files:**
- Modify: `Source/ProfileEditorTab.cs` (`DrawProfileEditorHeader`, `DrawProfileEditorTab`; delete `DrawProfileSelector` and `DrawNameField`)
- Create: `Source/Dialog_RenameProfile.cs`

**Interfaces:**
- Consumes: `DrawProfileEditorHeader(Rect)` from Task 4; `ProfileMenu(Action<string>)`, `LabelFor(string)`, `GetCustomProfile(string)`, `CreateNewCustomProfile()`, `DuplicateCurrentProfile()`, `EditingCustom`, `customProfiles`, `activeProfileId`, `RefreshResolved()` — all existing members of `PawnVarianceSettings`.
- Produces: `Dialog_RenameProfile(CustomProfile profile, Action onRenamed)`; `HeaderHeight` raised to `118f`.

- [ ] **Step 1: Create the rename dialog**

Create `Source/Dialog_RenameProfile.cs`:

```csharp
using System;
using Verse;

namespace PawnVarianceMod
{
    public class Dialog_RenameProfile : Dialog_Rename<CustomProfile>
    {
        private readonly Action onRenamed;

        public Dialog_RenameProfile(CustomProfile profile, Action onRenamed) : base(profile)
        {
            this.onRenamed = onRenamed;
        }

        protected override void OnRenamed(string name)
        {
            onRenamed?.Invoke();
        }
    }
}
```

`Dialog_Rename<T>` requires `T : IRenameable`. Add the interface to `CustomProfile` in `Source/VarianceProfile.cs` — this is a UI-plumbing addition, not a value change, so it does not touch HANDOVER Rule 5 territory:

```csharp
    public class CustomProfile : IExposable, IRenameable
    {
        public string id;
        public string name;
        public VarianceProfileValues values = new VarianceProfileValues();

        public string RenamableLabel
        {
            get => name;
            set => name = value;
        }

        public string BaseLabel => name;
        public string InspectLabel => name;
```

Leave the rest of `CustomProfile` — constructors, `ExposeData`, `Clone` — untouched. In particular do **not** add a Scribe entry: `name` is already persisted.

- [ ] **Step 2: Verify the `Dialog_Rename` shape against your RimWorld version**

`Dialog_Rename` was non-generic in RimWorld 1.3 and earlier and became `Dialog_Rename<T>` with `IRenameable` in 1.4+. This project targets 1.6, so the generic form above is correct.

Confirm before building:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
If you get `CS0305` ("using the generic type requires 1 type arguments") or `CS0246`, open `Assembly-CSharp.dll` in a decompiler and match the actual signature rather than guessing. Report the mismatch — do not work around it by writing a bespoke text-entry window.

- [ ] **Step 3: Add rows 1 to the header**

Raise the constant and add the action strip. Replace the top of `DrawProfileEditorHeader`:

```csharp
        private const float HeaderHeight = 118f;
```

then insert the block below **immediately after** the two existing opening lines of the method —

```csharp
            var v = Active;
            bool outerEnabled = GUI.enabled;
```

— and **before** the `// Row 3: quality slider + tier/power readout.` comment. The block reads `outerEnabled`, so it must come after that declaration:

```csharp
            // Row 1: profile picker + action strip.
            Rect pickerRow = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect pickerRect = new Rect(pickerRow.x, pickerRow.y, 240f, 28f);
            if (Widgets.ButtonText(pickerRect, LabelFor(activeProfileId)))
                ProfileMenu(id => { activeProfileId = id; RefreshResolved(); });

            var customProfile = GetCustomProfile(activeProfileId);
            float stripX = pickerRect.xMax + 10f;
            float stripW = pickerRow.xMax - stripX;
            float btnW = (stripW - 4f * 6f) / 5f;

            Rect NextBtn(int i) => new Rect(stripX + i * (btnW + 6f), pickerRow.y, btnW, 28f);

            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (Widgets.ButtonText(NextBtn(0), "+ New"))
                CreateNewCustomProfile();
            GUI.color = Color.white;

            if (Widgets.ButtonText(NextBtn(1), "Duplicate"))
                DuplicateCurrentProfile();

            GUI.enabled = outerEnabled && customProfile != null;

            if (Widgets.ButtonText(NextBtn(2), "Rename") && customProfile != null)
                Find.WindowStack.Add(new Dialog_RenameProfile(customProfile, RefreshResolved));

            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (Widgets.ButtonText(NextBtn(3), "Reset") && customProfile != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset this profile to Faithful? All of its current values will be replaced.",
                    () =>
                    {
                        customProfile.values = VarianceProfiles.VanillaLike.MakeValues();
                        RefreshResolved();
                    },
                    destructive: false));
            }
            GUI.color = Color.white;

            GUI.enabled = outerEnabled && customProfile != null && customProfiles.Count > 1;
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(NextBtn(4), "Delete") && customProfile != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Delete the profile \"{customProfile.name}\"? This cannot be undone.",
                    () =>
                    {
                        customProfiles.Remove(customProfile);
                        activeProfileId = customProfiles[0].id;
                        RefreshResolved();
                    },
                    destructive: true));
            }
            GUI.color = Color.white;
            GUI.enabled = outerEnabled;
```

Then shift the Row 3 rect down — change:

```csharp
            Rect qualityRow = new Rect(rect.x, rect.y, rect.width, 28f);
```

to:

```csharp
            Rect qualityRow = new Rect(rect.x, pickerRow.yMax + 4f, rect.width, 28f);
```

Note the enable-state rules encoded above, from spec §7: `+ New` and `Duplicate` stay live on presets (they are the only escape from a fully greyed tab), `Rename` and `Reset` require a custom profile, and `Delete` additionally requires `customProfiles.Count > 1`.

The button colours match the Overrides tab convention already in the codebase: green add, amber reset, red destructive.

- [ ] **Step 4: Delete the old selector and name field**

Delete `DrawProfileSelector` and `DrawNameField` from `Source/ProfileEditorTab.cs` entirely, and remove the call from `DrawProfileEditorTab`:

```csharp
            DrawProfileSelector(listing);
```

The body now begins directly with the `GUI.enabled` wrapper around `DrawGenerationSettings`.

`ProfileMenu` stays where it is — it is still the picker's dropdown and the Overrides tab uses it too.

- [ ] **Step 5: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

- [ ] **Step 6: Observe — the enable-state matrix**

Deploy and launch. Walk the matrix from spec §7 explicitly:

| Select | `+ New` | `Duplicate` | `Rename` | `Reset` | `Delete` | Body |
|---|---|---|---|---|---|---|
| `Faithful` (preset) | live | live | greyed | greyed | greyed | greyed |
| a custom, 1 exists | live | live | live | live | **greyed** | live |
| a custom, 2+ exist | live | live | live | live | live | live |

The first row is the one that matters most: a new user lands on `Faithful`, and if `+ New` and `Duplicate` are greyed there, the tab is a dead end.

- [ ] **Step 7: Observe — rename and the destructive guards**

1. Create a custom profile, click `Rename`, type a new name, confirm. Expected: the dialog closes and the picker button shows the new name immediately.
2. Click `Reset`. Expected: an amber-tinted confirmation appears. Cancel it — values unchanged. Accept it — values become Faithful's.
3. With two custom profiles, click `Delete`. Expected: a red destructive confirmation. Accept — the profile is gone and selection falls back to `customProfiles[0]`.
4. Check no button label is clipped at the default window size.

- [ ] **Step 8: Commit**

```bash
git add Source/ProfileEditorTab.cs Source/Dialog_RenameProfile.cs Source/VarianceProfile.cs
git commit -m "feat: move profile selection and management into the pinned header

Replaces the stacked full-width button column and the inline name field
with a single 28px row: picker plus a five-button action strip. Renaming
moves to a Dialog_Rename subclass, which keeps row geometry stable and
avoids an always-focused text field swallowing keystrokes.

+ New and Duplicate stay enabled on presets -- they are the only way off
a read-only profile."
```

---

## Task 6: The description / fingerprint row

**Files:**
- Modify: `Source/ProfileEditorTab.cs` (`DrawProfileEditorHeader`)

**Interfaces:**
- Consumes: `DrawProfileEditorHeader(Rect)` from Task 5.
- Produces:
  ```csharp
  // One-line summary of a custom profile's values. Never returns empty.
  private static string ProfileFingerprint(VarianceProfileValues v);
  ```
  `HeaderHeight` raised to its final `140f`.

**The row is never empty in either state.** Presets show authored prose; custom profiles show a generated fingerprint.

- [ ] **Step 1: Add the fingerprint builder**

In `Source/ProfileEditorTab.cs`:

```csharp
        private static string ProfileFingerprint(VarianceProfileValues v)
        {
            return string.Format(
                "Traits {0:F0}–{1:F0}  ·  Passions {2:F1}–{3:F1}  ·  Skill shift {4:F1} to {5:F1}  ·  Quality {6:F2}",
                v.traitCountMin, v.traitCountMax,
                v.passionCountMin, v.passionCountMax,
                v.skillShiftMin, v.skillShiftMax,
                v.averageQuality);
        }
```

Passions use `F1` and traits `F0`, matching the range widgets from Task 2 — so the fingerprint and the controls never disagree.

- [ ] **Step 2: Add the row to the header**

Raise the constant to its final value:

```csharp
        private const float HeaderHeight = 140f;
```

Insert after the Row 1 block and before Row 3:

```csharp
            // Row 2: authored prose for presets, generated fingerprint for customs.
            // Constant height, and never empty in either state, so the header cannot
            // shift when switching profiles.
            Rect descRow = new Rect(rect.x, pickerRow.yMax + 4f, rect.width, 20f);
            var preset = VarianceProfiles.GetPresetById(activeProfileId);
            string descText = preset != null ? preset.description : ProfileFingerprint(v);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            // All nine shipped descriptions fit whole at this width (longest is 122
            // chars, ~630-730px at Tiny against ~840px). Truncate is a safety net for
            // long localizations and unusually long fingerprints only.
            Widgets.Label(descRow, descText.Truncate(descRow.width));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(descRow, descText);
```

Then shift Row 3 down again — change:

```csharp
            Rect qualityRow = new Rect(rect.x, pickerRow.yMax + 4f, rect.width, 28f);
```

to:

```csharp
            Rect qualityRow = new Rect(rect.x, descRow.yMax + 2f, rect.width, 28f);
```

- [ ] **Step 3: Build**

Run:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

`Truncate` is `Verse.GenText.Truncate(string, float, Dictionary<string,string>)`, an extension method available via the existing `using Verse;`.

- [ ] **Step 4: Observe — no truncation on the shipped strings**

Deploy and launch. Cycle every one of the nine presets and read the description row.

Expected: **each renders whole, with no trailing ellipsis** — most importantly `Distinct`, the 122-character longest:
> "The mod's signature tuning. Pawns have strong individual strengths and weaknesses while maintaining a fair colony average."

If any preset shows an ellipsis at the default window size, the design's width assumption is wrong. Report the measured width rather than shrinking the font.

- [ ] **Step 5: Observe — customs are never blank, header never jumps**

1. Select a custom profile. Expected: a fingerprint like `Traits 2–4 · Passions 2.5–6.2 · Skill shift −1.0 to +3.8 · Quality 0.55`. **Never an empty row.**
2. Alternate between a preset and a custom several times. Expected: the header height does not change and the quality slider does not move vertically.
3. Drag a `Trait count` handle. Expected: the fingerprint updates live.
4. Duplicate `Elite` and read the fingerprint. Expected: `Passions 2.5–6.2`, confirming the fractional values survive into the summary.

- [ ] **Step 6: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "feat: add the header description/fingerprint row

Presets show their authored prose in full -- all nine fit on one line at
GameFont.Tiny, so no truncation is needed in the shipped strings.
Custom profiles show a generated fingerprint instead of a blank row.
Constant row height keeps the header from shifting between the two."
```

---

## Task 7: Whole-feature verification

**Files:** none modified — this task only runs checks and records the result.

**Interfaces:**
- Consumes: the finished tab from Tasks 1-6.
- Produces: an updated `HANDOVER.md` section and a verification record.

- [ ] **Step 1: Confirm the schema really did not change**

Run:
```bash
git diff main --stat -- Source/VarianceProfile.cs
```
Expected: `Source/VarianceProfile.cs` shows only the `IRenameable` additions from Task 5 Step 1 — no change to any numeric field, preset value, or `ExposeData` body.

Then:
```bash
git diff main -- Source/VarianceProfile.cs | grep -E "^[+-].*Scribe_"
```
Expected: **no output.** Any hit means a persisted field changed, which violates the "no new persisted state" constraint.

- [ ] **Step 2: Import/export round trip**

In-game:
1. Select a custom profile, set `Passion budget` to a fractional value such as `2.5`–`6.2`.
2. General tab → export settings to clipboard.
3. Delete the custom profile.
4. Import from clipboard.

Expected: the profile returns with `2.5`–`6.2` intact, not `2`–`6`. This exercises `SettingsTransfer.cs` against the Task 2 changes.

- [ ] **Step 3: Measure the real body height**

With Biotech active, a custom profile selected, and `Also shift skills when a child grows up` **checked** — the worst case the design targets:

1. Confirm the body scrollbar either does not appear or allows only a small amount of scroll (the spec predicts ~501px of content against a ~460px viewport, so ~40px).
2. If the body scrolls more than about a fifth of a screen, record the actual figure. The spec's estimate was deliberately conservative; a large miss is worth writing down rather than papering over.

- [ ] **Step 4: Confirm no drag hijack across the whole tab**

Drag each of the four range controls in turn, toggling `applyChildSkillShift` between drags. Expected: no control ever moves while a different one is being dragged.

- [ ] **Step 5: Update HANDOVER.md**

Add to the completed-work section of `HANDOVER.md`:

```markdown
## 4. Profile Editor Tab Layout Redesign — ✅ COMPLETED (2026-08-03)
- **Pinned 140px header**: profile picker + 5-button action strip, one-line
  description (prose for presets, generated fingerprint for customs), quality
  slider with the `{tier} ({power})` readout, and the full-width distribution
  curve. The curve is never greyed — it is a readout, and greying it would
  break preset comparison.
- **Body compacted from ~1600-2000px to ~500px**: four `Widgets.FloatRange`
  controls replace eight paired sliders, enable checkboxes moved into section
  headers, static captions demoted to tooltips.
- **`Widgets.IntRange` is forbidden here.** `passionCountMin`/`Max` hold
  fractional calibrated values (1.4, 2.5, 6.2); `IntRange` truncates them and
  would silently recalibrate a Rule 5 governed value.
- **Passion counts display to one decimal** (`:F0` → `:F1`). Display-only
  change, signed off 2026-08-03. No stored value moved.
- Profile Editor drawing now lives in `Source/ProfileEditorTab.cs`
  (`partial class PawnVarianceSettings`).
- Spec: `docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md`
```

- [ ] **Step 6: Final build and commit**

```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `0 Error(s), 0 Warning(s)`.

```bash
git add HANDOVER.md
git commit -m "docs: record the Profile Editor layout redesign in HANDOVER"
```

- [ ] **Step 7: Report honestly**

State plainly which of the in-game observations in Tasks 1-7 you actually performed and which you could not. If RimWorld was never launched, say so — every task in this plan is build-verified only in that case, and the layout is unverified.

---

## Deviations from the skill's defaults, and why

- **No TDD cycle.** There is no test harness and every touched symbol is a Unity/Verse static that needs a running game. See the warning block above. Verification is a clean build plus named in-game observations.
- **Task 1 is a pure refactor with no user-visible deliverable.** It is separated deliberately: mixing a 460-line file move with behavioural edits would make every later diff unreviewable.
- **`HeaderHeight` changes across Tasks 4, 5, and 6** (`86f` → `118f` → `140f`) as rows are added. Each task states its value. This keeps every task independently shippable rather than leaving a half-empty header.
