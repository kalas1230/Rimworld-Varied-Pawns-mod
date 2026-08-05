using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Identifies standard preset quality profiles. Custom profiles are dynamically stored
    // in PawnVarianceSettings.customProfiles using dynamic string IDs.
    public enum VarianceProfileId
    {
        Custom = 0,
        VanillaLike = 1,
        BalancedVariance = 2,
        WildSpread = 3,
        Hardscrabble = 5,
        Elite = 6,
        Sovereign = 7,
        Specialist = 8,
        Scavenger = 9,
    }

    // The generation-tuning half of the settings — everything a profile decides. Deliberately does
    // not include the housekeeping toggles (hostile pawns, grow-up, logging, tier tooltip): those
    // are player preferences that should survive a profile switch, not balance choices.
    public class VarianceProfileValues : IExposable
    {
        public float averageQuality = 0.5f;
        public float skillNoise = 0.35f;
        public float passionNoise = 0.35f;
        public float passionMajorBias = 0.8f;
        public float skillShiftMin = -4f;
        public float skillShiftMax = 6f;

        // Skills at the age-13 growth moment are a separate knob because vanilla treats them
        // separately: the trait and passion passes at 13 top a pawn up and never subtract, but a
        // child's skill levels are a play record, not a roll — vanilla's only skill change at a
        // growth moment is the chosen trait's additive skillGains. Re-shifting all twelve skills
        // there is the one place this mod invents randomness vanilla doesn't have, so it is
        // off by default and given its own range rather than borrowing the adult one.
        public bool applyChildSkillShift = false;
        public float childSkillShiftMin = -1f;
        public float childSkillShiftMax = 2f;

        public float traitCountMin = 1f;
        public float traitCountMax = 6f;
        public float passionCountMin = 1f;
        public float passionCountMax = 7f;
        public bool enableSkillVariance = true;
        public bool enableTraitVariance = true;
        public bool enablePassionVariance = true;
        public bool countProtectedTraits = true;

        // Display name of the profile these values were resolved from. Diagnostic only, never
        // scribed: without it a verbose trace cannot show WHICH profile produced a pawn, which is
        // the only way to confirm in-game that hostiles are really coming from the hostile profile.
        public string profileLabel = "?";

        // The Beta shape derived from averageQuality. Cached here rather than on the settings object
        // because there is now more than one live set of values at a time — the main profile and the
        // hostile-faction profile — and a single shared cache would hand one profile's shape to the
        // other's rolls.
        private bool distributionParamsDirty = true;
        private float cachedAlpha, cachedBeta;

        public void MarkDistributionParamsDirty() => distributionParamsDirty = true;

        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            if (distributionParamsDirty)
            {
                float m = Mathf.Clamp(averageQuality, Constants.QualityClampEpsilon, 1f - Constants.QualityClampEpsilon);
                cachedAlpha = m * Constants.BetaConcentrationK;
                cachedBeta = (1f - m) * Constants.BetaConcentrationK;
                distributionParamsDirty = false;
            }
            alpha = cachedAlpha;
            beta = cachedBeta;
        }

        public VarianceProfileValues Clone()
        {
            var copy = (VarianceProfileValues)MemberwiseClone();
            copy.distributionParamsDirty = true; // never inherit a cache entry that was computed for another instance's edits
            return copy;
        }

        // Edge Cases 1 & 9: clamp out-of-range sliders, swap inverted min/max ranges
        public void ClampAndSwap()
        {
            averageQuality = Mathf.Clamp01(averageQuality);
            skillNoise = Mathf.Clamp01(skillNoise);
            passionNoise = Mathf.Clamp01(passionNoise);
            passionMajorBias = Mathf.Clamp01(passionMajorBias);

            if (skillShiftMin > skillShiftMax) { var t = skillShiftMin; skillShiftMin = skillShiftMax; skillShiftMax = t; }
            if (childSkillShiftMin > childSkillShiftMax) { var t = childSkillShiftMin; childSkillShiftMin = childSkillShiftMax; childSkillShiftMax = t; }
            if (traitCountMin > traitCountMax) { var t = traitCountMin; traitCountMin = traitCountMax; traitCountMax = t; }
            if (passionCountMin > passionCountMax) { var t = passionCountMin; passionCountMin = passionCountMax; passionCountMax = t; }

            // The budget is spent in pips (Minor 1, Major 1.5) and each of the 12 skills can hold
            // at most one passion, so 12 x 1.5 = MaxPassionPips is the most that can ever be spent
            // -- and only at passionMajorBias 1.0. Anything above it is unspendable by
            // construction: PassionVarianceApplier converts the budget to Major/Minor counts
            // before it knows how many skills exist, then silently discards whatever will not fit.
            //
            // Clamped here as well as bounded on the slider because the slider only guards NEW
            // input. Settings saved before this bound existed -- the editor allowed up to 24, an
            // orphan of the era when the UI believed a Major cost 2 -- and profiles arriving
            // through SettingsTransfer's import both reach these fields without passing a widget.
            passionCountMin = Mathf.Clamp(passionCountMin, 0f, Constants.MaxPassionPips);
            passionCountMax = Mathf.Clamp(passionCountMax, 0f, Constants.MaxPassionPips);

            distributionParamsDirty = true;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref averageQuality, "averageQuality", 0.5f);
            Scribe_Values.Look(ref skillNoise, "skillNoise", 0.2f);
            Scribe_Values.Look(ref passionNoise, "passionNoise", 0.25f);
            Scribe_Values.Look(ref passionMajorBias, "passionMajorBias", 0.5f);
            Scribe_Values.Look(ref skillShiftMin, "skillShiftMin", -3f);
            Scribe_Values.Look(ref skillShiftMax, "skillShiftMax", 3f);
            Scribe_Values.Look(ref applyChildSkillShift, "applyChildSkillShift", false);
            Scribe_Values.Look(ref childSkillShiftMin, "childSkillShiftMin", -1f);
            Scribe_Values.Look(ref childSkillShiftMax, "childSkillShiftMax", 2f);
            Scribe_Values.Look(ref traitCountMin, "traitCountMin", 2f);
            Scribe_Values.Look(ref traitCountMax, "traitCountMax", 3f);
            Scribe_Values.Look(ref passionCountMin, "passionCountMin", 2f);
            Scribe_Values.Look(ref passionCountMax, "passionCountMax", 6f);
            Scribe_Values.Look(ref enableSkillVariance, "enableSkillVariance", true);
            Scribe_Values.Look(ref enableTraitVariance, "enableTraitVariance", true);
            Scribe_Values.Look(ref enablePassionVariance, "enablePassionVariance", true);
            Scribe_Values.Look(ref countProtectedTraits, "countProtectedTraits", true);
            distributionParamsDirty = true;
        }
    }

    public class CustomProfile : IExposable, IRenameable
    {
        public string id;
        public string name;
        public VarianceProfileValues values = new VarianceProfileValues();

        public string RenamableLabel
        {
            get => name;
            set => name = value;
        }

        public string BaseLabel => name;
        public string InspectLabel => name;

        public CustomProfile() { }

        public CustomProfile(string id, string name, VarianceProfileValues values)
        {
            this.id = id;
            this.name = name;
            if (values != null) this.values = values;
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref id, "id");
            Scribe_Values.Look(ref name, "name", "Custom Profile");
            if (Scribe.mode == LoadSaveMode.LoadingVars && values == null)
                values = new VarianceProfileValues();
            values.ExposeData();
        }

        public CustomProfile Clone(string newId, string newName)
        {
            return new CustomProfile(newId, newName, values.Clone());
        }
    }

    public class VarianceProfile
    {
        public readonly VarianceProfileId id;
        public readonly string stringId;
        public readonly string label;
        public readonly string description;
        private readonly VarianceProfileValues values;

        public VarianceProfile(VarianceProfileId id, string stringId, string label, string description, VarianceProfileValues values)
        {
            this.id = id;
            this.stringId = stringId;
            this.label = label;
            this.description = description;
            this.values = values;
        }

        // Always a copy: callers write into what they get back, and a preset must stay pristine.
        public VarianceProfileValues MakeValues() => values.Clone();
    }

	// In the current implementation preset profiles differ by +-35% at most from the faithful profile
    public static class VarianceProfiles
    {
        public const string FaithfulId = "preset_faithful";
        public const string DistinctId = "preset_distinct";
        public const string WildcardId = "preset_wildcard";
        public const string DesperateId = "preset_desperate";
        public const string EliteId = "preset_elite";
        public const string SovereignId = "preset_sovereign";
        public const string SpecialistId = "preset_specialist";
        public const string ScavengerId = "preset_scavenger";

        // Sits as close to unmodded generation as the mod's knobs allow: pawns still vary, but the
        // trait count, passion budget and skill spread stay inside vanilla's own envelope.
        public static readonly VarianceProfile VanillaLike = new VarianceProfile(
            VarianceProfileId.VanillaLike,
            FaithfulId,
            "Faithful",
            "Closest to unmodded RimWorld. Two to three traits, a vanilla passion budget, and a narrow skill spread.",
            new VarianceProfileValues
            {
                averageQuality = 0.5f,
                skillNoise = 0.2f,
                passionNoise = 0.25f,
                passionMajorBias = 0.5f,
                skillShiftMin = -3f,
                skillShiftMax = 3f,
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
                traitCountMax = 3f,
                passionCountMin = 2f,
                passionCountMax = 6f,
            });

        // The mod's original intended values for randomness, before it got to where it is with multiple
		// profiles, settings, and all the other stuff.
        public static readonly VarianceProfile BalancedVariance = new VarianceProfile(
            VarianceProfileId.BalancedVariance,
            DistinctId,
            "Distinct",
            "The mod's signature tuning. Pawns have strong individual strengths and weaknesses while maintaining a fair colony average.",
            new VarianceProfileValues
            {
                averageQuality = 0.32f,
                skillNoise = 0.35f,
                passionNoise = 0.35f,
                passionMajorBias = 0.8f,
                skillShiftMin = -3.3f,
                skillShiftMax = 6.5f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 3f,
                // Was 1-6. Narrowed purely to cut hazard exposure (22.2% -> 15.4% chance of a
                // trait that can trigger uncontrolled behaviour); score is unaffected since trait
                // count no longer feeds CalculateCompositeScore. Individual distinctiveness comes
                // from the skill/passion spread, which is untouched.
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 1.4f,
                passionCountMax = 7.2f,
            });

        public static readonly VarianceProfile WildSpread = new VarianceProfile(
            VarianceProfileId.WildSpread,
            WildcardId,
            "Wildcard",
            "Maximum variation. Pawns can arrive with 0 to 8 traits, zero or many passions, and wide skill swings.",
            new VarianceProfileValues
            {
                averageQuality = 0.37f,
                skillNoise = 0.85f,
                passionNoise = 0.85f,
                passionMajorBias = 0.6f,
                // Retuned 2026-08-04: narrowed to ~0.78x its previous dispersion, which pulls
                // Best-of-25 from +27.1% to +17.3% and Best-of-50 from a near-breach +33.1% to
                // +21.5%. Narrowing raises N=1 (-23.6% -> -18.1%) and lowers N=25 at the same
                // time, so it buys headroom at both ends. Still by far the widest preset -- it is
                // a variance preset, not a power tier, so it legitimately crosses Faithful as N
                // rises; it just may not leave the +-35% band.
                skillShiftMin = -8.7f,
                skillShiftMax = 4.2f,
                childSkillShiftMin = -5f,
                childSkillShiftMax = 6f,
                traitCountMin = 0f,
                traitCountMax = 8f,   // deliberately left wide: chaos is this preset's whole point
                passionCountMin = 1.2f,
                passionCountMax = 9.8f,
            });

        public static readonly VarianceProfile Hardscrabble = new VarianceProfile(
            VarianceProfileId.Hardscrabble,
            DesperateId,
            "Desperate",
            "Scraped together survivors. Low skills, few passions, and poor rolls are common.",
            new VarianceProfileValues
            {
                averageQuality = 0.37f,
                skillNoise = 0.25f,
                passionNoise = 0.25f,
                passionMajorBias = 0.35f,
                // Retuned 2026-08-04: translated up to -20.6% at Best-of-25 (was -27.3%), which
                // also lifts N=1 from a very tight -33.2% to -24.2%. This preset had only 1.8pp
                // of envelope headroom and was the single tightest number in the whole set.
                // It remains the lowest power tier by a clear margin at every N.
                skillShiftMin = -2.8f,
                skillShiftMax = 2.1f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 1f,
                traitCountMin = 2f,   // was 1: floor raised to vanilla's, cuts hazard exposure
                traitCountMax = 4f,
                passionCountMin = 1.7f,
                passionCountMax = 5.3f,
            });

        public static readonly VarianceProfile Elite = new VarianceProfile(
            VarianceProfileId.Elite,
            EliteId,
            "Elite",
            "Refined imperial nobility and high-born pawns. Consistently high capability and polished skills.",
            new VarianceProfileValues
            {
                averageQuality = 0.53f,
                skillNoise = 0.22f,
                passionNoise = 0.25f,
                passionMajorBias = 0.65f,
                skillShiftMin = -0.8f,
                skillShiftMax = 4.0f,
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 2.6f,
                passionCountMax = 6.3f,
            });

        public static readonly VarianceProfile Sovereign = new VarianceProfile(
            VarianceProfileId.Sovereign,
            SovereignId,
            "Sovereign",
            "Archite lords, Sanguophages, and supreme leaders. Top-tier skill growth and wide passions.",
            new VarianceProfileValues
            {
                averageQuality = 0.55f,
                skillNoise = 0.24f,
                passionNoise = 0.25f,
                passionMajorBias = 0.70f,
                // Retuned 2026-08-04 to +18.9% at Best-of-25 (was +16.2%). The skill range is
                // deliberately UNCHANGED -- skillShiftMin stays at 0 to keep the preset's mean band
                // at or above the vanilla baseline, which is its identity. NB this is a bound on
                // the BAND, not a per-skill guarantee: SkillVarianceApplier.Apply adds an unclamped
                // noise term (magnitude ~1.8 at skillNoise 0.24) on top of the band, so an
                // individual skill on a low-quality roll can still land below vanilla's level. The
                // entire increase comes from widening the passion budget (3.0-6.2 -> 2.2-6.6).
                // Translating the whole profile up instead would have hit +34.5% at N=1, leaving
                // 0.5pp of headroom; this shape lands at +28.5% with 6.5pp, better than before.
                skillShiftMin = 0f,
                skillShiftMax = 3.85f,
                childSkillShiftMin = 0f,
                childSkillShiftMax = 3f,
                traitCountMin = 2f,
                traitCountMax = 4f,   // was 5: cuts hazard exposure 18.9% -> 15.4%, matches Elite
                passionCountMin = 2.2f,
                passionCountMax = 6.6f,
            });

        public static readonly VarianceProfile Specialist = new VarianceProfile(
            VarianceProfileId.Specialist,
            SpecialistId,
            "Specialist",
            "Engineered single-domain specialists (Genies, Hussars). Focused skill spikes with domain passions.",
            new VarianceProfileValues
            {
                averageQuality = 0.50f,
                skillNoise = 0.25f,
                passionNoise = 0.25f,
                passionMajorBias = 0.60f,
                skillShiftMin = -1.8f,
                skillShiftMax = 3.7f,
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 2.1f,
                passionCountMax = 6.1f,
            });

        public static readonly VarianceProfile Scavenger = new VarianceProfile(
            VarianceProfileId.Scavenger,
            ScavengerId,
            "Scavenger",
            "Wasteland survivors, pirates, and scavengers. Lower baseline skills with tough survival rolls.",
            new VarianceProfileValues
            {
                averageQuality = 0.43f,
                skillNoise = 0.25f,
                passionNoise = 0.25f,
                passionMajorBias = 0.45f,
                skillShiftMin = -2.9f,
                skillShiftMax = 2.6f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 1f,
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 1.8f,
                passionCountMax = 5.3f,
            });

        // Display order in the dropdown. Custom is not in here — it is not a recipe, it is wherever
        // the player's own sliders are, and it is handled separately by the settings UI.
        public static readonly List<VarianceProfile> Presets = new List<VarianceProfile>
        {
            VanillaLike,
            BalancedVariance,
            WildSpread,
            Hardscrabble,
            Elite,
            Sovereign,
            Specialist,
            Scavenger,
        };

        public const string CustomDescription = "Editable custom profile. Adjust sliders below to customize pawn generation.";

        public static bool IsCustom(VarianceProfileId id) => GetPreset(id) == null;


        public static VarianceProfile GetPreset(VarianceProfileId id)
        {
            foreach (var p in Presets)
                if (p.id == id) return p;
            return null;
        }

        public static VarianceProfile GetPresetById(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            foreach (var p in Presets)
                if (p.stringId == id || p.id.ToString() == id) return p;
            return null;
        }

        public static string DescriptionFor(VarianceProfileId id) => GetPreset(id)?.description ?? CustomDescription;
        public static string DescriptionFor(string id) => GetPresetById(id)?.description ?? CustomDescription;

        // Fresh installs (and "reset Custom") start from Faithful, per design: the neutral profile is
        // the one a new player is least surprised by.
        public static VarianceProfileValues NewCustomDefaults() => VanillaLike.MakeValues();
    }
}
