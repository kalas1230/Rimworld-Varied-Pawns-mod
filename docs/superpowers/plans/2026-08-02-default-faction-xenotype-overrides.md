# Default Faction & Xenotype Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement 4 new empirically-bounded preset profiles (`Elite`, `Sovereign`, `Specialist`, `Scavenger`) and pre-populate out-of-the-box default overrides for core factions (`Empire`, `Pirates`, `Ancients`) and Biotech xenotypes (`Sanguophage`, `Highmate`, `Genie`, `Hussar`, `Waster`, `Pigskin`, `Neanderthal`, `Impid`).

**Architecture:** Extend `VarianceProfileId` enum and `VarianceProfiles.Presets` list in `Source/VarianceProfile.cs`. Update `PawnVarianceSettings` constructor and `ResetDefaultOverrides()` method in `Source/PawnVarianceSettings.cs` to populate default override dictionaries out-of-the-box with `enableOverrides = true`.

**Tech Stack:** C#, RimWorld Verse API, Harmony, .NET Framework / MSBuild.

## Global Constraints

- Preserve strict performance hierarchy: Sovereign > Elite > Specialist > Faithful > Scavenger across all selection tiers.
- Bounded within $\pm 25\%$ performance envelope relative to `Faithful`.
- Do not commit to git without explicit user request.

---

### Task 1: Register New Preset Profiles in `VarianceProfile.cs`

**Files:**
- Modify: `Source/VarianceProfile.cs:15-320`

**Interfaces:**
- Consumes: Existing `VarianceProfile`, `VarianceProfileValues`, `VarianceProfileId`
- Produces: Presets `Elite` (`preset_elite`), `Sovereign` (`preset_sovereign`), `Specialist` (`preset_specialist`), `Scavenger` (`preset_scavenger`)

- [ ] **Step 1: Add new Enum members to `VarianceProfileId`**

Add `Elite = 8`, `Sovereign = 9`, `Specialist = 10`, `Scavenger = 11` to `VarianceProfileId` enum in `Source/VarianceProfile.cs`.

```csharp
public enum VarianceProfileId
{
    Custom = 0,
    VanillaLike = 1,
    BalancedVariance = 2,
    WildSpread = 3,
    GiftedColony = 4,
    Hardscrabble = 5,
    Custom2 = 6,
    Custom3 = 7,
    Elite = 8,
    Sovereign = 9,
    Specialist = 10,
    Scavenger = 11,
}
```

- [ ] **Step 2: Add static constants and `VarianceProfile` definitions to `VarianceProfiles`**

In `Source/VarianceProfile.cs`, add string constants and static read-only instances for `Elite`, `Sovereign`, `Specialist`, and `Scavenger`:

```csharp
public const string EliteId = "preset_elite";
public const string SovereignId = "preset_sovereign";
public const string SpecialistId = "preset_specialist";
public const string ScavengerId = "preset_scavenger";

public static readonly VarianceProfile Elite = new VarianceProfile(
    VarianceProfileId.Elite,
    EliteId,
    "Elite",
    "Refined imperial nobility and high-born pawns. Consistently high capability and polished skills.",
    new VarianceProfileValues
    {
        averageQuality = 0.51f,
        skillNoise = 0.22f,
        passionNoise = 0.25f,
        passionMajorBias = 0.62f,
        skillShiftMin = -2f,
        skillShiftMax = 3.5f,
        childSkillShiftMin = -1f,
        childSkillShiftMax = 2f,
        traitCountMin = 2f,
        traitCountMax = 4f,
        passionCountMin = 2f,
        passionCountMax = 6.2f,
    });

public static readonly VarianceProfile Sovereign = new VarianceProfile(
    VarianceProfileId.Sovereign,
    SovereignId,
    "Sovereign",
    "Archite lords, Sanguophages, and supreme leaders. Top-tier skill growth and wide passions.",
    new VarianceProfileValues
    {
        averageQuality = 0.52f,
        skillNoise = 0.24f,
        passionNoise = 0.25f,
        passionMajorBias = 0.70f,
        skillShiftMin = -1f,
        skillShiftMax = 4.0f,
        childSkillShiftMin = 0f,
        childSkillShiftMax = 3f,
        traitCountMin = 2f,
        traitCountMax = 5f,
        passionCountMin = 2.2f,
        passionCountMax = 6.5f,
    });

public static readonly VarianceProfile Specialist = new VarianceProfile(
    VarianceProfileId.Specialist,
    SpecialistId,
    "Specialist",
    "Engineered single-domain specialists (Genies, Hussars). Focused skill spikes with domain passions.",
    new VarianceProfileValues
    {
        averageQuality = 0.48f,
        skillNoise = 0.28f,
        passionNoise = 0.25f,
        passionMajorBias = 0.58f,
        skillShiftMin = -3f,
        skillShiftMax = 3.8f,
        childSkillShiftMin = -1f,
        childSkillShiftMax = 2f,
        traitCountMin = 1f,
        traitCountMax = 4f,
        passionCountMin = 2f,
        passionCountMax = 6.0f,
    });

public static readonly VarianceProfile Scavenger = new VarianceProfile(
    VarianceProfileId.Scavenger,
    ScavengerId,
    "Scavenger",
    "Wasteland survivors, pirates, and scavengers. Lower baseline skills with tough survival rolls.",
    new VarianceProfileValues
    {
        averageQuality = 0.43f,
        skillNoise = 0.30f,
        passionNoise = 0.25f,
        passionMajorBias = 0.45f,
        skillShiftMin = -4f,
        skillShiftMax = 2.0f,
        childSkillShiftMin = -2f,
        childSkillShiftMax = 1f,
        traitCountMin = 2f,
        traitCountMax = 5f,
        passionCountMin = 1.8f,
        passionCountMax = 5.5f,
    });
```

Update `Presets` list to include `Elite`, `Sovereign`, `Specialist`, and `Scavenger`:

```csharp
public static readonly List<VarianceProfile> Presets = new List<VarianceProfile>
{
    VanillaLike,
    BalancedVariance,
    WildSpread,
    GiftedColony,
    Hardscrabble,
    Elite,
    Sovereign,
    Specialist,
    Scavenger,
};
```

---

### Task 2: Populate Built-in Default Overrides in `PawnVarianceSettings.cs`

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:20-800`

**Interfaces:**
- Consumes: `VarianceProfiles.EliteId`, `SovereignId`, `SpecialistId`, `ScavengerId`, `DistinctId`, `WildcardId`
- Produces: Out-of-the-box `factionOverrides` and `xenotypeOverrides` dictionary pre-population.

- [ ] **Step 1: Update `enableOverrides` default value**

In `Source/PawnVarianceSettings.cs`, change `public bool enableOverrides = false;` to `public bool enableOverrides = true;`.

- [ ] **Step 2: Add `PopulateDefaultOverrides()` helper method**

In `Source/PawnVarianceSettings.cs`, add method:

```csharp
public void PopulateDefaultOverrides()
{
    if (factionOverrides == null) factionOverrides = new Dictionary<string, string>();
    if (xenotypeOverrides == null) xenotypeOverrides = new Dictionary<string, string>();

    // Built-in Faction Overrides
    if (!factionOverrides.ContainsKey("Empire")) factionOverrides["Empire"] = VarianceProfiles.EliteId;
    if (!factionOverrides.ContainsKey("Pirate")) factionOverrides["Pirate"] = VarianceProfiles.ScavengerId;
    if (!factionOverrides.ContainsKey("PirateSavage")) factionOverrides["PirateSavage"] = VarianceProfiles.ScavengerId;
    if (!factionOverrides.ContainsKey("Ancients")) factionOverrides["Ancients"] = VarianceProfiles.SovereignId;
    if (!factionOverrides.ContainsKey("AncientsHostile")) factionOverrides["AncientsHostile"] = VarianceProfiles.SovereignId;

    // Built-in Xenotype Overrides
    if (!xenotypeOverrides.ContainsKey("Sanguophage")) xenotypeOverrides["Sanguophage"] = VarianceProfiles.SovereignId;
    if (!xenotypeOverrides.ContainsKey("Highmate")) xenotypeOverrides["Highmate"] = VarianceProfiles.EliteId;
    if (!xenotypeOverrides.ContainsKey("Genie")) xenotypeOverrides["Genie"] = VarianceProfiles.SpecialistId;
    if (!xenotypeOverrides.ContainsKey("Hussar")) xenotypeOverrides["Hussar"] = VarianceProfiles.SpecialistId;
    if (!xenotypeOverrides.ContainsKey("Waster")) xenotypeOverrides["Waster"] = VarianceProfiles.ScavengerId;
    if (!xenotypeOverrides.ContainsKey("Pigskin")) xenotypeOverrides["Pigskin"] = VarianceProfiles.ScavengerId;
    if (!xenotypeOverrides.ContainsKey("Neanderthal")) xenotypeOverrides["Neanderthal"] = VarianceProfiles.DistinctId;
    if (!xenotypeOverrides.ContainsKey("Impid")) xenotypeOverrides["Impid"] = VarianceProfiles.WildcardId;
}
```

- [ ] **Step 3: Call `PopulateDefaultOverrides()` in constructor and `ResetDefaultOverrides()`**

Invoke `PopulateDefaultOverrides()` in `PawnVarianceSettings` constructor and inside `ResetDefaultOverrides()` / `ExposeData()` post-load checks so defaults are always available.

---

### Task 3: Build Verification & Math Check

**Files:**
- Modify/Run: `dotnet build Source/PawnVarianceMod.csproj`
- Run: `python zzz-Do-Not-Commit/test_new_profiles_sim.py`

- [ ] **Step 1: Execute `dotnet build`**

Command:
```powershell
dotnet build Source/PawnVarianceMod.csproj
```
Expected: `Build succeeded. 0 Warning(s) 0 Error(s)`

- [ ] **Step 2: Execute python simulation verify**

Command:
```powershell
python zzz-Do-Not-Commit/test_new_profiles_sim.py
```
Expected output confirms:
- Sovereign strictly beats Elite
- Elite strictly beats Specialist
- Specialist strictly beats Faithful
- Faithful strictly beats Scavenger
- Sovereign max delta $\le +26.3\%$ / Best-of-50 $\le +15.9\%$.
