using System.Linq;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Vanilla gates how many traits a pawn can accumulate on growth birthdays, not on a flat number:
    // PawnGenerator.GenerateTraits walks `for (int k = 3; k <= ageBiologicalYears; k++)` and grants at
    // most one trait per GrowthUtility.IsGrowthBirthday(k). With GrowthMomentAges = { 7, 10, 13 } that
    // means 0 rolled traits below age 7, 1 at 7-9, 2 at 10-12 and 3 from 13 on. Without this cap the
    // mod would hand a five-year-old a full adult trait load, which vanilla structurally never does.
    // Thresholds are read from GrowthUtility at runtime rather than hardcoded, since Biotech content
    // or another mod may change them.
    public static class TraitAgeCap
    {
        public static int MaxRolledTraitsFor(Pawn pawn)
        {
            if (pawn?.ageTracker == null) return int.MaxValue;

            int[] momentAges = GrowthUtility.GrowthMomentAges;
            if (momentAges == null || momentAges.Length == 0) return int.MaxValue;

            int age = pawn.ageTracker.AgeBiologicalYears;

            // Past the last growth moment the pawn is fully grown and the cap stops binding, so the
            // mod's own target applies in full (this is what lets adults exceed vanilla's max of 3).
            if (age >= momentAges.Max()) return int.MaxValue;

            // UNREACHABLE UNDER VANILLA GROWTH AGES, and that is expected rather than dead code.
            // Both callers are gated to pawns aged 13+ (generation via HarmonyPatches'
            // VanillaAdultPassionAge check, the growth moment via GrowthUpPatch's Adult check), and
            // vanilla's GrowthMomentAges = { 7, 10, 13 } makes Max() 13 -- so the line above returns
            // first, every time. This branch exists because the thresholds are read from
            // GrowthUtility at runtime: a mod adding a growth moment at 16 makes Max() 16 and this
            // binds immediately at 13-15, as would a HAR race reaching Adult before 13.
            // Consequence worth knowing: nothing in the shipped configuration exercises this line,
            // so a bug in it would not surface in normal play.
            return momentAges.Count(a => a <= age);
        }
    }
}
