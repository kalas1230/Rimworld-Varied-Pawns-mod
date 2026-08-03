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
        GiftedColony = 4,
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
        public bool countProtectedTraits = false;

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
            Scribe_Values.Look(ref countProtectedTraits, "countProtectedTraits", false);
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
        public const string GiftedId = "preset_gifted";
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
                skillShiftMin = -4f,
                skillShiftMax = 6f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 3f,
                // Was 1-6. Narrowed purely to cut hazard exposure (22.2% -> 15.4% chance of a
                // trait that can trigger uncontrolled behaviour); score is unaffected since trait
                // count no longer feeds CalculateCompositeScore. Individual distinctiveness comes
                // from the skill/passion spread, which is untouched.
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 1f,
                passionCountMax = 7f,
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
                // Trimmed from -12/+7 and 12 passions: under Best-of-N its dispersion put it at
                // +38% vs Faithful at N=50 (outside the +-35% envelope) while sitting at -26% at
                // N=1. Still by far the widest preset — it is a variance preset, not a power tier,
                // so it legitimately crosses Faithful as N rises; it just may not leave the band.
                skillShiftMin = -10.5f,
                skillShiftMax = 6f,
                childSkillShiftMin = -5f,
                childSkillShiftMax = 6f,
                traitCountMin = 0f,
                traitCountMax = 8f,   // deliberately left wide: chaos is this preset's whole point
                passionCountMin = 0f,
                passionCountMax = 11f,
            });

        public static readonly VarianceProfile GiftedColony = new VarianceProfile(
            VarianceProfileId.GiftedColony,
            GiftedId,
            "Gifted",
            "Higher capability. Everyone skews talented and passionate with strong recruits and skilled raiders.",
            new VarianceProfileValues
            {
                averageQuality = 0.72f,
                skillNoise = 0.35f,
                passionNoise = 0.4f,
                passionMajorBias = 0.9f,
                skillShiftMin = 0f,
                skillShiftMax = 8f,
                childSkillShiftMin = 0f,
                childSkillShiftMax = 4f,
                traitCountMin = 1f,
                traitCountMax = 5f,
                passionCountMin = 5f,
                passionCountMax = 12f,
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
                // Raised from -4.5 skill floor and 1/4.5 passions: traits-free scoring put this at
                // -45% vs Faithful at N=1, well outside the -35% envelope. The old numbers only
                // looked acceptable because the trait term was propping it up (its traitNorm was
                // its single best component). It remains the lowest power tier by a clear margin.
                skillShiftMin = -3.4f,
                skillShiftMax = 1.5f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 1f,
                traitCountMin = 2f,   // was 1: floor raised to vanilla's, cuts hazard exposure
                traitCountMax = 4f,
                passionCountMin = 1.4f,
                passionCountMax = 5f,
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
                skillShiftMin = -1f,
                skillShiftMax = 3.8f,
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 2.5f,
                passionCountMax = 6.2f,
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
                // Trimmed from 4.2 skill / 6.5 passions: was +36.4% vs Faithful at N=1, just outside
                // the +35% envelope. Still the top power tier at every batch size.
                skillShiftMin = 0f,
                skillShiftMax = 3.85f,
                childSkillShiftMin = 0f,
                childSkillShiftMax = 3f,
                traitCountMin = 2f,
                traitCountMax = 4f,   // was 5: cuts hazard exposure 18.9% -> 15.4%, matches Elite
                passionCountMin = 3.0f,
                passionCountMax = 6.2f,
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
                skillShiftMin = -2f,
                skillShiftMax = 3.5f,
                childSkillShiftMin = -1f,
                childSkillShiftMax = 2f,
                traitCountMin = 2f,
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
                skillNoise = 0.25f,
                passionNoise = 0.25f,
                passionMajorBias = 0.45f,
                skillShiftMin = -3.5f,
                skillShiftMax = 2.0f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 1f,
                traitCountMin = 2f,
                traitCountMax = 4f,
                passionCountMin = 1.5f,
                passionCountMax = 5.0f,
            });

        // Display order in the dropdown. Custom is not in here — it is not a recipe, it is wherever
        // the player's own sliders are, and it is handled separately by the settings UI.
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
