# Design Spec: 5-Bucket Override Priority System

**Date**: 2026-08-02  
**Status**: Draft (Awaiting Final User Review)  
**Target Project**: Varied Pawns (RimWorld Pawn Variance Mod)  

---

## 1. Overview & Objectives

Implement a 5-bucket priority ranking system (`Lowest`, `Low`, `Normal`, `High`, `Highest`) for Faction and Xenotype profile overrides. This allows fine-grained override resolution for mixed-xenotype factions (e.g., Sanguophages overriding Empire defaults) without forcing users into master list reordering or complex rank typing.

---

## 2. Architecture & Data Model

### `OverridePriority` Enum (`Source/PawnVarianceSettings.cs`)
```csharp
public enum OverridePriority
{
    Lowest = 0,
    Low = 1,
    Normal = 2,  // Default for all new & migrated overrides
    High = 3,
    Highest = 4,
}
```

### Storage in `PawnVarianceSettings`:
- `public Dictionary<string, OverridePriority> factionPriorities = new Dictionary<string, OverridePriority>();`
- `public Dictionary<string, OverridePriority> xenotypePriorities = new Dictionary<string, OverridePriority>();`
- Serialized in `ExposeData()` via parallel `LookMode.Value` list pairs (`factionPriorityKeys`, `factionPriorityValues`, `xenotypePriorityKeys`, `xenotypePriorityValues`).

---

## 3. Override Resolution Algorithm (`ValuesFor(Pawn pawn)`)

When evaluating a pawn:

1. **Faction Check**:
   If `pawn.Faction?.def != null` and `factionOverrides.TryGetValue(factionDef, out var factionProfileId)`:
   - Priority = `factionPriorities.TryGetValue(factionDef, out var p) ? p : OverridePriority.Normal`.
2. **Xenotype Check**:
   If `ModsConfig.BiotechActive`, `GetXenotypeDefName(pawn)` returns a name, and `xenotypeOverrides.TryGetValue(xenoDef, out var xenoProfileId)`:
   - Priority = `xenotypePriorities.TryGetValue(xenoDef, out var p) ? p : OverridePriority.Normal`.

3. **Comparison & Conflict Resolution**:
   - If only Faction matches $\rightarrow$ return `Resolve(factionProfileId)`.
   - If only Xenotype matches $\rightarrow$ return `Resolve(xenoProfileId)`.
   - If **both** match:
     - **If `factionPriority > xenoPriority`** $\rightarrow$ Faction Override wins.
     - **If `xenoPriority > factionPriority`** $\rightarrow$ Xenotype Override wins.
     - **If `factionPriority == xenoPriority`** $\rightarrow$ Tie broken by `factionOverridesTakePrecedence` toggle (`Faction > Xenotype` or `Xenotype > Faction`).

4. **Fallback**:
   If zero overrides match $\rightarrow$ evaluate `applyToHostilePawns` (`Hostile` profile) $\rightarrow$ default `Active` profile.

---

## 4. UI Integration in Overrides Tab

1. **Explanatory Banner**:
   Render a clear UI message explaining priority buckets and tie-breaking rules.
2. **Priority Dropdown Selector**:
   Beside each override row, render a dropdown button allowing the user to select `Lowest`, `Low`, `Normal`, `High`, or `Highest`.
