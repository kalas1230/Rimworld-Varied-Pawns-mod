using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Which preset the generation settings are currently driven by. Custom is the only one whose
    // values the player can edit; the rest are read-only recipes. Custom is first (value 0) so a
    // settings file written before profiles existed — where the enum node is missing entirely —
    // loads as Custom and keeps the values that file already had.
    public enum VarianceProfileId
    {
        Custom = 0,
        VanillaLike = 1,
        BalancedVariance = 2,
        WildSpread = 3,
        GiftedColony = 4,
        Hardscrabble = 5,
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
        public float traitCountMin = 1f;
        public float traitCountMax = 6f;
        public float passionCountMin = 1f;
        public float passionCountMax = 7f;
        public bool enableSkillVariance = true;
        public bool enableTraitVariance = true;
        public bool enablePassionVariance = true;
        public bool countProtectedTraits = false;

        public VarianceProfileValues Clone() => (VarianceProfileValues)MemberwiseClone();

        // Edge Cases 1 & 9: clamp out-of-range sliders, swap inverted min/max ranges
        public void ClampAndSwap()
        {
            averageQuality = Mathf.Clamp01(averageQuality);
            skillNoise = Mathf.Clamp01(skillNoise);
            passionNoise = Mathf.Clamp01(passionNoise);
            passionMajorBias = Mathf.Clamp01(passionMajorBias);

            if (skillShiftMin > skillShiftMax) { var t = skillShiftMin; skillShiftMin = skillShiftMax; skillShiftMax = t; }
            if (traitCountMin > traitCountMax) { var t = traitCountMin; traitCountMin = traitCountMax; traitCountMax = t; }
            if (passionCountMin > passionCountMax) { var t = passionCountMin; passionCountMin = passionCountMax; passionCountMax = t; }
        }

        // Node names are the pre-profile field names on purpose: an existing settings file scribed
        // before profiles existed loads straight into the Custom profile, so nobody's tuning is lost
        // on update. Defaults must stay in sync with VarianceProfiles.VanillaLike, which is what a
        // fresh install starts Custom out as.
        public void ExposeData()
        {
            Scribe_Values.Look(ref averageQuality, "averageQuality", 0.5f);
            Scribe_Values.Look(ref skillNoise, "skillNoise", 0.2f);
            Scribe_Values.Look(ref passionNoise, "passionNoise", 0.25f);
            Scribe_Values.Look(ref passionMajorBias, "passionMajorBias", 0.5f);
            Scribe_Values.Look(ref skillShiftMin, "skillShiftMin", -3f);
            Scribe_Values.Look(ref skillShiftMax, "skillShiftMax", 3f);
            Scribe_Values.Look(ref traitCountMin, "traitCountMin", 2f);
            Scribe_Values.Look(ref traitCountMax, "traitCountMax", 3f);
            Scribe_Values.Look(ref passionCountMin, "passionCountMin", 2f);
            Scribe_Values.Look(ref passionCountMax, "passionCountMax", 6f);
            Scribe_Values.Look(ref enableSkillVariance, "enableSkillVariance", true);
            Scribe_Values.Look(ref enableTraitVariance, "enableTraitVariance", true);
            Scribe_Values.Look(ref enablePassionVariance, "enablePassionVariance", true);
            Scribe_Values.Look(ref countProtectedTraits, "countProtectedTraits", false);
        }
    }

    public class VarianceProfile
    {
        public readonly VarianceProfileId id;
        public readonly string label;
        public readonly string description;
        private readonly VarianceProfileValues values;

        public VarianceProfile(VarianceProfileId id, string label, string description, VarianceProfileValues values)
        {
            this.id = id;
            this.label = label;
            this.description = description;
            this.values = values;
        }

        // Always a copy: callers write into what they get back, and a preset must stay pristine.
        public VarianceProfileValues MakeValues() => values.Clone();
    }

    public static class VarianceProfiles
    {
        // Sits as close to unmodded generation as the mod's knobs allow: pawns still vary, but the
        // trait count, passion budget and skill spread stay inside vanilla's own envelope.
        public static readonly VarianceProfile VanillaLike = new VarianceProfile(
            VarianceProfileId.VanillaLike,
            "Faithful",
            "Closest to unmodded RimWorld. Two to three traits, a vanilla-sized passion budget and a narrow skill spread — pawns differ, but nobody is a prodigy or a write-off.",
            new VarianceProfileValues
            {
                averageQuality = 0.5f,
                skillNoise = 0.2f,
                passionNoise = 0.25f,
                passionMajorBias = 0.5f,
                skillShiftMin = -3f,
                skillShiftMax = 3f,
                traitCountMin = 2f,
                traitCountMax = 3f,
                passionCountMin = 2f,
                passionCountMax = 6f,
            });

        // The mod's original pre-profile defaults, preserved so long-time users can get their old
        // tuning back in one click.
        public static readonly VarianceProfile BalancedVariance = new VarianceProfile(
            VarianceProfileId.BalancedVariance,
            "Distinct",
            "The mod's signature tuning. Pawns are noticeably individual — a weak generalist and a narrow specialist come off the same drop pod — while the colony average stays fair.",
            new VarianceProfileValues
            {
                averageQuality = 0.5f,
                skillNoise = 0.35f,
                passionNoise = 0.35f,
                passionMajorBias = 0.8f,
                skillShiftMin = -4f,
                skillShiftMax = 6f,
                traitCountMin = 1f,
                traitCountMax = 6f,
                passionCountMin = 1f,
                passionCountMax = 7f,
            });

        public static readonly VarianceProfile WildSpread = new VarianceProfile(
            VarianceProfileId.WildSpread,
            "Wildcard",
            "Maximum swing. A pawn can arrive with no traits or eight, no passions or a fistful, and skills far above or below anything vanilla would roll. Same average, far bigger gamble.",
            new VarianceProfileValues
            {
                averageQuality = 0.5f,
                skillNoise = 0.85f,
                passionNoise = 0.85f,
                passionMajorBias = 0.6f,
                skillShiftMin = -12f,
                skillShiftMax = 14f,
                traitCountMin = 0f,
                traitCountMax = 8f,
                passionCountMin = 0f,
                passionCountMax = 14f,
            });

        public static readonly VarianceProfile GiftedColony = new VarianceProfile(
            VarianceProfileId.GiftedColony,
            "Gifted",
            "Power fantasy. Everyone skews talented and passionate — good starts, strong recruits, and raiders who can actually shoot. Pick this if you want capability over struggle.",
            new VarianceProfileValues
            {
                averageQuality = 0.82f,
                skillNoise = 0.35f,
                passionNoise = 0.4f,
                passionMajorBias = 0.9f,
                skillShiftMin = 0f,
                // 8, not 12: at 12 a 26-pawn sample averaged a top skill of 19.4 and clipped 29
                // skill slots at the level cap, so gifted pawns stopped being distinguishable from
                // each other at the high end. 8 keeps them clearly strong with headroom left.
                skillShiftMax = 8f,
                traitCountMin = 1f,
                traitCountMax = 5f,
                passionCountMin = 5f,
                passionCountMax = 12f,
            });

        public static readonly VarianceProfile Hardscrabble = new VarianceProfile(
            VarianceProfileId.Hardscrabble,
            "Desperate",
            "Scraped-together survivors. Low skills, few passions, and a genuinely bad pawn is common. Every competent colonist you find is worth defending.",
            new VarianceProfileValues
            {
                averageQuality = 0.22f,
                skillNoise = 0.3f,
                passionNoise = 0.3f,
                passionMajorBias = 0.35f,
                // -8, not -10: at -10 a 72-pawn sample averaged skill level 0.54 — most skills on
                // most pawns floored at 0, leaving colonies unable to do skilled work at all.
                // Bad-but-functional is the intent, not unusable.
                skillShiftMin = -8f,
                skillShiftMax = 2f,
                traitCountMin = 1f,
                traitCountMax = 6f,
                passionCountMin = 0f,
                passionCountMax = 4f,
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
        };

        public const string CustomLabel = "Custom";
        public const string CustomDescription = "Your own settings. Starts as a copy of Faithful; edit any slider below and it stays exactly as you leave it.";

        public static VarianceProfile GetPreset(VarianceProfileId id)
        {
            foreach (var p in Presets)
                if (p.id == id) return p;
            return null;
        }

        public static string LabelFor(VarianceProfileId id) => GetPreset(id)?.label ?? CustomLabel;

        public static string DescriptionFor(VarianceProfileId id) => GetPreset(id)?.description ?? CustomDescription;

        // Fresh installs (and "reset Custom") start from Faithful, per design: the neutral profile is
        // the one a new player is least surprised by.
        public static VarianceProfileValues NewCustomDefaults() => VanillaLike.MakeValues();
    }
}
