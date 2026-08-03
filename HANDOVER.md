# Handover — Varied Pawns Mod

Date: 2026-08-02
Repo: `C:\Users\gokal\Desktop\Rimworld-mod\Rimworld-Pawn-variance-mod`
Branch: **`main`**

---

# ⚠️ CURRENT PRIORITIES & IN-PROGRESS TASKS

## 1. User File-by-File Code Review (IN PROGRESS)
- [x] [`Source/VarianceProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs) — **DONE (REVIEWED)** (Legacy enum/comment cleanup, `IExposable` parameterless `ExposeData()`, `distributionParamsDirty` cache, `MakeValues()`, `?`/`??` operators).
- [ ] [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs) — **NEXT UP**
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

## 2. ~~Dynamic Data-Driven Trait Desirability Engine~~ — ✅ CLOSED, RESOLVED DIFFERENTLY (2026-08-03)

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

## 3. Overrides Tab Safety UX Improvement — ✅ COMPLETED (2026-08-03)
- **Button Colors**: Applied soft green (`new Color(0.4f, 0.85f, 0.4f)`) to `+ Add Override`, amber (`new Color(0.9f, 0.75f, 0.3f)`) to `Restore Defaults`, and soft red (`new Color(1f, 0.4f, 0.4f)`) to `Delete All`.
- **Confirmation Dialogs**: Added explicit confirmation prompts (`Dialog_MessageBox.CreateConfirmation`) before performing `Delete All` (destructive) or `Restore Defaults` (non-destructive reset) actions for both Faction and Xenotype overrides.

## 4. Profile Editor Tab Layout Redesign — ⚠️ BUILT, NOT YET VISUALLY VERIFIED (2026-08-03)

Branch: `feature/profile-editor-layout`. **Not merged.**
Spec: [`docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/docs/superpowers/specs/2026-08-03-profile-editor-layout-design.md)
Plan: [`docs/superpowers/plans/2026-08-03-profile-editor-layout.md`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/docs/superpowers/plans/2026-08-03-profile-editor-layout.md)

> [!WARNING]
> **Every layout figure below is arithmetic, not observation.** This repo has no
> test harness for IMGUI code, so all seven tasks were verified by clean build and
> static review only — RimWorld was never launched. The header sums to exactly
> `140f` on paper and the body should land near 500px, but **no pixel of this has
> been seen**. Do not treat it as working until the owner's in-game pass is done.
> The pass checklist is §9 of the plan.

- **Pinned 140px header** (`DrawProfileEditorHeader`), does not scroll: profile picker +
  5-button action strip (`+ New`, `Duplicate`, `Rename`, `Reset`, `Delete`) / one-line
  description / quality slider with `{tier} ({power})` readout / full-width distribution
  curve. Rows: 28 + 4 + 20 + 2 + 28 + 4 + 54 = 140.
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
3. Composite is `(1.2·skillNorm + 1.0·passionNorm) / 2.2` (`PawnVarianceSettings.cs:1085-1112`).
   **Trait count is deliberately NOT part of the score** — see the next section for why.
4. Compare each profile to `Faithful` **at the same N**. Deviation must stay inside ±35% at every N.

**Measured `Faithful` baseline is `0.3068`** (traits-free) at `q = 0.50` with
`AssumedVanillaSkillBaseline = 5`. *(Older revisions stated `0.328`, computed with the trait term
included; that figure is dead — recompute rather than trusting it.)*

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
| **Gifted** | `GiftedId` | `GiftedColony` | power tier | Higher capability across the board. **Not reachable in the default config** — manual selection only. |

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

**`Gifted` is the only preset not reachable by default.** It currently sits at roughly **+139%** vs
`Faithful` — far outside the envelope — because its passion budget reaches 12.3 pips against a `/12`
normalizer, so `passionNorm` **pins at 1.0**. It was left unpatched during the 2026-08-03 retune
precisely because it is not in the default config. **Fix it before ever making it a default.**

> **Remaining rules:**
> 3. **NEVER PUT TRAIT COUNT BACK INTO THE QUALITY SCORE**: `CalculateCompositeScore` MUST NOT include a trait term. Trait count is a **variance** parameter, not a **mean** one — vanilla's picker is quality-blind, so more traits buys more draws from an unchanged urn, not better traits. Scoring it (a) rewarded widening spreads even though that makes pawns strictly worse to play with, and (b) compressed the whole scale, propping weak profiles up and holding strong ones down. If you think you have found a way to score traits, read `TRAIT-DESIRABILITY-RESEARCH.md` §4 and §5 first — seven approaches were evaluated and rejected with measured data.
> 4. **DO NOT TOUCH KIDS BY DEFAULT**: The default setting for children and growth moments MUST be **OFF** (`applyVarianceToChildren = false` and `applyChildSkillShift = false`). Growth moments must be left untouched out-of-the-box unless explicitly enabled by the user.
> 5. **MANDATORY CONSULTATION**: **DO NOT MODIFY OR TOUCH** these percentage bounds, statistical scaling rules, children/growth moment defaults, or profile parameters without explicitly raising a question to the project creator / user and obtaining explicit approval first!

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
