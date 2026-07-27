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
