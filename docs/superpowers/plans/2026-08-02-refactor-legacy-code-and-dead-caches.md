# Refactor Legacy Code and Dead Caches Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Clean up unreleased legacy backwards-compatibility comments, dead custom slot enum values (`Custom2`, `Custom3`), and the unused `TraitDesirabilityCache.cs` dead code.

**Architecture:** Simplify `VarianceProfile.cs` by removing legacy 3-slot custom enum hacks, inline `FirstValidDegree` in `TraitVarianceApplier.cs`, delete `TraitDesirabilityCache.cs`, and clean up unused constants in `Constants.cs`.

**Tech Stack:** C# (.NET / RimWorld Modding), MSBuild (`dotnet build`).

## Global Constraints

- **MSBuild Cleanliness**: Project must compile with 0 Errors and 0 Warnings (`dotnet build Source/PawnVarianceMod.csproj`).
- **No Unused Code**: Delete completely unused files (`TraitDesirabilityCache.cs`) and dead helper methods (`CustomSlotIndex`).

---

### Task 1: Delete Dead TraitDesirabilityCache and Update TraitVarianceApplier & Constants

**Files:**
- Modify: `Source/TraitVarianceApplier.cs:204-208`
- Modify: `Source/Constants.cs:23-29`
- Delete: `Source/TraitDesirabilityCache.cs`

- [ ] **Step 1: Simplify FirstValidDegree in TraitVarianceApplier.cs**

Update [`FirstValidDegree`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/TraitVarianceApplier.cs#L204-L208) to check `def.degreeDatas` directly without referencing `TraitDesirabilityCache`:

```csharp
private static int FirstValidDegree(TraitDef def)
{
    return (def.degreeDatas != null && def.degreeDatas.Count > 0) ? def.degreeDatas[0].degree : 0;
}
```

- [ ] **Step 2: Remove unused constants in Constants.cs**

Remove unused trait desirability constants in [`Constants.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/Constants.cs#L23-L29):
- `SkillOffsetReferenceMagnitude`
- `StatReferenceMagnitude`
- `WorkTagDisablePenalty`
- `SocialReferenceMagnitude`
- `ZMultiplier`

- [ ] **Step 3: Delete TraitDesirabilityCache.cs**

Delete file `Source/TraitDesirabilityCache.cs`.

- [ ] **Step 4: Verify build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: Build succeeds with 0 Errors.

---

### Task 2: Clean Up Legacy Custom Profiles & Obsolete Comments in VarianceProfile.cs

**Files:**
- Modify: `Source/VarianceProfile.cs:7-29`, `110-138`, `410-435`

- [ ] **Step 1: Clean up VarianceProfileId enum & header comments**

Remove `Custom2` and `Custom3` dead enum values and obsolete backwards-compatibility comments in [`VarianceProfileId`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs#L15).

- [ ] **Step 2: Clean up ExposeData comments**

Remove obsolete comments regarding `custom2_`/`custom3_` prefix hacks in [`VarianceProfileValues.ExposeData`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs#L117).

- [ ] **Step 3: Remove dead custom slot helper code**

Remove unused `CustomSlots` array, `DefaultCustomNames`, and `CustomSlotIndex()` method in [`VarianceProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs#L416-L434).

- [ ] **Step 4: Build and Deploy**

Run: `dotnet build Source/PawnVarianceMod.csproj` and copy assemblies to RimWorld Mods folder.
Expected: Build succeeds with 0 Errors, 0 Warnings.
