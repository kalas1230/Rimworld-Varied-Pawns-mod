# Profile Editor Tab — Layout Redesign

Date: 2026-08-03
Status: Approved (design), not yet implemented
Scope: `Source/PawnVarianceSettings.cs` — the Profile Editor tab only

---

## 1. Problem

The Profile Editor tab is one single-column `Listing_Standard` inside one
`Widgets.BeginScrollView`. In a `Dialog_ModSettings` window the tab content rect is roughly
**860 x 600**. Measured content height is ~1600-2000px, so the tab is 3+ screens of scrolling.

Three specific faults:

1. **~200px of profile-management chrome scrolls away.** `DrawProfileSelector`
   (`PawnVarianceSettings.cs:848-906`) stacks a header, a picker button, a description label, a name
   field, and up to four full-width buttons before a single setting appears.
2. **The feedback is off-screen from the thing that drives it.** The tier readout and
   `DrawQualityDistributionCurve` sit at the very top. Dragging any Skills/Traits/Passions slider
   scrolls their own output out of view.
3. **Height is dominated by two helpers, not by content.** `LabeledSlider`
   (`PawnVarianceSettings.cs:809-816`) costs **~56px per slider** — a full-width label row, a 2px gap,
   a 22px slider, and a 10px `ControlGap`. There are **13 sliders**, so sliders alone are ~730px.
   `Section` (`PawnVarianceSettings.cs:790-798`) costs **~62px per header** — `SectionGap` +
   `GapLine` + a Medium-font label + 4px. Four sections cost ~248px.

Fault 3 is the real driver, and it is why this redesign compacts **controls**, not just layout.

## 2. Rejected alternatives

| Option | Why rejected |
|---|---|
| Split the settings into two ~415px columns | Labels such as `"Skill noise (spread between a pawn's own skills):  0.42"` clip. Column heights cannot be balanced because `ModsConfig.BiotechActive` and the now-conditional child-shift block change height at runtime. |
| Collapse every section behind `>` foldouts by default | Taxes the common path with clicks, and needs persisted expand state. Made moot by §4 — the scroll it solves largely disappears. |
| Master-detail: 300px left rail with a profile list + pinned preview | Trades 315px of permanent horizontal width to reclaim ~200px of vertical chrome. Worse, it forces the distribution curve into ~270px: 70 samples at 3.8px each, four tier bands at ~67px each — unreadable. It also puts the curve bottom-left while `averageQuality` stays top-right, which is a *worse* feedback loop than the one being fixed. |

## 3. Design: pinned header + compacted single column

Split the tab rect **once**, into two sibling rects. No nesting, so no nested-scrollview
mouse-wheel conflict.

```
┌─ PINNED, 140px, does not scroll ───────────────────────────────────────┐
│ [ Faithful  ▼ ]      [+ New] [Duplicate] [Rename] [Reset] [Delete]     │
│ Closest to unmodded RimWorld. The reference all envelope…    (hover ⓘ) │
│ Average pawn quality  ────────●────────  0.50   →  Standard (0.48)     │
│ ▁▂▄▆█▆▄▂▁  full-width distribution curve, always full opacity          │
├─ SCROLLS, 460px viewport ──────────────────────────────────────────────┤
│ Skills                                                          [x]    │
│   Skill noise ──────●──────── 0.20                                     │
│   Skill shift        [ −2.0 ◄════════════════► +5.0 ]                  │
│   [x] Also shift skills when a child grows up                          │
│   Child shift        [ −1.0 ◄══════► +2.0 ]                            │
│ Traits                                                          [x]    │
│   [ ] Count xenotype/forced traits toward the trait count              │
│   Trait count        [ 2 ◄══════► 4 ]                                  │
│ Passions                                                        [x]    │
│   Passion noise ───●─── 0.25        Major bias ────●──── 0.50          │
│   Passion budget     [ 2.5 ◄══════════► 6.2 ]                          │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.1 Pinned header (140px, full 840px width)

| Row | Height | Contents |
|---|---|---|
| 1 | 28 | Profile picker `ButtonText` (~240px, opens the existing `ProfileMenu` FloatMenu) + action strip: `+ New`, `Duplicate`, `Rename`, `Reset`, `Delete` |
| 2 | 20 | One-line description / fingerprint (§3.2) |
| 3 | 28 | `Average pawn quality` label + inline slider + `→ {tier} ({score})` readout, all on one row |
| 4 | 54 | `DrawQualityDistributionCurve` at full 840px width, unchanged |
| — | ~10 | gaps |

The header is drawn with explicit `Rect` maths, not `Listing_Standard`, so its height is a
constant and cannot drift.

Row 3 replaces a `LabeledSlider` (56px) with an inline row (28px). The curve keeps its full width,
which was the decisive objection to the rail.

### 3.2 The description slot

One constant-height 20px row, never empty, content depends on selection:

- **Preset selected** — the authored `preset.description`, rendered with `Text.WordWrap = false` so
  it clips rather than reflows. Full text on hover via `TooltipHandler.TipRegion`.
- **Custom profile selected** — an auto-generated one-line **fingerprint** derived from the live
  values, e.g. `Traits 2–4 · Passions 2.5–6.2 · Skill shift −1.0 to +3.8 · Quality 0.55`.

Rationale for the fingerprint over a user-editable description field: a user-authored field is
blank for most players, so it trades a boilerplate string for an empty bar. The fingerprint is
dense, always accurate, costs the user nothing, and makes profiles comparable at a glance.
`VarianceProfiles.CustomDescription` (`VarianceProfile.cs:412`) is no longer used by this tab.

**No data-model change.** The fingerprint is derived at draw time from `VarianceProfileValues`.
`CustomProfile` gains no field, so `Scribe_Deep` and `SettingsTransfer.cs` clipboard payloads are
untouched.

Per-frame string interpolation for this row is acceptable: `DoWindowContents` already interpolates
on every control every frame (e.g. `PawnVarianceSettings.cs:939`), and this is a modal settings
window with the simulation not running.

### 3.3 Settings body (scrolls, 460px viewport)

Four compactions, in order of impact:

**C1 — Four min/max slider pairs become four range widgets.**
`skillShiftMin/Max`, `childSkillShiftMin/Max`, `traitCountMin/Max`, `passionCountMin/Max`.
Eight `LabeledSlider` calls (~448px) plus four now-redundant range captions become four
~42px widgets (~168px). **Saves ~330px.** See §5 for the API contract.

**C2 — The `Enable X variance` checkbox moves into its section header row.**
A new `SectionHeader(listing, title, ref enabled, tooltip)` helper draws `GapLine` + a single 30px
row with a Medium-font label left and the checkbox right. ~45px per section versus the current
`Section` 62px + a separate 34px checkbox row. **Saves ~150px across three sections.**

**C3 — Static explanatory captions become tooltips.** Only captions computed from live values
survive as text: the tier readout, the `"a low-quality pawn can lose up to N levels"` warning
(`PawnVarianceSettings.cs:1006-1008`), and the passion-minimum note.

**C4 — `passionNoise` and `passionMajorBias` share one 28px row** at `LeftPart(0.48f)` /
`RightPart(0.48f)`. Labels shorten to `Passion noise: 0.25` and `Major bias: 0.50`; the full
explanations move to tooltips. At ~400px per half an 18-character label takes ~130px, leaving
~270px of slider. Two `LabeledSlider` calls (112px) become one 38px row — **saves ~74px.**

**Note: `Overall quality` is no longer a section here.** Its slider and curve moved to the header,
so the body has three sections, not four.

### 3.4 Measured worst case

The case to design against is **Biotech active AND `applyChildSkillShift` checked** — the latter
now genuinely adds height, since it was recently changed from `GUI.enabled` greying to conditional
rendering (`PawnVarianceSettings.cs:1000-1009`).

| Section | Breakdown | Height |
|---|---|---|
| Skills | header 45 + `skillNoise` LabeledSlider 56 + shift range 42 + child checkbox 34 + child range 42 + warning caption 18 | 237 |
| Traits | header 45 + `countProtectedTraits` checkbox 34 + count range 42 | 121 |
| Passions | header 45 + paired noise/bias row 38 + budget range 42 + caption 18 | 143 |
| **Total** | | **~501px** |

Against a 460px viewport that is **~1.1 screens** — a residual ~40px of scroll in the worst case,
versus 3+ screens today.

This figure is deliberately conservative and corrects two optimistic estimates made during design
review (370-394px), which under-counted `Section` and `LabeledSlider`.

**Therefore: no foldouts.** They would add expand/collapse state, persistence, and a click tax on
the common path to solve ~40px of scroll. The `BeginScrollView` and the existing dynamic
`profileEditorViewHeight = listing.CurHeight + 40f` measurement are **retained** — they cost nothing
and cover large UI scales and long localizations.

## 4. Rename

Renaming a custom profile moves out of the inline `DrawNameField`
(`PawnVarianceSettings.cs:928-938`) and into a `Rename` button in the Row 1 action strip, opening a
subclass of vanilla `Verse.Dialog_Rename`.

Rejected alternative: making the Row 1 profile control morph into a `TextField` for customs. It
changes Row 1's geometry between selections (reads as a glitch), and an always-focused IMGUI text
field swallows stray keystrokes.

**Known risk:** five action buttons across ~590px is ~118px each. `Duplicate` fits in English; a
long localization could clip. Accepted for now.

## 5. Range widget contract (C1)

```csharp
// Verse.Widgets — static, NOT a Listing_Standard method.
// Rects must be carved manually with listing.GetRect(h).
Widgets.FloatRange(Rect rect, int id, ref FloatRange range,
                   float min, float max, string labelKey, ToStringStyle valueStyle);
```

Binding rules:

1. **`Widgets.FloatRange` for all four pairs.** `Widgets.IntRange` is **forbidden** — see §6.
2. **`ref` staging is mandatory.** An r-value struct cannot be passed by `ref`. Marshal into a
   local, pass `ref` the local, copy back:
   ```csharp
   var r = new FloatRange(v.traitCountMin, v.traitCountMax);
   Widgets.FloatRange(rect, TraitRangeId, ref r, 0f, 15f, null, ToStringStyle.Integer);
   v.traitCountMin = r.min;
   v.traitCountMax = r.max;
   ```
3. **Stable `const int` ids, one per widget** — not a running counter, not a loop index:
   `SkillShiftRangeId = 1001`, `ChildSkillShiftRangeId = 1002`, `TraitCountRangeId = 1003`,
   `PassionCountRangeId = 1004`. `Widgets.FloatRange` uses the caller-supplied id against its own
   static drag-tracking state rather than `GUIUtility.GetControlID` auto-indexing, so conditionally
   rendering the child-shift block cannot shift ids or hijack a drag.
4. **No `roundTo` quantisation** on any of the four.
5. Display style: `ToStringStyle.Integer` for trait counts; `ToStringStyle.FloatOne` for passion
   counts (§6); existing precision for the two skill-shift ranges.

Marshalling happens purely in UI code, so `VarianceProfileValues` field types are unchanged and
`Scribe_Deep` / `SettingsTransfer.cs` are unaffected.

The pre-existing inversion guard at `VarianceProfile.cs:98-99` stays. Range widgets are **not**
being adopted for inversion safety — that is already handled.

## 6. Fractional passion counts — do not truncate

`passionCountMin` / `passionCountMax` hold **deliberately fractional calibrated values** across the
shipped presets: `1.4`, `2.5`, `3.0`, `5.0`, `6.0`, `6.2`, `11f`, `12f`
(`VarianceProfile.cs:280-394`).

`Widgets.IntRange` would silently truncate `6.2` → `6` the first time a user duplicated Elite and
nudged the slider. That is a stealth recalibration of a governed value: HANDOVER Rule 5 requires
explicit sign-off before changing profile parameters, and the ±35%-of-`Faithful` envelope is
measured against these numbers. **`IntRange` is therefore forbidden on all four pairs.**

Trait counts happen to be integral across every preset, but they use `FloatRange` with
`ToStringStyle.Integer` for consistency and to remove the truncation hazard entirely.

**Approved display change:** passion counts switch from `:F0` to `:F1`. Today `6.2` renders as
`"6"`, so dragging `6.2` → `6.4` leaves the label frozen and the control feels broken. This changes
**display only** — no stored value moves, so it is not a Rule 5 change. Signed off by the project
owner on 2026-08-03.

## 7. Enable-state matrix

Today the whole body is wrapped in `GUI.enabled = wasEnabled && EditingCustom`
(`PawnVarianceSettings.cs:497`). With the quality slider in the header, that single wrapper no
longer covers everything, so state is explicit per element:

| Element | Preset selected | Custom selected |
|---|---|---|
| Profile picker | Enabled | Enabled |
| `+ New`, `Duplicate` | **Enabled** | Enabled |
| `Rename`, `Reset` | Disabled | Enabled |
| `Delete` | Disabled | Enabled if `customProfiles.Count > 1` |
| Description / fingerprint row | Drawn (prose) | Drawn (fingerprint) |
| `Average pawn quality` slider | Disabled | Enabled |
| Tier readout + distribution curve | **Full opacity** | Full opacity |
| Settings body | Disabled | Enabled |

Two load-bearing rows:

- **`+ New` and `Duplicate` must stay enabled for presets.** A new user lands on `Faithful`, which
  is a preset; these buttons are the only escape from a fully greyed tab.
- **The curve is never greyed.** It is a readout, not a control. Greying it would destroy the
  ability to compare presets by cycling the picker — the one genuine benefit lost by dropping the
  rail. The greyed slider directly above it already communicates that quality is locked. The slider
  label gains a `(read-only)` suffix when a preset is active.

`Reset` and `Delete` keep confirmation dialogs via `Dialog_MessageBox.CreateConfirmation`,
consistent with the Overrides tab.

## 8. Out of scope

- The General and Overrides tabs.
- `SettingsTransfer.cs` import/export — unaffected by design (§3.2, §5).
- Any change to profile *values*, the composite score, or the ±35% envelope. This is a presentation
  change only. The one display-format change is documented and signed off in §6.

## 9. Verification

1. `dotnet build Source/PawnVarianceMod.csproj` → `0 Error(s), 0 Warning(s)`.
2. In-game, Biotech **active**, `applyChildSkillShift` **checked**, on a custom profile: confirm the
   body is ~500px and the header stays pinned while the body scrolls.
3. Select each of the 9 presets: description clips to one line, tooltip shows full prose, curve
   redraws at full opacity, body stays greyed, `+ New` / `Duplicate` stay clickable.
4. Duplicate `Elite`, then read back `passionCountMax`: must still be `6.2`, not `6`. Drag the
   passion range and confirm the label tracks in `0.1` steps.
5. Toggle `applyChildSkillShift` repeatedly while dragging the skill-shift range: no drag hijack
   between the two range widgets (validates the §5 id rule).
6. Export settings to clipboard, wipe, re-import: all four ranges round-trip with fractional values
   intact.
7. Create a custom profile, rename via the dialog, confirm the picker label and fingerprint update.
