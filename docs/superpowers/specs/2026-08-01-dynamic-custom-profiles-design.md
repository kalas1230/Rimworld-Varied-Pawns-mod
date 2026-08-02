# Dynamic Custom Profiles Design Spec

Date: 2026-08-01  
Mod: Varied Pawns (`PawnVarianceMod`)  
Status: Approved  

---

## 1. Overview

This feature removes the fixed 3-custom-profile limit and allows users to create, name, duplicate, edit, and delete an **unlimited number of custom profiles**. Custom profiles can be selected as the main Active profile, assigned to Hostile pawns, or mapped to specific Factions and Xenotypes in the Overrides tab.

---

## 2. Architecture & Data Model

### 2.1 `CustomProfile` Class
A dedicated serializable class representing a user-defined custom profile:

```csharp
public class CustomProfile : IExposable
{
    public string id;       // Unique string identifier (e.g. "custom_1", "custom_17224738")
    public string name;     // User-editable display name
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

---

### 2.2 Profile Identifier System (`profileId`)
Profiles are identified across the mod via unique string keys:
- **Preset IDs**: `"preset_faithful"`, `"preset_distinct"`, `"preset_wildcard"`, `"preset_gifted"`, `"preset_desperate"`.
- **Custom Profile IDs**: `"custom_1"`, `"custom_2"`, or generated IDs (e.g. `"custom_1722473892"`).

### 2.3 Storage in `PawnVarianceSettings`
- `public List<CustomProfile> customProfiles = new List<CustomProfile>();`
- `public string activeProfileId = "preset_faithful";`
- `public string hostileProfileId = "preset_distinct";`
- `public Dictionary<string, string> factionOverrides = new Dictionary<string, string>();`
- `public Dictionary<string, string> xenotypeOverrides = new Dictionary<string, string>();`

---

## 3. UI Controls

### 3.1 General & Profiles Tab
- **Profile Selector Dropdown**: Lists all built-in Presets and all dynamic Custom Profiles.
- **Profile Management Buttons**:
  - `+ New Custom Profile`: Creates a new custom profile instance, adds it to `customProfiles`, and switches selection to it.
  - `Duplicate Profile`: Creates a copy of the currently selected profile (preset or custom) as a new custom profile.
  - `Delete Profile` *(Custom profiles only)*: Removes the profile from `customProfiles`. If the deleted profile was set as Active or Hostile, resets those selections safely to `"preset_faithful"`.
  - `Reset to Faithful`: Resets the values of the currently selected custom profile to Faithful defaults.
- **Inline Name Editor**: Text field for renaming custom profiles.

### 3.2 Faction & Xenotype Overrides Tab
- Float menu dropdowns in the Overrides tab populate from all available presets and custom profiles dynamically.

---

## 4. Backwards Compatibility & Migration

During `ExposeData()` / `PostLoadInit`:
1. If legacy `customName1` or `custom2_` nodes exist in the settings XML, migrate legacy slots 0..2 into `CustomProfile` objects (`"custom_1"`, `"custom_2"`, `"custom_3"`).
2. If `activeProfile` or `hostileProfile` were scribed as old enum names/values, map them to new string IDs (`"preset_faithful"`, `"preset_distinct"`, `"custom_1"`, etc.).
3. Ensure at least one custom profile exists in `customProfiles` on fresh installs.

---

## 5. Testing & Verification

1. **Compilation**: Clean build with 0 errors.
2. **Profile Lifecycle**: Create 5 custom profiles, rename them, duplicate them, and delete 2. Verify settings save and reload cleanly.
3. **Pawn Generation**: Spawn pawns mapped to different custom profiles via overrides; verify diagnostic logs read the correct custom profile name.
