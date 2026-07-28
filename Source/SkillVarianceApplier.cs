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
                float noise = (TriangularSample() * 2f - 1f) * magnitude;
                int newLevel = Mathf.RoundToInt(record.levelInt + baseline + noise);
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
