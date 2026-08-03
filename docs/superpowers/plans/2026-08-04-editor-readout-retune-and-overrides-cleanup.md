# Editor Readout, Preset Retune & Overrides Cleanup — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the Profile Editor's silent hijacking of the colony profile, add a Best-of-25 power readout, retune seven presets to the owner's approved targets, remove the unreachable `Gifted` preset, label the override columns, and move permanently-visible rationale prose into tooltips.

**Architecture:** All changes are confined to the settings UI and the preset value table. No pawn-generation code is touched. `CalculateCompositeScore` is reused unchanged — the Best-of-N readout only feeds it a different `q`. No `Scribe_` line is added or removed, so the save schema stays untouched (a property this branch has deliberately maintained).

**Tech Stack:** C# 7.3 targeting .NET Framework 4.7.2, RimWorld 1.5 IMGUI (`Verse.Widgets`, `Listing_Standard`), Harmony. Python 3 for `docs/tools/envelope_check.py`.

**Spec:** [`docs/superpowers/specs/2026-08-04-editor-readout-retune-and-overrides-cleanup.md`](../specs/2026-08-04-editor-readout-retune-and-overrides-cleanup.md)

## Global Constraints

- **There is no automated test harness for IMGUI code in this repo.** Do not invent one and do not
  write fake unit tests. The three real gates are: `dotnet build` clean, `envelope_check.py`
  passing, and scripted in-game verification. Each task states which apply.
- **Build must return `0 Error(s), 0 Warning(s)`**: `dotnet build Source/PawnVarianceMod.csproj`
- **Guard before deploying**: `tasklist /FI "IMAGENAME eq RimWorldWin64.exe"` must show no running
  instance, or the DLL copy fails on a file lock.
- **Rule 1**: every enforced preset within ±35% of `Faithful` at N = 1, 5, 25, 50.
- **Rule 2**: `Desperate < Scavenger < Faithful < Specialist < Elite < Sovereign` at every N.
  `Distinct` and `Wildcard` are exempt (variance presets).
- **Rule 3**: `CalculateCompositeScore` must never regain a trait-count term.
- **Rule 6**: after any preset or scoring-constant change, run `python docs/tools/envelope_check.py`
  and paste its output into HANDOVER. **Never hand-edit those percentages.**
- **`Widgets.IntRange` is forbidden** on the four min/max pairs — `passionCountMin/Max` hold
  fractional values and `IntRange` truncates them.
- Preserve the three global draw-state restores in the header rows (`Text.Font`, `GUI.color`,
  `Text.WordWrap`). `WordWrap = false` is what structurally guarantees fixed-height rows stay on
  one line.

---

## File Structure

| File | Responsibility | Change |
|---|---|---|
| `Source/PawnVarianceSettings.cs` | Settings state, General + Overrides tabs, scoring | Modify — editor cursor, Best-of-N maths, readout helpers, column headers, prose moves |
| `Source/ProfileEditorTab.cs` | Profile Editor tab drawing (`partial class`) | Modify — use editor cursor, new header row, header height, string fixes |
| `Source/VarianceProfile.cs` | Preset definitions | Modify — retune 7 presets, delete `Gifted` |
| `Source/Constants.cs` | Tunable constants | Modify — add Best-of-N constants, drop `Gifted` comment |
| `HANDOVER.md` | Project memory | Modify — new envelope table, closed items |
| `docs/tools/envelope_check.py` | Envelope gate | Unchanged (parses source directly) |

---

## Task 1: Split the Profile Editor's selection from the colony's active profile

Fixes the bug where cycling presets in the Profile Editor silently reassigns the colony profile.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:28` (field block), `:67` (`EditingCustom`), `:807-825` (new/duplicate)
- Modify: `Source/ProfileEditorTab.cs:109-233` (header), `:248` (body)

**Interfaces:**
- Consumes: nothing from earlier tasks.
- Produces: `PawnVarianceSettings.EditorProfileId` (`string`, get-only),
  `PawnVarianceSettings.Editing` (`VarianceProfileValues`, get-only),
  `PawnVarianceSettings.SetEditorProfile(string id)` (`void`),
  `PawnVarianceSettings.RefreshEditor()` (`void`). Tasks 2 and 4 use `Editing`.

- [ ] **Step 1: Add the editor cursor fields and accessors**

In `Source/PawnVarianceSettings.cs`, directly below `public string hostileProfileId = ...;` (line 29):

```csharp
        // Which profile the Profile Editor tab is LOOKING AT. Deliberately separate from
        // activeProfileId and deliberately NOT Scribed: this is a view cursor, not a setting.
        // Sharing one field meant that cycling the editor's picker to compare presets silently
        // reassigned the colony's active profile out from under the player.
        private string editorProfileId;
        private VarianceProfileValues editingValues;
```

- [ ] **Step 2: Replace `EditingCustom` with the cursor-based accessors**

In `Source/PawnVarianceSettings.cs`, replace line 67:

```csharp
        public bool EditingCustom => GetCustomProfile(activeProfileId) != null;
```

with:

```csharp
        public string EditorProfileId
        {
            get
            {
                // Opens on whatever the colony is using, then diverges freely.
                if (string.IsNullOrEmpty(editorProfileId))
                    editorProfileId = activeProfileId;
                return editorProfileId;
            }
        }

        // Resolved values the Profile Editor edits. Cached rather than resolved per frame:
        // Resolve() hands back a fresh MakeValues() for presets, so a per-frame call would
        // allocate every frame and discard the Beta cache on VarianceProfileValues each time.
        public VarianceProfileValues Editing
        {
            get
            {
                if (editingValues == null) RefreshEditor();
                return editingValues;
            }
        }

        public bool EditingCustom => GetCustomProfile(EditorProfileId) != null;

        public void SetEditorProfile(string id)
        {
            editorProfileId = id;
            RefreshEditor();
        }

        public void RefreshEditor()
        {
            editingValues = Resolve(EditorProfileId);
            editingValues.profileLabel = LabelFor(EditorProfileId);
        }
```

- [ ] **Step 3: Point new/duplicate at the editor cursor**

In `Source/PawnVarianceSettings.cs`, replace lines 807-825 entirely:

```csharp
        private void CreateNewCustomProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
            string newName = "Custom " + (customProfiles.Count + 1);
            var profile = new CustomProfile(newId, newName, VarianceProfiles.VanillaLike.MakeValues());
            customProfiles.Add(profile);
            // Selects it in the editor only. The colony keeps whatever profile it was using.
            SetEditorProfile(newId);
        }

        private void DuplicateCurrentProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
            string newName = LabelFor(EditorProfileId) + " Copy";
            var profile = new CustomProfile(newId, newName, Resolve(EditorProfileId).Clone());
            customProfiles.Add(profile);
            SetEditorProfile(newId);
        }
```

- [ ] **Step 4: Repoint the editor header at the cursor**

In `Source/ProfileEditorTab.cs`, apply these five replacements inside `DrawProfileEditorHeader`:

| Line | From | To |
|---|---|---|
| 109 | `var v = Active;` | `var v = Editing;` |
| 115 | `LabelFor(activeProfileId)` | `LabelFor(EditorProfileId)` |
| 116 | `ProfileMenu(id => { activeProfileId = id; RefreshResolved(); });` | `ProfileMenu(SetEditorProfile);` |
| 118 | `GetCustomProfile(activeProfileId)` | `GetCustomProfile(EditorProfileId)` |
| 176 | `VarianceProfiles.GetPresetById(activeProfileId)` | `VarianceProfiles.GetPresetById(EditorProfileId)` |

And in `DrawGenerationSettings`, line 248: `var v = Active;` → `var v = Editing;`

- [ ] **Step 5: Make Reset refresh the editor's cached values**

`Reset` replaces the profile's `values` object wholesale, which orphans the cached reference. In
`Source/ProfileEditorTab.cs`, replace the Reset confirmation body (lines 143-148):

```csharp
                    () =>
                    {
                        customProfile.values = VarianceProfiles.VanillaLike.MakeValues();
                        // The cached editing reference points at the object we just replaced.
                        RefreshEditor();
                        RefreshResolved();
                    },
```

- [ ] **Step 6: Make Delete clean up every reference to the removed profile**

In `Source/ProfileEditorTab.cs`, replace the Delete confirmation body (lines 158-166):

```csharp
                    () =>
                    {
                        string deletedId = customProfile.id;
                        customProfiles.Remove(customProfile);

                        // The editor moves to whatever is left.
                        SetEditorProfile(customProfiles != null && customProfiles.Count > 0
                            ? customProfiles[0].id
                            : VarianceProfiles.FaithfulId);

                        // The deleted profile may ALSO have been in use as the colony profile, the
                        // hostile profile, or in an override map. Before the editor cursor was split
                        // out, the colony case was handled implicitly because they were one field.
                        // Now it has to be explicit or the settings keep a dangling id.
                        if (activeProfileId == deletedId) activeProfileId = VarianceProfiles.FaithfulId;
                        if (hostileProfileId == deletedId) hostileProfileId = VarianceProfiles.DistinctId;

                        var staleFactions = new List<string>();
                        foreach (var kv in factionOverrides)
                            if (kv.Value == deletedId) staleFactions.Add(kv.Key);
                        foreach (var k in staleFactions)
                        {
                            factionOverrides.Remove(k);
                            factionPriorities.Remove(k);
                        }

                        var staleXenotypes = new List<string>();
                        foreach (var kv in xenotypeOverrides)
                            if (kv.Value == deletedId) staleXenotypes.Add(kv.Key);
                        foreach (var k in staleXenotypes)
                        {
                            xenotypeOverrides.Remove(k);
                            xenotypePriorities.Remove(k);
                        }

                        RefreshResolved();
                    },
```

- [ ] **Step 7: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`

- [ ] **Step 8: Verify in-game**

Deploy, then in Mod Settings → Varied Pawns:
1. General tab → set Active Colony Profile to `Faithful`.
2. Profile Editor tab → cycle the picker through all presets.
3. Return to General. **Expected: still reads `Faithful`.** (Before this task it would read whatever
   was last selected in the editor.)
4. Create a custom profile, set it as the colony profile on General, then delete it in the editor.
   **Expected: General falls back to `Faithful`, no red exception in the log.**

- [ ] **Step 9: Commit**

```bash
git add Source/PawnVarianceSettings.cs Source/ProfileEditorTab.cs
git commit -m "fix: stop the Profile Editor from reassigning the colony profile

The editor had no selection state of its own -- its picker read and wrote
activeProfileId, the same field the General tab uses for the colony's active
profile. Browsing presets to compare them silently changed which profile the
colony generated pawns from.

Adds a separate, non-persisted editorProfileId view cursor. Delete now also
clears the deleted id out of the colony profile, hostile profile and both
override maps, which the shared-field version handled only by accident.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 2: Remove the `Gifted` preset

`Gifted` sits at +152% vs `Faithful` at N=1, is unreachable in the default config, and has survived
two retunes unpatched. The owner's decision is to delete it rather than tune it.

**Files:**
- Modify: `Source/VarianceProfile.cs:15` (enum), `:192` (id const), `:274-293` (definition), `:413` (list)
- Modify: `Source/Constants.cs:29` (comment)

**Interfaces:**
- Consumes: nothing.
- Produces: nothing. `VarianceProfiles.GiftedId` and `VarianceProfiles.GiftedColony` cease to exist.

- [ ] **Step 1: Delete the enum member**

In `Source/VarianceProfile.cs`, remove line 15:

```csharp
        GiftedColony = 4,
```

**Do not renumber the remaining members.** The mod is unpublished so nothing depends on the values,
but the churn buys nothing and would obscure the diff.

- [ ] **Step 2: Delete the id constant**

In `Source/VarianceProfile.cs`, remove line 192:

```csharp
        public const string GiftedId = "preset_gifted";
```

- [ ] **Step 3: Delete the profile definition**

In `Source/VarianceProfile.cs`, remove the entire `GiftedColony` declaration, lines 274-293
(`public static readonly VarianceProfile GiftedColony = new VarianceProfile(` through its closing
`});`).

- [ ] **Step 4: Remove it from the display list**

In `Source/VarianceProfile.cs`, remove line 413 from the `Presets` list:

```csharp
            GiftedColony,
```

- [ ] **Step 5: Update the stale constant comment**

In `Source/Constants.cs`, line 29 currently reads:

```csharp
        // that made passionNorm saturate a third early and pinned Gifted (12.3 pips) at 1.0.
```

Replace with:

```csharp
        // that made passionNorm saturate a third early. (The preset that exposed this, Gifted,
        // was removed 2026-08-04: it sat at +152% vs Faithful and was unreachable by default.)
```

- [ ] **Step 6: Build and confirm no dangling references**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`. A compile error here means a reference was missed — fix it
rather than restoring the preset.

Then confirm nothing references it textually:

Run: `git grep -n "Gifted" -- Source/`
Expected: no output.

- [ ] **Step 7: Confirm a stale local config still loads**

No migration is required (the mod is unpublished, so no user config can hold `preset_gifted`), but a
local dev config might. Launch RimWorld and open Mod Settings.
Expected: settings open with no red exception. If a stale id is present, `Resolve` falls through to
`customProfiles[0]` or `VanillaLike` and reselecting a profile fixes it permanently.

- [ ] **Step 8: Commit**

```bash
git add Source/VarianceProfile.cs Source/Constants.cs
git commit -m "refactor: remove the unreachable Gifted preset

Gifted sat at +152% vs Faithful at N=1 -- far outside the +-35% envelope --
and was not reachable in the default config, so it was skipped by both the
2026-08-03 retune and the /12 -> /18 normalizer fix. It was noise in the
picker and in every envelope table. Deleting rather than tuning it, per
owner decision.

No migration path is needed: the mod is unpublished.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 3: Retune the seven remaining presets

Values below were solved against the real Best-of-N integration and verified at the tool's full
20000-node precision. **They are exact — do not re-derive or round them further.**

Only four fields change per profile: `skillShiftMin`, `skillShiftMax`, `passionCountMin`,
`passionCountMax`. `averageQuality`, noise, bias and trait counts are untouched.

**Files:**
- Modify: `Source/VarianceProfile.cs` — `BalancedVariance`, `WildSpread`, `Hardscrabble`, `Elite`, `Sovereign`, `Specialist`, `Scavenger`

**Interfaces:**
- Consumes: Task 2 (the `Gifted` row must already be gone or the envelope table will not match).
- Produces: the verified envelope table pasted into HANDOVER by Task 7.

- [ ] **Step 1: Apply the new values**

In `Source/VarianceProfile.cs`, make exactly these fourteen line edits:

| Profile (C# name) | Field | From | To |
|---|---|---|---|
| `BalancedVariance` (Distinct) | `skillShiftMin` | `-4f` | `-3.3f` |
| | `skillShiftMax` | `6f` | `6.5f` |
| | `passionCountMin` | `1f` | `1.4f` |
| | `passionCountMax` | `7f` | `7.2f` |
| `WildSpread` (Wildcard) | `skillShiftMin` | `-10.5f` | `-8.7f` |
| | `skillShiftMax` | `6f` | `4.2f` |
| | `passionCountMin` | `0f` | `1.2f` |
| | `passionCountMax` | `11f` | `9.8f` |
| `Hardscrabble` (Desperate) | `skillShiftMin` | `-3.4f` | `-2.8f` |
| | `skillShiftMax` | `1.5f` | `2.1f` |
| | `passionCountMin` | `1.4f` | `1.7f` |
| | `passionCountMax` | `5f` | `5.3f` |
| `Elite` | `skillShiftMin` | `-1f` | `-0.8f` |
| | `skillShiftMax` | `3.8f` | `4.0f` |
| | `passionCountMin` | `2.5f` | `2.6f` |
| | `passionCountMax` | `6.2f` | `6.3f` |
| `Sovereign` | `skillShiftMin` | `0f` | *(unchanged)* |
| | `skillShiftMax` | `3.85f` | *(unchanged)* |
| | `passionCountMin` | `3.0f` | `2.2f` |
| | `passionCountMax` | `6.2f` | `6.6f` |
| `Specialist` | `skillShiftMin` | `-2f` | `-1.8f` |
| | `skillShiftMax` | `3.5f` | `3.7f` |
| | `passionCountMin` | `2f` | `2.1f` |
| | `passionCountMax` | `6.0f` | `6.1f` |
| `Scavenger` | `skillShiftMin` | `-3.5f` | `-2.9f` |
| | `skillShiftMax` | `2.0f` | `2.6f` |
| | `passionCountMin` | `1.5f` | `1.8f` |
| | `passionCountMax` | `5.0f` | `5.3f` |

- [ ] **Step 2: Replace the two stale rationale comments**

The comments on `WildSpread` (lines 260-263) and `Hardscrabble` (lines 306-309) describe the
*previous* retune and now misstate the shipped values.

Replace the `WildSpread` comment block with:

```csharp
                // Retuned 2026-08-04: narrowed to ~0.78x its previous dispersion, which pulls
                // Best-of-25 from +27.1% to +17.3% and Best-of-50 from a near-breach +33.1% to
                // +21.5%. Narrowing raises N=1 (-23.6% -> -18.1%) and lowers N=25 at the same
                // time, so it buys headroom at both ends. Still by far the widest preset -- it is
                // a variance preset, not a power tier, so it legitimately crosses Faithful as N
                // rises; it just may not leave the +-35% band.
```

Replace the `Hardscrabble` comment block with:

```csharp
                // Retuned 2026-08-04: translated up to -20.6% at Best-of-25 (was -27.3%), which
                // also lifts N=1 from a very tight -33.2% to -24.2%. This preset had only 1.8pp
                // of envelope headroom and was the single tightest number in the whole set.
                // It remains the lowest power tier by a clear margin at every N.
```

Replace the `Sovereign` comment block (lines 352-353) with:

```csharp
                // Retuned 2026-08-04 to +18.9% at Best-of-25 (was +16.2%). The skill range is
                // deliberately UNCHANGED -- skillShiftMin stays at 0 so a Sovereign pawn can never
                // roll below the vanilla baseline, which is the preset's identity. The entire
                // increase comes from widening the passion budget (3.0-6.2 -> 2.2-6.6).
                // Translating the whole profile up instead would have hit +34.5% at N=1, leaving
                // 0.5pp of headroom; this shape lands at +28.5% with 6.5pp, better than before.
```

- [ ] **Step 3: Run the envelope gate**

Run: `python docs/tools/envelope_check.py`

Expected output — **these exact figures** (the tool parses the source, so any mismatch means a value
was mistyped):

```
Faithful baseline @ q=0.50: 0.2500

profile                     N=1                N=5               N=25               N=50
Faithful        0.2500   +0.0%     0.3022   +0.0%     0.3333   +0.0%     0.3424   +0.0%
Distinct        0.2261   -9.6%     0.3075   +1.7%     0.3670  +10.1%     0.3866  +12.9%   (variance)
Wildcard        0.2047  -18.1%     0.3120   +3.2%     0.3909  +17.3%     0.4161  +21.5%   (variance)
Desperate       0.1895  -24.2%     0.2341  -22.5%     0.2648  -20.6%     0.2746  -19.8%
Elite           0.3101  +24.0%     0.3561  +17.8%     0.3826  +14.8%     0.3902  +13.9%
Sovereign       0.3213  +28.5%     0.3695  +22.2%     0.3965  +18.9%     0.4041  +18.0%
Specialist      0.2749   +9.9%     0.3260   +7.9%     0.3565   +6.9%     0.3654   +6.7%
Scavenger       0.2112  -15.5%     0.2580  -14.6%     0.2883  -13.5%     0.2976  -13.1%
```

Rule 2 must print `OK` at all four N. Final line must read
`PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.`

Exit code must be `0`:

Run: `python docs/tools/envelope_check.py; echo "exit=$?"`
Expected: `exit=0`

- [ ] **Step 4: Confirm the headroom improved**

The tool's "Tightest envelope margins" block must now read:

```
  Sovereign @ N=1: +28.5%  (6.5pp of headroom)
  Desperate @ N=1: -24.2%  (10.8pp of headroom)
  Elite @ N=1: +24.0%  (11.0pp of headroom)
```

The previous tightest was `Desperate` at 1.8pp. **If the tightest margin is smaller than 6.0pp, a
value was mistyped — stop and diff against the table in Step 1.**

- [ ] **Step 5: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`

- [ ] **Step 6: Commit**

```bash
git add Source/VarianceProfile.cs
git commit -m "balance: retune seven presets to the approved Best-of-25 targets

Targets set by the project owner, solved against the Best-of-N integration
and verified at envelope_check.py's full precision:

  Sovereign  +16.2% -> +18.9%     Elite      +12.5% -> +14.8%
  Specialist  +4.6% ->  +6.9%     Distinct    +3.5% -> +10.1%
  Wildcard   +27.1% -> +17.3%     Scavenger  -20.3% -> -13.5%
  Desperate  -27.3% -> -20.6%

Wildcard came down because a variance preset sitting above Sovereign at N=25
reads as a bug to a player even though Rule 2 formally exempts it.

Sovereign's skill range is untouched: skillShiftMin stays 0 so it can never
roll below the vanilla baseline. The gain is all passion budget. Translating
the profile instead would have left 0.5pp of envelope headroom at N=1.

Net effect on the envelope is strongly positive -- the tightest margin in the
set goes from 1.8pp to 6.5pp.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 4: Add the Best-of-25 readout to the Profile Editor header

The header currently shows only the N=1 mean. On the two variance presets the mean and the
Best-of-25 figure **disagree in sign**, so a player reads `Wildcard −18%`, concludes "weaker", picks
it for a harder run, and gets an easier one.

**Files:**
- Modify: `Source/Constants.cs` (add two constants)
- Modify: `Source/PawnVarianceSettings.cs:932-949` (readout helpers), add `CalculateBestOfNScore`
- Modify: `Source/ProfileEditorTab.cs:75` (header height), `:199-233` (rows 3, 3b), `:308` (passion tooltip)

**Interfaces:**
- Consumes: `Editing` from Task 1; the retuned values from Task 3 (the cross-check in Step 7 compares
  against shipped numbers).
- Produces: `Constants.BestOfNSampleCount` (`int`), `Constants.BestOfNIntegrationNodes` (`int`),
  `PawnVarianceSettings.CalculateBestOfNScore(VarianceProfileValues v, int n)` → `float`,
  `PawnVarianceSettings.FormatPowerPercent(float composite)` → `string`.

- [ ] **Step 1: Add the constants**

In `Source/Constants.cs`, add near `MaxPassionPips`:

```csharp
        // The Profile Editor shows power at two anchors: the typical pawn (N=1) and the best of
        // N rerolls. 25 rather than 50: at 50 Wildcard would display +21.5%, and a UI that
        // advertises how close a preset sits to the +-35% envelope invites players to treat the
        // limit as a target.
        public const int BestOfNSampleCount = 25;

        // Midpoint-rule nodes for the Best-of-N integral. Measured against the 20000-node
        // reference in docs/tools/envelope_check.py across all seven presets: 512 nodes lands
        // 0.35pp off, which can flip a whole-percent readout; 1024 lands 0.17pp. Do not lower it.
        public const int BestOfNIntegrationNodes = 1024;
```

- [ ] **Step 2: DRY the baseline lazy-init**

`cachedFaithfulBaseline` is lazily initialised by three separate copies of the same four lines
(`PawnVarianceSettings.cs:934-937`, `:996-999`, and the new helper needs it too). Add, immediately
above `private static float cachedFaithfulBaseline = -1f;` (line 992):

```csharp
        private static float FaithfulBaseline()
        {
            if (cachedFaithfulBaseline < 0f)
                cachedFaithfulBaseline = CalculateCompositeScore(0.50f, VarianceProfiles.VanillaLike.MakeValues());
            return cachedFaithfulBaseline;
        }
```

Then in `FormatPowerReadout` replace lines 934-938:

```csharp
            if (cachedFaithfulBaseline < 0f)
            {
                cachedFaithfulBaseline = CalculateCompositeScore(0.50f, VarianceProfiles.VanillaLike.MakeValues());
            }
            float baseC = cachedFaithfulBaseline;
```

with:

```csharp
            float baseC = FaithfulBaseline();
```

And in `MapToCenteredX` replace lines 996-1000 the same way:

```csharp
            float baseC = FaithfulBaseline();
```

- [ ] **Step 3: Add the percentage-only formatter**

In `Source/PawnVarianceSettings.cs`, directly after `FormatPowerReadout` (after line 949):

```csharp
        // Just the signed percentage. FormatPowerReadout returns a whole sentence, which would
        // print "vs Faithful" twice when two anchors sit on screen together.
        public static string FormatPowerPercent(float composite)
        {
            float baseC = FaithfulBaseline();
            if (baseC <= 0f) return composite.ToString("F2");

            float diffPct = ((composite - baseC) / baseC) * 100f;
            if (Mathf.Abs(diffPct) < 0.5f) return "baseline";

            return $"{(diffPct > 0f ? "+" : "")}{diffPct:F0}%";
        }
```

- [ ] **Step 4: Add the Best-of-N integration**

In `Source/PawnVarianceSettings.cs`, directly after `CalculateCompositeScore` (after line 990):

```csharp
        // Scratch buffer for the Best-of-N grid. Static and reused: the settings window redraws
        // every frame while open, and a fresh 1024-float array per frame is pure GC churn.
        private static float[] betaDensityScratch;

        // Expected composite score of the best of n pawns: E[composite(max(q1..qn))].
        //
        // This is the figure that describes actual play. The player CHOOSES which pawns to keep --
        // rerolling start scenarios, picking from raid captures, accepting or refusing quest pawns
        // -- so the pawn that ends up in the colony is the maximum of n rolls, not a typical roll.
        // A mean-based figure systematically understates any high-dispersion profile, which is
        // exactly why the project's own envelope maths is Best-of-N.
        //
        // Mirror of expected_best_of_n() in docs/tools/envelope_check.py. If you change one, change
        // both, and re-run the cross-check -- the UI and HANDOVER's table must not disagree.
        // Density of the max is n * F(q)^(n-1) * f(q).
        public static float CalculateBestOfNScore(VarianceProfileValues v, int n)
        {
            if (v == null || n < 1) return 0f;
            if (n == 1) return CalculateCompositeScore(v.averageQuality, v);

            int nodes = Constants.BestOfNIntegrationNodes;
            if (betaDensityScratch == null || betaDensityScratch.Length != nodes)
                betaDensityScratch = new float[nodes];

            v.GetBetaAlphaBeta(out float alpha, out float beta);
            float dq = 1f / nodes;

            // Unnormalised Beta density on a midpoint grid. The normalising constant is divided
            // out below rather than computed via lgamma, which keeps this allocation-free.
            float total = 0f;
            for (int i = 0; i < nodes; i++)
            {
                float q = (i + 0.5f) * dq;
                float d = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                betaDensityScratch[i] = d;
                total += d * dq;
            }

            if (total <= 0f || float.IsNaN(total) || float.IsInfinity(total))
                return CalculateCompositeScore(v.averageQuality, v);

            float acc = 0f;
            float cdf = 0f;
            for (int i = 0; i < nodes; i++)
            {
                float q = (i + 0.5f) * dq;
                float density = betaDensityScratch[i] / total;
                cdf += density * dq;   // running CDF, inclusive of the current cell
                acc += CalculateCompositeScore(q, v) * n * Mathf.Pow(cdf, n - 1) * density * dq;
            }

            return acc;
        }
```

- [ ] **Step 5: Grow the header and rebalance row 3**

In `Source/ProfileEditorTab.cs`, line 75:

```csharp
        private const float HeaderHeight = 140f;
```

becomes:

```csharp
        // Rows: 28 (picker) + 4 + 20 (description) + 2 + 28 (quality) + 2 + 20 (best-of-N) + 4
        // + 54 (curve) = 162. Was 140 before the best-of-N row. The body scrolls, so header
        // height costs scrolled space, not visible content.
        private const float HeaderHeight = 162f;
```

Then replace the three rects on lines 201-203. Row 3's readout gets a "Typical" label, so it needs
more width — the label and slider give some back:

```csharp
            Rect qLabel = qualityRow.LeftPart(0.32f);
            Rect qSlider = new Rect(qualityRow.x + rect.width * 0.33f, qualityRow.y + 3f, rect.width * 0.30f, 22f);
            Rect qReadout = qualityRow.RightPart(0.34f);
```

- [ ] **Step 6: Label row 3 and add row 3b**

In `Source/ProfileEditorTab.cs`, replace lines 224-232 (from the `// The readout is output` comment
through the `DrawQualityDistributionCurve` call):

```csharp
            // The readout is output, not input -- always full opacity, even on a
            // read-only preset, so presets stay comparable by cycling the picker.
            // Labelled "Typical" so it reads as one half of a pair with the row below.
            float meanComposite = CalculateCompositeScore(v.averageQuality, v);
            bool prevReadoutWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(qReadout, $"→  Typical  {PawnVarianceSettings.FormatPowerReadout(meanComposite)}");
            Text.WordWrap = prevReadoutWordWrap;
            TooltipHandler.TipRegion(qReadout,
                "The average pawn this profile generates, compared to the Faithful baseline (0.25).");

            // Row 3b: the Best-of-N anchor.
            //
            // Row 3 alone actively misleads. Players reroll starts, pick from captures and refuse
            // quest pawns, so the pawn they keep is the best of many -- and on the two variance
            // presets the two figures disagree in SIGN. Wildcard reads -18% typical but +17% at
            // best-of-25: a player picking it for a harder run gets an easier one.
            Rect bestRow = new Rect(rect.x, qualityRow.yMax + 2f, rect.width, 20f);
            float bestComposite = PawnVarianceSettings.CalculateBestOfNScore(v, Constants.BestOfNSampleCount);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            bool prevBestWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(bestRow,
                $"Best of {Constants.BestOfNSampleCount} rerolls:  "
                + $"{PawnVarianceSettings.FormatPowerPercent(bestComposite)} vs Faithful ({bestComposite:F2})"
                + "   —   what you actually get if you reroll for this profile");
            Text.WordWrap = prevBestWordWrap;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(bestRow,
                "Generate " + Constants.BestOfNSampleCount + " pawns and keep the best one.\n\n"
                + "This is closer to how the game is actually played: you reroll starting colonists, "
                + "choose which captures to recruit, and refuse quest pawns you do not want.\n\n"
                + "A profile whose two figures are close is a power tier -- consistent. A profile "
                + "that climbs steeply between them is a variance preset -- it pays off when you "
                + "get to choose.");

            // Row 4: the distribution curve, full width, never greyed.
            Rect curveRect = new Rect(rect.x, bestRow.yMax + 4f, rect.width, CurveHeight);
            DrawQualityDistributionCurve(curveRect, v);
```

- [ ] **Step 7: Fix the two stale strings**

`Source/ProfileEditorTab.cs:308` misstates the passion budget. The code spends **1.5** per Major
(`PassionVarianceApplier.cs:61-64`), and `MaxPassionPips = 18` is derived as 12 skills × 1.5, so the
entire envelope already assumes 1.5. Only this string says 2.

Replace:

```csharp
                "Minor passion = 1, Major passion = 2. Presets use fractional budgets, so this reads to one decimal.");
```

with:

```csharp
                "Minor passion = 1, Major passion = 1.5. Presets use fractional budgets, so this reads to one decimal.");
```

(The old row-3 tooltip claiming a `0.31` baseline was replaced in Step 6 — the baseline has been
`0.2500` since the 2026-08-03 retune.)

- [ ] **Step 8: Cross-check the C# integration against the Python reference**

The C# port and `envelope_check.py` must agree, or the UI and HANDOVER's table will state different
numbers for the same thing.

Enable **Verbose logging (dev mode)** in General settings, open the Profile Editor, and cycle through
all seven presets, recording each `Best of 25 rerolls` percentage. Compare against Task 3 Step 3's
verified N=25 column:

| Preset | Expected N=25 |
|---|---|
| `Faithful` | `baseline` |
| `Distinct` | `+10%` |
| `Wildcard` | `+17%` |
| `Desperate` | `-21%` |
| `Elite` | `+15%` |
| `Sovereign` | `+19%` |
| `Specialist` | `+7%` |
| `Scavenger` | `-13%` |

**Every value must match exactly** (the displayed figure is rounded to whole percent, and 1024 nodes
is accurate to 0.17pp). A mismatch of 1pp means the integration is subtly wrong — most likely the
running CDF is exclusive rather than inclusive of the current cell. Do not "fix" it by raising
`BestOfNIntegrationNodes`.

- [ ] **Step 9: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`

- [ ] **Step 10: Verify the layout in-game**

Deploy and open the Profile Editor. Check:
1. The header is 162px and no row overlaps the distribution curve.
2. Row 3 reads `→ Typical +19% vs Faithful (0.40)` on `Sovereign` **on one line, not clipped**.
3. Row 3b renders whole at the default UI scale.
4. Repeat at a non-default UI scale (Options → UI scale). Both rows must still be single-line.
5. Select `Wildcard`: the two figures must show opposite signs (`−18%` typical, `+17%` best-of-25).

- [ ] **Step 11: Commit**

```bash
git add Source/Constants.cs Source/PawnVarianceSettings.cs Source/ProfileEditorTab.cs
git commit -m "feat: show Best-of-25 power alongside the typical-pawn figure

The Profile Editor showed only the N=1 mean, which is the one figure that
does not describe how the game is played -- players reroll starts, pick from
captures and refuse quest pawns, so the pawn they keep is the best of many.

On the two variance presets the two figures disagree in sign. Wildcard read
-18% and a player picking it for a harder run got an easier one.

Reuses CalculateCompositeScore unchanged; only the q fed to it differs. The
integration mirrors envelope_check.py at 1024 nodes (0.17pp of its 20000-node
reference). No new persisted state, no schema change.

Also fixes two stale strings: the passion budget tooltip said Major = 2 when
the code has always spent 1.5, and the power tooltip still quoted the
pre-retune 0.31 baseline.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 5: Label the override columns

Neither override list labels its columns, so a row reads as three unexplained buttons.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs` — add `OverrideColumnHeaders`, call from
  `DrawFactionOverridesSection` (~`:526`) and `DrawXenotypeOverridesSection` (~`:640`)

**Interfaces:**
- Consumes: nothing.
- Produces: `OverrideColumnHeaders(Listing_Standard listing, string firstColumn)` → `void`.
  Task 6 relocates the priority prose into its tooltip.

- [ ] **Step 1: Add the header-row helper**

In `Source/PawnVarianceSettings.cs`, directly above `DrawFactionOverridesSection` (line 521):

```csharp
        // Column captions for the two override lists. Geometry mirrors the row rects below
        // (0.35 / 0.28 / 0.20 / 0.14) -- if those move, move these with them.
        private static void OverrideColumnHeaders(Listing_Standard listing, string firstColumn)
        {
            Rect row = listing.GetRect(18f);
            Rect c1 = new Rect(row.x, row.y, row.width * 0.35f, row.height);
            Rect c2 = new Rect(row.x + row.width * 0.36f, row.y, row.width * 0.28f, row.height);
            Rect c3 = new Rect(row.x + row.width * 0.65f, row.y, row.width * 0.20f, row.height);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            Widgets.Label(c1, firstColumn);
            Widgets.Label(c2, "Profile");
            Widgets.Label(c3, "Priority");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // The fourth column is the Remove button and needs no caption.
            TooltipHandler.TipRegion(c3,
                "Every override defaults to Normal. Overrides set to High or Highest take "
                + "precedence over lower ones.\n\n"
                + "Ties at the same priority are broken by the faction-vs-xenotype toggle above.\n\n"
                + "Factions and xenotypes not listed here have no override and fall back to the "
                + "hostile or colony profile.");

            listing.Gap(2f);
        }
```

- [ ] **Step 2: Call it from the faction section**

In `DrawFactionOverridesSection`, inside the `else` branch (currently line 530-531), immediately
before `string toRemove = null;`:

```csharp
                OverrideColumnHeaders(listing, "Faction");
                string toRemove = null;
```

It goes inside the `else` deliberately — when the list is empty the "No faction overrides
configured." caption reads better on its own.

- [ ] **Step 3: Call it from the xenotype section**

In `DrawXenotypeOverridesSection`, in the matching position inside its `else` branch:

```csharp
                OverrideColumnHeaders(listing, "Xenotype");
```

- [ ] **Step 4: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`

- [ ] **Step 5: Verify in-game**

Open Overrides. Expected:
- Both sections show `Faction`/`Xenotype`, `Profile`, `Priority` captions aligned above the
  corresponding buttons.
- Hovering `Priority` shows the precedence explanation.
- `Delete All` on a section leaves the empty-state caption with **no** orphaned column headers.

- [ ] **Step 6: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "feat: label the columns in both override lists

A row was three unexplained buttons. Adds Faction/Xenotype, Profile and
Priority captions, with the priority rules on the Priority header tooltip.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 6: Move permanently-visible rationale prose into tooltips

Cut from screen, keep in tooltips — nothing is lost, it just stops occupying vertical space by
default.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:483` (priority block), `:524` and `:638` (section
  captions), `:840-843` (hostile rationale), `:866-867` (share paragraph)

**Interfaces:**
- Consumes: `OverrideColumnHeaders` from Task 5 — its Priority tooltip is where `:483` lands.
- Produces: nothing.

- [ ] **Step 1: Delete the priority-levels block**

Its content now lives on the Priority column-header tooltip (Task 5, Step 1). In `DrawOverridesTab`,
delete lines 483-484:

```csharp
            Caption(listing, "Priority Levels: Every override defaults to Normal. ...");
            listing.Gap(4f);
```

- [ ] **Step 2: Delete both per-section captions**

The column headers now say what the rows are, and the precedence rule is already stated in the
checkbox tooltip at line 503.

Delete line 524:

```csharp
            Caption(listing, "Assign custom profiles to specific factions. Faction overrides take precedence over Hostile and General settings.");
```

Delete line 638:

```csharp
            Caption(listing, "Assign custom profiles to specific xenotypes. Xenotype overrides take precedence over Faction, Hostile, and General settings.");
```

**Keep** line 497 (explains why the section is greyed) and lines 528 / 642 (empty-state — tells the
player the list is empty, not broken).

- [ ] **Step 3: Move the hostile-profile rationale to a tooltip**

In `DrawGlobalSettings`, replace lines 839-844:

```csharp
                listing.Gap(ControlGap);
                Caption(listing, "Profile used for raiders and other hostiles:");
                Rect hostileRow = listing.GetRect(30f);
                if (Widgets.ButtonText(hostileRow, LabelFor(hostileProfileId)))
                    ProfileMenu(id => { hostileProfileId = id; RefreshResolved(); });
                TooltipHandler.TipRegion(hostileRow,
                    "Colonists are selected by the player, but raiders arrive directly. Using a "
                    + "separate hostile profile balances raider difficulty independently from your colony.");
                listing.Gap(ControlGap);
```

The short caption above the button stays — it labels the control. Only the two-sentence rationale
moves.

- [ ] **Step 4: Move the Share Settings paragraph to a tooltip**

In `DrawShareSettingsSection`, delete line 867:

```csharp
            Caption(listing, "Copies your whole configuration to the clipboard as text: ...");
```

Then after `exportRect` is declared (line 871) and before the button call, attach it:

```csharp
            TooltipHandler.TipRegion(exportRect,
                "Copies your whole configuration to the clipboard as text: every custom profile, "
                + "both override lists with their priorities, and the options above. Paste it "
                + "anywhere to share it, or import someone else's.");
```

- [ ] **Step 5: Leave the Profile Editor body alone**

**Do not** touch `ProfileEditorTab.cs:309-311` or `:332-334`. Those are *value-derived* captions
that change as the sliders move, and HANDOVER §5 records that they were deliberately kept visible
when every fixed-string caption in that section became a tooltip. They are not redundant prose.

- [ ] **Step 6: Build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `0 Error(s), 0 Warning(s)`

- [ ] **Step 7: Verify in-game**

1. Overrides tab is visibly shorter; scroll to the bottom and confirm nothing is cut off.
2. Hovering the Priority column header shows the priority rules.
3. General tab: hovering the hostile profile button shows the rationale; hovering
   `Export to Clipboard` shows what export includes.
4. Export still round-trips: export, then import the clipboard payload, and confirm settings survive.

- [ ] **Step 8: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "refactor: move always-visible rationale prose into tooltips

The Overrides tab carried three overlapping prose blocks that restated each
other and the two checkbox tooltips. With the new column headers the priority
block has a natural home on hover.

The General tab's hostile-profile rationale and the Share Settings paragraph
are explanations, not instructions, and were permanently occupying rows.

Nothing is deleted outright -- every sentence survives on a tooltip. The
Profile Editor body is untouched: its remaining captions are value-derived
and were deliberately kept visible.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Task 7: Update HANDOVER and close out the branch state

**Files:**
- Modify: `HANDOVER.md`

**Interfaces:**
- Consumes: the verified envelope output from Task 3.
- Produces: nothing.

- [ ] **Step 1: Replace the envelope table**

Run: `python docs/tools/envelope_check.py`

Paste its output verbatim into the table under **"⚖️ The skill ↔ passion exchange rate (`R`)"**
(currently HANDOVER lines 283-293). Delete the `Gifted` row and the sentence
"`Gifted` remains far out — see below; it is unreachable by default and was again left unpatched."

**Do not hand-edit any percentage.** (Rule 6.)

- [ ] **Step 2: Update the tightest-margins table**

Replace HANDOVER lines 298-304 with the tool's new "Tightest envelope margins" block, and replace
the surrounding "there is very little room left" framing — the tightest margin is now 6.5pp, not
1.8pp. Suggested replacement for the heading:

```markdown
**Tightest margins** — the 2026-08-04 retune roughly tripled the worst-case headroom
(was 1.8pp on `Desperate`):
```

- [ ] **Step 3: Remove `Gifted` from the profile tables**

- Delete the `Gifted` row from the "What each profile represents" table (line 384).
- Delete the "`Gifted` is the only preset not reachable by default" paragraph (lines 411-414).
- Delete the `[!NOTE]` block about the `/18` normalizer not fixing `Gifted` (lines 416-423).

- [ ] **Step 4: Close the completed §1.5 sub-items**

Update lines 62-64:

```markdown
- [x] **1.5b. Best-of-N readout** — shipped 2026-08-04. Header shows a `Typical` and a
  `Best of 25 rerolls` anchor. N=25 not N=50, and no `N` slider: it is a lens, not a setting.
- [x] **1.5c. `Gifted` profile tuning** — resolved by removing the preset entirely (2026-08-04).
- [ ] **1.5d. Commit.**
```

Then delete the now-obsolete "📊 Proposed: show Best-of-N in the Profile Editor" section
(lines 66-109) — it is a design proposal for something that now ships.

- [ ] **Step 5: Record the header height change**

In §5, update the pinned-header bullet (line 187-190): `140px` → `162px`, and the row sum
`28 + 4 + 20 + 2 + 28 + 4 + 54 = 140` → `28 + 4 + 20 + 2 + 28 + 2 + 20 + 4 + 54 = 162`.

- [ ] **Step 6: Record the passion-budget correction**

In the `R` section, after the "Passion is an **XP-rate multiplier**" bullet, add:

```markdown
- **The UI said `Major = 2` until 2026-08-04.** It was always a text bug, never a maths bug:
  `PassionVarianceApplier.cs:61-64` has always spent `1.5` per Major, and `MaxPassionPips = 18`
  is derived as 12 skills × 1.5. No envelope figure ever depended on the wrong string.
```

- [ ] **Step 7: Add a §1.6 recording this batch**

Insert after §1.5:

```markdown
## 1.6. 🟢 EDITOR CURSOR FIX, BEST-OF-25 READOUT, RETUNE & UI CLEANUP — SHIPPED (2026-08-04)

- **Profile Editor no longer hijacks the colony profile.** It had no selection state of its own —
  its picker read and wrote `activeProfileId`. Now uses a separate, non-persisted
  `editorProfileId`. Delete also clears the deleted id from the colony profile, hostile profile
  and both override maps; the shared-field version handled only the colony case, by accident.
- **Best-of-25 readout** in the header, beside the typical-pawn figure. Mirrors
  `envelope_check.py` at 1024 integration nodes (0.17pp of its 20000-node reference).
  **If you change one, change both.**
- **Seven presets retuned** to owner-approved targets. `Sovereign`'s skill range is deliberately
  untouched — `skillShiftMin` stays `0` so it can never roll below the vanilla baseline; the
  whole gain is passion budget. Translating it instead would have left 0.5pp of N=1 headroom.
- **`Gifted` removed.** +152% at N=1, unreachable by default, skipped by two retunes.
- **Override columns labelled**, and the overlapping prose blocks moved to tooltips.
- **Neanderthal stays `Distinct`** — reviewed 2026-08-04 and deliberately left alone.
```

- [ ] **Step 8: Commit**

```bash
git add HANDOVER.md
git commit -m "docs: record the 2026-08-04 retune, readout and UI cleanup

Envelope table regenerated from envelope_check.py, not hand-edited. Gifted
removed from every table. Closes 1.5b and 1.5c.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Final verification

- [ ] `dotnet build Source/PawnVarianceMod.csproj` → `0 Error(s), 0 Warning(s)`
- [ ] `python docs/tools/envelope_check.py` → `PASS`, exit code `0`
- [ ] `git grep -n "Gifted" -- Source/` → no output
- [ ] All eight Best-of-25 figures in the UI match the tool's N=25 column (Task 4, Step 8)
- [ ] Cycling the editor picker leaves the General tab's Active Colony Profile unchanged
- [ ] Deleting a custom profile in use as the colony profile leaves no dangling id
- [ ] Header renders at 162px with no row overlap, at default **and** non-default UI scale
- [ ] Export → import round-trips after the Share Settings caption move
- [ ] `git status` clean; branch ready for the §1 merge decision
