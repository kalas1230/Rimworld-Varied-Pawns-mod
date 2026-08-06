using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class TraitVarianceApplier
    {
        // Trait selection itself is delegated entirely to vanilla's own public
        // PawnGenerator.GenerateTraitsFor — the real commonality-weighted picker, with every real
        // vanilla rejection rule (backstory disallows, required work types/tags, gender/sexuality
        // edge cases, hostile-spawn allowance, the mental-break stat gate, degree weighting, etc.)
        // applied exactly as vanilla does it. This mod's only remaining job for traits is: (1) classify
        // which traits are protected from removal, and (2) decide how many total traits the pawn should
        // have, driven by quality/traitCountMin/Max. The trait list is reconciled in place — never
        // cleared — so no trait state belonging to vanilla or another mod is ever destroyed and rebuilt.
        // There is no
        // longer a quality-driven "desirability" scoring step — vanilla's own picker has no concept
        // of trait quality at all, so that axis of variance no longer exists for traits (traitNoise
        // was removed accordingly; see settings).
        public static void Apply(Pawn pawn, float quality, PawnGenerationRequest request, VarianceProfileValues v)
        {
            // pawn.story is optional on a Humanlike race (HAR can switch it off); callers gate on
            // this too, but this is a public entry point.
            if (pawn?.story?.traits == null) return;

            var protection = TraitProtection.Build(pawn, request);
            List<Trait> current = pawn.story.traits.allTraits;

            var trace = TraitTrace.Begin(pawn, quality, "generation", v);
            TraitTrace.AppendTraits(trace, "incoming", new List<Trait>(current), protection);

            int protectedCount = 0;
            var removable = new List<Trait>();
            foreach (Trait trait in current)
            {
                if (protection.IsProtected(trait)) protectedCount++;
                else removable.Add(trait);
            }

            float targetMean = Mathf.Lerp(v.traitCountMin, v.traitCountMax, quality);
            float jitter = JitterSample();
            int rolledTarget = Mathf.Clamp(
                Mathf.RoundToInt(targetMean + jitter),
                Mathf.RoundToInt(v.traitCountMin),
                Mathf.RoundToInt(v.traitCountMax));

            int ageCap = TraitAgeCap.MaxRolledTraitsFor(pawn);
            int uncappedTarget = rolledTarget;
            rolledTarget = Mathf.Min(rolledTarget, ageCap);

            // Protected traits are a floor, never part of the budget: at a target of 0 an Yttakin still
            // keeps its xenotype-forced Psychically Dull and ends with exactly one trait.
            int desiredTotal = v.countProtectedTraits
                ? Mathf.Max(protectedCount, rolledTarget)
                : protectedCount + rolledTarget;

            int delta = desiredTotal - current.Count;

            if (trace != null)
            {
                trace.AppendLine($"  target: lerp({v.traitCountMin:F0}..{v.traitCountMax:F0}, q) = {targetMean:F2} + jitter {jitter:+0.00;-0.00} -> rolled {uncappedTarget}"
                    + $", age cap {TraitTrace.DescribeAgeCap(ageCap)}"
                    + (rolledTarget != uncappedTarget ? $" -> CAPPED to {rolledTarget}" : string.Empty));
                trace.AppendLine($"  countProtectedTraits {(v.countProtectedTraits ? "ON (target is total traits)" : "off (rolled added on top of protected)")}"
                    + $": protected {protectedCount}, rolled {rolledTarget} -> desired total {desiredTotal} vs current {current.Count} = delta {delta:+0;-0;0}");
            }

            if (delta > 0)
            {
                // Vanilla's own commonality-weighted picker, with every real rejection rule applied.
                // Threading the real request through gives it the kindDef disallowed-traits,
                // required-work-tag and hostile-spawn checks it can only do with one.
                List<Trait> generated = PawnGenerator.GenerateTraitsFor(pawn, delta, request, growthMomentTrait: false);
                foreach (Trait trait in generated)
                    pawn.story.traits.GainTrait(trait);

                // Vanilla's picker returns fewer than asked when it runs out of non-conflicting
                // candidates, so log what it actually produced rather than what we requested.
                trace?.AppendLine($"  ADDED {generated.Count} of {delta} requested via vanilla GenerateTraitsFor: "
                    + (generated.Count == 0 ? "(picker returned none)" : string.Join(", ", generated.Select(TraitTrace.Describe))));
            }
            else if (delta < 0)
            {
                // Remove through vanilla's real TraitSet.RemoveTrait rather than clearing the backing
                // list. That runs vanilla's own ability cleanup and fires other mods' RemoveTrait
                // patches — notably Vanilla Traits Expanded's, which drops the VTE_SlowWorkSpeed hediff
                // it attached on GainTrait. Bypassing it (as allTraits.Clear() did) left that hediff
                // orphaned, and it then removed itself from inside Pawn_HealthTracker.Notify_Spawned's
                // foreach over hediffSet.hediffs, throwing "Collection was modified" and aborting the
                // pawn's spawn. Uniform random choice: vanilla's picker has no concept of a "better"
                // trait, so there is no quality axis to bias this by.
                int toRemove = Mathf.Min(-delta, removable.Count);

                // toRemove < -delta means protection held the line: the pawn stays above target
                // because everything left is unremovable. That is correct behaviour, not a failure,
                // but it is the single most likely explanation for "why does this pawn have too many
                // traits", so name it explicitly.
                if (trace != null && toRemove < -delta)
                    trace.AppendLine($"  wanted to remove {-delta} but only {removable.Count} removable — {(-delta) - toRemove} over target, all remaining traits are protected");

                for (int i = 0; i < toRemove; i++)
                {
                    Trait victim = removable.RandomElement();
                    removable.Remove(victim);
                    pawn.story.traits.RemoveTrait(victim);
                    trace?.AppendLine($"  REMOVED {TraitTrace.Describe(victim)} (uniform random among removable)");
                }
            }
            else if (trace != null)
            {
                trace.AppendLine("  no change needed");
            }

            // No sexuality-trait call here, deliberately. Vanilla's own roll already ran during
            // generation and — because nothing is cleared any more — it survives untouched. Calling
            // PawnGenerator.TryGenerateSexualityTraitFor again would give every straight pawn a second
            // independent roll and skew the population's sexuality distribution away from vanilla's.

            TraitTrace.AppendTraits(trace, "final", new List<Trait>(pawn.story.traits.allTraits), protection);
            TraitTrace.Flush(trace);
        }

        // Returns TraitDef -> degree, not just a set of TraitDefs: both real forced-trait sources
        // (PawnKindDef.forcedTraits: List<TraitRequirement>, GeneDef.forcedTraits:
        // List<GeneticTraitData>) specify a degree for the trait they force. Verified against
        // decompiled Assembly-CSharp.dll (Task 11 in-game verification) — granting a forced trait
        // with a hardcoded degree 0 instead of its real specified degree produces an invalid Trait
        // instance for any multi-degree trait without a degree-0 entry (e.g. PsychicSensitivity),
        // which vanilla logs an error for on every stat/tick lookup involving that trait, not just
        // once at generation. NB: vanilla's own GenerateTraits uses a plain `GetValueOrDefault()`
        // (defaults to 0) for this same nullable case — we deliberately keep our smarter fallback
        // since it's what fixed a real in-game error; not reverting to match vanilla exactly here.
        public static Dictionary<TraitDef, int> CaptureForcedTraits(Pawn pawn)
        {
            var forced = new Dictionary<TraitDef, int>();

            if (pawn.kindDef?.forcedTraits != null)
                foreach (var t in pawn.kindDef.forcedTraits)
                    forced[t.def] = t.degree ?? FirstValidDegree(t.def); // TraitRequirement.degree is nullable — null means "any degree", so fall back to a real defined one

            if (ModsConfig.BiotechActive && pawn.genes != null)
                foreach (var gene in pawn.genes.GenesListForReading)
                    if (gene.def.forcedTraits != null)
                        foreach (var t in gene.def.forcedTraits)
                            forced[t.def] = t.degree; // GeneticTraitData.degree is non-nullable, always trust it directly

            return forced;
        }

        // Third forced-trait source: the pawn's own selected BackstoryDefs (Childhood/Adulthood),
        // each with its own optional forcedTraits list — separate from PawnKindDef.forcedTraits and
        // gene forcedTraits (see the comment on Apply above). BackstoryTrait.degree is non-nullable
        // (unlike TraitRequirement.degree on the kindDef path), so no FirstValidDegree fallback is
        // needed here.
        public static Dictionary<TraitDef, int> CaptureBackstoryForcedTraits(Pawn pawn)
        {
            var forced = new Dictionary<TraitDef, int>();

            void CaptureFrom(BackstoryDef backstory)
            {
                if (backstory?.forcedTraits == null) return;
                foreach (var t in backstory.forcedTraits)
                    forced[t.def] = t.degree;
            }

            // Both current callers gate on pawn.story upstream, so this is defence in depth rather
            // than a live path — but this is a public helper and the next caller may not.
            if (pawn?.story == null) return forced;

            CaptureFrom(pawn.story.Childhood);
            CaptureFrom(pawn.story.Adulthood);

            return forced;
        }

        // Fourth forced-trait source: see the comment on Apply above. PawnGenerationRequest.
        // ForcedTraits is a plain List<TraitDef> with no degree data, so unlike
        // CaptureBackstoryForcedTraits there is genuinely no real degree to trust — this uses the
        // same FirstValidDegree fallback as the kindDef nullable-degree case.
        public static Dictionary<TraitDef, int> CaptureRequestForcedTraits(PawnGenerationRequest request)
        {
            var forced = new Dictionary<TraitDef, int>();
            if (request.ForcedTraits != null)
                foreach (var def in request.ForcedTraits)
                    if (def != null)
                        forced[def] = FirstValidDegree(def);
            return forced;
        }

        // Which active gene (if any) forces this TraitDef — used by GrowthUpPatch so a newly-forced
        // gene trait added mid-childhood gets the same real Trait.sourceGene link (and
        // suppressConflicts: true grant) vanilla's own Pawn_GeneTracker.AddGene would have given it,
        // instead of an orphaned plain Trait (see the sourceGene comment on Apply above).
        public static Gene FindForcingGene(Pawn pawn, TraitDef def)
        {
            if (!ModsConfig.BiotechActive || pawn.genes == null) return null;
            foreach (var gene in pawn.genes.GenesListForReading)
                if (gene.def.forcedTraits != null)
                    foreach (var t in gene.def.forcedTraits)
                        if (t.def == def) return gene;
            return null;
        }

        // Fallback for a forced-trait source that doesn't specify a degree: use the trait's own
        // first genuinely-defined degree rather than an assumed 0, which may not exist for that trait.
        private static int FirstValidDegree(TraitDef def)
        {
            return (def.degreeDatas != null && def.degreeDatas.Count > 0) ? def.degreeDatas[0].degree : 0;
        }

        // Still needed by GrowthUpPatch's forced-vs-disallowed warning check — vanilla's own
        // GenerateTraitsFor already applies PawnKindDef.disallowedTraits itself when given a real
        // PawnGenerationRequest (see Apply above), so this is no longer used to filter candidates,
        // only to flag the edge case of a trait being simultaneously forced and disallowed.
        public static HashSet<TraitDef> CaptureDisallowedTraits(Pawn pawn)
        {
            var disallowed = new HashSet<TraitDef>();
            if (pawn.kindDef?.disallowedTraits != null)
                foreach (var t in pawn.kindDef.disallowedTraits) disallowed.Add(t); // PawnKindDef.disallowedTraits is List<TraitDef> directly, unlike forcedTraits' List<TraitRequirement>

            // Deliberately no Ideology-sourced entries here: verified against decompiled source
            // (Global Constraints) that Ideology has no hard "forbid this trait" mechanism in this
            // RimWorld version — PreceptDef has no disallowedTraits field. The only trait-related
            // Ideology concept is MemeDef.disagreeableTraits, a soft mood/certainty preference, not
            // a hard exclusion; conflating the two would make this mod forbid traits vanilla itself
            // only disapproves of, which is a real behavior change beyond what this spec intended.

            return disallowed;
        }

        private static float JitterSample()
        {
            return (Rand.Value - 0.5f) * Constants.SmallRandomJitter;
        }
    }
}
