using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Verified against RimWorld 1.5/1.6's decompiled Assembly-CSharp.dll: Pawn_AgeTracker has no
    // DevelopmentalStage member at all (it's a read-only computed property on Pawn itself, not
    // Pawn_AgeTracker, and has no setter to hook). The real target is
    // Pawn_AgeTracker.PostResolveLifeStageChange() — public, parameterless, called on EVERY
    // life-stage transition (Baby->Child, Child->Adult, etc.), not just Child->Adult. This is
    // exactly the "generic developmental-stage-changed callback" scenario the spec's own
    // defensive DevelopmentalStage != Adult check (below) was already written to handle — no
    // other code change needed. Pawn_AgeTracker's private backing field is literally named
    // "pawn", matching the existing ___pawn Harmony field-injection parameter.
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.PostResolveLifeStageChange))]
    public static class DevelopmentalStage_Postfix
    {
        private static readonly HashSet<int> Processed = new HashSet<int>();

        public static void Postfix(Pawn ___pawn)
        {
            var settings = PawnVarianceMod.Settings;
            if (!settings.applyVarianceOnGrowUp) return;
            if (___pawn == null || ___pawn.RaceProps == null || !___pawn.RaceProps.Humanlike) return; // this hook fires for every pawn including animals; without this gate every maturing animal would NRE in the appliers below
            if (___pawn.DevelopmentalStage != DevelopmentalStage.Adult) return; // defensive re-check, see Growth-up step 0
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

            if (Processed.Contains(___pawn.thingIDNumber)) return; // idempotency guard (Growth-up variance, Idempotency guard)
            Processed.Add(___pawn.thingIDNumber);

            try
            {
                float quality = QualityRoller.RollQuality();

                // Ordering matches the main postfix (HarmonyPatches.cs): trait, then skill, then
                // passion — trait variance can disable work tags, which passion placement's
                // TotallyDisabled exclusion depends on.
                if (settings.enableTraitVariance) ApplyTraitGrowthUp(___pawn, quality);
                if (settings.enableSkillVariance) ApplySkillGrowthUp(___pawn, quality);
                if (settings.enablePassionVariance) ApplyPassionGrowthUp(___pawn, quality);
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception applying growth-up variance to {___pawn.LabelShort}: {ex}");
            }
        }

        // Not Scribe-persisted by design (the idempotency guard is deliberately session-only —
        // see Growth-up variance's Idempotency guard). Cleared when a game is loaded (Game.LoadGame)
        // or a new colony is started (Game.InitNewGame) so a thingIDNumber collision between
        // separate save files loaded in the same RimWorld session (IDs are assigned per-save, not
        // globally unique) can't cause a false-positive "already processed" skip on an unrelated
        // pawn in a newly loaded save.
        internal static void ClearForNewGame()
        {
            Processed.Clear();
        }

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

    // Target method unverified — confirm Game.LoadGame's exact signature against decompiled
    // source (Global Constraints). Isolated as its own patch class (see PawnVarianceMod's
    // per-class patch isolation) so a wrong target here can't take down the rest of the mod.
    [HarmonyPatch(typeof(Game), nameof(Game.LoadGame))]
    public static class Game_LoadGame_Postfix
    {
        public static void Postfix()
        {
            DevelopmentalStage_Postfix.ClearForNewGame();
        }
    }

    // Target method unverified — confirm Game.InitNewGame's exact signature against decompiled
    // source (Global Constraints). Isolated as its own patch class (see PawnVarianceMod's
    // per-class patch isolation) so a wrong target here can't take down the rest of the mod.
    // Covers the "start a new colony" path — Game.LoadGame (above) only covers loading an
    // existing save; without this, the idempotency guard's collision risk (see
    // DevelopmentalStage_Postfix.ClearForNewGame) is still reachable via "load save A, then
    // start new colony B" in the same session.
    [HarmonyPatch(typeof(Game), nameof(Game.InitNewGame))]
    public static class Game_InitNewGame_Postfix
    {
        public static void Postfix()
        {
            DevelopmentalStage_Postfix.ClearForNewGame();
        }
    }
}
