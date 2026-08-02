# Per-Faction & Xenotype Profile Overrides Design Spec

Date: 2026-08-01  
Mod: Varied Pawns (`PawnVarianceMod`)  
Status: Approved  

---

## 1. Overview

This feature adds a tabbed settings interface to Varied Pawns and introduces **Per-Faction and Per-Xenotype Profile Overrides**. Players can assign specific variance profiles (e.g. *Wildcard*, *Gifted*, *Desperate*, or any *Custom Profile*) to specific RimWorld factions or Biotech xenotypes.

---

## 2. User Interface Design

### 2.1 Tabbed Navigation Bar
The settings window (`DoWindowContents` in [`PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs)) will render top-level tabs using RimWorld's native `TabRecord` and `TabDrawer.DrawTabs`:

1. **Tab 1: `General & Profiles`**
   - Active Profile selector
   - Profile tuning sliders (Skills, Traits, Passions, Quality)
   - Compound Quality Distribution curve graph
   - Hostile-faction profile assignment
   - Child skill shift options
   - Verbose logging toggle
2. **Tab 2: `Faction & Xenotype Overrides`**
   - Main feature toggle: `[x] Enable Faction & Xenotype Overrides`
   - Section 1: **Faction Overrides**
   - Section 2: **Xenotype Overrides** *(Biotech-gated)*

---

### 2.2 Overrides Tab UI & Layout

#### Enable Checkbox
- Situated at the top of Tab 2.
- Setting: `enableOverrides` (`bool`, default `false`).
- When disabled, override list controls are greyed out (`GUI.enabled = false`) with an explanatory caption: *"Enable this setting to assign custom profiles to specific factions or xenotypes."*

#### Override Row Structure
Each configured override rule is displayed as a row containing:
- **Def Label**: Faction label or Xenotype label (with icon if available).
- **Profile Selector Button**: Opens a `FloatMenu` listing all preset and custom profiles.
- **Delete Button (`[X]`)**: Removes the override rule.

#### Adding New Rules
- **`+ Add Faction Override`**: Opens a `FloatMenu` listing all loaded `FactionDef`s in the game (excluding those already added).
- **`+ Add Xenotype Override`**: Opens a `FloatMenu` listing all loaded `XenotypeDef`s in the game (excluding those already added). Only drawn if Biotech is active (`ModsConfig.BiotechActive`).

---

## 3. Resolution Priority in `ValuesFor(Pawn pawn)`

When generating or resolving a pawn's profile, [`PawnVarianceSettings.ValuesFor(pawn)`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs#L68-L74) evaluates rules in a strict hierarchy:

$$\text{Xenotype Override} \longrightarrow \text{Faction Override} \longrightarrow \text{Hostile Profile} \longrightarrow \text{Default Active Profile}$$

```csharp
public VarianceProfileValues ValuesFor(Pawn pawn)
{
    if (pawn == null) return Active;

    if (enableOverrides)
    {
        // 1. Xenotype Override (Biotech)
        if (ModsConfig.BiotechActive && pawn.genes?.Xenotype != null)
        {
            if (xenotypeOverrides.TryGetValue(pawn.genes.Xenotype.defName, out var xenoProfileId))
                return Resolve(xenoProfileId);
        }

        // 2. Faction Override
        if (pawn.Faction?.def != null)
        {
            if (factionOverrides.TryGetValue(pawn.Faction.def.defName, out var factionProfileId))
                return Resolve(factionProfileId);
        }
    }

    // 3. Hostile Fallback
    if (applyToHostilePawns && pawn.Faction != null && Faction.OfPlayerSilentFail != null 
        && pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
    {
        return Hostile;
    }

    // 4. Default Active Profile
    return Active;
}
```

---

## 4. Serialization & Data Persistence

In `ExposeData()`:
- `enableOverrides`: `Scribe_Values.Look(ref enableOverrides, "enableOverrides", false)`
- Overrides stored internally as:
  - `Dictionary<string, VarianceProfileId> factionOverrides`
  - `Dictionary<string, VarianceProfileId> xenotypeOverrides`
- Serialized as parallel key/value lists using `Scribe_Collections.Look`:
  - `factionOverrideKeys` (`List<string>`) & `factionOverrideValues` (`List<VarianceProfileId>`)
  - `xenotypeOverrideKeys` (`List<string>`) & `xenotypeOverrideValues` (`List<VarianceProfileId>`)
- Post-load cleanup: If a saved `defName` is no longer present in the game (e.g. uninstalled mod), the lookup gracefully skips it during `ValuesFor(pawn)`.

---

## 5. Testing & Verification Plan

1. **Unit & Settings Serialization Verification**:
   - Save & reload settings; verify `enableOverrides`, `factionOverrides`, and `xenotypeOverrides` persist correctly across game restarts.
2. **In-Game Spawn Traces via RimBridge / GABS**:
   - Set Pirate Faction $\rightarrow$ `Wildcard`. Spawn Pirate: verify log reads `profile Wildcard`.
   - Set Waster Xenotype $\rightarrow$ `Desperate`. Spawn Waster Pirate: verify log reads `profile Desperate` (Xenotype priority > Faction priority).
   - Toggle `enableOverrides` = `false`: verify Pirates revert to `Hostile` / `Active` profile.
