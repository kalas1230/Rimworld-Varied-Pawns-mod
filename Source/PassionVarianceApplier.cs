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

            AddPassionsWithoutClearing(pawn, targetCount);
        }

        // Placement loop used both by generation-time Apply (after its unconditional clear, so the
        // "already carrying a passion" exclusion below is a no-op there) and by GrowthUpPatch
        // (without any clear, so the exclusion keeps pre-existing growth-moment passions untouched
        // and skips them as placement candidates).
        public static void AddPassionsWithoutClearing(Pawn pawn, int countToAdd)
        {
            var settings = PawnVarianceMod.Settings;

            var candidates = pawn.skills.skills.Where(r => !r.TotallyDisabled && r.passion == Passion.None).ToList();
            if (candidates.Count == 0) return;

            float maxLevel = candidates.Max(r => r.Level);
            float temperature = Mathf.Lerp(Constants.MinTemperatureFloor, Constants.MaxTemperature, settings.passionNoise);

            var pool = new List<SkillRecord>(candidates);
            for (int i = 0; i < countToAdd && pool.Count > 0; i++)
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
