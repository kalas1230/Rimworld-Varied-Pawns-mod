# Spec — Best-of-25 readout, preset retune, overrides labels, prose cleanup

Date: 2026-08-04
Branch: `feature/profile-editor-layout`
Status: **approved in brainstorming, not yet implemented**

Covers the project owner's list of 2026-08-04, items 2 through 5. Item 2.5 (Neanderthal
override) was resolved as **no change** — it stays `Distinct`, by owner decision.

---

## Decisions taken in brainstorming (do not relitigate)

| # | Question | Decision |
|---|---|---|
| 2.5 | Should Neanderthal move off `Distinct`? | **No.** Stays `Distinct`. Closed, no code change. |
| 3 | Sovereign/Elite retune targets | **Option (a)** — retarget to what is reachable while holding the ±35% envelope at N=1. The owner's original +30%/+25% at N=25 was not adopted; see §3. |
| 2 | Second Best-of-N anchor at 25 or 50? | **N=25.** N=50 would put `Wildcard` on screen at +33.1%, ~2pp from a visible envelope breach. |
| 2 | Ghosted second curve for Best-of-N? | **No.** Curve stays single-line. |
| 5 | How aggressive is the prose trim? | Cut always-visible captions; **relocate survivors into tooltips**, do not delete outright. |
| 5 | Which tabs? | Owner delegated the call. See §5 — Overrides and General are trimmed, Profile Editor body is deliberately left alone. |

---

## 1. Bug: the Profile Editor mutates the colony's active profile (item 2.4)

**Confirmed.** The Profile Editor has no selection state of its own.

- `ProfileEditorTab.cs:115-116` — the picker reads and writes `activeProfileId`.
- `PawnVarianceSettings.cs:459-460` — the General tab's *Active Colony Profile* button writes the
  same field.
- `PawnVarianceSettings.cs:67` — `EditingCustom` is keyed off `activeProfileId`.
- `PawnVarianceSettings.cs:359` — `RefreshResolved()` sets `Active = Resolve(activeProfileId)`, and
  the whole editor body edits `Active`.

So browsing presets in the editor silently reassigns the colony profile.

### Fix

Add a **separate, non-persisted** editor selection:

```csharp
// UI-only: which profile the Profile Editor tab is pointed at. Deliberately NOT Scribed —
// it is a view cursor, not a setting, and persisting it would add the branch's first
// schema change (see HANDOVER §5, "Schema unchanged").
private string editorProfileId = null;
public VarianceProfileValues Editing { get; private set; }
public bool EditingCustom => GetCustomProfile(editorProfileId) != null;
```

Rules:

1. `editorProfileId` initialises to `activeProfileId` when null (first open, or after load).
2. Add `RefreshEditor()` mirroring `RefreshResolved()`: `Editing = Resolve(editorProfileId)`.
   **`Editing` must be cached, not recomputed per frame** — `Resolve` returns a fresh
   `MakeValues()` for presets (`PawnVarianceSettings.cs:154-155`), so a per-frame call would
   allocate every frame and thrash the Beta cache on `VarianceProfileValues`.
3. `ProfileEditorTab.cs` uses `Editing` everywhere it currently uses `Active`, and
   `editorProfileId` everywhere it currently uses `activeProfileId` — including
   `DrawProfileEditorHeader` (`:109`, `:115-118`, `:176`) and `DrawGenerationSettings` (`:248`).
4. **Call sites that must move from `activeProfileId` to `editorProfileId`:**
   - `ProfileEditorTab.cs:116` — picker
   - `ProfileEditorTab.cs:162-164` — delete fallback
   - `PawnVarianceSettings.cs:813` — `CreateNewCustomProfile`
   - `PawnVarianceSettings.cs:820-823` — `DuplicateCurrentProfile`
5. **Call sites that must NOT move** — these are genuinely about the colony profile:
   - `PawnVarianceSettings.cs:459-460` — General tab picker
   - `:359-360` — `RefreshResolved`
   - `:378`, `:392` — `CopyFrom`
   - `:917` — reset-to-defaults
   - `:260`, `:307-308` — `ExposeData`
6. **Deleting the profile that is also the active colony profile** must still repoint
   `activeProfileId` to a valid id. Today `ProfileEditorTab.cs:160-165` does this implicitly
   because the two are the same field. After the split it must be explicit: on delete, if
   `activeProfileId` or `hostileProfileId` pointed at the deleted profile, fall back to
   `FaithfulId` / `DistinctId` respectively and call `RefreshResolved()`.

### Verification

- Set the colony profile to `Faithful` on the General tab. Cycle every preset in the Profile
  Editor picker. Return to General — it must still read `Faithful`.
- Delete a custom profile that is the active colony profile; confirm no dangling id and no
  null-ref on the General tab.

---

## 2. Best-of-25 readout in the Profile Editor (item 2, HANDOVER §1.5b)

### What is wrong today

Row 3's `→ +21% vs Faithful (0.30)` (`ProfileEditorTab.cs:225-226`) is the **N=1 mean**. The
mod's own envelope maths is Best-of-N precisely because a mean understates any high-dispersion
profile — and then the UI ships the mean. The readout is most wrong on the two variance presets,
which **cross zero**:

| | N=1 (shown today) | N=25 (not shown) |
|---|---|---|
| `Distinct` | −19.9% | +3.5% |
| `Wildcard` | −23.6% | +27.1% |

A player reads `Wildcard: −24%`, concludes "weaker", picks it for a harder run, and gets an
easier one.

### Layout

Row 3 gains one word so the two figures read as a progression; a new row carries the
best-of-25 figure alone. **The new row must not restate row 3's percentage** — they are the same
quantity at different N, and printing "typical" twice was an error in the original mockup.

```
Average pawn quality: 0.53 (read-only)  [====|====]   →  Typical  +21% vs Faithful (0.30)
Best of 25 rerolls:  +12% vs Faithful (0.37)  —  what you actually get if you reroll for this profile
```

- Header grows `140f → 162f` (new 20px row + 2px gap). Update `HeaderHeight`
  (`ProfileEditorTab.cs:75`) and the row-sum comment in HANDOVER §5.
- Cost is scrolled body space only; the body already scrolls with `min 750f`. Nothing visible is lost.
- New row is **full width**, styled like row 2 (`GameFont.Tiny`, `WordWrap = false`, restore both).
- **No `N` slider.** N is a lens, not a setting; a slider invites fiddling with something that has
  no correct value.
- No new persisted state. No schema change.

### Computing it

Port `envelope_check.py:120-139` to C#: the expected composite of the max of N Beta draws,
`E[composite(max(q₁…q₂₅))] = ∫ composite(q) · N · F(q)^(N−1) · f(q) dq`.

- The Python tool uses `GRID = 20000`. **That is far too heavy for a per-frame UI path.** Use a
  512-node midpoint grid.
- **Mandatory one-time cross-check:** the C# figure must agree with `envelope_check.py` to within
  **0.5pp** on all eight enforced presets. If it does not, raise the grid until it does. Record the
  comparison in the plan's verification step — the UI and the HANDOVER table must not disagree.
- Cache per profile; recompute only when the values change. `VarianceProfileValues` already has a
  `distributionParamsDirty` flag (`VarianceProfile.cs:59`) to hang this off.
- Reuse `CalculateCompositeScore` unchanged. Only the `q` fed to it differs.

### Supporting changes

- `FormatPowerReadout` (`PawnVarianceSettings.cs:932-949`) returns a whole sentence
  (`"+21% vs Faithful (0.30)"`). Add an overload returning just the signed percentage, so the pair
  of anchors does not print "vs Faithful" twice on screen.
- **`ProfileEditorTab.cs:228` is a stale string:** the tooltip says *"compared to Faithful baseline
  (0.31)"*. The baseline has been `0.2500` since the 2026-08-03 retune. Fix it in the same edit.

---

## 3. Preset retune (item 3)

### Why the owner's original targets were not adopted

Deviation vs `Faithful` **compresses structurally as N rises** — Best-of-N drives `q` toward 1.0
for every profile, so each converges on its own ceiling and the gaps narrow. `Sovereign` is +30.9%
at N=1 but +16.2% at N=25 for exactly this reason.

So "Sovereign ~30% at N=25" implies N=1 lands well past +35% — a Rule 1 breach at the tight end.
Same shape for "Elite ~25% at N=25". The only lever that reaches those numbers is raising the
ceiling while dropping the floor, which converts `Sovereign` from a power tier into a second
variance preset — contradicting its stated identity and Rule 2's tier framing.

The owner chose **option (a)**: keep them power tiers, retarget to what is reachable.

### Targets

Current → target, measured at N=25 unless stated. **These are directional; `envelope_check.py` is
the arbiter.**

| Profile | N=25 now | N=25 target | Note |
|---|---|---|---|
| `Sovereign` | +16.2% | **+18 to +20%** | retargeted from owner's +30% |
| `Elite` | +12.5% | **+14 to +15%** | retargeted from owner's +25% |
| `Specialist` | +4.6% | **~+7%** | minor buff, as owner asked |
| `Wildcard` | +27.1% | **~+17%** | revised down 2026-08-04 — see "Second-pass compression" |
| `Distinct` | +3.5% | **~+10%** | and **~−10% at N=1** (from −19.9%) |
| `Scavenger` | −20.3% | **~−13%** | as owner asked |
| `Desperate` | −27.3% | **~−20%** | revised in from −25% — see "Second-pass compression" |

### Second-pass compression (owner, 2026-08-04)

Because `Sovereign` and `Elite` are landing at ~+19% / ~+14% rather than the originally proposed
+30% / +25%, the outer presets were pulled in to match that narrower spread:

- **`Wildcard` +25% → ~+17%.** At +25% a *variance* preset would sit above `Sovereign`, the top
  power tier, at N=25. Rule 2 formally exempts `Wildcard` from ordering, but a variance preset
  visibly out-powering the top tier reads as a bug to a player even when it isn't one. ~+17% seats
  it just under `Sovereign`.
- **`Desperate` −25% → ~−20%.** Keeps the low end proportionate to the compressed high end.
  `Scavenger` at ~−13% preserves `Desperate < Scavenger` comfortably.

**Both revisions move the two tightest presets further inward** (`Wildcard` had 1.9pp of headroom at
N=50, `Desperate` 1.8pp at N=1), so this pass should buy back a substantial amount of envelope
margin rather than spending it.

### Hard constraints

1. **Rule 1** — every enforced preset inside ±35% at N = 1, 5, 25, 50.
2. **Rule 2** — `Desperate < Scavenger < Faithful < Specialist < Elite < Sovereign` at every N.
   `Distinct` and `Wildcard` are exempt (variance presets).
3. **Do not break the N=1 and N=5 figures** to hit an N=25 target — the owner stated this
   explicitly. Treat N=1 as the binding constraint on `Sovereign`/`Elite`.
4. **Rule 3** — trait count stays out of the composite score, and widening a trait spread to move a
   score is forbidden. Move `averageQuality`, `skillShift*`, `passionCount*`, `passionMajorBias`.
5. `Gifted` is being removed (§4), so it drops out of the table entirely.
6. After every change: `python docs/tools/envelope_check.py`, and paste its output into the
   HANDOVER table. **Never hand-edit those percentages** (Rule 6).

### Headroom warning

Current tightest margins are `Desperate` 1.8pp and `Wildcard` 1.9pp. The composite weights are
shared across all presets, so a change to any weight moves every profile at once. The targets above
deliberately move both tight presets *inward*, which should relieve rather than add pressure — but
verify, do not assume.

---

## 4. Remove `Gifted` (item 2.2)

`Gifted` sits at **+152% at N=1** — far outside the envelope — and is not reachable in the default
config. It has survived two retunes unpatched. The owner's decision is to delete rather than tune
it: it is unused and creates noise.

Remove from:

- `VarianceProfile.cs:274-...` — the `GiftedColony` definition
- `VarianceProfile.cs:413` — its entry in the preset list
- `VarianceProfile.cs:192` — `GiftedId`
- `VarianceProfile.cs:15` — `VarianceProfileId.GiftedColony` enum member
- `Constants.cs:29` — the comment referencing it
- HANDOVER: the profile table row, the envelope table row, the `/18`-normalizer note, and the
  "Gifted is the only preset not reachable by default" paragraph

### Migration — not needed

An earlier draft of this spec required a load-time remap of `"preset_gifted"` to `SovereignId`.
**The owner confirmed the mod is unpublished, so no user config exists that could hold that id.**
Drop the remap.

A stale *local* dev config is still harmless: `GetPresetById` returns null and `Resolve`
(`PawnVarianceSettings.cs:151-166`) falls through to `customProfiles[0]` or `VanillaLike`. Arbitrary
but not an error, and reselecting a profile fixes it permanently.

Renumbering `VarianceProfileId` is likewise safe for the same reason, but leave the remaining
members on their current values anyway — the churn buys nothing.

---

## 5. Overrides column labels (item 4)

Neither override section labels its columns. Add a header row above each list, matching the
existing 0.35 / 0.28 / 0.20 / 0.14 column geometry (`PawnVarianceSettings.cs:545-548`, and the
xenotype equivalent).

```
── Faction Overrides ──────────────────────────────────
  Faction          Profile        Priority
  Empire           Elite          Highest      Remove

── Xenotype Overrides ─────────────────────────────────
  Xenotype         Profile        Priority
  Sanguophage      Sovereign      Highest      Remove
```

- Column 1: **Faction** / **Xenotype**. Column 2: **Profile**. Column 3: **Priority**. Column 4:
  no label (the buttons are self-describing).
- Style as `GameFont.Tiny` at ~65% alpha, matching the existing caption treatment.
- Only draw the header row when the list is non-empty — the empty-state caption reads better alone.
- The **Priority** header carries the priority-rules tooltip relocated from `:483` (see §6).

---

## 6. Prose cleanup (item 5)

Approach: **cut from screen, keep in tooltips.** Nothing is lost; it stops occupying vertical space
by default.

### Overrides tab — trim

| Line | Text | Action |
|---|---|---|
| `:483` | 4-sentence "Priority Levels: …" block | **Remove from screen.** Relocate to the new **Priority** column-header tooltip (§5). |
| `:524` | "Assign custom profiles to specific factions. Faction overrides take precedence over Hostile and General settings." | **Remove.** Column headers now say what the rows are, and the precedence rule is already in the `:503` checkbox tooltip. |
| `:638` | Xenotype equivalent | **Remove**, same reasoning. |

**Keep** `:497` (state-dependent — explains why the section is greyed) and `:528` / `:642`
(empty-state — tells the player the list is empty, not broken).

### General tab — trim

| Line | Text | Action |
|---|---|---|
| `:843` | "Colonists are selected by the player, but raiders arrive directly. Using a separate hostile profile balances raider difficulty independently from your colony." | **Move to a tooltip** on the hostile-profile button. It is rationale, not instruction. |
| `:867` | 3-line Share Settings paragraph | **Move to a tooltip** on the export button. |

**Keep** `:457`, `:830`, `:840` — short functional captions that label the control directly beneath
them.

### Profile Editor body — deliberately NOT trimmed

Its fixed-string captions were already converted to tooltips in the layout redesign. The only
always-visible prose left is `ProfileEditorTab.cs:309-311` and `:332-334`, and those are
**value-derived** captions that change with the slider — HANDOVER §5 records that these were
deliberately kept visible. Leave them.

### One factual fix in the same pass (item 2.3)

`ProfileEditorTab.cs:308` reads *"Minor passion = 1, Major passion = 2."*

**The code spends 1.5 per Major**, not 2 — `PassionVarianceApplier.cs:61-64`. `MaxPassionPips = 18`
(`Constants.cs:33`) is derived as 12 skills × 1.5, so the entire envelope already assumes 1.5. The
"2" exists only in this one string.

**This is a text bug, not a maths bug. Change the string to `Major passion = 1.5`. Nothing
recalculates, and no envelope figure moves.**

---

## Out of scope

- Merging the branch (HANDOVER §1 gates it; §1's checklist is complete but §1.5d commit is not).
- The file-by-file code review in HANDOVER §2.
- Anything touching `CalculateCompositeScore`'s weights or `Constants.cs` scoring values — §3 moves
  *preset inputs* only, not shared constants. Moving a shared constant would move all presets at
  once and is a Rule 5 consultation item in its own right.

---

## Verification checklist

- [ ] `dotnet build Source/PawnVarianceMod.csproj` → 0 errors, 0 warnings
- [ ] `python docs/tools/envelope_check.py` → PASS, and its table pasted into HANDOVER
- [ ] C# Best-of-25 figures agree with `envelope_check.py` to within 0.5pp on all eight presets
- [ ] Editor picker cycling leaves the General tab's Active Colony Profile unchanged
- [ ] Deleting the profile that is also the colony profile leaves no dangling id
- [ ] A local config still holding `preset_gifted` loads without an exception (falls back; no remap expected)
- [ ] In-game: header renders at 162f with no row overlap at 1x and at UI scale
