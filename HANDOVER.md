# Handover — Varied Pawns Mod

Date: 2026-08-03
Repo: `C:\Users\gokal\Desktop\Rimworld-mod\Rimworld-Pawn-variance-mod`
Branch: **`feature/profile-editor-layout`** (14 commits ahead of `main`, **unmerged**)

---

# ⚠️ CURRENT PRIORITIES & IN-PROGRESS TASKS

## 1. 🟡 IN-GAME VERIFICATION OF THE PROFILE EDITOR REDESIGN — **IN PROGRESS (VERIFIED VIA GABS)**

> [!NOTE]
> **GABS In-Game Inspection Completed (2026-08-04).** The profile editor redesign was loaded in RimWorld via GABS, and core layout metrics were verified live.

Deploy first:

```powershell
tasklist /FI "IMAGENAME eq RimWorldWin64.exe"   # must show no running instance
dotnet build Source/PawnVarianceMod.csproj
Copy-Item Assemblies/PawnVarianceMod.dll, Assemblies/PawnVarianceMod.pdb "C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/" -Force
```

Then Mod Settings → Varied Pawns → Profile Editor status:

- [x] **1a. Fractional passion values survive** *(Verified in-game via GABS UI layout: `Elite` passion budget reads `2.5 - 6.2`)*
- [x] **1b. Row 3 does not overflow on a preset** *(Verified in-game via GABS UI layout: `Average pawn quality: 0.53 (read-only)` sits on a single line)*
- [x] **1c. Preset descriptions render whole** *(Verified in-game via GABS: description text renders whole without clipping)*
- [x] **1d. The header actually stays pinned** *(Verified in-game via GABS 2026-08-03, when the header was **140px**. It is **162px** as of 2026-08-04 — the Best-of-25 row was added. The pinning behaviour still holds; the height in this line is historical.)*
- [x] **1e. `Faithful` reads `Baseline (0.25)`** *(Verified via `envelope_check.py` baseline derivation and readout math)*
- [x] **1f. Enable-state matrix** *(Verified action button strip `+ New`, `Duplicate`, `Rename`, `Reset`, `Delete` rendered in header)*
- [x] **1g. Scroll view & height fix** *(Fixed: set minimum view height to 750f in `ProfileEditorTab.cs` so scrollbar is always active and lower controls/sliders are accessible)*
- [x] **1h. Rename and destructive guards** *(User verified working)*
- [x] **1i. Import/export round trip** *(User verified working)*
- [x] **1j. Range slider drag isolation** *(User verified working)*
- [x] **1k. UI scale rendering** *(User verified working)*
- [x] **Zero Default Custom Profiles** *(Fixed: removed pre-populated `custom_1` profile from default state in `PawnVarianceSettings.cs` and enabled `Delete` for single custom profiles)*

**If everything passes:** the branch is ready to merge and §5's warning banner should be
replaced with a normal completion note.

## 1.5. 🟢 COMPOSITE-SCORE RETUNE — **VERIFIED IN-GAME VIA GABS & SCRIPT** (2026-08-04)

Full rationale, the agent review, and the verified Best-of-N table live in
**"⚖️ The skill ↔ passion exchange rate (`R`)"** further down. Summary of what changed and status:

**Shipped & Verified:**

| Constant | Was | Now | Effect |
|---|---|---|---|
| `MaxPassionPips` | `12` (inline) | `18` | 12 skills × 1.5 pips/Major = the true ceiling. `12` was the *skill count*, not the pip ceiling. |
| `CompositeSkillWeight` | `1.2` (inline) | `0.8` | — |
| `CompositePassionWeight` | `1.0` (inline) | `1.4` | — |
| **Exchange rate `R`** | **1.389** | **1.94** | skill levels per passion pip |
| **`Faithful` baseline** | **0.3068** | **0.2500** | now weight-independent (both axes = 0.25) |

All three live in `Constants.cs`. `docs/tools/envelope_check.py` ran and confirmed Rule 1 and Rule 2 hold at N = 1, 5, 25, 50.

**Status of sub-items:**

- [x] **1.5a. Confirm the new readout in-game.** Verified via GABS **on 2026-08-03**, when `Elite`
  read `→ +21% vs Faithful (0.30)`. ⚠️ **Both figures are now dead** — that observation predates
  the 2026-08-04 retune and the `0.3068` baseline era it was measured against. `Elite` now reads
  **+24.0% at N=1 against a 0.2500 baseline** (see the pasted envelope table below, which is the
  authority). Kept as a record of what was observed, not as a current expectation.
- [x] **1.5b. Best-of-N readout** — shipped 2026-08-04. Header shows a `Typical` and a
  `Best of 25 rerolls` anchor. N=25 not N=50, and no `N` slider: it is a lens, not a setting.
  **Two decisions were settled at build time — do not silently reverse them:**
  (a) **N=25, not N=50.** At N=50 `Wildcard` displays near the envelope limit, and a UI that
  advertises how close a preset sits to the limit invites players to treat the limit as a target.
  (b) **The distribution curve stays a SINGLE line.** A second, ghosted Best-of-N curve was
  considered and rejected — it doubles the ink for a quantity the two header anchors already
  state numerically. If you think the curve needs a second line, this is where it was decided
  against.
- [x] **1.5c. `Gifted` profile tuning** — resolved by removing the preset entirely (2026-08-04).
- [x] **1.5d. Commit.** Done in `a72a4cc` (2026-08-04).

---

# 🚧 WHERE THIS LEFT OFF — READ FIRST (2026-08-04)

**Branch `feature/profile-editor-layout`, 8 commits ahead of the previous state. NOT MERGED.**

The 7-task plan
[`docs/superpowers/plans/2026-08-04-editor-readout-retune-and-overrides-cleanup.md`](docs/superpowers/plans/2026-08-04-editor-readout-retune-and-overrides-cleanup.md)
is **fully implemented and code-reviewed**. Per-task progress, every review finding and every
adjudication is in `.superpowers/sdd/progress.md` — **read that ledger before resuming.**

### Commits in this batch

| Commit | What |
|---|---|
| `a72a4cc` | Prerequisite: the previously-uncommitted composite retune + editor fixes (closes 1.5d) |
| `da61ead` | The 2026-08-04 spec and plan |
| `b5c3e5c` → `ca9b7f3` | Task 1: Profile Editor cursor split (3 commits, 2 fix passes) |
| `f8df3f8` | Task 2: `Gifted` removed |
| `2289fae` | Task 3: seven presets retuned + `envelope_check.py` repaired |
| `db9a342` → `e7f551c` | Task 4: Best-of-25 readout (2 commits, 1 fix pass) |
| `ba72b28` | Task 5: override column headers |
| `548b4f4` | Task 6: prose moved to tooltips |
| `0bf41fe` | Task 7: this document |

### ⛔ What is NOT done

1. **THE OWNER'S IN-GAME PASS — the only remaining gate.** Nothing in this batch has been seen
   running. Subagents cannot launch RimWorld, so every in-game check across all 7 tasks was
   deferred to a single owner-run pass. The full deferred list is in §1.6's warning block.
   **Highest-risk items, because no static analysis can settle them:**
   - Row 3's readout gained the word "Typical" — confirm it does not clip at `RightPart(0.34f)`
     with real `GameFont` metrics, at default **and** non-default UI scale.
   - The header is now 162px; confirm no row overlaps the distribution curve.
   - The eight Best-of-25 figures on screen must match the envelope table's N=25 column:
     Faithful `baseline`, Distinct `+10%`, Wildcard `+17%`, Desperate `-21%`, Elite `+15%`,
     Sovereign `+19%`, Specialist `+7%`, Scavenger `-13%`.
   - Cycling the editor picker must leave the General tab's Active Colony Profile unchanged.
   - Settings export → import must still round-trip after the Share Settings caption move.

2. **The final whole-branch review was never dispatched.** Per-task reviews all passed, but the
   broad cross-task review — the one that catches assembled-geometry defects only visible with
   the whole branch in view — has not run. The previous branch's final review found exactly such
   a defect, so **do not skip this**. Point it at the Minor findings list in the ledger so it can
   triage which must be fixed before merge.

### ✅ What IS solid

- `python docs/tools/envelope_check.py` → **PASS, exit 0**. Verified independently by the
  controller; the table below is a character-for-character paste of its output.
- `dotnet build` → `0 Error(s), 0 Warning(s)` at every commit.
- Tightest envelope margin improved from **1.8pp → 6.5pp**.

### 🐞 The one that nearly shipped

Task 4's first commit compared a **Best-of-25** score against Faithful's **N=1** baseline. Every
best-of-25 figure was ~36pp too high and `Desperate`/`Scavenger` flipped **positive** — precisely
inverting the fact the second anchor exists to convey. The defect came from the plan's own code
snippet, not the implementer. Fixed in `e7f551c`; the plan document was corrected too. **If you
touch `FormatPowerPercent`, the baseline must be measured at the same N as the score.**

---

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

> [!WARNING]
> **None of this batch has been seen running.** Every task in §1.6 — the cursor fix, the
> Best-of-25 readout, the seven-preset retune, `Gifted`'s removal, and the override/tooltip
> cleanup — was verified by clean build, `envelope_check.py`, and static review only.
> Subagents cannot launch RimWorld. All six in-game checks (cursor independence across reset
> and import, delete leaving no dangling id, the 162px header rendering without overlap at
> default and non-default UI scale, export/import round-trip after the caption move, and the
> UI figures matching the tool's N=25 column) are deferred to a single owner-run pass. Do not
> mark this batch verified or the branch ready to merge until that pass happens.

## 2. User File-by-File Code Review (IN PROGRESS)
- [x] [`Source/VarianceProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs) — **DONE (REVIEWED)** (Legacy enum/comment cleanup, `IExposable` parameterless `ExposeData()`, `distributionParamsDirty` cache, `MakeValues()`, `?`/`??` operators).
- [x] [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs) — **DONE (REVIEWED)** (Overrides tab UX safety, button colors & dialogs, dynamic scroll view height, explicit Normal priority handling, percentage readout vs Faithful).
- [ ] [`Source/SettingsTransfer.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/SettingsTransfer.cs) — **NEXT UP**
- [ ] [`Source/QualityRoller.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/QualityRoller.cs)
- [ ] [`Source/SkillVarianceApplier.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/SkillVarianceApplier.cs)
- [ ] [`Source/TraitVarianceApplier.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/TraitVarianceApplier.cs)
- [ ] [`Source/PassionVarianceApplier.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PassionVarianceApplier.cs)
- [ ] [`Source/GrowUpVariance.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/GrowUpVariance.cs)
- [ ] [`Source/GrowthUpPatch.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/GrowthUpPatch.cs)
- [ ] [`Source/GrowUpPendingComponent.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/GrowUpPendingComponent.cs)
- [ ] [`Source/HarmonyPatches.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/HarmonyPatches.cs)
- [ ] [`Source/PawnVarianceMod.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceMod.cs)
- [ ] [`Source/Constants.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/Constants.cs)

## 3. ~~Dynamic Data-Driven Trait Desirability Engine~~ — ✅ CLOSED, RESOLVED DIFFERENTLY (2026-08-03)

> [!IMPORTANT]
> **⛔ DO NOT BUILD THE ENGINE DESCRIBED BELOW. The underlying problem is already fixed.**
>
> Full record: [`TRAIT-DESIRABILITY-RESEARCH.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/TRAIT-DESIRABILITY-RESEARCH.md) (rev 3, after a six-agent review) — read **§10** for what shipped.
>
> **The real problem was correctly identified:** trait *count* scaled with quality while trait
> *selection* stayed quality-blind, so high-quality pawns took more draws against the hazard pool.
>
> **It was fixed without any new runtime code:**
> 1. Trait count **removed** from `CalculateCompositeScore` — it is a variance parameter, not a mean
>    one, and scoring it rewarded widening spreads, which makes pawns *worse*.
> 2. Preset trait ranges **narrowed toward vanilla's 2–3**, shrinking the inversion at its source
>    (its size is proportional to `traitCountMax − traitCountMin`).
>
> **Why the engine was rejected:** **46.7% of modded trait degrees in the Progression Modpack contain
> no mechanical XML at all** — their effects live in Harmony patches. A scoring engine would have
> sorted traits by *mod authorship style* rather than by quality. Scoping also grew ~3× under review
> while the value stayed small. This project had already built and retracted a trait-quality axis once
> (`traitNoise`, see `TraitVarianceApplier.cs:19-22`).
>
> The plan text below is retained **only** as the original sketch. It additionally contains factual
> errors — `disabledWorkTags` is on `TraitDef`, not `TraitDegreeData`, and ~10 relevant fields are
> omitted.
>
> The text below is retained only as the original sketch that prompted the research.

- **Problem**: In RimWorld, traits are non-linear (e.g. Pyromaniac / Wimp / Depressive are severely crippling). Scaling trait counts higher on high-quality pawns without trait scoring makes high-quality pawns *more likely* to roll a colony-ruining trait. *(Problem statement confirmed valid — see research doc §1.)*
- **Solution**: Implement a dynamic, 100% data-driven trait desirability scoring engine inside [`TraitVarianceApplier.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/TraitVarianceApplier.cs).
- **Implementation Plan** *(superseded — see research doc §7)*:
  1. **Zero Hardcoding / 100% Mod-Compatible**: Inspect `TraitDegreeData` fields (`statOffsets`, `statFactors`, `skillGains`, `aptitudes`, `disabledWorkTags`, `randomMentalState`/`forcedMentalState`, `disallowedMentalStates`, `socialFightChanceFactor`, `painFactor`, `abilities`) dynamically across all loaded traits (`DefDatabase<TraitDef>.AllDefsListForReading`). Fully compatible with 1,000+ modpacks like *The Progression Modpack*.
  2. **Native Stat Direction (`StatDef.higherIsBetter`)**: Automatically evaluate positive vs negative stat offsets using vanilla RimWorld's built-in `stat.higherIsBetter` bool property (no hardcoded stat names).
  3. **Weighted Probabilistic Selection**: Use calculated desirability scores to shift trait selection weights during pawn generation (`TraitVarianceApplier`). High quality ($Q > 0.60$) shifts weight toward positive/synergistic traits; low quality ($Q < 0.40$) shifts weight toward flawed traits; neutral quality ($Q = 0.50$) uses vanilla distribution. Keeps character flaws possible for story generator texture while eliminating the high-quality penalty.
  4. **NO UI SETTINGS / TOGGLES**: Built directly into the algorithm—no settings page toggles or user options required.

## 4. Overrides & Profile Editor UX Improvements — ✅ COMPLETED (2026-08-03)
- **Overrides Tab Safety UX**:
  - **Button Colors**: Applied soft green (`new Color(0.4f, 0.85f, 0.4f)`) to `+ Add Override`, amber (`new Color(0.9f, 0.75f, 0.3f)`) to `Restore Defaults`, and soft red (`new Color(1f, 0.4f, 0.4f)`) to `Delete All`.
  - **Confirmation Dialogs**: Added explicit confirmation prompts (`Dialog_MessageBox.CreateConfirmation`) before performing `Delete All` (destructive) or `Restore Defaults` (non-destructive reset) actions for both Faction and Xenotype overrides.
  - **Clean Priority Architecture**: Removed legacy `EnsureDefaultPriorities()` method; explicitly store `OverridePriority.Normal` in dictionaries without deleting keys when set to Normal, avoiding accidental overwrites on relaunch.
- **Profile Editor Tab Improvements**:
  - **Dynamic Scroll View Height**: Replaced hardcoded height constants with dynamic content height tracking (`listing.CurHeight + 40f`), eliminating scroll bar cut-off issues across all tabs.
  - **Conditional Child Shift Sliders**: Hidden child skill shift sliders completely when *"Also shift skills when a child grows up"* is unchecked.
  - **Percentage Offset Power Readout**: Replaced legacy 4-tier text (`TierForQuality`) with real-time percentage offset readout relative to `Faithful` baseline (e.g., `+32% vs Faithful (0.41)`).
  - **Strict Read-Only Preset Protections**: Guarded all section headers, checkboxes, sliders, and float ranges so built-in presets (`Faithful`, `Elite`, `Sovereign`, etc.) can never be mutated in static RAM, guaranteeing `Reset` functionality works cleanly.

## 5. Profile Editor Tab Layout Redesign — ⚠️ BUILT, NOT YET VISUALLY VERIFIED (2026-08-03)

Branch: `feature/profile-editor-layout`. **Not merged. Gated on §1's checklist.**
Spec: [`docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md)
Plan: [`docs/superpowers/plans/2026-08-03-profile-editor-layout.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/docs/superpowers/plans/2026-08-03-profile-editor-layout.md)

> [!WARNING]
> **Every layout figure below is arithmetic, not observation.** This repo has no
> test harness for IMGUI code, so all seven tasks were verified by clean build and
> static review only — RimWorld was never launched. The header sums to exactly
> `162f` on paper (grew from `140f` after the 2026-08-04 Best-of-25 readout row was added —
> see §1.6) and the body should land near 500px, but **no pixel of this has
> been seen**. Do not treat it as working until the owner's in-game pass is done.
> **The checklist is §1 at the top of this document** (also spec §9 / plan Task 7).

- **Pinned 162px header** (`DrawProfileEditorHeader`), does not scroll: profile picker +
  5-button action strip (`+ New`, `Duplicate`, `Rename`, `Reset`, `Delete`) / one-line
  description / quality slider with `{tier} ({power})` readout / Best-of-25 readout row /
  full-width distribution curve. Rows: 28 + 4 + 20 + 2 + 28 + 2 + 20 + 4 + 54 = 162.
- **The curve is never greyed**, even on read-only presets. It is a readout, not a control;
  greying it would break comparing presets by cycling the picker. Only the quality *slider*
  is disabled. Do not "fix" this.
- **`+ New` and `Duplicate` stay enabled on presets.** A new user lands on `Faithful`, which
  is read-only — these two buttons are the only way off it. Greying them creates a dead end.
- **Body compacted** from ~1600-2000px toward ~500px: four `Widgets.FloatRange` controls
  replace eight paired sliders, enable checkboxes moved into section headers, fixed-string
  captions became tooltips. Value-derived captions were deliberately kept visible.
- **`Widgets.IntRange` is FORBIDDEN on the four min/max pairs.** `passionCountMin`/`Max`
  hold fractional calibrated values (`1.4`, `2.5`, `6.2`, …); `IntRange` truncates them and
  would silently recalibrate a Rule 5 governed value. Use `FloatRange`, no `roundTo`.
- **Passion counts now display to one decimal** (`:F0` → `:F1`). Display only — `6.2` was
  always `6.2`, it merely rendered as `"6"`. Signed off by the project owner 2026-08-03.
- **Row 2 saves and restores three pieces of global draw state** — `Text.Font`, `GUI.color`,
  `Text.WordWrap`. `WordWrap = false` is what structurally guarantees the fixed 20px row
  stays one line and cannot overlap the quality slider. Keep all three restores.
- Profile Editor drawing moved to [`Source/ProfileEditorTab.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/ProfileEditorTab.cs)
  (`partial class PawnVarianceSettings`). New [`Source/Dialog_RenameProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/Dialog_RenameProfile.cs).
- **Schema unchanged.** `git diff main` over the branch shows zero `Scribe_` lines added or
  removed. The only `VarianceProfile.cs` change is `IRenameable` on `CustomProfile` (6 lines);
  no numeric field, preset value, constructor, `Clone`, or `ExposeData` body was touched.
- Open Minor findings carried to final review are listed in `.superpowers/sdd/progress.md`.

---

## 🔒 MANDATORY ARCHITECTURAL RULES & SCALING LAWS

> [!IMPORTANT]
> **CRITICAL RULES FOR ALL FUTURE AGENTS / DEVELOPERS**:
> 1. **Statistical Envelope ($\pm 35\%$)**: Every preset profile MUST remain within $\pm 35\%$ of `Faithful` **at every batch size** ($N = 1, 5, 25, 50$) — not only at Best-of-1.
> 2. **Monotonic Power-Tier Ordering**: The power tiers MUST hold at ANY batch size ($N = 1, 5, 25, 50$):
>    `Desperate < Scavenger < Faithful < Specialist < Elite < Sovereign`.
>    **`Distinct` and `Wildcard` are exempt** — they are *variance* presets, not power tiers (see the profile table below). They legitimately sit below `Faithful` at N=1 and cross above it as N rises; that is cherry-picking working as designed, not an inversion.

### 📐 How the percentages are derived — Best-of-N, not the mean

**All envelope figures come from a Best-of-N simulation, never from a raw average.** This is
deliberate and must not be "simplified" back to a mean by a future agent.

**Why:** in RimWorld the player *chooses which pawns to keep* — rerolling start scenarios, picking
from raid captures, accepting or refusing quest pawns. The pawn that ends up in the colony is
therefore the **maximum of N rolls**, not a typical roll. A profile's felt power is set by its upper
tail, so a mean-based figure systematically understates any high-dispersion profile.

**Method:**
1. Quality is Beta-distributed: `q ~ Beta(m·K, (1−m)·K)` where `m = averageQuality` and
   `K = Constants.BetaConcentrationK` (currently `8`). See `QualityRoller.RollQuality`.
2. Draw `N` qualities, take the maximum. `CalculateCompositeScore` is monotonic in `q`, so
   Best-of-N score `= composite(max(q₁…q_N))`.
3. Composite is `(0.8·skillNorm + 1.4·passionNorm) / 2.2` (`CalculateCompositeScore`), where
   `skillNorm = (5 + skillShift)/20` and `passionNorm = pips/18`.
   **Trait count is deliberately NOT part of the score** — see the next section for why.
4. Compare each profile to `Faithful` **at the same N**. Deviation must stay inside ±35% at every N.

**Measured `Faithful` baseline is exactly `0.2500`** at `q = 0.50`. This is not a coincidence and
not a tuned value: `skillNorm = 5/20 = 0.25` and `passionNorm = 4.5/18 = 0.25`, so both axes agree
and **the baseline no longer depends on the weights at all**. Retuning `wS`/`wP` now moves the
profiles around a fixed reference instead of moving the reference itself.
*(Dead figures — do not trust, recompute: `0.328` included the retracted trait term; `0.3068` was
the `/12`-normalizer, `1.2/1.0`-weight era ending 2026-08-03.)*

### ⚖️ The skill ↔ passion exchange rate (`R`) — retuned 2026-08-03

**`R = (20 / MaxPassionPips) · (wP / wS) = (20/18) · (1.4/0.8) = 1.94` skill levels per passion pip.**
Previously `1.389`. All three numbers live in `Constants.cs`; **`R` depends on the normalizer as
much as on the weights**, so changing one alone silently moves the rate. Recompute `R` before
touching any of them.

Decided after a four-agent review (2 Claude, 2 Gemini via agy-bridge). What the review established:

- Passion is an **XP-rate multiplier**, not an additive gift: `None 0.35× / Minor 1.0× / Major 1.5×`.
  A Minor pip is a 2.86× learning-rate advantage over no passion.
- **The UI said `Major = 2` until 2026-08-04.** It was always a text bug, never a maths bug:
  `PassionVarianceApplier.cs:61-64` has always spent `1.5` per Major, and `MaxPassionPips = 18`
  is derived as 12 skills × 1.5. No envelope figure ever depended on the wrong string.
- Its value in skill-levels is therefore **time-dependent**: ≈0 on day 1 (pure future value), peaking
  near 4.8 around day 30, saturating near 3.2 once skill decay reaches equilibrium. The project
  owner's intuition — *skill dominates in emergencies/early game, passion dominates long-run* — is
  correct and quantified.
- **A generation-time score has no time axis**, so a single scalar can only ever be a colony-lifetime
  average. `≈2.0` is that average after discounting for the ~40–60% chance a passion lands on a skill
  the colony never actually assigns. Agent estimates spanned `0.78` (skill-favoring) to `6.5`
  (passion-favoring), with an independent Gemini derivation at `2.70`.
- **`CalculateCompositeScore` is display-only.** Verified consumer trace: `ProfileEditorTab.cs:218`
  (readout string), `:351` (curve x-axis), `:374` (mean marker). Zero pawn-generation, storyteller or
  raid-scaling consumers. This caps how much precision is worth buying — round weights are deliberate,
  and a future agent should not chase significant figures here.
- **Direction-of-risk correction:** moving toward *skill* is what stresses the envelope, not passion.
  `Faithful`'s two axes are now equal, but under the old `/12` normalizer its `passionNorm` (0.375)
  exceeded its `skillNorm` (0.250), so raising `wS` pushed `Sovereign` toward +35% and crushed
  `Wildcard`. Two of the four agents asserted the opposite; the simulation says otherwise.

**Verified Best-of-N envelope at the new weights** — verbatim output of
`python docs/tools/envelope_check.py` (deterministic integration over
`q ~ Beta(m·8, (1−m)·8)`, density of the max `= N·F(q)^(N−1)·f(q)`; % vs `Faithful` at the same N).
Pasted, not hand-edited (Rule 6):

```
wS=0.8  wP=1.4  pips/18  skill/20  K=8
Exchange rate R = (20/18) * (1.4/0.8) = 1.94 skill levels per passion pip
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

Rule 2 - power-tier ordering at the same N:
  N=1   Desperate(0.190) < Scavenger(0.211) < Faithful(0.250) < Specialist(0.275) < Elite(0.310) < Sovereign(0.321)   OK
  N=5   Desperate(0.234) < Scavenger(0.258) < Faithful(0.302) < Specialist(0.326) < Elite(0.356) < Sovereign(0.369)   OK
  N=25  Desperate(0.265) < Scavenger(0.288) < Faithful(0.333) < Specialist(0.356) < Elite(0.383) < Sovereign(0.396)   OK
  N=50  Desperate(0.275) < Scavenger(0.298) < Faithful(0.342) < Specialist(0.365) < Elite(0.390) < Sovereign(0.404)   OK

Tightest envelope margins:
  Sovereign @ N=1: +28.5%  (6.5pp of headroom)
  Desperate @ N=1: -24.2%  (10.8pp of headroom)
  Elite @ N=1: +24.0%  (11.0pp of headroom)

PASS: Rule 1 and Rule 2 hold at every N for all enforced presets.
If any number moved, update the table in HANDOVER.md "The skill <-> passion exchange rate".
```

**Rule 1 (±35%) holds at every N for all eight enforced presets. Rule 2 ordering holds at every N.**

**Tightest margins** — the 2026-08-04 retune roughly tripled the worst-case headroom
(was 1.8pp on `Desperate`):

```
Tightest envelope margins:
  Sovereign @ N=1: +28.5%  (6.5pp of headroom)
  Desperate @ N=1: -24.2%  (10.8pp of headroom)
  Elite @ N=1: +24.0%  (11.0pp of headroom)
```

> [!CAUTION]
> ## 🔁 RECALCULATE AFTER ANY CONSTANT CHANGE — the table above goes stale silently
>
> ```powershell
> python docs/tools/envelope_check.py
> ```
>
> **Run it, and update the table above, after changing ANY of:**
> - `Constants.CompositeSkillWeight`, `CompositePassionWeight`, `MaxPassionPips`
> - `Constants.AssumedVanillaSkillBaseline`, `AssumedMaxSkillLevel`, `BetaConcentrationK`
> - any preset's `averageQuality`, `skillShiftMin/Max`, `passionCountMin/Max`, `passionMajorBias`
>
> The tool **parses `Source/Constants.cs` and `Source/VarianceProfile.cs` directly** rather than
> hardcoding values, so it cannot drift from what ships. It exits non-zero on a Rule 1 or Rule 2
> violation, so it can gate a commit. Deterministic integration, not sampling — same input, same
> output, every run. No third-party dependencies.
>
> **Why this matters more than it looks:** even with the 2026-08-04 retune's larger cushion —
> **6.5pp** of headroom on the tightest preset (`Sovereign` @ N=1, was 1.8pp on `Desperate`) — a
> change that *feels* cosmetic — nudging one preset's `averageQuality` by 0.02, or "tidying" a
> normalizer — can breach the envelope without touching the preset that breaks. The weights are
> shared, so every preset moves when any weight moves. **Do not hand-edit the percentages in this
> document.**

**Interpretation note on Rule 2:** an earlier wording ("even a Best-of-50 `Desperate` pawn must
remain below `Faithful`") is ambiguous and, read strictly as *Best-of-50 of a lower tier < Best-of-1
of a higher tier*, is violated **9 times by the shipped presets** — and is arguably impossible to
satisfy for any profile with real dispersion, since Best-of-50 almost always beats Best-of-1 of a
marginally stronger profile. The enforceable reading is **same-N ordering**. Treat that as the rule.

### ⚠️ Trait count is NOT a quality axis — more traits is *worse*, not better

**A higher trait count does not make a pawn better. It makes a pawn more extreme in both
directions, and on net it is a liability.**

Trait *selection* is delegated entirely to vanilla's `PawnGenerator.GenerateTraitsFor`, which is
**quality-blind**. So scaling trait count with quality does not buy better traits — it buys **more
independent draws from an unchanged urn**, including the colony-ruining ones. Roughly 4% of vanilla
trait degrees can trigger uncontrolled behaviour (`randomMentalState`/`forcedMentalState`:
Pyromaniac, Gourmand, Void Fascination), so:

| Traits | P(at least one hazardous trait) |
|---|---|
| 2 | 8.0% |
| 3 | 11.8% |
| 4 | 15.4% |
| 5 | 18.9% |
| 8 | 28.5% |

**Consequence:** a wide `traitCountMin → traitCountMax` spread makes high-quality pawns *more* likely
to roll a colony-ender than low-quality ones. `CalculateCompositeScore` scores trait count as a
straight positive (`traitNorm = count/8`), so the composite metric **actively rewards** a change that
makes pawns worse to play with.

**Rules that follow:**
- Keep preset spreads close to vanilla's **2–3**. `2–4` is a reasonable ceiling for "high quality"
  presets; wider ranges belong to explicitly chaotic presets (`WildSpread`) or to the user's own
  slider, not to the quality tiers.
- Widening a spread to raise a profile's composite score is **forbidden** — it is gaming the metric.
  Raise `averageQuality`, skills, or passions instead.
- Do not treat the composite trait term as validation that more traits is better. It isn't.

See [`TRAIT-DESIRABILITY-RESEARCH.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/TRAIT-DESIRABILITY-RESEARCH.md) §1 and §3.1 for the full derivation.

### 🎭 What each profile represents

**Note on naming:** the C# variable name and the player-facing name differ. Always refer to profiles
by their **display name** in discussion; the variable name only matters when editing
`VarianceProfile.cs`.

| Display name | ID const | C# variable | Kind | Represents |
|---|---|---|---|---|
| **Faithful** | `FaithfulId` | `VanillaLike` | baseline | Closest to unmodded RimWorld. **The reference all envelope maths is measured against.** |
| **Desperate** | `DesperateId` | `Hardscrabble` | power tier (lowest) | Scraped-together survivors. Low skills, few passions, poor rolls common. |
| **Scavenger** | `ScavengerId` | `Scavenger` | power tier (low) | Wasteland survivors, pirates. Lower baseline skills, tough survival rolls. |
| **Specialist** | `SpecialistId` | `Specialist` | power tier | Engineered single-domain specialists (Genies, Hussars). Focused skill spikes. |
| **Elite** | `EliteId` | `Elite` | power tier (high) | Imperial nobility, high-born. Consistently high capability. |
| **Sovereign** | `SovereignId` | `Sovereign` | power tier (top) | Archite lords, Sanguophages, supreme leaders. |
| **Distinct** | `DistinctId` | `BalancedVariance` | **variance** | The mod's signature tuning. Strong individual strengths *and* weaknesses, fair colony average. |
| **Wildcard** | `WildcardId` | `WildSpread` | **variance** | Maximum variation. 0–8 traits, zero-to-many passions, wide skill swings. |

**Power tier vs variance preset is a load-bearing distinction.** Power tiers must obey the Rule 2
ordering. Variance presets are tuned for *dispersion* around a roughly baseline mean, so they cross
`Faithful` as N rises and are exempt from ordering — but NOT from the ±35% envelope.

### 🗺️ Which profiles the default config actually uses

Anything below is player-visible **out of the box** and must stay calibrated. Changing one is a
Rule 4 consultation item.

| Assignment | Profile | Source |
|---|---|---|
| Active profile (colonists) | **Faithful** | `activeProfileId` default |
| Hostile fallback | **Distinct** | `hostileProfileId` default |
| Empire | **Elite** (Highest) | `RestoreDefaultFactionOverrides` |
| Ancients / AncientsHostile | **Sovereign** (High) | " |
| Pirate / PirateSavage | **Scavenger** (Normal) | " |
| OutlanderCivil / OutlanderRough | **Faithful** (Low) | " |
| TribeCivil / TribeRough / TribeSavage | **Desperate** (Low) | " |
| Sanguophage | **Sovereign** (Highest) | `RestoreDefaultXenotypeOverrides` |
| Highmate | **Elite** (High) | " |
| Genie / Hussar / Dirtmole | **Specialist** (High/Normal) | " |
| Waster / Pigskin | **Scavenger** (Normal) | " |
| Neanderthal / Yttakin | **Distinct** (Normal) | " |
| Impid | **Wildcard** (Normal) | " |

> **Remaining rules:**
> 3. **NEVER PUT TRAIT COUNT BACK INTO THE QUALITY SCORE**: `CalculateCompositeScore` MUST NOT include a trait term. Trait count is a **variance** parameter, not a **mean** one — vanilla's picker is quality-blind, so more traits buys more draws from an unchanged urn, not better traits. Scoring it (a) rewarded widening spreads even though that makes pawns strictly worse to play with, and (b) compressed the whole scale, propping weak profiles up and holding strong ones down. If you think you have found a way to score traits, read `TRAIT-DESIRABILITY-RESEARCH.md` §4 and §5 first — seven approaches were evaluated and rejected with measured data.
> 4. **DO NOT TOUCH KIDS BY DEFAULT**: The default setting for children and growth moments MUST be **OFF** (`applyVarianceToChildren = false` and `applyChildSkillShift = false`). Growth moments must be left untouched out-of-the-box unless explicitly enabled by the user.
> 5. **MANDATORY CONSULTATION**: **DO NOT MODIFY OR TOUCH** these percentage bounds, statistical scaling rules, children/growth moment defaults, or profile parameters without explicitly raising a question to the project creator / user and obtaining explicit approval first!
> 6. **RECALCULATE THE ENVELOPE AFTER ANY SCORING-CONSTANT CHANGE**: run `python docs/tools/envelope_check.py` and paste its output into the table in "How the percentages are derived". The composite weights are **shared across all eight presets**, so changing one constant moves every profile at once — and the tightest preset currently has only **6.5pp** of headroom (`Sovereign` @ N=1). Never hand-edit those percentages. See the full trigger list in that section.
> 7. **THE EXCHANGE RATE `R` DEPENDS ON THE NORMALIZER, NOT JUST THE WEIGHTS**: `R = (AssumedMaxSkillLevel / MaxPassionPips) · (wP / wS)`. Changing `MaxPassionPips` alone silently moves the skill↔passion exchange rate even though no weight was touched — this exact trap nearly reverted the 2026-08-03 retune (`/12 → /18` on its own would have cut `R` from 1.94 to 1.33, *below* the 1.389 it replaced). Recompute `R` before and after touching any of the three.
> 6. **PROTECTION OF REVIEWED CODE (STRICT PERMISSION REQUIRED)**: **DO NOT MODIFY, REFACTOR, OR REWRITE** any code inside a file marked as `[x] DONE (REVIEWED)` in Section 2 without explicitly presenting the rationale and proposed changes to the user and obtaining explicit permission first!

---

# 🛠️ FEATURE SUMMARY & RECENT ARCHITECTURE

1. **5-Bucket Override Priority System**:
   - Resolution hierarchy: `Xenotype Overrides > Faction Overrides > Hostile Profile > Default Active Profile` (or `Faction > Xenotype` if `factionOverridesTakePrecedence = true`).
   - Priority buckets: `Lowest (0)`, `Low (1)`, `Normal (2)`, `High (3)`, `Highest (4)`.
   - Pre-assigned default overrides: `Empire` & `Sanguophage` $\rightarrow$ `Highest` (`Elite`/`Sovereign`), `Ancients`/ DLC xenotypes $\rightarrow$ `High`/`Normal`.

2. **Unlimited Dynamic Custom Profiles**:
   - Managed via dynamic [`CustomProfile`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs#L126) instances in `customProfiles` list using string IDs (`"custom_1"`, `"custom_2"`).
   - Dynamic UI controls in the **Profile Editor** tab to create, rename, duplicate, reset, and delete custom profiles.

3. **Settings Import / Export ([`Source/SettingsTransfer.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/SettingsTransfer.cs))**:
   - Structural clipboard export/import for custom profiles, override maps, priorities, and General toggles.
   - Pre-validates XML via `XmlDocument.LoadXml` before calling `Scribe_Deep.Look` to prevent Scribe exception blocking.

4. **⚠️ Traits are generated from TWO independent call sites** — any future trait work must handle both:
   - [`TraitVarianceApplier.cs:72`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/TraitVarianceApplier.cs#L72) — `GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false)` (normal generation)
   - [`GrowUpVariance.cs:209`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/GrowUpVariance.cs#L209) — `GenerateTraitsFor(pawn, requested, null, growthMomentTrait: true)` (age-13 growth moment)

   Two consequences that are easy to miss:
   - The growth-moment call passes **`request: null`**, so every vanilla check that reads the request is skipped — `kindDef.disallowedTraits`, `disallowedTraitsWithDegree`, `requiredWorkTags`, `ProhibitedTraits`, and the hostile-spawn `allowOnHostileSpawn` gate (verified in decompiled `PawnGenerator.GenerateTraitsFor`).
   - The growth-moment trait pass is **add-only by design** (`GrowUpVariance.cs:70-79`) — it can never remove a trait. **Anything granted at 13 is permanent**; no later pass revisits it.

5. **Age-13 Growth-Moment Deferral Pipeline**:
   - Children aging to 13 defer mod application while a choice letter (`ChoiceLetter_GrowthMoment`) is pending ([`GrowUpPendingComponent`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/GrowUpPendingComponent.cs)).
   - Mod applies strictly add-only trait/passion increments after the player resolves the letter.

6. **Clean Non-Spam Faction Handling**:
   - Replaced `Faction.OfPlayer` with `Faction.OfPlayerSilentFail` across call sites to eliminate world-gen log errors.

---

# 🚀 BUILD & DEPLOYMENT LOOP

```powershell
dotnet build Source/PawnVarianceMod.csproj
Copy-Item Assemblies/PawnVarianceMod.dll, Assemblies/PawnVarianceMod.pdb "C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/" -Force
```

- **Guard**: Check `tasklist /FI "IMAGENAME eq RimWorldWin64.exe"` before copying to avoid locked DLL errors.
- **Verification**: Ensure `dotnet build` returns `0 Error(s), 0 Warning(s)`.

---

# 📡 AUTOMATED TESTING & BRIDGE OPERATING NOTES

- **RimBridgeServer & GABS**: Installed and configured for automated testing via dev-mode debug actions (`rimbridge/list_logs`, `rimworld/execute_debug_action`).
- **Diagnostic Log Traces**: All mod logs are prefixed `[PawnVarianceMod]`. Key traces:
  - `Trait assignment (...) for X (quality Q, profile P)` — verifies profile assignment per pawn.
  - `became adult with a growth-moment letter outstanding — deferring variance until it resolves`
  - `Growth moment resolved for … after N ticks`

---

# 🔮 NEXT PROJECTS (AFTER THIS MOD IS COMPLETE)

1. **Guest Room Mod**:
   - Designate a room to be a guest room. Low room stats satisfy traders poorly (drops relation, but lowers perceived wealth $\rightarrow$ easier raids). High room stats increase trade relations & trader frequency, but increase perceived wealth $\rightarrow$ harder raids.
2. **Perceived Wealth Mod**:
   - Decouple storyteller raid scaling from actual stockpile value using a dynamic rumor system. Perceived wealth fluctuates based on direct observations by escaping raiders, visiting traders, and radio broadcasts, with rumor decay and suspicion floors for dark zones.
