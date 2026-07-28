# Pawn Variance Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the RimWorld Harmony-patched mod described in `docs/superpowers/specs/2026-07-27-pawn-variance-mod-design.md` — continuous, quality-driven variance to pawn skills/traits/passions at generation time, a Biotech growth-up patch, and a cosmetic tier bio-label.

**Architecture:** A single C# assembly loaded by RimWorld, using Harmony to postfix `PawnGenerator.GeneratePawn` (main variance), a Biotech Child→Adult transition method (growth-up variance), and a pawn bio-description method (cosmetic tier label). All per-pawn effects are written through vanilla-typed fields already covered by RimWorld's own save system — no new Defs, Comps, or Scribe-saved types.

**Tech Stack:** C# (.NET Framework 4.7.2, RimWorld's target), Harmony (0Harmony.dll via RimWorld's `Assembly-CSharp.dll` + Harmony library shipped with RimWorld), MSBuild `.csproj` referencing RimWorld's managed assemblies.

## Global Constraints

- Target RimWorld 1.5/1.6 only (Non-goals) — no multi-version compatibility shims.
- **No new save-file footprint**: no `GameComponent`, `WorldComponent`, `ThingComp`, or custom `Scribe`-saved types, and no new Defs (Save-Game Safety). Settings persist only via `ModSettings.ExposeData()` into mod config XML.
- **No automated unit test harness** (Non-goals) — every task in this plan ends in a manual/in-game verification step referencing the spec's Testing Plan, not an automated test run. This is a deliberate spec decision (impractical for a Harmony-patched game mod), not an omission.
- **No new UI beyond the standard Mod Settings window** plus the one bio-text tier label append (Non-goals) — no custom dialogs/panels.
- Exactly two Harmony patch points per Architecture: `PawnGenerator.GeneratePawn` postfix, and a Biotech Child→Adult transition postfix — plus the separate, independent `TierLabelPatch` postfix on the bio-description method (three patches total, two "patch points" in the spec's own count because the third is cosmetic-only).
- Every "unverified, flagged for implementation-time confirmation" item in the spec (listed below) must be resolved by decompiling the target RimWorld version's source **before** writing the task that depends on it:
  - `PawnGenerator.GeneratePawn`'s internal retry behavior and whether quest-reward/trader-redress pawns route through it (Per-pawn flow step 1).
  - `PawnKindDef.forcedTraits`'s existence/name (Trait variance step 2).
  - `TraitDef.conflictingTraits`/`exclusionTags` field names (Trait variance step 6).
  - Vanilla's Minor/Major passion-assignment ratio logic (Passion variance step 5).
  - The exact Biotech Child→Adult transition method (Architecture, `GrowthUpPatch`).
  - The exact pawn bio-description-generation method, confirmed distinct from the inspect-string method (Tier bio label).
  - `TraitDegreeData`'s field names (`skillGains`, `statOffsets`, `statFactors`, `disabledWorkTags`, `socialFightChanceFactor`) used by `TraitDesirabilityCache.ScoreTrait` (Task 4) — surfaced by Task 4's review: no local RimWorld install exists in this implementation's sandbox, so `dotnet build` could only ever hit assembly-not-found errors, never member-not-found errors; these field names were transcribed from the plan text, not confirmed against decompiled source. This is a real, so-far-unresolved item, not a completed check.
  - `SkillRecord.Level`'s public setter (`SkillVarianceApplier.Apply`, Task 5) — same sandbox limitation as above: the build attempt only exercises missing-assembly errors, never a real member-resolution check.
- All per-pawn logging uses `Log.ErrorOnce` keyed by exception type+stack trace for the two main postfixes' fail-safe catches (not a single global flag), except `GrowthUpPatch`'s live-pawn-mutation failure path, which uses plain `Log.Error` every time (Growth-up variance step 5).
- **Compatibility: touch only what the spec requires, nothing more.** Every Harmony patch in this plan is a `[HarmonyPatch]` **Postfix only** — never a `Prefix` that can suppress vanilla or another mod's behavior, and never a `Transpiler` (IL-level patches are the single biggest source of cross-mod incompatibility, since two transpilers on the same method can conflict in ways Harmony can't reconcile). If a task ever seems to need a Prefix or Transpiler to satisfy the spec, stop and flag it rather than adding one — none of the spec's three patch points require it. Each patch also targets the narrowest method that does the job (`GeneratePawn`, the one Child→Adult transition method, `GetDescription`) rather than a broader/shared method, and mutates only the specific fields the spec names (`pawn.skills`, `pawn.story.traits`, passion fields) — never touches gear, health, relationships, ideoligion, or anything else vanilla or another mod might also be managing, even where it would be easy to reach. No task should read or write a `Def`, `Comp`, or static registry belonging to another mod's namespace.
- **Performance: every per-pawn code path must stay cheap, since it runs on the hot pawn-generation path.** `TraitDesirabilityCache` computes its scan once at startup (`[StaticConstructorOnStartup]`) and rebuilds only on mod-list change (Edge Case 6) — never per-pawn, never per-tick. `QualityRoller`, `SkillVarianceApplier`, `TraitVarianceApplier`, and `PassionVarianceApplier` all run at most once per pawn, only at generation or growth-up (never in `Tick()`/`Update()`/any per-frame hook), and their per-pawn work is bounded by the number of loaded skills/traits (low hundreds at most even with large content mods), consistent with the spec's own "cheap O(number of loaded traits) pass" framing (Trait variance step 5). When implementing each task's LINQ-heavy code (`WeightedPick`, the softmax loop, `TraitDesirabilityCache.Rebuild`), prefer it as written for clarity, but if profiling in Task 11 shows measurable generation-time cost, replace per-pawn LINQ chains with plain loops before shipping — correctness and compatibility come first, but a mod that visibly stutters pawn generation is a real regression to fix, not a style nit to defer indefinitely.

---

## File Structure

```
About/
  About.xml                          — mod metadata, target version, dependencies (Harmony, optionally Biotech as soft dependency)
  LoadFolders.xml                    — version-folder mapping (1.5/, 1.6/ or Common/)
Source/
  PawnVarianceMod.csproj             — references RimWorld's Assembly-CSharp.dll, 0Harmony.dll, UnityEngine assemblies
  PawnVarianceMod.cs                 — Mod entry point, Harmony.PatchAll(), Mod Settings window UI
  PawnVarianceSettings.cs            — ModSettings subclass: all fields, ExposeData, clamp/swap-on-load, Beta-cache dirty flag
  Constants.cs                       — every fixed internal constant named in Settings Schema, in one place
  QualityRoller.cs                   — Beta/Gamma sampling, quality roll
  TraitDesirabilityCache.cs          — [StaticConstructorOnStartup] trait scoring cache
  SkillVarianceApplier.cs            — skill baseline shift + noise
  TraitVarianceApplier.cs            — trait count/forced-disallowed/weighted sampling
  PassionVarianceApplier.cs          — passion count/softmax weighting/placement
  HarmonyPatches.cs                  — main GeneratePawn postfix, gating, ordering, fail-safe
  GrowthUpPatch.cs                   — Biotech Child→Adult postfix, idempotency guard
  TierUtility.cs                     — shared effective-quality reconstruction + tier lookup (used by both the settings window and TierLabelPatch)
  TierLabelPatch.cs                  — bio-description postfix appending the color-coded label
```

Each applier is a static class with a single public entry point taking `(Pawn pawn, float quality, PawnVarianceSettings settings)` and no other state, so `HarmonyPatches` and `GrowthUpPatch` can both call the identical logic without duplication.

---

### Task 1: Project scaffolding

**Files:**
- Create: `About/About.xml`
- Create: `About/LoadFolders.xml`
- Create: `Source/PawnVarianceMod.csproj`
- Create: `Source/PawnVarianceMod.cs`

**Interfaces:**
- Produces: `PawnVarianceMod : Verse.Mod` — the entry point every later task's Harmony patches attach to via `[StaticConstructorOnStartup]` or `Harmony.PatchAll()` called from this class's constructor.

- [ ] **Step 1: Write `About/About.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<ModMetaData>
  <packageId>yourname.pawnvariance</packageId>
  <name>Pawn Variance</name>
  <author>yourname</author>
  <supportedVersions>
    <li>1.5</li>
    <li>1.6</li>
  </supportedVersions>
  <description>Adds configurable, randomized variance to a pawn's skill levels, trait count/quality, and passion assignment at generation time.</description>
  <modDependencies>
    <li>
      <packageId>brrainz.harmony</packageId>
      <displayName>Harmony</displayName>
      <steamWorkshopUrl>steam://url/CommunityFilePage/2009463077</steamWorkshopUrl>
      <downloadUrl>https://github.com/pardeike/HarmonyRimWorld/releases/latest</downloadUrl>
    </li>
  </modDependencies>
  <loadAfter>
    <li>brrainz.harmony</li>
  </loadAfter>
</ModMetaData>
```

- [ ] **Step 2: Write `About/LoadFolders.xml`**

```xml
<?xml version="1.0" encoding="utf-8"?>
<loadFolders>
  <v1.5>
    <li>/</li>
  </v1.5>
  <v1.6>
    <li>/</li>
  </v1.6>
</loadFolders>
```

- [ ] **Step 3: Write `Source/PawnVarianceMod.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <AssemblyName>PawnVarianceMod</AssemblyName>
    <RootNamespace>PawnVarianceMod</RootNamespace>
    <LangVersion>9.0</LangVersion>
    <OutputPath>..\Assemblies\</OutputPath>
    <AppendTargetFrameworkToOutputPath>false</AppendTargetFrameworkToOutputPath>
    <Nullable>disable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <!-- Paths below must point at the local RimWorld install; adjust RIMWORLD_DIR per machine -->
    <Reference Include="Assembly-CSharp">
      <HintPath>$(RIMWORLD_DIR)\RimWorldWin64_Data\Managed\Assembly-CSharp.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.CoreModule">
      <HintPath>$(RIMWORLD_DIR)\RimWorldWin64_Data\Managed\UnityEngine.CoreModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="UnityEngine.IMGUIModule">
      <HintPath>$(RIMWORLD_DIR)\RimWorldWin64_Data\Managed\UnityEngine.IMGUIModule.dll</HintPath>
      <Private>false</Private>
    </Reference>
    <Reference Include="0Harmony">
      <HintPath>$(RIMWORLD_DIR)\Mods\Core\Assemblies\0Harmony.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Write `Source/PawnVarianceMod.cs`**

```csharp
using HarmonyLib;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public class PawnVarianceMod : Mod
    {
        public static PawnVarianceSettings Settings;

        public PawnVarianceMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PawnVarianceSettings>();
            var harmony = new Harmony("yourname.pawnvariance");
            harmony.PatchAll();
        }

        public override string SettingsCategory() => "Pawn Variance";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.MarkDirtyOnWrite();
        }
    }
}
```

- [ ] **Step 5: Verify the project builds**

Set the `RIMWORLD_DIR` environment variable (or hardcode the HintPath) to a local RimWorld install, then run:

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: build succeeds (empty settings class doesn't exist yet — this will fail until Task 2 adds `PawnVarianceSettings`; for this step, temporarily stub `PawnVarianceSettings` as an empty `ModSettings` subclass with a no-op `DoWindowContents`/`MarkDirtyOnWrite`, then delete the stub once Task 2 lands).

- [ ] **Step 6: Commit**

```bash
git add About/ Source/PawnVarianceMod.csproj Source/PawnVarianceMod.cs
git commit -m "chore: scaffold mod project structure"
```

---

### Task 2: Settings

**Files:**
- Create: `Source/Constants.cs`
- Create: `Source/PawnVarianceSettings.cs`

**Interfaces:**
- Consumes: nothing.
- Produces: `PawnVarianceSettings` with public fields `averageQuality: float`, `skillNoise/traitNoise/passionNoise: float`, `skillShiftMin/skillShiftMax: float`, `traitCountMin/traitCountMax: float`, `passionCountMin/passionCountMax: float`, `enableSkillVariance/enableTraitVariance/enablePassionVariance/applyToHostilePawns/applyVarianceOnGrowUp/verboseLogging: bool`; methods `GetBetaAlphaBeta(out float alpha, out float beta)` (lazily rebuilds using `MarkDirtyOnWrite`'s dirty flag), `DoWindowContents(Rect)`, `MarkDirtyOnWrite()`. `Constants.cs` produces `public static class Constants` with every named constant from Settings Schema (see Step 1).

- [ ] **Step 1: Write `Source/Constants.cs`**

```csharp
namespace PawnVarianceMod
{
    public static class Constants
    {
        // Quality roll (Core Algorithms > Quality roll)
        public const float QualityClampEpsilon = 1e-3f;
        public const float BetaConcentrationK = 8f; // fixed population-spread constant

        // Noise-floor / max constants (Settings Schema)
        public const float MinMagnitudeFloor = 0.5f;
        public const float MaxMagnitude = 6f;
        public const float MinSpreadFloor = 0.05f;
        public const float MaxSpread = 2f;
        public const float MinTemperatureFloor = 0.5f;
        public const float MaxTemperature = 8f;
        public const float SmallRandomJitter = 0.5f;

        // Trait desirability scoring (Core Algorithms > Trait desirability scoring)
        public const float SkillOffsetReferenceMagnitude = 6f;      // category 1
        public const float StatReferenceMagnitude = 1.0f;           // category 2
        public const float WorkTagDisablePenalty = 0.15f;           // category 3, per disabled tag
        public const float SocialReferenceMagnitude = 20f;          // category 4
        public const float ZMultiplier = 2f;                        // observedMinScore/MaxScore bound width

        // Tier bio label (Core Algorithms > Tier bio label)
        public const float AssumedVanillaSkillBaseline = 5f;
    }
}
```

- [ ] **Step 2: Write `Source/PawnVarianceSettings.cs`**

```csharp
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public class PawnVarianceSettings : ModSettings
    {
        public float averageQuality = 0.5f;
        public float skillNoise = 0.35f;
        public float traitNoise = 0.35f;
        public float passionNoise = 0.35f;
        public float skillShiftMin = -6f;
        public float skillShiftMax = 6f;
        public float traitCountMin = 1f;
        public float traitCountMax = 6f;
        public float passionCountMin = 0f;
        public float passionCountMax = 3f;
        public bool enableSkillVariance = true;
        public bool enableTraitVariance = true;
        public bool enablePassionVariance = true;
        public bool applyToHostilePawns = true;
        public bool applyVarianceOnGrowUp = true;
        public bool verboseLogging = false;

        private bool betaCacheDirty = true;
        private float cachedAlpha, cachedBeta;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref averageQuality, "averageQuality", 0.5f);
            Scribe_Values.Look(ref skillNoise, "skillNoise", 0.35f);
            Scribe_Values.Look(ref traitNoise, "traitNoise", 0.35f);
            Scribe_Values.Look(ref passionNoise, "passionNoise", 0.35f);
            Scribe_Values.Look(ref skillShiftMin, "skillShiftMin", -6f);
            Scribe_Values.Look(ref skillShiftMax, "skillShiftMax", 6f);
            Scribe_Values.Look(ref traitCountMin, "traitCountMin", 1f);
            Scribe_Values.Look(ref traitCountMax, "traitCountMax", 6f);
            Scribe_Values.Look(ref passionCountMin, "passionCountMin", 0f);
            Scribe_Values.Look(ref passionCountMax, "passionCountMax", 3f);
            Scribe_Values.Look(ref enableSkillVariance, "enableSkillVariance", true);
            Scribe_Values.Look(ref enableTraitVariance, "enableTraitVariance", true);
            Scribe_Values.Look(ref enablePassionVariance, "enablePassionVariance", true);
            Scribe_Values.Look(ref applyToHostilePawns, "applyToHostilePawns", true);
            Scribe_Values.Look(ref applyVarianceOnGrowUp, "applyVarianceOnGrowUp", true);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ClampAndSwapOnLoad();
                betaCacheDirty = true;
            }
        }

        // Edge Cases 1 & 9: clamp out-of-range sliders, swap inverted min/max ranges
        private void ClampAndSwapOnLoad()
        {
            averageQuality = Mathf.Clamp01(averageQuality);
            skillNoise = Mathf.Clamp01(skillNoise);
            traitNoise = Mathf.Clamp01(traitNoise);
            passionNoise = Mathf.Clamp01(passionNoise);

            if (skillShiftMin > skillShiftMax) { var t = skillShiftMin; skillShiftMin = skillShiftMax; skillShiftMax = t; }
            if (traitCountMin > traitCountMax) { var t = traitCountMin; traitCountMin = traitCountMax; traitCountMax = t; }
            if (passionCountMin > passionCountMax) { var t = passionCountMin; passionCountMin = passionCountMax; passionCountMax = t; }
        }

        // Edge Case 10: Write() from the settings window marks the Beta cache dirty; trait score bounds are untouched here (they rebuild only on mod-list change, in TraitDesirabilityCache)
        public void MarkDirtyOnWrite()
        {
            ClampAndSwapOnLoad();
            betaCacheDirty = true;
        }

        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            if (betaCacheDirty)
            {
                float m = Mathf.Clamp(averageQuality, Constants.QualityClampEpsilon, 1f - Constants.QualityClampEpsilon);
                cachedAlpha = m * Constants.BetaConcentrationK;
                cachedBeta = (1f - m) * Constants.BetaConcentrationK;
                betaCacheDirty = false;
            }
            alpha = cachedAlpha;
            beta = cachedBeta;
        }

        public void DoWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);

            listing.Label($"Average pawn quality: {averageQuality:F2}");
            averageQuality = listing.Slider(averageQuality, 0f, 1f);
            listing.Label($"Average pawn currently reads as: {TierUtility.TierForQuality(averageQuality)}");

            listing.Gap();
            listing.CheckboxLabeled("Enable skill variance", ref enableSkillVariance);
            listing.Label($"Skill noise: {skillNoise:F2}");
            skillNoise = listing.Slider(skillNoise, 0f, 1f);

            listing.Gap();
            listing.CheckboxLabeled("Enable trait variance", ref enableTraitVariance);
            listing.Label($"Trait noise: {traitNoise:F2}");
            traitNoise = listing.Slider(traitNoise, 0f, 1f);

            listing.Gap();
            listing.CheckboxLabeled("Enable passion variance", ref enablePassionVariance);
            listing.Label($"Passion noise: {passionNoise:F2}");
            passionNoise = listing.Slider(passionNoise, 0f, 1f);

            listing.Gap();
            listing.CheckboxLabeled("Apply to hostile-faction pawns", ref applyToHostilePawns);
            if (ModsConfig.BiotechActive)
                listing.CheckboxLabeled("Apply variance on grow-up (Biotech)", ref applyVarianceOnGrowUp);
            listing.CheckboxLabeled("Verbose logging (dev mode, rethrows exceptions)", ref verboseLogging);

            listing.End();
        }
    }
}
```

- [ ] **Step 3: Delete the temporary `PawnVarianceSettings` stub from Task 1 Step 5 (if created) and rebuild**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: build fails only on the missing `TierUtility.TierForQuality` reference (added in Task 11) — temporarily stub `TierUtility.TierForQuality(float) => "Standard"` in a throwaway file to confirm the rest compiles, then delete the stub once Task 11 lands.

- [ ] **Step 4: Manual verification**

Launch RimWorld with the mod active, open Mod Settings, confirm all sliders/checkboxes render and persist across a settings-window close/reopen (Testing Plan item 7 partially — full config-tampering test happens once load-time clamping is exercised via hand-edited XML in Task 12).

- [ ] **Step 5: Commit**

```bash
git add Source/Constants.cs Source/PawnVarianceSettings.cs
git commit -m "feat: add mod settings with clamp-on-load and Beta cache"
```

---

### Task 3: QualityRoller

**Files:**
- Create: `Source/QualityRoller.cs`

**Interfaces:**
- Consumes: `PawnVarianceMod.Settings.GetBetaAlphaBeta(out alpha, out beta)` (Task 2).
- Produces: `public static class QualityRoller { public static float RollQuality(); }` — the single entry point every applier and both postfixes call once per pawn.

- [ ] **Step 1: Write `Source/QualityRoller.cs`**

```csharp
using System;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class QualityRoller
    {
        public static float RollQuality()
        {
            PawnVarianceMod.Settings.GetBetaAlphaBeta(out float alpha, out float beta);
            float x = SampleGamma(alpha);
            float y = SampleGamma(beta);

            // Guard against both underflowing to exactly 0 in the same draw (0/0 = NaN)
            while (x == 0f && y == 0f)
            {
                x = SampleGamma(alpha);
                y = SampleGamma(beta);
            }

            float quality = x / (x + y);
            return Mathf.Clamp01(quality);
        }

        // Marsaglia-Tsang for shape >= 1; Stuart's-theorem boost trick for shape < 1.
        private static float SampleGamma(float shape)
        {
            if (shape < 1f)
            {
                float u = (float)Rand.Value;
                // u^(1/shape) can legitimately underflow to 0.0 for small shape — that's
                // treated as a valid extreme draw, not an error (Quality roll numerical-floor note).
                float boost = Mathf.Pow(u, 1f / shape);
                return SampleGammaShapeAtLeastOne(shape + 1f) * boost;
            }
            return SampleGammaShapeAtLeastOne(shape);
        }

        private static float SampleGammaShapeAtLeastOne(float shape)
        {
            float d = shape - 1f / 3f;
            float c = 1f / Mathf.Sqrt(9f * d);

            while (true)
            {
                float x, v;
                do
                {
                    x = NextGaussian();
                    v = 1f + c * x;
                } while (v <= 0f);

                v = v * v * v;
                float u = (float)Rand.Value;

                if (u < 1f - 0.0331f * x * x * x * x)
                    return d * v;
                if (Mathf.Log(u) < 0.5f * x * x + d * (1f - v + Mathf.Log(v)))
                    return d * v;
            }
        }

        private static float NextGaussian()
        {
            // Box-Muller
            float u1 = 1f - (float)Rand.Value; // avoid log(0)
            float u2 = (float)Rand.Value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds (no game dependency beyond `Verse.Rand`, which is available headless via a loaded RimWorld assembly reference — actual RNG calls still require the game running to verify output, next step).

- [ ] **Step 3: Manual verification (Testing Plan item 5 — extreme settings)**

In-game, with dev mode on: set "Average pawn quality" to 0.0, generate ~20 pawns via dev tools, confirm no crash/hang and that rolled quality values cluster near 0 with visibly narrower spread than at 0.5 (per the Quality roll note on fixed concentration). Repeat at 1.0. Repeat with quality at 0.001 and 0.999 to specifically exercise values near — but not at — the `ε` clamp boundary.

- [ ] **Step 4: Commit**

```bash
git add Source/QualityRoller.cs
git commit -m "feat: add Beta-distributed quality roller via Gamma sampling"
```

---

### Task 4: TraitDesirabilityCache

**Files:**
- Create: `Source/TraitDesirabilityCache.cs`

**Interfaces:**
- Consumes: `Constants.SkillOffsetReferenceMagnitude/StatReferenceMagnitude/WorkTagDisablePenalty/SocialReferenceMagnitude/ZMultiplier` (Task 2).
- Produces: `public static class TraitDesirabilityCache { public static float ScoreOf(TraitDef def, int degree); public static float ObservedMinScore; public static float ObservedMaxScore; public static float MeanScore; public static float StdDevScore; public static void Rebuild(); }` — consumed by `TraitVarianceApplier` (Task 6) and `TierUtility` (Task 11).

- [ ] **Step 1: Write `Source/TraitDesirabilityCache.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    [StaticConstructorOnStartup]
    public static class TraitDesirabilityCache
    {
        private static readonly Dictionary<(TraitDef, int), float> Scores = new Dictionary<(TraitDef, int), float>();

        public static float ObservedMinScore { get; private set; }
        public static float ObservedMaxScore { get; private set; }
        public static float MeanScore { get; private set; }
        public static float StdDevScore { get; private set; }

        static TraitDesirabilityCache()
        {
            Rebuild();
        }

        public static void Rebuild()
        {
            Scores.Clear();
            foreach (TraitDef def in DefDatabase<TraitDef>.AllDefsListForReading)
            {
                for (int degree = 0; degree < (def.degreeDatas?.Count ?? 1); degree++)
                {
                    float score = ScoreTrait(def, degree);
                    Scores[(def, degree)] = score;
                }
            }

            if (Scores.Count == 0)
            {
                ObservedMinScore = -1f;
                ObservedMaxScore = 1f;
                MeanScore = 0f;
                StdDevScore = 1f;
                return;
            }

            var values = Scores.Values.ToList();
            MeanScore = values.Average();
            float variance = values.Select(v => (v - MeanScore) * (v - MeanScore)).Average();
            StdDevScore = Mathf.Sqrt(variance);

            float rawMin = MeanScore - Constants.ZMultiplier * StdDevScore;
            float rawMax = MeanScore + Constants.ZMultiplier * StdDevScore;
            float trueMin = values.Min();
            float trueMax = values.Max();

            ObservedMinScore = Mathf.Max(rawMin, trueMin);
            ObservedMaxScore = Mathf.Min(rawMax, trueMax);
        }

        public static float ScoreOf(TraitDef def, int degree)
        {
            return Scores.TryGetValue((def, degree), out float score) ? score : 0f;
        }

        private static float ScoreTrait(TraitDef def, int degree)
        {
            TraitDegreeData data = def.degreeDatas != null && degree < def.degreeDatas.Count
                ? def.degreeDatas[degree]
                : null;
            if (data == null) return 0f;

            float skillCategory = 0f;
            if (data.skillGains != null)
            {
                float sum = data.skillGains.Values.Sum();
                skillCategory = Mathf.Clamp(sum / Constants.SkillOffsetReferenceMagnitude, -1f, 1f);
            }

            float statCategory = 0f;
            if (data.statOffsets != null || data.statFactors != null)
            {
                float sum = 0f;
                if (data.statOffsets != null)
                    sum += data.statOffsets.Sum(m => m.value);
                if (data.statFactors != null)
                    sum += data.statFactors.Sum(m => m.value - 1f);
                statCategory = Mathf.Clamp(sum / Constants.StatReferenceMagnitude, -1f, 1f);
            }

            float workTagCategory = 0f;
            if (data.disabledWorkTags != WorkTags.None)
            {
                int disabledCount = System.Enum.GetValues(typeof(WorkTags))
                    .Cast<WorkTags>()
                    .Count(tag => tag != WorkTags.None && (data.disabledWorkTags & tag) != 0);
                workTagCategory = Mathf.Clamp(-disabledCount * Constants.WorkTagDisablePenalty, -1f, 1f);
            }

            float socialCategory = 0f;
            if (data.socialFightChanceFactor != 1f)
            {
                socialCategory += (data.socialFightChanceFactor - 1f);
            }
            socialCategory = Mathf.Clamp(socialCategory / Constants.SocialReferenceMagnitude, -1f, 1f);

            return skillCategory + statCategory + workTagCategory + socialCategory;
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds. **Verify field names** (`degreeDatas`, `skillGains`, `statOffsets`, `statFactors`, `disabledWorkTags`, `socialFightChanceFactor`) against the target RimWorld version's decompiled `TraitDegreeData` before trusting this compiles against the real assembly — these are not in this plan's "flagged unverified" list because `TraitDegreeData`'s public fields are stable RimWorld API, but confirm regardless since this is the first task touching them.

- [ ] **Step 3: Manual verification (Testing Plan items 6 & startup sanity)**

Launch with dev mode, use a dev-tool console command (or a temporary `Log.Message` in `Rebuild()`) to print `ObservedMinScore`/`ObservedMaxScore`/`MeanScore`/`StdDevScore` and a handful of individual trait scores at startup; confirm vanilla traits like `Industrious` (positive skill gains) score positive and traits like `Lazy` (negative skill gains) score negative. Repeat with a trait-expansion mod loaded (Testing Plan item 6) and confirm modded traits get sane-looking non-zero scores without any code changes.

- [ ] **Step 4: Commit**

```bash
git add Source/TraitDesirabilityCache.cs
git commit -m "feat: add trait desirability cache with mod-agnostic scoring"
```

---

### Task 5: SkillVarianceApplier

**Files:**
- Create: `Source/SkillVarianceApplier.cs`

**Interfaces:**
- Consumes: `PawnVarianceMod.Settings` (Task 2), `float quality` (Task 3).
- Produces: `public static class SkillVarianceApplier { public static void Apply(Pawn pawn, float quality); }` — called by `HarmonyPatches` and `GrowthUpPatch` (Tasks 8-9). Mutates `pawn.skills.skills[i].levelInt` (or the appropriate public setter) in place.

- [ ] **Step 1: Write `Source/SkillVarianceApplier.cs`**

```csharp
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class SkillVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            float baseline = Mathf.Lerp(settings.skillShiftMin, settings.skillShiftMax, quality);
            float magnitude = Mathf.Lerp(Constants.MinMagnitudeFloor, Constants.MaxMagnitude, settings.skillNoise);

            foreach (SkillRecord record in pawn.skills.skills)
            {
                float noise = (TriangularSample() * 2f - 1f) * magnitude;
                int newLevel = Mathf.RoundToInt(record.Level + baseline + noise);
                record.Level = Mathf.Clamp(newLevel, 0, 20);
            }
        }

        // Average of two uniform rolls in [0,1] -> triangular distribution clustered near 0.5
        private static float TriangularSample()
        {
            return ((float)Rand.Value + (float)Rand.Value) / 2f;
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds. Confirm `SkillRecord.Level`'s setter exists and is public (RimWorld's `SkillRecord` typically exposes `Level` as a settable property backed by `levelInt`); if it's read-only in the target version, mutate `levelInt` directly via reflection or confirm the correct public API before this task is considered done.

- [ ] **Step 3: Manual verification (Testing Plan items 1 & 2)**

Generate ~30 pawns at quality 0.5 (default), confirm average skill shift is roughly 0 (parity with vanilla). Set skill noise to 0 and confirm skill levels cluster tightly around the quality-driven baseline without collapsing to a single deterministic value (noise floor is nonzero). Set skill noise to 1 and confirm visibly looser spread.

- [ ] **Step 4: Commit**

```bash
git add Source/SkillVarianceApplier.cs
git commit -m "feat: add skill variance applier"
```

---

### Task 6: TraitVarianceApplier

**Files:**
- Create: `Source/TraitVarianceApplier.cs`

**Interfaces:**
- Consumes: `TraitDesirabilityCache.ScoreOf/ObservedMinScore/ObservedMaxScore` (Task 4), `PawnVarianceMod.Settings` (Task 2).
- Produces: `public static class TraitVarianceApplier { public static void Apply(Pawn pawn, float quality); public static HashSet<TraitDef> CaptureForcedTraits(Pawn pawn); public static HashSet<TraitDef> CaptureDisallowedTraits(Pawn pawn); }` — the capture methods are also called directly by `GrowthUpPatch` (Task 9) for its own fresh re-derivation.

- [ ] **Step 1: Write `Source/TraitVarianceApplier.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class TraitVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;

            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality) + JitterSample()),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            HashSet<TraitDef> forced = CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = CaptureDisallowedTraits(pawn);

            pawn.story.traits.allTraits.Clear();
            foreach (TraitDef def in forced)
                pawn.story.traits.GainTrait(new Trait(def, 0, true));

            if (pawn.story.traits.allTraits.Count >= targetCount)
                return; // Accepted limitation: forced set alone can meet/exceed target — see Trait variance step 3.

            float target = Mathf.Lerp(TraitDesirabilityCache.ObservedMinScore, TraitDesirabilityCache.ObservedMaxScore, quality);
            float spread = Mathf.Lerp(Constants.MinSpreadFloor, Constants.MaxSpread, settings.traitNoise);

            List<TraitDef> eligible = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(def => !disallowed.Contains(def) && !forced.Contains(def))
                .ToList();

            while (pawn.story.traits.allTraits.Count < targetCount && eligible.Count > 0)
            {
                TraitDef picked = WeightedPick(eligible, target, spread, pawn);
                if (picked == null)
                {
                    Log.Message($"[PawnVarianceMod] Ran out of eligible traits for {pawn.LabelShort}, stopping at {pawn.story.traits.allTraits.Count}/{targetCount}.");
                    break;
                }
                pawn.story.traits.GainTrait(new Trait(picked, 0, false));
                eligible.Remove(picked);
            }
        }

        public static HashSet<TraitDef> CaptureForcedTraits(Pawn pawn)
        {
            var forced = new HashSet<TraitDef>();
            // PawnKindDef.forcedTraits: unverified field name, confirm at implementation time (Global Constraints).
            if (pawn.kindDef?.forcedTraits != null)
                foreach (var t in pawn.kindDef.forcedTraits) forced.Add(t.def);

            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (var gene in pawn.genes.GenesListForReading)
                    if (gene.def.forcedTraits != null)
                        foreach (var t in gene.def.forcedTraits) forced.Add(t.def);

            return forced;
        }

        public static HashSet<TraitDef> CaptureDisallowedTraits(Pawn pawn)
        {
            var disallowed = new HashSet<TraitDef>();
            if (pawn.kindDef?.disallowedTraits != null)
                foreach (var t in pawn.kindDef.disallowedTraits) disallowed.Add(t.def);

            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                foreach (var precept in pawn.Ideo.PreceptsListForReading)
                    if (precept.def.disallowedTraits != null)
                        foreach (var def in precept.def.disallowedTraits) disallowed.Add(def);

            return disallowed;
        }

        private static TraitDef WeightedPick(List<TraitDef> candidates, float target, float spread, Pawn pawn)
        {
            var weights = new List<(TraitDef def, float weight)>();
            float minDistSq = float.MaxValue;

            foreach (var def in candidates)
            {
                if (pawn.story.traits.HasTrait(def)) continue;
                if (ConflictsWithExisting(def, pawn)) continue;
                float score = TraitDesirabilityCache.ScoreOf(def, 0);
                float distSq = (score - target) * (score - target);
                if (distSq < minDistSq) minDistSq = distSq;
                weights.Add((def, distSq));
            }

            if (weights.Count == 0) return null;

            var finalWeights = weights.Select(w => (w.def, weight: Mathf.Exp(-(w.weight - minDistSq) / spread))).ToList();
            float total = finalWeights.Sum(w => w.weight);
            float roll = (float)Rand.Value * total;
            float cumulative = 0f;
            foreach (var (def, weight) in finalWeights)
            {
                cumulative += weight;
                if (roll <= cumulative) return def;
            }
            return finalWeights.Last().def;
        }

        // TraitDef.conflictingTraits/exclusionTags: unverified field names, confirm at implementation time (Global Constraints).
        private static bool ConflictsWithExisting(TraitDef def, Pawn pawn)
        {
            foreach (Trait existing in pawn.story.traits.allTraits)
            {
                if (def.conflictingTraits != null && def.conflictingTraits.Contains(existing.def)) return true;
                if (existing.def.conflictingTraits != null && existing.def.conflictingTraits.Contains(def)) return true;
                if (def.exclusionTags != null && existing.def.exclusionTags != null &&
                    def.exclusionTags.Intersect(existing.def.exclusionTags).Any()) return true;
            }
            return false;
        }

        private static float JitterSample()
        {
            return ((float)Rand.Value - 0.5f) * Constants.SmallRandomJitter;
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds, pending the field-name verifications flagged inline and in Global Constraints.

- [ ] **Step 3: Manual verification (Testing Plan items 1, 2, 3, 7)**

Generate ~30 pawns across the quality range, confirm trait count tracks the slider with no hard cutoff at trait noise 0 (tight-but-not-deterministic clustering around the quality-derived target score). Confirm a pawn with a kind-def-forced trait (e.g. a special quest pawn kind, if available) always keeps it regardless of quality. Hand-edit config XML with inverted trait-count min/max and confirm swap-on-load (Testing Plan item 7).

- [ ] **Step 4: Commit**

```bash
git add Source/TraitVarianceApplier.cs
git commit -m "feat: add trait variance applier with forced/disallowed handling"
```

---

### Task 7: PassionVarianceApplier

**Files:**
- Create: `Source/PassionVarianceApplier.cs`

**Interfaces:**
- Consumes: `pawn.skills` post-`SkillVarianceApplier.Apply` (Task 5), `PawnVarianceMod.Settings` (Task 2).
- Produces: `public static class PassionVarianceApplier { public static void Apply(Pawn pawn, float quality); }` — must run after both `TraitVarianceApplier.Apply` and `SkillVarianceApplier.Apply` in the same postfix invocation (Per-pawn flow step 4).

- [ ] **Step 1: Write `Source/PassionVarianceApplier.cs`**

```csharp
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class PassionVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;

            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.passionCountMin, settings.passionCountMax, quality) + JitterSample()),
                Mathf.RoundToInt(settings.passionCountMin),
                Mathf.RoundToInt(settings.passionCountMax));

            foreach (SkillRecord record in pawn.skills.skills)
                record.passion = Passion.None;

            var candidates = pawn.skills.skills.Where(r => !r.TotallyDisabled).ToList();
            if (candidates.Count == 0) return;

            float maxLevel = candidates.Max(r => r.Level);
            float temperature = Mathf.Lerp(Constants.MinTemperatureFloor, Constants.MaxTemperature, settings.passionNoise);

            var pool = new List<SkillRecord>(candidates);
            for (int i = 0; i < targetCount && pool.Count > 0; i++)
            {
                var weights = pool.Select(r => (r, weight: Mathf.Exp((r.Level - maxLevel) / temperature))).ToList();
                float total = weights.Sum(w => w.weight);
                float roll = (float)Rand.Value * total;
                float cumulative = 0f;
                SkillRecord picked = weights.Last().r;
                foreach (var (r, weight) in weights)
                {
                    cumulative += weight;
                    if (roll <= cumulative) { picked = r; break; }
                }

                // Minor/Major ratio logic: unverified vanilla internal, confirm at implementation time (Global Constraints).
                picked.passion = VanillaPassionRatio.RollMinorOrMajor();
                pool.Remove(picked);
            }
        }

        private static float JitterSample()
        {
            return ((float)Rand.Value - 0.5f) * Constants.SmallRandomJitter;
        }
    }

    // Placeholder wrapper isolating the unverified vanilla-ratio dependency so it's a one-line
    // swap once the real method is confirmed against decompiled source.
    internal static class VanillaPassionRatio
    {
        public static Passion RollMinorOrMajor()
        {
            return Rand.Value < 0.75f ? Passion.Minor : Passion.Major;
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds. Confirm `SkillRecord.TotallyDisabled` is a public property in the target version.

- [ ] **Step 3: Manual verification (Testing Plan items 1, 2, 3, 14)**

Confirm passion count tracks quality with default settings. With skill variance off and passion variance on, confirm passion count still tracks quality but placement looks uncorrelated with quality (expected scope limit, not a bug). Force (dev tools) a trait that disables a work tag covering the pawn's highest skill; confirm that skill never receives a passion (Testing Plan item 14 — validates trait-before-passion ordering once `HarmonyPatches` wires the call order in Task 8).

- [ ] **Step 4: Commit**

```bash
git add Source/PassionVarianceApplier.cs
git commit -m "feat: add passion variance applier"
```

---

### Task 8: Main Harmony postfix (HarmonyPatches)

**Files:**
- Create: `Source/HarmonyPatches.cs`

**Interfaces:**
- Consumes: `QualityRoller.RollQuality()` (Task 3), `TraitVarianceApplier.Apply`/`SkillVarianceApplier.Apply`/`PassionVarianceApplier.Apply` (Tasks 5-7), `PawnVarianceMod.Settings` (Task 2).
- Produces: the `[HarmonyPatch]` class Harmony's `PatchAll()` (Task 1) discovers and applies automatically. No other task calls this directly.

- [ ] **Step 1: Write `Source/HarmonyPatches.cs`**

```csharp
using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class GeneratePawn_Postfix
    {
        public static void Postfix(Pawn __result)
        {
            var settings = PawnVarianceMod.Settings;
            Pawn pawn = __result;

            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return;
            if (ModsConfig.BiotechActive && pawn.DevelopmentalStage != DevelopmentalStage.Adult) return;

            try
            {
                float quality = QualityRoller.RollQuality();

                // Ordering per Per-pawn flow step 4: trait, then skill, then passion.
                if (settings.enableTraitVariance) TraitVarianceApplier.Apply(pawn, quality);
                if (settings.enableSkillVariance) SkillVarianceApplier.Apply(pawn, quality);
                if (settings.enablePassionVariance) PassionVarianceApplier.Apply(pawn, quality);
            }
            catch (Exception ex)
            {
                if (settings.verboseLogging) throw;
                Log.ErrorOnce($"[PawnVarianceMod] Exception applying variance to {pawn.LabelShort}: {ex}", (ex.GetType().FullName + ex.StackTrace).GetHashCode());
            }
        }
    }
}
```

- [ ] **Step 2: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds. **Verify `PawnGenerator.GeneratePawn`'s exact signature and retry behavior** against decompiled source (Global Constraints) — the `[HarmonyPatch]` attribute's parameter-type array must match exactly or Harmony silently fails to patch.

- [ ] **Step 3: Manual verification (Testing Plan items 1, 3, 8, 11)**

Generate starting pawns, wanderers, prisoners, and (with a raid) hostile pawns with `applyToHostilePawns` on and off; confirm gating matches Edge Cases 3-5, 11. Spawn animals and confirm they're untouched (Edge Case 4 / Testing Plan item 8). With Biotech active, spawn a Baby/Child pawn and confirm no variance applies (Edge Case 11).

- [ ] **Step 4: Commit**

```bash
git add Source/HarmonyPatches.cs
git commit -m "feat: add main GeneratePawn postfix with gating and fail-safe"
```

---

### Task 9: GrowthUpPatch

**Files:**
- Create: `Source/GrowthUpPatch.cs`

**Interfaces:**
- Consumes: `QualityRoller.RollQuality()` (Task 3), `TraitVarianceApplier.CaptureForcedTraits/CaptureDisallowedTraits` (Task 6), `SkillVarianceApplier`/`PassionVarianceApplier` internals reimplemented inline per the compute-then-apply-per-step mitigation (Growth-up variance step 5) rather than reused wholesale, since growth-up's preserve-don't-remove semantics differ from generation-time's clear-and-rebuild.
- Produces: the `[HarmonyPatch]` class Harmony discovers automatically; an in-memory `HashSet<int>` of processed `thingIDNumber`s, never `Scribe`-serialized.

- [ ] **Step 1: Write `Source/GrowthUpPatch.cs`**

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Target method unverified — confirm the Biotech Child->Adult transition hook against
    // decompiled source before finalizing this attribute (Global Constraints).
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.DevelopmentalStage), MethodType.Setter)]
    public static class DevelopmentalStage_Postfix
    {
        private static readonly HashSet<int> Processed = new HashSet<int>();

        public static void Postfix(Pawn_AgeTracker __instance, Pawn ___pawn)
        {
            var settings = PawnVarianceMod.Settings;
            if (!settings.applyVarianceOnGrowUp) return;
            if (___pawn == null) return;
            if (___pawn.DevelopmentalStage != DevelopmentalStage.Adult) return; // defensive re-check, see Growth-up step 0
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

            if (Processed.Contains(___pawn.thingIDNumber)) return; // idempotency guard (Growth-up variance, Idempotency guard)
            Processed.Add(___pawn.thingIDNumber);

            try
            {
                float quality = QualityRoller.RollQuality();

                if (settings.enableSkillVariance) ApplySkillGrowthUp(___pawn, quality);
                if (settings.enableTraitVariance) ApplyTraitGrowthUp(___pawn, quality);
                if (settings.enablePassionVariance) ApplyPassionGrowthUp(___pawn, quality);
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception applying growth-up variance to {___pawn.LabelShort}: {ex}");
            }
        }

        // Skill: compute then immediately apply (step-scoped, not deferred across 2-4 — see
        // Growth-up variance step 5's mitigation).
        private static void ApplySkillGrowthUp(Pawn pawn, float quality)
        {
            SkillVarianceApplier.Apply(pawn, quality); // identical logic to generation-time; additive, so safe on accumulated childhood levels
        }

        private static void ApplyTraitGrowthUp(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            HashSet<TraitDef> forced = TraitVarianceApplier.CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = TraitVarianceApplier.CaptureDisallowedTraits(pawn);

            var alreadyAdded = new HashSet<TraitDef>(pawn.story.traits.allTraits.Select(t => t.def).Where(forced.Contains));

            foreach (TraitDef def in forced)
            {
                if (pawn.story.traits.HasTrait(def)) continue;

                bool disallowedToo = disallowed.Contains(def);
                var conflicting = pawn.story.traits.allTraits.FirstOrDefault(t => ConflictsWith(def, t.def));

                if (conflicting != null)
                {
                    if (forced.Contains(conflicting.def) || alreadyAdded.Contains(conflicting.def))
                    {
                        Log.Error($"[PawnVarianceMod] Forced-vs-forced trait conflict on {pawn.LabelShort}: {def.defName} conflicts with already-present forced trait {conflicting.def.defName}; skipping {def.defName}.");
                        continue;
                    }
                    pawn.story.traits.RemoveTrait(conflicting, true);
                    Log.Message($"[PawnVarianceMod] Removed growth-moment trait {conflicting.def.defName} on {pawn.LabelShort} to make room for newly-forced {def.defName}.");
                }

                if (disallowedToo)
                    Log.Error($"[PawnVarianceMod] {def.defName} is simultaneously forced and disallowed for {pawn.LabelShort}; forced wins.");

                pawn.story.traits.GainTrait(new Trait(def, 0, true));
                alreadyAdded.Add(def);
            }

            int currentCount = pawn.story.traits.allTraits.Count;
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality)),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            if (currentCount >= targetCount) return; // accepted limitation, see Growth-up variance closing paragraph

            // Fill remaining slots via the same weighted-sampling procedure as generation time,
            // excluding disallowed and respecting conflicts against traits already present.
            TraitVarianceApplier.FillRemainingSlots(pawn, quality, targetCount, disallowed);
        }

        private static bool ConflictsWith(TraitDef a, TraitDef b)
        {
            if (a.conflictingTraits != null && a.conflictingTraits.Contains(b)) return true;
            if (b.conflictingTraits != null && b.conflictingTraits.Contains(a)) return true;
            if (a.exclusionTags != null && b.exclusionTags != null && a.exclusionTags.Intersect(b.exclusionTags).Any()) return true;
            return false;
        }

        private static void ApplyPassionGrowthUp(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            int existingPassionCount = pawn.skills.skills.Count(r => r.passion != Passion.None);
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.passionCountMin, settings.passionCountMax, quality)),
                Mathf.RoundToInt(settings.passionCountMin),
                Mathf.RoundToInt(settings.passionCountMax));

            if (existingPassionCount >= targetCount) return;

            PassionVarianceApplier.AddPassionsWithoutClearing(pawn, targetCount - existingPassionCount);
        }
    }
}
```

- [ ] **Step 2: Add `FillRemainingSlots` and `AddPassionsWithoutClearing` to the appliers from Tasks 6-7**

Refactor `TraitVarianceApplier.Apply` (Task 6) to extract its weighted-sampling loop into `public static void FillRemainingSlots(Pawn pawn, float quality, int targetCount, HashSet<TraitDef> disallowed)`, called by both `Apply` and `GrowthUpPatch`. Refactor `PassionVarianceApplier.Apply` (Task 7) to extract its placement loop into `public static void AddPassionsWithoutClearing(Pawn pawn, int countToAdd)` that excludes skills already carrying a passion (in addition to the existing `TotallyDisabled` exclusion), called by both `Apply` (after its unconditional clear) and `GrowthUpPatch` (without any clear).

- [ ] **Step 3: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds pending confirmation of the actual Child→Adult transition hook (the `Pawn_AgeTracker.DevelopmentalStage` setter is a plausible guess, not a confirmed target — Global Constraints).

- [ ] **Step 4: Manual verification (Testing Plan item 10, 12, 15)**

With Biotech active: spawn/birth a Child, confirm no variance while developing; age to Adult with `applyVarianceOnGrowUp` on, confirm variance applies once without removing growth-moment traits/passions. Repeat with the setting off, confirm permanent vanilla. Force the hook to fire twice (repeated save-reload at the boundary, if feasible) and confirm no double-shift. Test the forced-vs-forced and forced-vs-disallowed conflict paths per Testing Plan item 10's added clauses. Run Testing Plan item 15 (quest-forced trait on a Baby/Child pawn) to empirically resolve the flagged `PawnGenerationRequest.ForcedTraits` question.

- [ ] **Step 5: Commit**

```bash
git add Source/GrowthUpPatch.cs Source/TraitVarianceApplier.cs Source/PassionVarianceApplier.cs
git commit -m "feat: add Biotech growth-up variance patch"
```

---

### Task 10: TierUtility + TierLabelPatch

**Files:**
- Create: `Source/TierUtility.cs`
- Create: `Source/TierLabelPatch.cs`

**Interfaces:**
- Consumes: `TraitDesirabilityCache.ScoreOf/ObservedMinScore/ObservedMaxScore` (Task 4), `PawnVarianceMod.Settings` (Task 2).
- Produces: `public static class TierUtility { public static string TierForQuality(float quality); public static string EffectiveTierFor(Pawn pawn); public static bool IsEligibleForLabel(Pawn pawn); }` — `TierForQuality` is already consumed by `PawnVarianceSettings.DoWindowContents` (Task 2); `EffectiveTierFor`/`IsEligibleForLabel` are consumed by `TierLabelPatch` here.

- [ ] **Step 1: Write `Source/TierUtility.cs`**

```csharp
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class TierUtility
    {
        // Fixed, ascending, not user-configurable in v1 (Settings Schema, Tier display thresholds).
        private const float IncompetentMax = 0.2f;
        private const float StandardMax = 0.5f;
        private const float SpecialistMax = 0.8f;

        public static string TierForQuality(float quality)
        {
            if (quality < IncompetentMax) return "Incompetent";
            if (quality < StandardMax) return "Standard";
            if (quality < SpecialistMax) return "Specialist";
            return "Prodigy";
        }

        public static string ColorFor(string tier)
        {
            switch (tier)
            {
                case "Incompetent": return "#B33A3A";
                case "Specialist": return "#3A7CB3";
                case "Prodigy": return "#D4AF37";
                default: return null; // Standard: no color tag
            }
        }

        public static bool IsEligibleForLabel(Pawn pawn)
        {
            var settings = PawnVarianceMod.Settings;
            if (pawn == null || !pawn.RaceProps.Humanlike) return false;
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return false;
            if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return false;
            if (ModsConfig.BiotechActive && pawn.DevelopmentalStage != DevelopmentalStage.Adult) return false;
            return true;
        }

        public static float EffectiveQualityFor(Pawn pawn)
        {
            float skillComponent = SkillComponent(pawn);
            float traitComponent = TraitComponent(pawn);
            return (skillComponent + traitComponent) / 2f;
        }

        public static string EffectiveTierFor(Pawn pawn)
        {
            return TierForQuality(EffectiveQualityFor(pawn));
        }

        private static float SkillComponent(Pawn pawn)
        {
            var settings = PawnVarianceMod.Settings;
            float lower = Mathf.Max(0f, settings.skillShiftMin + Constants.AssumedVanillaSkillBaseline);
            float upper = Mathf.Min(20f, settings.skillShiftMax + Constants.AssumedVanillaSkillBaseline);

            if (Mathf.Approximately(lower, upper)) return 0.5f; // degenerate-range guard, see Tier bio label

            float avgLevel = pawn.skills.skills.Count > 0 ? (float)pawn.skills.skills.Average(r => r.Level) : 0f;
            return Mathf.Clamp01(Mathf.InverseLerp(lower, upper, avgLevel));
        }

        private static float TraitComponent(Pawn pawn)
        {
            if (pawn.story.traits.allTraits.Count == 0) return 0.5f;
            float avgScore = (float)pawn.story.traits.allTraits.Average(t => TraitDesirabilityCache.ScoreOf(t.def, t.Degree));
            if (Mathf.Approximately(TraitDesirabilityCache.ObservedMinScore, TraitDesirabilityCache.ObservedMaxScore)) return 0.5f;
            return Mathf.Clamp01(Mathf.InverseLerp(TraitDesirabilityCache.ObservedMinScore, TraitDesirabilityCache.ObservedMaxScore, avgScore));
        }
    }
}
```

- [ ] **Step 2: Write `Source/TierLabelPatch.cs`**

```csharp
using HarmonyLib;
using Verse;

namespace PawnVarianceMod
{
    // Target method unverified — confirm the bio-description generator against decompiled
    // source, and confirm it's distinct from the inspect-string method (Global Constraints).
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDescription))]
    public static class GetDescription_Postfix
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!TierUtility.IsEligibleForLabel(__instance)) return;

            string tier = TierUtility.EffectiveTierFor(__instance);
            string color = TierUtility.ColorFor(tier);

            string labelText = color != null
                ? $"<color={color}>{tier}</color>"
                : tier;

            __result += $"\n\n{labelText}";
        }
    }
}
```

- [ ] **Step 3: Verify build**

```bash
dotnet build Source/PawnVarianceMod.csproj
```

Expected: succeeds pending confirmation of `Pawn.GetDescription()` as the correct, non-inspect-string bio target.

- [ ] **Step 4: Manual verification (Testing Plan items 9, 13)**

Open several pawns' bio tabs, confirm the label appears with correct color and roughly matches their stats. Edit a pawn's skills via dev tools, reopen the bio tab, confirm the label updates live. Confirm no label for: a raider with `applyToHostilePawns` off, a pawn with all three toggles off, a Baby/Child pawn. Change "Skill shift range" mid-save and confirm an unchanged pawn's tier can shift (Testing Plan item 13). Set skill shift min == max and confirm no crash/NaN (degenerate-range guard).

- [ ] **Step 5: Commit**

```bash
git add Source/TierUtility.cs Source/TierLabelPatch.cs
git commit -m "feat: add cosmetic tier bio label"
```

---

### Task 11: Full manual verification pass

**Files:** none (verification only).

**Interfaces:** none — this task exercises the fully-assembled mod end to end.

- [ ] **Step 1: Run Testing Plan items 1-15 in full**, in order, against a real RimWorld install with the mod (and Harmony, and optionally Biotech/Ideology/a trait-expansion mod) active. Use the spec's Testing Plan (`docs/superpowers/specs/2026-07-27-pawn-variance-mod-design.md`, "Testing Plan" section) as the authoritative checklist — each item maps to functionality built in Tasks 3-10 above.

- [ ] **Step 1b: Compatibility and performance check**. Confirm (via `Harmony.GetAllPatchedMethods()` in the dev console, or a `Log.Message` dump at startup) that this mod's patches are exactly the three postfixes from Global Constraints, all `Postfix`, none `Prefix`/`Transpiler`. Load alongside 1-2 other trait/skill-affecting mods (e.g. a trait-expansion mod) and confirm no exceptions or missing-patch warnings from Harmony's conflict detection. Generate a large batch (~100) of pawns in one dev-tool call and time it against the same batch with this mod disabled; if generation time visibly increases, profile `WeightedPick`/`TraitDesirabilityCache.Rebuild`/the softmax loop and replace LINQ chains with plain loops per the Performance constraint above.

- [ ] **Step 2: File a follow-up note for every "unverified" item from Global Constraints** that turned out to require a code change (wrong method name, wrong field, different vanilla behavior than assumed) rather than just confirming the plan's guess was right.

- [ ] **Step 3: Commit any fixes discovered during verification**

```bash
git add -A
git commit -m "fix: address issues found during manual verification pass"
```
