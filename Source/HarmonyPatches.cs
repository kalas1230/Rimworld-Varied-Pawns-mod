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
            // Humanlike is an INTELLIGENCE check, not a promise that the optional trackers exist —
            // a race def can turn skills or story off, and Humanoid Alien Races (which this mod
            // supports) lets it. Every applier below walks pawn.skills.skills or
            // pawn.story.traits unguarded, and with verboseLogging on the resulting NRE is
            // rethrown into vanilla's pawn generation rather than logged. Gate once, here.
            if (pawn.skills?.skills == null || pawn.story?.traits == null) return;
            // Full faction resolution, not bare pawn.Faction — see EffectiveFactionOf.
            if (settings.IsExcludedAsHostile(pawn, request)) return;
            // Vanilla's own rule, matched exactly: PawnGenerator.GenerateSkills returns early at
            // `AgeBiologicalYears < 13`, so a pawn below it never receives a rolled passion budget
            // at all -- its passions come from forced traits and growth birthdays instead. Rolling
            // one here would hand a child something vanilla structurally never gives.
            //
            // An AGE, not DevelopmentalStage, and not gated on Biotech. This check used to read
            // `ModsConfig.BiotechActive && pawn.DevelopmentalStage != DevelopmentalStage.Adult`,
            // which agrees for humans on Biotech (HumanlikeTeenager starts at 13 and inherits
            // LifeStageDef.developmentalStage's Adult default) but disagrees in two real cases:
            // a race declaring a Child life stage past 13 -- HAR races do -- would be skipped here
            // while vanilla had already given it the full adult treatment, and without Biotech the
            // guard did not run at all.
            if (pawn.ageTracker != null && pawn.ageTracker.AgeBiologicalYears < Constants.VanillaAdultPassionAge) return;

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
