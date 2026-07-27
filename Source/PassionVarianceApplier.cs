using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class PassionVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;

            int targetPips = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.passionCountMin, settings.passionCountMax, quality) + JitterSample()),
                Mathf.RoundToInt(settings.passionCountMin),
                Mathf.RoundToInt(settings.passionCountMax));

            foreach (SkillRecord record in pawn.skills.skills)
                record.passion = Passion.None;

            AddPassionsWithoutClearing(pawn, targetPips);
        }

        // "Passion count range" in settings is measured in PIPS (RimWorld's flame icons), not
        // distinct skills: Minor passion = 1 pip, Major passion = 2 pips — matching how a player
        // actually reads the UI (a Major passion shows two flames), and allowing the full range
        // vanilla supports (up to 24 pips across 12 skills, if every skill somehow got Major).
        // Because Major adds 2 pips in one step, the final total can overshoot the target by at
        // most 1 pip (e.g. target=5, sum=4, next pick rolls Major -> sum=6) — accepted, not
        // corrected, since forcing Minor specifically to avoid a 1-pip overshoot would bias the
        // Minor/Major ratio right at the target boundary for no real benefit.
        //
        // Placement loop used both by generation-time Apply (after its unconditional clear, so the
        // "already carrying a passion" exclusion below is a no-op there) and by GrowthUpPatch
        // (without any clear, so the exclusion keeps pre-existing growth-moment passions untouched
        // and skips them as placement candidates).
        public static void AddPassionsWithoutClearing(Pawn pawn, int pipsToAdd)
        {
            var settings = PawnVarianceMod.Settings;

            var candidates = pawn.skills.skills.Where(r => !r.TotallyDisabled && r.passion == Passion.None).ToList();
            if (candidates.Count == 0) return;

            float maxLevel = candidates.Max(r => r.Level);
            float temperature = Mathf.Lerp(Constants.MinTemperatureFloor, Constants.MaxTemperature, settings.passionNoise);

            var pool = new List<SkillRecord>(candidates);
            int pipsPlaced = 0;
            while (pipsPlaced < pipsToAdd && pool.Count > 0)
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
                Passion assigned = VanillaPassionRatio.RollMinorOrMajor();
                picked.passion = assigned;
                pipsPlaced += assigned == Passion.Major ? 2 : 1;
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
