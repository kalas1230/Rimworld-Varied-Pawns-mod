using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Which profile a set of generation settings is driven by. The three Custom slots are the only
    // ones whose values the player can edit; the rest are read-only recipes.
    //
    // Two names here are load-bearing and must not be renamed. Scribe_Values writes an enum as its
    // member NAME, not its number, so renaming a member silently orphans every settings file that
    // holds it. `Custom` (value 0) in particular is what a settings file written before profiles
    // existed — where the node is missing entirely — falls back to, which is how those files keep
    // the values they already had. Custom2/Custom3 were added later and take fresh numbers.
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

    // The generation-tuning half of the settings — everything a profile decides. Deliberately does
    // not include the housekeeping toggles (hostile pawns, grow-up, logging, tier tooltip): those
    // are player preferences that should survive a profile switch, not balance choices.
    public class VarianceProfileValues
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
        private bool betaDirty = true;
        private float cachedAlpha, cachedBeta;

        public void MarkBetaDirty() => betaDirty = true;

        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            if (betaDirty)
            {
                float m = Mathf.Clamp(averageQuality, Constants.QualityClampEpsilon, 1f - Constants.QualityClampEpsilon);
                cachedAlpha = m * Constants.BetaConcentrationK;
                cachedBeta = (1f - m) * Constants.BetaConcentrationK;
                betaDirty = false;
            }
            alpha = cachedAlpha;
            beta = cachedBeta;
        }

        public VarianceProfileValues Clone()
        {
            var copy = (VarianceProfileValues)MemberwiseClone();
            copy.betaDirty = true; // never inherit a cache entry that was computed for another instance's edits
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
            betaDirty = true;
        }

        // The first custom slot passes an empty prefix, so its node names are the pre-profile field
        // names verbatim: an existing settings file scribed before profiles existed loads straight
        // into custom slot 1 and nobody's tuning is lost on update. Slots 2 and 3 were added later
        // and carry a prefix, which is also what keeps three sets of identically-named values from
        // colliding in one flat Scribe node. Defaults must stay in sync with
        // VarianceProfiles.VanillaLike, which is what a fresh install starts every slot out as.
        public void ExposeData(string prefix)
        {
            Scribe_Values.Look(ref averageQuality, prefix + "averageQuality", 0.5f);
            Scribe_Values.Look(ref skillNoise, prefix + "skillNoise", 0.2f);
            Scribe_Values.Look(ref passionNoise, prefix + "passionNoise", 0.25f);
            Scribe_Values.Look(ref passionMajorBias, prefix + "passionMajorBias", 0.5f);
            Scribe_Values.Look(ref skillShiftMin, prefix + "skillShiftMin", -3f);
            Scribe_Values.Look(ref skillShiftMax, prefix + "skillShiftMax", 3f);
            Scribe_Values.Look(ref applyChildSkillShift, prefix + "applyChildSkillShift", false);
            Scribe_Values.Look(ref childSkillShiftMin, prefix + "childSkillShiftMin", -1f);
            Scribe_Values.Look(ref childSkillShiftMax, prefix + "childSkillShiftMax", 2f);
            Scribe_Values.Look(ref traitCountMin, prefix + "traitCountMin", 2f);
            Scribe_Values.Look(ref traitCountMax, prefix + "traitCountMax", 3f);
            Scribe_Values.Look(ref passionCountMin, prefix + "passionCountMin", 2f);
            Scribe_Values.Look(ref passionCountMax, prefix + "passionCountMax", 6f);
            Scribe_Values.Look(ref enableSkillVariance, prefix + "enableSkillVariance", true);
            Scribe_Values.Look(ref enableTraitVariance, prefix + "enableTraitVariance", true);
            Scribe_Values.Look(ref enablePassionVariance, prefix + "enablePassionVariance", true);
            Scribe_Values.Look(ref countProtectedTraits, prefix + "countProtectedTraits", false);
            betaDirty = true;
        }
    }

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

        // The mod's original pre-profile defaults, preserved so long-time users can get their old
        // tuning back in one click.
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
                traitCountMin = 1f,
                traitCountMax = 6f,
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
                skillShiftMin = -12f,
                skillShiftMax = 7f,
                childSkillShiftMin = -5f,
                childSkillShiftMax = 6f,
                traitCountMin = 0f,
                traitCountMax = 8f,
                passionCountMin = 0f,
                passionCountMax = 12f,
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
                skillShiftMin = -4.5f,
                skillShiftMax = 1.5f,
                childSkillShiftMin = -2f,
                childSkillShiftMax = 1f,
                traitCountMin = 1f,
                traitCountMax = 4f,
                passionCountMin = 1f,
                passionCountMax = 4.5f,
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
                skillShiftMin = 0f,
                skillShiftMax = 4.2f,
                childSkillShiftMin = 0f,
                childSkillShiftMax = 3f,
                traitCountMin = 2f,
                traitCountMax = 5f,
                passionCountMin = 3.0f,
                passionCountMax = 6.5f,
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

        // The three editable slots, in display order. Index in this array is the slot number the
        // settings object stores values and names under, so the order is persisted data — append
        // here, never reorder.
        public static readonly VarianceProfileId[] CustomSlots =
        {
            VarianceProfileId.Custom,
            VarianceProfileId.Custom2,
            VarianceProfileId.Custom3,
        };

        public static readonly string[] DefaultCustomNames = { "Custom 1", "Custom 2", "Custom 3" };

        public static bool IsCustom(VarianceProfileId id) => GetPreset(id) == null;

        // -1 for anything that is not a custom slot. Callers that index arrays with this must check.
        public static int CustomSlotIndex(VarianceProfileId id)
        {
            for (int i = 0; i < CustomSlots.Length; i++)
                if (CustomSlots[i] == id) return i;
            return -1;
        }

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
