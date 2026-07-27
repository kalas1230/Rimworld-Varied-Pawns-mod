using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class TraitVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;

            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality) + JitterSample()),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            HashSet<TraitDef> forced = CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = CaptureDisallowedTraits(pawn);

            pawn.story.traits.allTraits.Clear();
            foreach (TraitDef def in forced)
                pawn.story.traits.GainTrait(new Trait(def, 0, true));

            if (pawn.story.traits.allTraits.Count >= targetCount)
                return; // Accepted limitation: forced set alone can meet/exceed target — see Trait variance step 3.

            FillRemainingSlots(pawn, quality, targetCount, disallowed);
        }

        // Weighted-sampling fill used both by generation-time Apply (after clear+forced) and by
        // GrowthUpPatch (after preserving pre-existing traits). Any trait already present on the
        // pawn (including forced ones) is naturally skipped by WeightedPick's HasTrait check, so
        // callers do not need to pre-filter their own "already present" set out of `eligible`.
        public static void FillRemainingSlots(Pawn pawn, float quality, int targetCount, HashSet<TraitDef> disallowed)
        {
            var settings = PawnVarianceMod.Settings;

            float target = Mathf.Lerp(TraitDesirabilityCache.ObservedMinScore, TraitDesirabilityCache.ObservedMaxScore, quality);
            float spread = Mathf.Lerp(Constants.MinSpreadFloor, Constants.MaxSpread, settings.traitNoise);

            List<TraitDef> eligible = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(def => !disallowed.Contains(def))
                .ToList();

            while (pawn.story.traits.allTraits.Count < targetCount && eligible.Count > 0)
            {
                TraitDef picked = WeightedPick(eligible, target, spread, pawn);
                if (picked == null)
                {
                    Log.Message($"[PawnVarianceMod] Ran out of eligible traits for {pawn.LabelShort}, stopping at {pawn.story.traits.allTraits.Count}/{targetCount}.");
                    break;
                }
                pawn.story.traits.GainTrait(new Trait(picked, 0, false));
                eligible.Remove(picked);
            }
        }

        public static HashSet<TraitDef> CaptureForcedTraits(Pawn pawn)
        {
            var forced = new HashSet<TraitDef>();
            // PawnKindDef.forcedTraits: unverified field name, confirm at implementation time (Global Constraints).
            if (pawn.kindDef?.forcedTraits != null)
                foreach (var t in pawn.kindDef.forcedTraits) forced.Add(t.def);

            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (var gene in pawn.genes.GenesListForReading)
                    if (gene.def.forcedTraits != null)
                        foreach (var t in gene.def.forcedTraits) forced.Add(t.def);

            return forced;
        }

        public static HashSet<TraitDef> CaptureDisallowedTraits(Pawn pawn)
        {
            var disallowed = new HashSet<TraitDef>();
            if (pawn.kindDef?.disallowedTraits != null)
                foreach (var t in pawn.kindDef.disallowedTraits) disallowed.Add(t.def);

            if (ModsConfig.IdeologyActive && pawn.Ideo != null)
                foreach (var precept in pawn.Ideo.PreceptsListForReading)
                    if (precept.def.disallowedTraits != null)
                        foreach (var def in precept.def.disallowedTraits) disallowed.Add(def);

            return disallowed;
        }

        private static TraitDef WeightedPick(List<TraitDef> candidates, float target, float spread, Pawn pawn)
        {
            var weights = new List<(TraitDef def, float weight)>();
            float minDistSq = float.MaxValue;

            foreach (var def in candidates)
            {
                if (pawn.story.traits.HasTrait(def)) continue;
                if (ConflictsWithExisting(def, pawn)) continue;
                float score = TraitDesirabilityCache.ScoreOf(def, 0);
                float distSq = (score - target) * (score - target);
                if (distSq < minDistSq) minDistSq = distSq;
                weights.Add((def, distSq));
            }

            if (weights.Count == 0) return null;

            var finalWeights = weights.Select(w => (w.def, weight: Mathf.Exp(-(w.weight - minDistSq) / spread))).ToList();
            float total = finalWeights.Sum(w => w.weight);
            float roll = (float)Rand.Value * total;
            float cumulative = 0f;
            foreach (var (def, weight) in finalWeights)
            {
                cumulative += weight;
                if (roll <= cumulative) return def;
            }
            return finalWeights.Last().def;
        }

        // TraitDef.conflictingTraits/exclusionTags: unverified field names, confirm at implementation time (Global Constraints).
        private static bool ConflictsWithExisting(TraitDef def, Pawn pawn)
        {
            foreach (Trait existing in pawn.story.traits.allTraits)
            {
                if (def.conflictingTraits != null && def.conflictingTraits.Contains(existing.def)) return true;
                if (existing.def.conflictingTraits != null && existing.def.conflictingTraits.Contains(def)) return true;
                if (def.exclusionTags != null && existing.def.exclusionTags != null &&
                    def.exclusionTags.Intersect(existing.def.exclusionTags).Any()) return true;
            }
            return false;
        }

        private static float JitterSample()
        {
            return ((float)Rand.Value - 0.5f) * Constants.SmallRandomJitter;
        }
    }
}
