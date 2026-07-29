using System.Collections.Generic;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // Tracks pawns that have become adult but whose age-13 growth moment has not resolved yet, so
    // the grow-up pass can run afterwards and count what vanilla granted.
    //
    // Unlike DevelopmentalStage_Postfix's session-only LastKnownStage dictionary, this IS scribed:
    // the growth-moment letter survives save/load and can sit for two in-game days, so the pending
    // state has to survive with it. A GameComponent is per-save, so the cross-save thingIDNumber
    // collision hazard that forces LastKnownStage to clear on load does not apply here.
    public class GrowUpPendingComponent : GameComponent
    {
        private const int SweepIntervalTicks = 2500;

        // Parallel lists rather than a Dictionary<Pawn, int>: Scribe resolves Pawn references AFTER
        // collections are rebuilt, so a dictionary keyed by pawn hashes its keys before they point
        // at anything. Parallel lists are the standard RimWorld idiom for exactly this reason.
        private List<Pawn> pendingPawns = new List<Pawn>();
        private List<int> pendingSinceTicks = new List<int>();

        public GrowUpPendingComponent(Game game)
        {
        }

        public static GrowUpPendingComponent Instance =>
            Verse.Current.Game?.GetComponent<GrowUpPendingComponent>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingPawns, "pendingGrowUpPawns", LookMode.Reference);
            Scribe_Collections.Look(ref pendingSinceTicks, "pendingGrowUpSinceTicks", LookMode.Value);

            if (Scribe.mode != LoadSaveMode.PostLoadInit) return;

            if (pendingPawns == null) pendingPawns = new List<Pawn>();
            if (pendingSinceTicks == null) pendingSinceTicks = new List<int>();

            // A null entry means the referenced pawn no longer exists in the save. Drop those, and
            // drop everything if the two lists ever disagree in length rather than risk pairing a
            // pawn with another pawn's timestamp.
            if (pendingPawns.Count != pendingSinceTicks.Count)
            {
                Log.Warning($"[PawnVarianceMod] Pending grow-up lists out of sync ({pendingPawns.Count} vs {pendingSinceTicks.Count}); clearing.");
                pendingPawns.Clear();
                pendingSinceTicks.Clear();
                return;
            }

            for (int i = pendingPawns.Count - 1; i >= 0; i--)
            {
                if (pendingPawns[i] != null) continue;
                pendingPawns.RemoveAt(i);
                pendingSinceTicks.RemoveAt(i);
            }
        }

        public void Register(Pawn pawn)
        {
            if (pawn == null || pendingPawns.Contains(pawn)) return;
            pendingPawns.Add(pawn);
            pendingSinceTicks.Add(Find.TickManager.TicksGame);
        }

        public bool Deregister(Pawn pawn, out int ticksPending)
        {
            ticksPending = 0;
            int index = pendingPawns.IndexOf(pawn);
            if (index < 0) return false;

            ticksPending = Find.TickManager.TicksGame - pendingSinceTicks[index];
            pendingPawns.RemoveAt(index);
            pendingSinceTicks.RemoveAt(index);
            return true;
        }

        // The sweep's real job is cleaning up a pawn that died or was otherwise lost while pending.
        // The "letter vanished unresolved" case is near-unreachable — vanilla force-opens the dialog
        // on the last tick before timeout and refuses to let it close unchosen — so it is covered by
        // the condition below rather than by dedicated machinery.
        public override void GameComponentTick()
        {
            if (pendingPawns.Count == 0) return;
            if (Find.TickManager.TicksGame % SweepIntervalTicks != 0) return;

            for (int i = pendingPawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = pendingPawns[i];

                if (pawn == null || pawn.Dead || pawn.Destroyed)
                {
                    pendingPawns.RemoveAt(i);
                    pendingSinceTicks.RemoveAt(i);
                    continue;
                }

                // Looks racy against the deadline: HasUnresolvedGrowthLetter goes false the instant
                // TimeoutPassed trips, which sounds like it could fire while the player is mid-dialog.
                // It can't — Dialog_GrowthMomentChoices's constructor sets forcePause = true, so ticks
                // (and TicksGame) stop the moment the dialog opens and can never advance past the
                // timeout while a choice is pending. Also, Find.LetterStack.RemoveLetter runs AFTER
                // MakeChoices, so the letter is still on the stack — and still findable here — at the
                // moment our own MakeChoices postfix processes it.
                if (HasUnresolvedGrowthLetter(pawn)) continue;

                int ticksPending = Find.TickManager.TicksGame - pendingSinceTicks[i];
                pendingPawns.RemoveAt(i);
                pendingSinceTicks.RemoveAt(i);
                GrowUpVariance.Apply(pawn, $"fallback sweep after {ticksPending} ticks pending");
            }
        }

        public static bool HasUnresolvedGrowthLetter(Pawn pawn)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null) return false;

            for (int i = 0; i < letters.Count; i++)
            {
                if (letters[i] is ChoiceLetter_GrowthMoment growthLetter
                    && growthLetter.pawn == pawn
                    && !growthLetter.choiceMade
                    && !growthLetter.TimeoutPassed)
                    return true;
            }
            return false;
        }
    }
}
