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

                // remainingSlots includes `picked` itself (not yet removed from `pool`), matching
                // what RollMinorOrMajor needs: "how many skill-slots, including this one, remain
                // to place the remaining pips."
                int remainingNeed = pipsToAdd - pipsPlaced;
                int remainingSlots = pool.Count;
                Passion assigned = VanillaPassionRatio.RollMinorOrMajor(remainingNeed, remainingSlots);
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

    // Adaptive Major/Minor roll: Major probability scales with how many pips are still needed
    // relative to how many skill-slots remain, so the configured pip target stays reachable
    // instead of being capped by a fixed ratio. The original placeholder here was a FIXED 75%
    // Minor / 25% Major split, independent of the target — this was a real, in-game-confirmed bug
    // (Task 11 verification): across 12 skills, a fixed 75/25 split averages only ~1.25 pips per
    // skill (12 * 1.25 = 15), so any configured target above roughly 15 pips was structurally
    // unreachable no matter how the dice landed, since every skill is committed after exactly one
    // roll and never revisited. Observed symptom: min/max set to 22-24 pips, actual results landed
    // around 13-16, or ~16-20 with a wide min/max spread.
    //
    // pMajor = clamp01((remainingNeed - remainingSlots) / remainingSlots): when remainingNeed
    // equals remainingSlots (need exactly 1 pip/slot on average to finish), pMajor = 0, forcing
    // Minor. When remainingNeed equals 2*remainingSlots (need the maximum every remaining slot can
    // give), pMajor = 1, forcing Major. In between, it scales linearly, preserving genuine
    // randomness in the common case while guaranteeing the target is always reachable whenever it
    // is mathematically achievable (<= 2 * available skill count).
    //
    // Genuine vanilla Minor/Major ratio logic is still unverified against decompiled source
    // (Global Constraints) — this adaptive approach deliberately prioritizes actually honoring the
    // user's configured target over guessing at an unknown vanilla probability.
    internal static class VanillaPassionRatio
    {
        public static Passion RollMinorOrMajor(int remainingNeed, int remainingSlots)
        {
            if (remainingSlots <= 0) return Passion.Minor; // defensive; caller should never invoke with no slots left
            float pMajor = Mathf.Clamp01((remainingNeed - remainingSlots) / (float)remainingSlots);
            return Rand.Value < pMajor ? Passion.Major : Passion.Minor;
        }
    }
}
