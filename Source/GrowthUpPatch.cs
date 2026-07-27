using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Target method unverified — confirm the Biotech Child->Adult transition hook against
    // decompiled source before finalizing this attribute (Global Constraints).
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.DevelopmentalStage), MethodType.Setter)]
    public static class DevelopmentalStage_Postfix
    {
        private static readonly HashSet<int> Processed = new HashSet<int>();

        public static void Postfix(Pawn_AgeTracker __instance, Pawn ___pawn)
        {
            var settings = PawnVarianceMod.Settings;
            if (!settings.applyVarianceOnGrowUp) return;
            if (___pawn == null) return;
            if (___pawn.DevelopmentalStage != DevelopmentalStage.Adult) return; // defensive re-check, see Growth-up step 0
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

            if (Processed.Contains(___pawn.thingIDNumber)) return; // idempotency guard (Growth-up variance, Idempotency guard)
            Processed.Add(___pawn.thingIDNumber);

            try
            {
                float quality = QualityRoller.RollQuality();

                if (settings.enableSkillVariance) ApplySkillGrowthUp(___pawn, quality);
                if (settings.enableTraitVariance) ApplyTraitGrowthUp(___pawn, quality);
                if (settings.enablePassionVariance) ApplyPassionGrowthUp(___pawn, quality);
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception applying growth-up variance to {___pawn.LabelShort}: {ex}");
            }
        }

        // Skill: compute then immediately apply (step-scoped, not deferred across 2-4 — see
        // Growth-up variance step 5's mitigation).
        private static void ApplySkillGrowthUp(Pawn pawn, float quality)
        {
            SkillVarianceApplier.Apply(pawn, quality); // identical logic to generation-time; additive, so safe on accumulated childhood levels
        }

        private static void ApplyTraitGrowthUp(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            HashSet<TraitDef> forced = TraitVarianceApplier.CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = TraitVarianceApplier.CaptureDisallowedTraits(pawn);

            var alreadyAdded = new HashSet<TraitDef>(pawn.story.traits.allTraits.Select(t => t.def).Where(forced.Contains));

            foreach (TraitDef def in forced)
            {
                if (pawn.story.traits.HasTrait(def)) continue;

                bool disallowedToo = disallowed.Contains(def);
                var conflicting = pawn.story.traits.allTraits.FirstOrDefault(t => ConflictsWith(def, t.def));

                if (conflicting != null)
                {
                    if (forced.Contains(conflicting.def) || alreadyAdded.Contains(conflicting.def))
                    {
                        Log.Error($"[PawnVarianceMod] Forced-vs-forced trait conflict on {pawn.LabelShort}: {def.defName} conflicts with already-present forced trait {conflicting.def.defName}; skipping {def.defName}.");
                        continue;
                    }
                    pawn.story.traits.RemoveTrait(conflicting, true);
                    Log.Message($"[PawnVarianceMod] Removed growth-moment trait {conflicting.def.defName} on {pawn.LabelShort} to make room for newly-forced {def.defName}.");
                }

                if (disallowedToo)
                    Log.Error($"[PawnVarianceMod] {def.defName} is simultaneously forced and disallowed for {pawn.LabelShort}; forced wins.");

                pawn.story.traits.GainTrait(new Trait(def, 0, true));
                alreadyAdded.Add(def);
            }

            int currentCount = pawn.story.traits.allTraits.Count;
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.traitCountMin, settings.traitCountMax, quality)),
                Mathf.RoundToInt(settings.traitCountMin),
                Mathf.RoundToInt(settings.traitCountMax));

            if (currentCount >= targetCount) return; // accepted limitation, see Growth-up variance closing paragraph

            // Fill remaining slots via the same weighted-sampling procedure as generation time,
            // excluding disallowed and respecting conflicts against traits already present.
            TraitVarianceApplier.FillRemainingSlots(pawn, quality, targetCount, disallowed);
        }

        private static bool ConflictsWith(TraitDef a, TraitDef b)
        {
            if (a.conflictingTraits != null && a.conflictingTraits.Contains(b)) return true;
            if (b.conflictingTraits != null && b.conflictingTraits.Contains(a)) return true;
            if (a.exclusionTags != null && b.exclusionTags != null && a.exclusionTags.Intersect(b.exclusionTags).Any()) return true;
            return false;
        }

        private static void ApplyPassionGrowthUp(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            int existingPassionCount = pawn.skills.skills.Count(r => r.passion != Passion.None);
            int targetCount = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.passionCountMin, settings.passionCountMax, quality)),
                Mathf.RoundToInt(settings.passionCountMin),
                Mathf.RoundToInt(settings.passionCountMax));

            if (existingPassionCount >= targetCount) return;

            PassionVarianceApplier.AddPassionsWithoutClearing(pawn, targetCount - existingPassionCount);
        }
    }
}
