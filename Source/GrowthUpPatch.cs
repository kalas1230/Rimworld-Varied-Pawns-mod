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
        // Tracks each pawn's own last-observed DevelopmentalStage (in-memory only, never
        // persisted). Replaces an earlier "already processed once" HashSet<int> guard that
        // trusted PostResolveLifeStageChange itself as a reliable one-time "just became Adult"
        // signal — that assumption was FALSE and caused real save corruption in practice (Task 11
        // in-game verification): IL inspection of the target RimWorld version's decompiled
        // Assembly-CSharp.dll shows Pawn_AgeTracker.AgeTickInterval is this method's ONLY caller,
        // and it also re-fires once on the first tick after ANY save load for potentially every
        // pawn on the map (Verse.Pawn_AgeTracker.cachedLifeStageIndex is not restored by
        // ExposeData, so vanilla's own cache reads as stale immediately post-load and gets
        // "resynced" via this same event) — not exclusively at genuine life-stage transitions.
        // The old guard could not tell a genuine transition from this load-time resync noise,
        // since both looked identical to it ("event fired, pawn not yet in the processed set");
        // the observed symptom was every already-adult pawn's skills being additively re-shifted
        // on every single reload. This dictionary fixes that by requiring an actually-observed
        // NotAdult -> Adult transition (using our own prior observation as the baseline, not
        // vanilla's), which resync noise for an already-adult pawn can never satisfy.
        private static readonly Dictionary<int, DevelopmentalStage> LastKnownStage = new Dictionary<int, DevelopmentalStage>();

        public static void Postfix(Pawn ___pawn)
        {
            var settings = PawnVarianceMod.Settings;
            if (!settings.applyVarianceOnGrowUp) return;
            if (___pawn == null || ___pawn.RaceProps == null || !___pawn.RaceProps.Humanlike) return; // this hook fires for every pawn including animals; without this gate every maturing animal would NRE in the appliers below

            DevelopmentalStage currentStage = ___pawn.DevelopmentalStage;
            bool hadBaseline = LastKnownStage.TryGetValue(___pawn.thingIDNumber, out DevelopmentalStage previousStage);
            LastKnownStage[___pawn.thingIDNumber] = currentStage; // record unconditionally, even for non-Adult/no-op firings, so later real transitions have an accurate baseline

            if (currentStage != DevelopmentalStage.Adult) return; // still developing (or a non-Adult resync) — nothing to do yet
            if (!hadBaseline || previousStage == DevelopmentalStage.Adult) return; // no prior baseline (can't confirm a genuine transition, e.g. first observation after a fresh load) or was already Adult last we checked — not a real transition, just vanilla's own cache resync; see the class-level comment
            // Reaching here means we ourselves observed this exact pawn as NotAdult on a prior
            // firing and now see Adult — a genuine, once-per-pawn-per-session transition.

            if (!settings.enableSkillVariance && !settings.enableTraitVariance && !settings.enablePassionVariance) return;
            if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

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

        // Not Scribe-persisted by design (deliberately session-only, consistent with the original
        // idempotency guard this replaced). Cleared when a game is loaded (Game.LoadGame) or a new
        // colony is started (Game.InitNewGame) so a thingIDNumber collision between separate save
        // files loaded in the same RimWorld session (IDs are assigned per-save, not globally
        // unique) can't misattribute one pawn's last-known stage to an unrelated pawn in a newly
        // loaded save.
        internal static void ClearForNewGame()
        {
            LastKnownStage.Clear();
        }

        private static void ApplySkillGrowthUp(Pawn pawn, float quality)
        {
            SkillVarianceApplier.Apply(pawn, quality); // identical logic to generation-time; additive, so safe on accumulated childhood levels
        }

        private static void ApplyTraitGrowthUp(Pawn pawn, float quality)
        {
            var settings = PawnVarianceMod.Settings;
            Dictionary<TraitDef, int> forced = TraitVarianceApplier.CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = TraitVarianceApplier.CaptureDisallowedTraits(pawn);

            var alreadyAdded = new HashSet<TraitDef>(pawn.story.traits.allTraits.Select(t => t.def).Where(forced.ContainsKey));

            foreach (KeyValuePair<TraitDef, int> kvp in forced)
            {
                TraitDef def = kvp.Key;
                int degree = kvp.Value;
                if (pawn.story.traits.HasTrait(def)) continue;

                bool disallowedToo = disallowed.Contains(def);
                var conflicting = pawn.story.traits.allTraits.FirstOrDefault(t => ConflictsWith(def, t.def));

                if (conflicting != null)
                {
                    if (forced.ContainsKey(conflicting.def) || alreadyAdded.Contains(conflicting.def))
                    {
                        Log.Error($"[PawnVarianceMod] Forced-vs-forced trait conflict on {pawn.LabelShort}: {def.defName} conflicts with already-present forced trait {conflicting.def.defName}; skipping {def.defName}.");
                        continue;
                    }
                    pawn.story.traits.RemoveTrait(conflicting, true);
                    Log.Message($"[PawnVarianceMod] Removed growth-moment trait {conflicting.def.defName} on {pawn.LabelShort} to make room for newly-forced {def.defName}.");
                }

                if (disallowedToo)
                    Log.Error($"[PawnVarianceMod] {def.defName} is simultaneously forced and disallowed for {pawn.LabelShort}; forced wins.");

                pawn.story.traits.GainTrait(new Trait(def, degree, true));
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
            // Pips, not distinct skills — matches PassionVarianceApplier's pip-based semantic
            // (Minor=1, Major=2), so existing growth-moment passions are weighed on the same scale
            // as the quality-derived target.
            int existingPips = pawn.skills.skills.Sum(r => r.passion == Passion.Major ? 2 : r.passion == Passion.Minor ? 1 : 0);
            int targetPips = Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Lerp(settings.passionCountMin, settings.passionCountMax, quality)),
                Mathf.RoundToInt(settings.passionCountMin),
                Mathf.RoundToInt(settings.passionCountMax));

            if (existingPips >= targetPips) return;

            PassionVarianceApplier.AddPassionsWithoutClearing(pawn, targetPips - existingPips);
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
