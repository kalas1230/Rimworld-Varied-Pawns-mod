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
            if (!settings.showQualityTier) return false;
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

        // Shared by every tier-tooltip patch site (character card, colonist bar) so the wording
        // and color formatting can't drift between them.
        public static string TierTooltipTextFor(Pawn pawn)
        {
            string tier = EffectiveTierFor(pawn);
            string color = ColorFor(tier);
            string tierText = color != null ? $"<color={color}>{tier}</color>" : tier;
            return $"Quality tier: {tierText}";
        }

        private static float SkillComponent(Pawn pawn)
        {
            var settings = PawnVarianceMod.Settings;
            float lower = Mathf.Max(0f, settings.skillShiftMin + Constants.AssumedVanillaSkillBaseline);
            float upper = Mathf.Min(20f, settings.skillShiftMax + Constants.AssumedVanillaSkillBaseline);

            if (Mathf.Approximately(lower, upper)) return 0.5f; // degenerate-range guard, see Tier bio label

            // Excludes TotallyDisabled skills (e.g. from an incapability trait/backstory), same
            // reasoning PassionVarianceApplier already applies to its own candidate pool: a
            // hard-locked-to-0 skill reflects the pawn's incapability, not their quality roll, and
            // including it in the average could keep an otherwise-maxed, genuinely high-quality
            // pawn from ever reading as Prodigy just because one unrelated skill is disabled.
            var usableSkills = pawn.skills.skills.Where(r => !r.TotallyDisabled).ToList();
            float avgLevel = usableSkills.Count > 0 ? (float)usableSkills.Average(r => r.Level) : 0f;
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
