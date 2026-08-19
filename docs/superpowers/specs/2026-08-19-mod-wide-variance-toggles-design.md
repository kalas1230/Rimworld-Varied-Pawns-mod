# Mod-Wide Variance Toggles Design Spec

Date: 2026-08-19  
Mod: Varied Pawns (`PawnVarianceMod`)  
Status: Approved  

---

## 1. Overview

Adds three mod-wide master switches — skill, trait and passion variance — that sit above the
existing per-profile section toggles. Semantics are a strict AND, authoritative in one direction
only: **mod-wide off is a hard stop; mod-wide on defers entirely to the profile.** A per-profile
toggle can never re-enable a dimension its master has switched off.

Two problems motivate this, one of them a live bug.

**The settings model has a hole.** The per-profile enable checkbox is greyed out on all eight
presets, because the whole Profile Editor body is drawn inside
`GUI.enabled = wasEnabled && EditingCustom` (`ProfileEditorTab.cs:101`), and no preset sets the
flag — every one inherits `= true` from the field initialiser (`VarianceProfile.cs:88-90`). A
player who wants Varied Pawns to leave one dimension alone must therefore abandon presets
entirely and rebuild every faction / race / xenotype assignment as a custom profile. A mod-wide
switch collapses that to one click and applies regardless of which profile a pawn resolves to.

The concrete case that surfaced this: **More Trait Slots** (`Arkymn.MoreTraitSlots`) postfixes
`PawnGenerator.GenerateTraits`, which is nested inside `GenerateNewPawnInternal`
(`PawnGenerator.cs:819` / `:700` in the decompile), so Varied Pawns' postfix always runs last and
reconciles the trait count back to its own target. The two mods coexist without error, but More
Trait Slots' sliders have no visible effect. Turning trait variance off mod-wide hands that axis
back to it cleanly.

**Section greying is missing entirely.** Every `GUI.enabled` write in `ProfileEditorTab.cs`
(lines 101, 136, 164, 248) keys on `EditingCustom` or `customProfile != null`. None consults
`enableSkillVariance` / `enableTraitVariance` / `enablePassionVariance`, and `SectionHeader`
(`:51-73`) draws its checkbox and returns without touching `GUI.enabled` at all. Today you can
untick Traits on a custom profile and still drag the trait-count slider, which reads as though it
does something. Fixed here as part of the same change, since both flags feed the same greying
rule.

---

## 2. Data Model

Three new fields on `PawnVarianceSettings`, adjacent to `applyToHostilePawns` (`:46-48`):

```csharp
public bool enableSkillVarianceModWide = true;
public bool enableTraitVarianceModWide = true;
public bool enablePassionVarianceModWide = true;
```

Defaults are `true`, so existing configs load unchanged and the feature is invisible until used.

`ExposeData` (near `:431`):

```csharp
Scribe_Values.Look(ref enableSkillVarianceModWide, "enableSkillVarianceModWide", true);
Scribe_Values.Look(ref enableTraitVarianceModWide, "enableTraitVarianceModWide", true);
Scribe_Values.Look(ref enablePassionVarianceModWide, "enablePassionVarianceModWide", true);
```

`CopyFrom` (`:593`) carries all three. A settings payload exported with trait variance disabled
must import that way; omitting them would silently re-enable a dimension on import.

---

## 3. Reading the Effective Value

### 3.1 Three accessors on `PawnVarianceSettings`

```csharp
public bool SkillVarianceActive(VarianceProfileValues v)   => enableSkillVarianceModWide   && v != null && v.enableSkillVariance;
public bool TraitVarianceActive(VarianceProfileValues v)   => enableTraitVarianceModWide   && v != null && v.enableTraitVariance;
public bool PassionVarianceActive(VarianceProfileValues v) => enablePassionVarianceModWide && v != null && v.enablePassionVariance;
```

Each of `VarianceProfileValues.enableSkillVariance` / `enableTraitVariance` /
`enablePassionVariance` gains a comment naming its accessor as the only correct read, matching how
this file already guards its traps.

### 3.2 Why not AND into the resolved values

Rejected deliberately. `ValuesFor` does not hand back a throwaway object. `Resolve` clones presets
(`preset.MakeValues()`, `PawnVarianceSettings.cs:246`) but returns **custom** profiles as a live
reference (`:254`, with an explicit comment forbidding the "fix"), and `Active` / `Hostile` are
session-cached clones (`:550-553`). Writing an ANDed flag into the resolved values would therefore
persist a mod-wide toggle into the player's saved custom profile on disk, and would corrupt the
cached `Active` / `Hostile` values for the rest of the session. The accessors never write.

Also rejected: cloning in `ValuesFor` so call sites need no change. It allocates per pawn
generated — material during world generation — and it makes the effective value indistinguishable
from the profile's stored value, destroying the ability to report *why* a dimension is off. That
distinction is exactly the support question a master switch generates.

### 3.3 Call sites

Eight gates across three files.

| File | Line | Change |
|---|---|---|
| `HarmonyPatches.cs` | 51 | all-three early return → accessors |
| `HarmonyPatches.cs` | 58, 59, 60 | per-dimension gates → accessors |
| `GrowthUpPatch.cs` | 140 | all-three early return → accessors |
| `GrowUpVariance.cs` | 139, 140, 141 | per-dimension gates → accessors |

The two all-three early returns are not optional. Left reading the raw flags, a pawn with every
dimension disabled mod-wide still pays for `QualityRoller.RollQuality` and the full profile
resolution walk on every generation.

---

## 4. UI

The same three bools are drawn in both tabs, each reflecting the other.

### 4.1 General tab

A "Variance types" block in `DrawGlobalSettings` (`:1320`), after `applyVarianceToChildren`.
Three checkboxes with neutral labels, sharing one tooltip whose job is the precedence story: these
apply to every pawn regardless of which profile or override matches, and turning one off here
overrides the per-profile setting, which cannot turn it back on.

### 4.2 Profile Editor

`SectionHeader` (`:51-73`) gains a second checkbox — `Mod-wide` — bound to the settings field, and
becomes the single place the greying rule is applied.

**The greying rule.** On the way out of each header:

```csharp
GUI.enabled = outer && EditingCustom && modWide && perProfile;
```

The baseline at `:101` stays as `outer`; each header narrows from it. This is what fixes the
missing per-profile greying described in §1.

**Both checkboxes sit outside their own grey.** `SectionHeader` draws the mod-wide box and the
per-profile box at full `GUI.enabled` before narrowing. Two independent reasons:

- The mod-wide box must remain clickable while the body is greyed for a preset, or it is dead on
  exactly the eight profiles this feature exists to rescue.
- If the per-profile box were inside the grey it controls, unticking it would lock the player out
  of re-ticking it.

The per-profile box remains clickable when its dimension is off mod-wide, on a custom profile. Its
stored value still matters the moment the master is switched back on, so the player must be able
to pre-arrange it; the hint caption is what communicates that it is currently ignored.

**Hint caption.** Drawn under the header, following the `VP_EnableOverridesHint` pattern at
`:785`. Three reasons a section can be greyed, reported in precedence order:

| Condition | Caption |
|---|---|
| Viewing a preset | none — existing whole-body grey, unchanged |
| Mod-wide off | "Turned off mod-wide. This profile's setting is ignored." |
| Profile off | "Turned off for this profile." |

---

## 5. Localisation

Nine new keys in `Languages/English/Keyed/VariedPawns.xml`:

`VP_Section_VarianceTypes`, `VP_VarianceTypesCaption`, `VP_ModWideSkill`, `VP_ModWideTrait`,
`VP_ModWidePassion`, `VP_ModWide` (short label for the Profile Editor header box),
`VP_ModWideTip`, `VP_ModWideOffHint`, `VP_ProfileOffHint`.

All are consumed inside per-frame UI methods, so none is at risk of the translated-value snapshot
trap that bit `RefreshResolvedLabels` (`PawnVarianceSettings.cs:556`).
`tools/check-translation-keys.ps1` must pass.

---

## 6. Verification

**Automated.** Clean release build; `tools/check-translation-keys.ps1` green.

**Manual, in game.** The greying cannot be covered by the usual layout gate — the comment at
`ProfileEditorTab.cs:245-246` records that GABS's `get_ui_layout` cannot observe ambient
`GUI.enabled`, so no probe in this file can catch a greying regression. Screenshot or manual pass
required.

Behaviour matrix, checked against a generated pawn:

| Mod-wide | Per-profile | Expected |
|---|---|---|
| off | on | dimension untouched |
| on | off | dimension untouched |
| off | off | dimension untouched |
| on | on | variance applies as today |

Plus the two cases that distinguish this from the status quo:

- **A preset with trait variance off mod-wide** — impossible before this change, and the whole
  point of it.
- **With More Trait Slots active and trait variance off mod-wide** — pawn trait counts should
  follow More Trait Slots' sliders, confirming Varied Pawns has genuinely stood down.

Settings round-trip: export a config with a dimension disabled, import it, confirm the flag
survives.
