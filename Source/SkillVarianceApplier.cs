using RimWorld;
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
