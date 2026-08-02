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
            if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayerSilentFail)) return;
            if (ModsConfig.BiotechActive && pawn.DevelopmentalStage != DevelopmentalStage.Adult) return;

            // Which profile this pawn is generated from — the player's, or the separate hostile one.
            // Resolved before the enable checks below because those toggles are themselves per-profile,
            // so a hostile pawn can legitimately have variance switched off while the colony's is on.
            VarianceProfileValues v = settings.ValuesFor(pawn, request);
            if (!v.enableSkillVariance && !v.enableTraitVariance && !v.enablePassionVariance) return;

            try
            {
                float quality = QualityRoller.RollQuality(v);

                // Ordering per Per-pawn flow step 4: trait, then skill, then passion.
                if (v.enableTraitVariance) TraitVarianceApplier.Apply(pawn, quality, request, v);
                if (v.enableSkillVariance) SkillVarianceApplier.Apply(pawn, quality, v);
                if (v.enablePassionVariance) PassionVarianceApplier.Apply(pawn, quality, v);
            }
            catch (Exception ex)
            {
                if (settings.verboseLogging) throw;
                Log.ErrorOnce($"[PawnVarianceMod] Exception applying variance to {pawn.LabelShort}: {ex}", (ex.GetType().FullName + ex.StackTrace).GetHashCode());
            }
        }
    }
}
