using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class SkillVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality, VarianceProfileValues v)
        {
            Shift(pawn, quality, v, v.skillShiftMin, v.skillShiftMax, clampToRange: false);
        }

        // The age-13 growth moment. Its own range, and — unlike generation — the range is a hard
        // per-skill bound rather than a band the noise term is added on top of. At generation the
        // range describes where the pawn's baseline sits and noise spreads their skills around it,
        // which is fine because there is nothing to lose: vanilla has just rolled those levels. At
        // 13 the levels are twelve years of play, so a range whose minimum reads 0 has to actually
        // mean "never subtracts" — and it would not, unclamped: on Distinct the noise term alone
        // spans +-2.4, which is where the observed "Construction 5 -> 3 on a birthday" came from
        // (its baseline was only -0.3). Clamping keeps per-skill variety inside a bound the slider
        // honestly describes.
        public static void ApplyGrowUp(Pawn pawn, float quality, VarianceProfileValues v)
        {
            Shift(pawn, quality, v, v.childSkillShiftMin, v.childSkillShiftMax, clampToRange: true);
        }

        private static void Shift(Pawn pawn, float quality, VarianceProfileValues v, float shiftMin, float shiftMax, bool clampToRange)
        {
            float baseline = Mathf.Lerp(shiftMin, shiftMax, quality);
            float magnitude = Mathf.Lerp(Constants.MinMagnitudeFloor, Constants.MaxMagnitude, v.skillNoise);

            foreach (SkillRecord record in pawn.skills.skills)
            {
                // Read levelInt (the raw learned level), NOT record.Level. SkillRecord's property is
                // asymmetric: the getter is GetLevel() => Mathf.Clamp(levelInt + Aptitude, 0, 20), but
                // the setter writes straight to levelInt. Reading through the getter and writing back
                // through the setter therefore BAKES the Biotech gene aptitude bonus into levelInt,
                // after which the getter adds it a second time — a permanent, save-persisted double
                // count that scales with the aptitude. In-game evidence: an Yttakin (Animals aptitude
                // +8) generated with a learned level of 6 read as 14, stored 14, then displayed 22
                // clamped to a maxed 20. Vanilla itself never round-trips through this property for
                // exactly this reason. Aptitude is applied by the getter on top of whatever we store,
                // so shifting the raw learned level preserves the gene bonus correctly.
                float shift = baseline + (TriangularSample() * 2f - 1f) * magnitude;
                if (clampToRange) shift = Mathf.Clamp(shift, shiftMin, shiftMax);
                int newLevel = Mathf.RoundToInt(record.levelInt + shift);
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
