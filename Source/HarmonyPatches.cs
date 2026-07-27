using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    public static class GeneratePawn_Postfix
    {
        public static void Postfix(Pawn __result)
        {
            var settings = PawnVarianceMod.Settings;
            Pawn pawn = __result;

            if (pawn == null || !pawn.RaceProps.Humanlike) return;
            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer)) return;
            if (ModsConfig.BiotechActive && pawn.DevelopmentalStage != DevelopmentalStage.Adult) return;

            try
            {
                float quality = QualityRoller.RollQuality();

                // Ordering per Per-pawn flow step 4: trait, then skill, then passion.
                if (settings.enableTraitVariance) TraitVarianceApplier.Apply(pawn, quality);
                if (settings.enableSkillVariance) SkillVarianceApplier.Apply(pawn, quality);
                if (settings.enablePassionVariance) PassionVarianceApplier.Apply(pawn, quality);
            }
            catch (Exception ex)
            {
                if (settings.verboseLogging) throw;
                Log.ErrorOnce($"[PawnVarianceMod] Exception applying variance to {pawn.LabelShort}: {ex}", (ex.GetType().FullName + ex.StackTrace).GetHashCode());
            }
        }
    }
}
