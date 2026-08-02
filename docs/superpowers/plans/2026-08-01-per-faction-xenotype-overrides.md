# Per-Faction & Xenotype Profile Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a tabbed settings UI to Varied Pawns and implement per-faction and per-xenotype profile overrides with a toggle checkbox and priority resolution cascade.

**Architecture:** Update [`PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs) to store and serialize override mappings (`enableOverrides`, `factionOverrides`, `xenotypeOverrides`). Refactor `DoWindowContents` to render top-level tabs (`TabDrawer.DrawTabs`) separating General/Profile controls from Faction & Xenotype Overrides. Update `ValuesFor(pawn)` to enforce the resolution cascade: Xenotype Override $\rightarrow$ Faction Override $\rightarrow$ Hostile Profile $\rightarrow$ Default Active Profile.

**Tech Stack:** C# (.NET Framework 4.7.2 / RimWorld 1.5 API), Verse/RimWorld UI (`TabRecord`, `TabDrawer`, `Listing_Standard`, `FloatMenu`).

## Global Constraints

- Never break pre-existing settings file loading.
- Always gate Biotech xenotype checks with `ModsConfig.BiotechActive`.
- Keep commit policy in mind: do not `git commit` unless requested by the user.

---

### Task 1: Add Serialization & Storage Fields in `PawnVarianceSettings.cs`

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs)

**Interfaces:**
- Produces:
  - `public bool enableOverrides` (default `false`)
  - `public Dictionary<string, VarianceProfileId> factionOverrides`
  - `public Dictionary<string, VarianceProfileId> xenotypeOverrides`

- [ ] **Step 1: Declare dictionary and toggle fields in `PawnVarianceSettings.cs`**

Add fields and backing lists for scribe serialization to `PawnVarianceSettings`:

```csharp
public bool enableOverrides = false;
public Dictionary<string, VarianceProfileId> factionOverrides = new Dictionary<string, VarianceProfileId>();
public Dictionary<string, VarianceProfileId> xenotypeOverrides = new Dictionary<string, VarianceProfileId>();

private List<string> factionOverrideKeys = new List<string>();
private List<VarianceProfileId> factionOverrideValues = new List<VarianceProfileId>();
private List<string> xenotypeOverrideKeys = new List<string>();
private List<VarianceProfileId> xenotypeOverrideValues = new List<VarianceProfileId>();
```

- [ ] **Step 2: Update `ExposeData()` for serialization**

In `ExposeData()`:

```csharp
Scribe_Values.Look(ref enableOverrides, "enableOverrides", false);

if (Scribe.mode == LoadSaveMode.Saving)
{
    factionOverrideKeys = new List<string>(factionOverrides.Keys);
    factionOverrideValues = new List<VarianceProfileId>(factionOverrides.Values);
    xenotypeOverrideKeys = new List<string>(xenotypeOverrides.Keys);
    xenotypeOverrideValues = new List<VarianceProfileId>(xenotypeOverrides.Values);
}

Scribe_Collections.Look(ref factionOverrideKeys, "factionOverrideKeys", LookMode.Value);
Scribe_Collections.Look(ref factionOverrideValues, "factionOverrideValues", LookMode.Value);
Scribe_Collections.Look(ref xenotypeOverrideKeys, "xenotypeOverrideKeys", LookMode.Value);
Scribe_Collections.Look(ref xenotypeOverrideValues, "xenotypeOverrideValues", LookMode.Value);

if (Scribe.mode == LoadSaveMode.PostLoadInit)
{
    factionOverrides = new Dictionary<string, VarianceProfileId>();
    if (factionOverrideKeys != null && factionOverrideValues != null)
    {
        for (int i = 0; i < Math.Min(factionOverrideKeys.Count, factionOverrideValues.Count); i++)
        {
            if (!string.IsNullOrEmpty(factionOverrideKeys[i]))
                factionOverrides[factionOverrideKeys[i]] = factionOverrideValues[i];
        }
    }

    xenotypeOverrides = new Dictionary<string, VarianceProfileId>();
    if (xenotypeOverrideKeys != null && xenotypeOverrideValues != null)
    {
        for (int i = 0; i < Math.Min(xenotypeOverrideKeys.Count, xenotypeOverrideValues.Count); i++)
        {
            if (!string.IsNullOrEmpty(xenotypeOverrideKeys[i]))
                xenotypeOverrides[xenotypeOverrideKeys[i]] = xenotypeOverrideValues[i];
        }
    }
}
```

- [ ] **Step 3: Compile to ensure zero syntax errors**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 2: Implement Resolution Priority Cascade in `ValuesFor(pawn)`

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs:68-74`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs#L68-L74)

**Interfaces:**
- Consumes: `enableOverrides`, `factionOverrides`, `xenotypeOverrides`
- Produces: Updated `ValuesFor(Pawn pawn)` resolving profile by priority hierarchy

- [ ] **Step 1: Update `ValuesFor(Pawn pawn)` implementation**

Replace `ValuesFor` in [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs#L68-L74) with:

```csharp
public VarianceProfileValues ValuesFor(Pawn pawn)
{
    if (pawn == null) return Active;

    if (enableOverrides)
    {
        // 1. Xenotype Override (Biotech)
        if (ModsConfig.BiotechActive && pawn.genes?.Xenotype != null)
        {
            string xenoDef = pawn.genes.Xenotype.defName;
            if (xenotypeOverrides.TryGetValue(xenoDef, out var xenoProfileId))
                return Resolve(xenoProfileId);
        }

        // 2. Faction Override
        if (pawn.Faction?.def != null)
        {
            string factionDef = pawn.Faction.def.defName;
            if (factionOverrides.TryGetValue(factionDef, out var factionProfileId))
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

- [ ] **Step 2: Compile to verify**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 3: Implement Tabbed Interface in `PawnVarianceSettings.cs`

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs:157-181`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs#L157-L181)

**Interfaces:**
- Consumes: RimWorld `TabRecord`, `TabDrawer`
- Produces: `activeTab` field and tabbed layout in `DoWindowContents`

- [ ] **Step 1: Add tab tracking field**

Add to `PawnVarianceSettings`:

```csharp
private enum SettingsTab { General, Overrides }
private SettingsTab currentTab = SettingsTab.General;
```

- [ ] **Step 2: Update `DoWindowContents` to render tabs**

Refactor `DoWindowContents` in [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs#L157-L181):

```csharp
public void DoWindowContents(Rect inRect)
{
    var tabs = new List<TabRecord>
    {
        new TabRecord("General & Profiles", () => currentTab = SettingsTab.General, currentTab == SettingsTab.General),
        new TabRecord("Faction & Xenotype Overrides", () => currentTab = SettingsTab.Overrides, currentTab == SettingsTab.Overrides)
    };

    Rect tabRect = new Rect(inRect.x, inRect.y + 32f, inRect.width, inRect.height - 32f);
    TabDrawer.DrawTabs(tabRect, tabs);

    var outRect = tabRect.ContractedBy(10f);
    
    if (currentTab == SettingsTab.General)
    {
        DrawGeneralTab(outRect);
    }
    else
    {
        DrawOverridesTab(outRect);
    }
}
```

- [ ] **Step 3: Move existing layout code into `DrawGeneralTab(Rect outRect)`**

Extract existing `DoWindowContents` body into `DrawGeneralTab(Rect outRect)`.

- [ ] **Step 4: Compile to verify**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 4: Implement `DrawOverridesTab` UI Controls

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs)

**Interfaces:**
- Consumes: `enableOverrides`, `factionOverrides`, `xenotypeOverrides`, RimWorld `FloatMenu`, `FactionDef`, `XenotypeDef`
- Produces: `DrawOverridesTab(Rect outRect)` implementation

- [ ] **Step 1: Implement `DrawOverridesTab` layout and enable checkbox**

```csharp
private Vector2 overridesScrollPos = Vector2.zero;

private void DrawOverridesTab(Rect outRect)
{
    var viewRect = new Rect(0f, 0f, outRect.width - 24f, 1400f);
    Widgets.BeginScrollView(outRect, ref overridesScrollPos, viewRect);
    var listing = new Listing_Standard();
    listing.Begin(viewRect);

    listing.CheckboxLabeled("Enable Faction & Xenotype Overrides", ref enableOverrides, 
        "When enabled, specific faction and xenotype profiles take precedence over Hostile and General profiles.");
    
    listing.Gap(SectionGap);

    bool wasEnabled = GUI.enabled;
    if (!enableOverrides)
    {
        GUI.enabled = false;
        Caption(listing, "Enable the checkbox above to configure per-faction and per-xenotype profiles.");
    }

    DrawFactionOverridesSection(listing);
    
    if (ModsConfig.BiotechActive)
    {
        DrawXenotypeOverridesSection(listing);
    }

    GUI.enabled = wasEnabled;

    listing.End();
    Widgets.EndScrollView();
}
```

- [ ] **Step 2: Implement Faction Overrides Section (`DrawFactionOverridesSection`)**

Add method rendering configured `factionOverrides` with profile pickers, remove `[X]` buttons, and the `+ Add Faction Override` `FloatMenu` button listing unadded `FactionDef`s.

- [ ] **Step 3: Implement Xenotype Overrides Section (`DrawXenotypeOverridesSection`)**

Add method rendering configured `xenotypeOverrides` with profile pickers, remove `[X]` buttons, and the `+ Add Xenotype Override` `FloatMenu` button listing unadded `XenotypeDef`s.

- [ ] **Step 4: Compile and test build**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 5: Deploy & Empirical Verification via RimBridge / GABS

**Files:**
- Assemblies: `Assemblies/PawnVarianceMod.dll`

- [ ] **Step 1: Deploy DLL to RimWorld Mods directory**

```bash
cp Assemblies/PawnVarianceMod.dll Assemblies/PawnVarianceMod.pdb \
   "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/"
```

- [ ] **Step 2: Start game via GABS & verify settings window UI**

Start RimWorld, open mod settings window via GABS/RimBridge, and verify both tabs render cleanly.

- [ ] **Step 3: Test profile resolution hierarchy via pawn spawns**

Spawn pawns with overridden factions/xenotypes and verify log traces match expected priority (`Xenotype > Faction > Hostile > Active`).
