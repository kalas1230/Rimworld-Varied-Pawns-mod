# Dynamic Custom Profiles Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove the 3-custom-profile limit and allow users to create, rename, duplicate, and delete an unlimited number of custom profiles.

**Architecture:** Create `CustomProfile` class in [`Source/VarianceProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs). Update [`PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs) to use `string profileId` identifiers for Active, Hostile, and Overrides, storing custom profiles in a dynamic `List<CustomProfile>`. Update UI controls to support creating, renaming, duplicating, and deleting custom profiles. Include backward compatibility migration for legacy save files.

**Tech Stack:** C# (.NET Framework 4.7.2 / RimWorld 1.5 API), Verse/RimWorld UI (`FloatMenu`, `Listing_Standard`).

## Global Constraints

- Backwards compatibility: legacy settings files must load cleanly without losing existing custom profile values.
- Never crash if an invalid/missing `profileId` is encountered; fallback safely to `"preset_faithful"`.

---

### Task 1: Create `CustomProfile` Class & Update `VarianceProfile.cs`

**Files:**
- Modify: [`Source/VarianceProfile.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/VarianceProfile.cs)

**Interfaces:**
- Produces:
  - `public class CustomProfile : IExposable`
  - Preset string constants (`VarianceProfiles.FaithfulId`, etc.)
  - Helper methods for resolution by string `profileId`

- [ ] **Step 1: Define `CustomProfile` class in `Source/VarianceProfile.cs`**

```csharp
public class CustomProfile : IExposable
{
    public string id;
    public string name;
    public VarianceProfileValues values = new VarianceProfileValues();

    public CustomProfile() { }

    public CustomProfile(string id, string name, VarianceProfileValues values)
    {
        this.id = id;
        this.name = name;
        this.values = values ?? new VarianceProfileValues();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref id, "id");
        Scribe_Values.Look(ref name, "name", "Custom Profile");
        if (Scribe.mode == LoadSaveMode.LoadingVars && values == null)
            values = new VarianceProfileValues();
        values.ExposeData(string.Empty);
    }

    public CustomProfile Clone(string newId, string newName)
    {
        return new CustomProfile(newId, newName, values.Clone());
    }
}
```

- [ ] **Step 2: Add Preset string ID constants in `VarianceProfiles`**

```csharp
public const string FaithfulId = "preset_faithful";
public const string DistinctId = "preset_distinct";
public const string WildcardId = "preset_wildcard";
public const string GiftedId = "preset_gifted";
public const string DesperateId = "preset_desperate";
```

- [ ] **Step 3: Compile to verify**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 2: Refactor `PawnVarianceSettings.cs` Storage & Resolution

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs)

**Interfaces:**
- Consumes: `CustomProfile`, preset string IDs
- Produces:
  - `public List<CustomProfile> customProfiles`
  - `public string activeProfileId`
  - `public string hostileProfileId`
  - `public Dictionary<string, string> factionOverrides`
  - `public Dictionary<string, string> xenotypeOverrides`
  - Updated `Resolve(string profileId)` and `ValuesFor(pawn)`

- [ ] **Step 1: Update fields in `PawnVarianceSettings.cs`**

Replace `customValues` array and enum fields with string ID tracking and `customProfiles` list:

```csharp
public List<CustomProfile> customProfiles = new List<CustomProfile>();
public string activeProfileId = VarianceProfiles.FaithfulId;
public string hostileProfileId = VarianceProfiles.DistinctId;

public Dictionary<string, string> factionOverrides = new Dictionary<string, string>();
public Dictionary<string, string> xenotypeOverrides = new Dictionary<string, string>();

private List<string> factionOverrideKeys = new List<string>();
private List<string> factionOverrideValues = new List<string>();
private List<string> xenotypeOverrideKeys = new List<string>();
private List<string> xenotypeOverrideValues = new List<string>();
```

- [ ] **Step 2: Implement string-based `Resolve(string id)` and `LabelFor(string id)`**

```csharp
public CustomProfile GetCustomProfile(string id)
{
    if (string.IsNullOrEmpty(id) || customProfiles == null) return null;
    return customProfiles.Find(p => p.id == id);
}

public VarianceProfileValues Resolve(string id)
{
    var preset = VarianceProfiles.GetPresetById(id);
    if (preset != null) return preset.MakeValues();

    var custom = GetCustomProfile(id);
    if (custom != null) return custom.values;

    if (customProfiles.Count > 0) return customProfiles[0].values;
    return VarianceProfiles.VanillaLike.MakeValues();
}

public string LabelFor(string id)
{
    var preset = VarianceProfiles.GetPresetById(id);
    if (preset != null) return preset.label;

    var custom = GetCustomProfile(id);
    if (custom != null) return custom.name;

    return id;
}
```

- [ ] **Step 3: Update `ExposeData()` with legacy migration**

Add migration for legacy settings nodes (`customName1`, `custom2_`, `custom3_`, enum string values) so old save files convert smoothly to `CustomProfile` entries.

- [ ] **Step 4: Compile to verify**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 3: Update Profile Selection & Management UI

**Files:**
- Modify: [`Source/PawnVarianceSettings.cs`](file:///C:/Users/gokal/Desktop/Rimworld-mod/Rimworld-Pawn-variance-mod/Source/PawnVarianceSettings.cs)

**Interfaces:**
- Produces:
  - `ProfileMenu(Action<string> onPick)`
  - Buttons for `+ New Custom Profile`, `Duplicate Profile`, `Delete Profile`, `Reset to Faithful`
  - Name editing text field for custom profiles

- [ ] **Step 1: Update `ProfileMenu` dropdown to list presets and dynamic custom profiles**

```csharp
private void ProfileMenu(Action<string> onPick)
{
    var options = new List<FloatMenuOption>();
    
    foreach (var custom in customProfiles)
    {
        var captured = custom.id;
        options.Add(new FloatMenuOption(custom.name, () => onPick(captured)));
    }
    
    foreach (var preset in VarianceProfiles.Presets)
    {
        var captured = preset.stringId;
        options.Add(new FloatMenuOption(preset.label, () => onPick(captured)));
    }
    
    Find.WindowStack.Add(new FloatMenu(options));
}
```

- [ ] **Step 2: Add management buttons in `DrawProfileSelector`**

Implement `+ New Custom Profile`, `Duplicate Profile`, `Delete Profile` (with confirmation), and inline name text field.

- [ ] **Step 3: Update Overrides Tab dropdowns to use string `profileId`**

Update `DrawFactionOverridesSection` and `DrawXenotypeOverridesSection` to store and display string profile IDs.

- [ ] **Step 4: Compile to verify**

Run: `dotnet build Source/PawnVarianceMod.csproj`
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

---

### Task 4: Deploy & Empirical Verification via RimBridge / GABS

**Files:**
- Assemblies: `Assemblies/PawnVarianceMod.dll`

- [ ] **Step 1: Deploy DLL to RimWorld Mods directory**

```bash
cp Assemblies/PawnVarianceMod.dll Assemblies/PawnVarianceMod.pdb \
   "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/"
```

- [ ] **Step 2: Verify dynamic custom profiles in-game**

Create 3 new custom profiles, delete 1, assign 1 to Pirate faction override, spawn pawns, and verify traces resolve correct profile names.
