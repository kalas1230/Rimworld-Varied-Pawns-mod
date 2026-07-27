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
            // Forced traits are granted at degree 0 regardless of what degree their source
            // (kind-def/gene requirement) may have specified — capturing per-source degree would
            // require expanding CaptureForcedTraits' return type, which Task 9's GrowthUpPatch
            // already depends on as HashSet<TraitDef>. Accepted, not fixed, in this round: this
            // only affects forced traits, never the weighted-sampled ones (see FillRemainingSlots).
            foreach (TraitDef def in forced)
                pawn.story.traits.GainTrait(new Trait(def, 0, true));

            if (pawn.story.traits.allTraits.Count >= targetCount)
                return; // Accepted limitation: forced set alone can meet/exceed target — see Trait variance step 3.

            FillRemainingSlots(pawn, quality, targetCount, disallowed);
        }

        // Weighted-sampling fill used both by generation-time Apply (after clear+forced) and by
        // GrowthUpPatch (after preserving pre-existing traits). Traits already present on the
        // pawn (including forced ones) are excluded from `eligible` up front via HasTrait, so the
        // pool shrinks to empty once no genuinely-new sampleable candidates remain — this keeps the
        // while loop's own exit condition (eligible.Count > 0) accurate without callers needing to
        // pass in their own "already present"/forced set. WeightedPick's internal HasTrait check
        // (retained from Task 6) is now redundant against this narrowed list but harmless.
        //
        // Candidates are (TraitDef, degree) pairs, not bare TraitDefs: a multi-degree trait (e.g.
        // Industriousness at -2/-1/1/2) has each degree scored independently by
        // TraitDesirabilityCache, so sampling must pick trait AND degree together rather than
        // always granting a hardcoded degree 0, which doesn't exist for many such traits.
        public static void FillRemainingSlots(Pawn pawn, float quality, int targetCount, HashSet<TraitDef> disallowed)
        {
            var settings = PawnVarianceMod.Settings;

            float target = Mathf.Lerp(TraitDesirabilityCache.ObservedMinScore, TraitDesirabilityCache.ObservedMaxScore, quality);
            float spread = Mathf.Lerp(Constants.MinSpreadFloor, Constants.MaxSpread, settings.traitNoise);

            List<(TraitDef def, int degree)> eligible = DefDatabase<TraitDef>.AllDefsListForReading
                .Where(def => !disallowed.Contains(def) && !pawn.story.traits.HasTrait(def))
                .SelectMany(def => TraitDesirabilityCache.DegreesFor(def).Select(degree => (def, degree)))
                .ToList();

            while (pawn.story.traits.allTraits.Count < targetCount && eligible.Count > 0)
            {
                var picked = WeightedPick(eligible, target, spread, pawn);
                if (picked == null)
                {
                    Log.Message($"[PawnVarianceMod] Ran out of eligible traits for {pawn.LabelShort}, stopping at {pawn.story.traits.allTraits.Count}/{targetCount}.");
                    break;
                }

                var (def, degree) = picked.Value;
                pawn.story.traits.GainTrait(new Trait(def, degree, false));
                // A trait can only be held at one degree at a time — once a def is picked, remove
                // EVERY remaining degree of that same def from the pool, not just the picked entry.
                eligible.RemoveAll(c => c.def == def);
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

        private static (TraitDef def, int degree)? WeightedPick(List<(TraitDef def, int degree)> candidates, float target, float spread, Pawn pawn)
        {
            var weights = new List<(TraitDef def, int degree, float distSq)>();
            float minDistSq = float.MaxValue;

            foreach (var (def, degree) in candidates)
            {
                if (pawn.story.traits.HasTrait(def)) continue;
                if (ConflictsWithExisting(def, pawn)) continue;
                float score = TraitDesirabilityCache.ScoreOf(def, degree);
                float distSq = (score - target) * (score - target);
                if (distSq < minDistSq) minDistSq = distSq;
                weights.Add((def, degree, distSq));
            }

            if (weights.Count == 0) return null;

            var finalWeights = weights.Select(w => (w.def, w.degree, weight: Mathf.Exp(-(w.distSq - minDistSq) / spread))).ToList();
            float total = finalWeights.Sum(w => w.weight);
            float roll = (float)Rand.Value * total;
            float cumulative = 0f;
            foreach (var (def, degree, weight) in finalWeights)
            {
                cumulative += weight;
                if (roll <= cumulative) return (def, degree);
            }
            var last = finalWeights.Last();
            return (last.def, last.degree);
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
