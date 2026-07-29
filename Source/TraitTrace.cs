using System.Collections.Generic;
using System.Text;
using RimWorld;
using Verse;

namespace PawnVarianceMod
{
    // The trait-side counterpart to PassionVarianceApplier's inline passion trace. Traits had no
    // diagnostic at all, which is why two passion bugs were found by reading a trace in one pass
    // while trait behaviour could only be reasoned about from source. Gated on verboseLogging and
    // built into a StringBuilder so one pawn's trace is a single contiguous log entry rather than a
    // dozen interleaved lines when many pawns generate at once (a raid generates them in a tight
    // loop, so per-line Log.Message would shuffle pawns together).
    //
    // Shared by both trait paths — generation (TraitVarianceApplier.Apply) and grow-up
    // (GrowthUpPatch.ApplyTraitGrowthUp) — so their output is directly comparable; the "mode" in the
    // header line is what tells them apart.
    internal static class TraitTrace
    {
        // Returns null when verbose logging is off. Every other method here no-ops on a null trace,
        // so call sites need no further gating.
        internal static StringBuilder Begin(Pawn pawn, float quality, string mode)
        {
            if (!PawnVarianceMod.Settings.verboseLogging) return null;

            var trace = new StringBuilder();
            trace.AppendLine($"[PawnVarianceMod] Trait assignment ({mode}) for {pawn.LabelShortCap} (quality {quality:F2})");
            trace.AppendLine($"  kind {pawn.kindDef?.defName ?? "none"}, age {pawn.ageTracker?.AgeBiologicalYears ?? -1}, stage {pawn.DevelopmentalStage}"
                + $", childhood {pawn.story?.Childhood?.defName ?? "none"}, adulthood {pawn.story?.Adulthood?.defName ?? "none"}");
            return trace;
        }

        // Dumps a trait list with each entry's protection verdict and reason. Used for both the
        // incoming and the final list so a before/after diff is readable at a glance.
        internal static void AppendTraits(StringBuilder trace, string caption, List<Trait> traits, TraitProtection protection)
        {
            if (trace == null) return;

            trace.AppendLine($"  {caption} ({traits.Count}):");
            if (traits.Count == 0)
            {
                trace.AppendLine("    (none)");
                return;
            }

            foreach (Trait trait in traits)
            {
                string reason = protection.ProtectionReason(trait);
                trace.AppendLine($"    {Describe(trait),-32} {(reason == null ? "removable" : "PROTECTED — " + reason)}");
            }
        }

        // Degree matters here: a trait's identity is def+degree (Nervous vs Volatile are both
        // NaturalMood), and the real-degree fallback for forced traits is an explicit, in-game-verified
        // divergence from vanilla — so a trace that printed only defNames could not show whether it
        // had fired correctly.
        internal static string Describe(Trait trait)
        {
            return trait.def.defName + (trait.Degree == 0 ? string.Empty : $"({trait.Degree})");
        }

        // Renders TraitAgeCap's int.MaxValue "not binding" sentinel as something readable rather than
        // as 2147483647, which reads like a bug in a log.
        internal static string DescribeAgeCap(int cap)
        {
            return cap == int.MaxValue ? "none (fully grown)" : cap.ToString();
        }

        internal static void Flush(StringBuilder trace)
        {
            if (trace != null) Log.Message(trace.ToString().TrimEnd());
        }
    }
}
