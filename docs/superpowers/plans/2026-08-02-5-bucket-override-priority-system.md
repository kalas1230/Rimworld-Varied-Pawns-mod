# 5-Bucket Override Priority System Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement a 5-bucket priority ranking system (`Lowest`, `Low`, `Normal`, `High`, `Highest`) for Faction and Xenotype overrides, enabling granular conflict resolution across mixed-xenotype factions.

**Architecture:** Add `OverridePriority` enum to `Source/PawnVarianceSettings.cs`. Add priority dictionaries for factions and xenotypes with Scribe persistence. Update `ValuesFor(Pawn pawn)` resolution algorithm to compare priority buckets before applying tie-breaking. Render priority selector buttons in `DrawOverridesTab`.

**Tech Stack:** C#, RimWorld Verse / RimWorld UI API, Harmony, .NET Framework / MSBuild.

## Global Constraints

- Default priority for all new and existing overrides is `Normal`.
- Unlisted factions and xenotypes have no override entries.
- Tie-breaking between identical priority levels uses `factionOverridesTakePrecedence`.

---

### Task 1: Add Data Model, Storage, & Priority Resolution Algorithm

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:20-220`

**Interfaces:**
- Consumes: `Pawn`, `Faction`, `ModsConfig.BiotechActive`
- Produces: `OverridePriority` enum, `factionPriorities`, `xenotypePriorities`, updated `ValuesFor(Pawn pawn)`

- [ ] **Step 1: Define `OverridePriority` enum in `PawnVarianceSettings.cs`**

```csharp
public enum OverridePriority
{
    Lowest = 0,
    Low = 1,
    Normal = 2,
    High = 3,
    Highest = 4,
}
```

- [ ] **Step 2: Add priority dictionary fields & Scribe serialization in `PawnVarianceSettings.cs`**

```csharp
public Dictionary<string, OverridePriority> factionPriorities = new Dictionary<string, OverridePriority>();
public Dictionary<string, OverridePriority> xenotypePriorities = new Dictionary<string, OverridePriority>();

private List<string> factionPriorityKeys = new List<string>();
private List<int> factionPriorityValues = new List<int>();
private List<string> xenotypePriorityKeys = new List<string>();
private List<int> xenotypePriorityValues = new List<int>();
```

In `ExposeData()`:
```csharp
if (Scribe.mode == LoadSaveMode.Saving)
{
    factionPriorityKeys = new List<string>(factionPriorities.Keys);
    factionPriorityValues = factionPriorities.Values.Select(v => (int)v).ToList();
    xenotypePriorityKeys = new List<string>(xenotypePriorities.Keys);
    xenotypePriorityValues = xenotypePriorities.Values.Select(v => (int)v).ToList();
}

Scribe_Collections.Look(ref factionPriorityKeys, "factionPriorityKeys", LookMode.Value);
Scribe_Collections.Look(ref factionPriorityValues, "factionPriorityValues", LookMode.Value);
Scribe_Collections.Look(ref xenotypePriorityKeys, "xenotypePriorityKeys", LookMode.Value);
Scribe_Collections.Look(ref xenotypePriorityValues, "xenotypePriorityValues", LookMode.Value);

if (Scribe.mode == LoadSaveMode.PostLoadInit)
{
    factionPriorities = new Dictionary<string, OverridePriority>();
    if (factionPriorityKeys != null && factionPriorityValues != null && factionPriorityKeys.Count == factionPriorityValues.Count)
    {
        for (int i = 0; i < factionPriorityKeys.Count; i++)
            factionPriorities[factionPriorityKeys[i]] = (OverridePriority)factionPriorityValues[i];
    }

    xenotypePriorities = new Dictionary<string, OverridePriority>();
    if (xenotypePriorityKeys != null && xenotypePriorityValues != null && xenotypePriorityKeys.Count == xenotypePriorityValues.Count)
    {
        for (int i = 0; i < xenotypePriorityKeys.Count; i++)
            xenotypePriorities[xenotypePriorityKeys[i]] = (OverridePriority)xenotypePriorityValues[i];
    }
}
```

- [ ] **Step 3: Update `ValuesFor(Pawn pawn)` resolution logic**

```csharp
public VarianceProfileValues ValuesFor(Pawn pawn)
{
    if (pawn == null) return Active;

    if (enableOverrides)
    {
        string factionProfileId = null;
        OverridePriority factionPrio = OverridePriority.Normal;
        bool hasFactionOverride = false;
        if (pawn.Faction?.def != null && factionOverrides.TryGetValue(pawn.Faction.def.defName, out factionProfileId))
        {
            hasFactionOverride = true;
            if (factionPriorities.TryGetValue(pawn.Faction.def.defName, out var p))
                factionPrio = p;
        }

        string xenoProfileId = null;
        OverridePriority xenoPrio = OverridePriority.Normal;
        bool hasXenoOverride = false;
        if (ModsConfig.BiotechActive)
        {
            string xenoDef = GetXenotypeDefName(pawn);
            if (xenoDef != null && xenotypeOverrides.TryGetValue(xenoDef, out xenoProfileId))
            {
                hasXenoOverride = true;
                if (xenotypePriorities.TryGetValue(xenoDef, out var p))
                    xenoPrio = p;
            }
        }

        if (hasFactionOverride && hasXenoOverride)
        {
            if (factionPrio > xenoPrio) return Resolve(factionProfileId);
            if (xenoPrio > factionPrio) return Resolve(xenoProfileId);

            // Equal priority tie-break
            if (factionOverridesTakePrecedence) return Resolve(factionProfileId);
            return Resolve(xenoProfileId);
        }

        if (hasFactionOverride) return Resolve(factionProfileId);
        if (hasXenoOverride) return Resolve(xenoProfileId);
    }

    if (applyToHostilePawns && pawn.Faction != null && Faction.OfPlayerSilentFail != null
        && pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))
    {
        return Hostile;
    }

    return Active;
}
```

---

### Task 2: Update Overrides Tab UI (`DrawOverridesTab`)

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:320-550`

- [ ] **Step 1: Add Info Banner in `DrawOverridesTab`**

Add an explanatory help box at the top of the overrides view explaining `Lowest`, `Low`, `Normal`, `High`, `Highest` buckets and tie-breaking.

- [ ] **Step 2: Add Priority Dropdown Selectors next to override rows**

Add priority dropdown menus for each entry in `DrawFactionOverridesSection` and `DrawXenotypeOverridesSection`. Clicking the priority button opens a `FloatMenu` with `Lowest`, `Low`, `Normal`, `High`, `Highest`.

```csharp
private void PriorityMenu(Action<OverridePriority> onPick)
{
    var options = new List<FloatMenuOption>();
    foreach (OverridePriority p in Enum.GetValues(typeof(OverridePriority)))
    {
        var captured = p;
        options.Add(new FloatMenuOption(captured.ToString(), () => onPick(captured)));
    }
    Find.WindowStack.Add(new FloatMenu(options));
}
```

---

### Task 3: Build Verification & Testing

**Files:**
- Build: `dotnet build Source/PawnVarianceMod.csproj`

- [ ] **Step 1: Compile assembly via `dotnet build`**

Command:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`
