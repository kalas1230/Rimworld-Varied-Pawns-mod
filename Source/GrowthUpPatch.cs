using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
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
            if (!settings.applyVarianceToChildren) return;
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

            // The age-13 growth moment grants a trait and one or more passions, and it resolves
            // AFTER this point: BirthdayBiological sends its letter on the tick before
            // PostResolveLifeStageChange fires, and the player clicks it whenever they like. Applying
            // now would stack our full budget on top of that grant. So if a letter is outstanding,
            // wait for it — GrowthMomentMakeChoices_Postfix or the sweep will finish the job.
            //
            // No unresolved letter means one of three things, and all are safe to apply immediately:
            // either the pawn took vanilla's silent auto-apply path (non-player faction, not
            // notification-worthy, or a quest lodger — the grant already landed inline last tick), or
            // the growth tier offered nothing at all, or the player already resolved the letter
            // (via GrowthMomentMakeChoices_Postfix) in the window between BirthdayBiological sending
            // it and this hook firing.
            var pending = GrowUpPendingComponent.Instance;
            if (pending == null)
            {
                Log.Warning($"[PawnVarianceMod] GrowUpPendingComponent.Instance was null while processing {___pawn.LabelShort}'s life-stage change; cannot check for an outstanding growth-moment letter, so proceeding as if none exists.");
            }
            else if (GrowUpPendingComponent.HasUnresolvedGrowthLetter(___pawn))
            {
                pending.Register(___pawn);
                if (settings.verboseLogging)
                    Log.Message($"[PawnVarianceMod] {___pawn.LabelShortCap} became adult with a growth-moment letter outstanding — deferring variance until it resolves.");
                return;
            }

            GrowUpVariance.Apply(___pawn, "no unresolved letter");
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

    // The single point at which a growth moment's choices are actually applied: MakeChoices
    // increments the chosen passions, calls GainTrait plus TraitUtility.ApplySkillGainFromTrait, and
    // at exactly age 13 also runs PawnGenerator.TryGenerateSexualityTraitFor. Running our pass in a
    // postfix here means we observe the real grant instead of predicting it.
    //
    // Verified uncontested: a scan of all 512 installed mod assemblies found zero references to
    // ChoiceLetter_GrowthMoment or MakeChoices, so there is no patch-ordering conflict to manage.
    // Isolated as its own patch class per this mod's per-class patch isolation.
    [HarmonyPatch(typeof(ChoiceLetter_GrowthMoment), nameof(ChoiceLetter_GrowthMoment.MakeChoices))]
    public static class GrowthMomentMakeChoices_Postfix
    {
        public static void Postfix(ChoiceLetter_GrowthMoment __instance)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null) return;

            var pending = GrowUpPendingComponent.Instance;
            if (pending == null) return;
            if (!pending.Deregister(pawn, out int ticksPending)) return; // not one of ours — a growth moment at age 7 or 10
            // The letter can be re-opened from the History tab after the pawn was destroyed
            // (ChoiceLetter_GrowthMoment.ArchiveView keeps a destroyed pawn's letter visible), and its
            // OK button still calls MakeChoices even though MakeChoices itself grants nothing for a
            // dead/destroyed pawn. Deregister already ran above so the pending entry is consumed
            // either way; bail before building the log line or touching the pawn further.
            if (pawn.Dead || pawn.DestroyedOrNull()) return;

            // This postfix runs inside vanilla's UI dialog-close path, so an escaping exception would
            // break the dialog. Guard against corrupted saves (e.g., null SkillDef in chosenPassions).
            try
            {
                if (PawnVarianceMod.Settings.verboseLogging)
                {
                    string grantedTrait = __instance.chosenTrait != null && __instance.chosenTrait != ChoiceLetter_GrowthMoment.NoTrait
                        ? TraitTrace.Describe(__instance.chosenTrait)
                        : "none";
                    string grantedPassions = __instance.chosenPassions.NullOrEmpty()
                        ? "none"
                        : string.Join(", ", __instance.chosenPassions.Select(s => s.defName));
                    Log.Message($"[PawnVarianceMod] Growth moment resolved for {pawn.LabelShortCap} after {ticksPending} ticks: trait {grantedTrait}, passion increments {grantedPassions}");
                }

                GrowUpVariance.Apply(pawn, $"letter resolved after {ticksPending} ticks pending");
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception resolving growth moment for {pawn.LabelShort}: {ex}");
            }
        }
    }
}
