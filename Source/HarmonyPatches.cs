using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Targets the private GenerateNewPawnInternal rather than the public GeneratePawn: the latter
    // can silently "redress" (reuse) an already-existing world pawn instead of creating a new one
    // (PawnGenerator.GenerateOrRedressPawnInternal, decompiled), and redressed pawns already have
    // their one-time quality/trait/skill/passion roll from whenever they were first generated —
    // patching the outer method reapplied variance to them every single redress, wiping/rerolling
    // traits and compounding skill shifts each time. GenerateNewPawnInternal only runs on the
    // genuinely-new-pawn path, so this fires exactly once per pawn, ever. Also has fewer collision
    // risks with other mods than the far more commonly-patched public GeneratePawn.
    [HarmonyPatch(typeof(PawnGenerator), "GenerateNewPawnInternal")]
    public static class GeneratePawn_Postfix
    {
        public static void Postfix(Pawn __result, ref PawnGenerationRequest request)
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
                if (settings.enableTraitVariance) TraitVarianceApplier.Apply(pawn, quality, request);
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
