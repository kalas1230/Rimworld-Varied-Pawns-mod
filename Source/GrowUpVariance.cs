using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // The single entry point for grow-up variance. Three triggers reach it — the life-stage change
    // when no growth letter is outstanding, the growth-moment letter being resolved, and the
    // fallback sweep — and all three must behave identically, which is why this lives in one place
    // rather than being duplicated per trigger.
    public static class GrowUpVariance
    {
        // triggerPath is purely diagnostic: it names which of the three routes got here, so a
        // verbose trace says why the pass ran at the moment it did.
        public static void Apply(Pawn pawn, string triggerPath)
        {
            if (pawn == null) return;
            // Same reason as the generation postfix: Humanlike does not guarantee these optional
            // trackers exist, and everything below walks pawn.skills.skills / pawn.story.traits
            // unguarded. This path reaches pawns that were never seen by the generation gate.
            if (pawn.skills?.skills == null || pawn.story?.traits == null) return;

            var settings = PawnVarianceMod.Settings;

            // The decline checks live INSIDE the try, not above it: GrowUpPendingComponent's sweep
            // calls this straight from GameComponentTick with no handler of its own, so anything that
            // throws here — Faction.OfPlayer being null, most plausibly — would escape into vanilla's
            // component-tick loop and repeat every sweep. The suppression logging is not worth
            // throwing for.
            try
            {
                // Repeated here even though DevelopmentalStage_Postfix already checked both at
                // registration time: settings can change (or the pawn's faction can change hostility)
                // during however long the growth-moment letter sits pending, and this is the one place
                // all three trigger paths funnel through.
                if (!settings.applyVarianceToChildren)
                {
                    if (settings.verboseLogging)
                        Log.Message($"[PawnVarianceMod] Suppressed grow-up variance for {pawn.LabelShort} ({triggerPath}): applyVarianceToChildren is off.");
                    return;
                }
                if (settings.IsExcludedAsHostile(pawn, null))
                {
                    if (settings.verboseLogging)
                        Log.Message($"[PawnVarianceMod] Suppressed grow-up variance for {pawn.LabelShort} ({triggerPath}): hostile pawn and applyToHostilePawns is off.");
                    return;
                }
                if (pawn.Dead || pawn.DestroyedOrNull())
                {
                    if (settings.verboseLogging)
                        Log.Message($"[PawnVarianceMod] Suppressed grow-up variance for {pawn.LabelShort} ({triggerPath}): pawn is dead or destroyed.");
                    return;
                }

                // Same per-pawn profile resolution as generation: a hostile faction's child growing
                // up is generated from the hostile profile, not the player's.
                VarianceProfileValues v = settings.ValuesFor(pawn);

                float quality = QualityRoller.RollQuality(v);

                // Ordering matches the main postfix (HarmonyPatches.cs): trait, then skill, then
                // passion — trait variance can disable work tags, which passion placement's
                // TotallyDisabled exclusion depends on.
                if (v.enableTraitVariance) ApplyTraitGrowthUp(pawn, quality, triggerPath, v);
                if (v.enableSkillVariance) ApplySkillGrowthUp(pawn, quality, v);
                if (v.enablePassionVariance) ApplyPassionGrowthUp(pawn, quality, v);
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Exception applying growth-up variance to {pawn.LabelShort}: {ex}");
            }
        }

        // Off by default, and deliberately so. The trait and passion passes at 13 only ever top a
        // pawn up — neither can remove a trait or downgrade a passion — because vanilla genuinely
        // generates traits and passions at the growth moment and this mod's job is to vary that
        // roll. Skills are the exception: vanilla's only skill change at a growth moment is the
        // chosen trait's additive skillGains, so there is no vanilla skill randomness here to
        // vary, and a child's levels are a play record rather than a roll. Shifting them is a real
        // divergence from vanilla, hence the opt-in and its own clamped range.
        private static void ApplySkillGrowthUp(Pawn pawn, float quality, VarianceProfileValues v)
        {
            var settings = PawnVarianceMod.Settings;
            if (!v.applyChildSkillShift)
            {
                // Logged rather than silent: every other decision on this path leaves a trace, and
                // an unexplained absence of the skill step is exactly what reads as a bug in a log.
                if (settings.verboseLogging)
                    Log.Message($"[PawnVarianceMod] Skill shift skipped for {pawn.LabelShortCap} at grow-up: 'Also shift skills at 13' is off (traits and passions still apply).");
                return;
            }

            SkillVarianceApplier.ApplyGrowUp(pawn, quality, v);
        }

        private static void ApplyPassionGrowthUp(Pawn pawn, float quality, VarianceProfileValues v)
        {
            // Pips, not distinct skills, so existing growth-moment passions are weighed on the same
            // scale as the rolled budget. The prices must match what AssignPassions' spend loop
            // actually charges — Minor 1, Major 1.5 — not the round numbers you might assume. Charging
            // an existing Major 2 here (as this did before) over-counts by 0.5 per Major and lands
            // grown-up children under the passion slider, which is the mirror image of the
            // growth-moment stacking bug this whole path exists to fix.
            float existingPips = pawn.skills.skills.Sum(
                r => r.passion == Passion.Major ? Constants.MajorPassionCost
                   : r.passion == Passion.Minor ? Constants.MinorPassionCost : 0f);
            PassionVarianceApplier.AssignPassions(pawn, quality, existingPips, v);
        }

        private static void ApplyTraitGrowthUp(Pawn pawn, float quality, string triggerPath, VarianceProfileValues v)
        {
            var settings = PawnVarianceMod.Settings;
            Dictionary<TraitDef, int> forced = TraitVarianceApplier.CaptureForcedTraits(pawn);
            HashSet<TraitDef> disallowed = TraitVarianceApplier.CaptureDisallowedTraits(pawn);

            var trace = TraitTrace.Begin(pawn, quality, $"grow-up: {triggerPath}", v);
            // Built before the forced-trait pass below mutates anything, but reported against the same
            // TraitProtection instance the target maths uses further down, so "incoming" and "final"
            // are judged by identical rules.
            var incoming = trace == null ? null : new List<Trait>(pawn.story.traits.allTraits);

            var alreadyAdded = new HashSet<TraitDef>(pawn.story.traits.allTraits.Select(t => t.def).Where(forced.ContainsKey));

            foreach (KeyValuePair<TraitDef, int> kvp in forced)
            {
                TraitDef def = kvp.Key;
                int degree = kvp.Value;
                if (pawn.story.traits.HasTrait(def)) continue;

                bool disallowedToo = disallowed.Contains(def);
                var conflicting = pawn.story.traits.allTraits.FirstOrDefault(t => def.ConflictsWith(t.def));

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

                // See TraitVarianceApplier.Apply's sourceGene comment: a newly-forced gene trait
                // needs sourceGene set and suppressConflicts:true to match vanilla's real
                // Pawn_GeneTracker.AddGene grant, or it's permanently orphaned from gene-removal
                // tracking. kindDef-forced traits have no such mechanism, so they stay as plain grants.
                Gene forcingGene = TraitVarianceApplier.FindForcingGene(pawn, def);
                var newTrait = new Trait(def, degree, true);
                if (forcingGene != null) newTrait.sourceGene = forcingGene;
                pawn.story.traits.GainTrait(newTrait, suppressConflicts: forcingGene != null);
                alreadyAdded.Add(def);
                trace?.AppendLine($"  FORCED GRANT: {TraitTrace.Describe(newTrait)}"
                    + (forcingGene != null ? $" (sourceGene {forcingGene.def.defName}, conflicts suppressed)" : " (kindDef)"));
            }

            int currentCount = pawn.story.traits.allTraits.Count;

            // Same target semantics as generation-time Apply, so a pawn's trait count means the same
            // thing however it was produced. No PawnGenerationRequest exists this late (the pawn
            // already exists, mid-game), so request-forced traits can't be classified here — that's
            // fine, they were applied at generation and this path only ever adds.
            var protection = TraitProtection.Build(pawn, null);
            TraitTrace.AppendTraits(trace, "incoming", incoming, protection);
            int protectedCount = pawn.story.traits.allTraits.Count(t => protection.IsProtected(t));

            float targetMean = Mathf.Lerp(v.traitCountMin, v.traitCountMax, quality);
            int rolledTarget = Mathf.Clamp(
                Mathf.RoundToInt(targetMean),
                Mathf.RoundToInt(v.traitCountMin),
                Mathf.RoundToInt(v.traitCountMax));

            int ageCap = TraitAgeCap.MaxRolledTraitsFor(pawn);
            int uncappedTarget = rolledTarget;
            rolledTarget = Mathf.Min(rolledTarget, ageCap);

            int targetCount = v.countProtectedTraits
                ? Mathf.Max(protectedCount, rolledTarget)
                : protectedCount + rolledTarget;

            if (trace != null)
            {
                // No jitter on this path, unlike generation-time Apply — noted so a side-by-side
                // comparison of the two traces doesn't read the missing term as a lost line.
                trace.AppendLine($"  target: lerp({v.traitCountMin:F0}..{v.traitCountMax:F0}, q) = {targetMean:F2} (no jitter on this path) -> rolled {uncappedTarget}"
                    + $", age cap {TraitTrace.DescribeAgeCap(ageCap)}"
                    + (rolledTarget != uncappedTarget ? $" -> CAPPED to {rolledTarget}" : string.Empty));
                trace.AppendLine($"  countProtectedTraits {(v.countProtectedTraits ? "ON (target is total traits)" : "off (rolled added on top of protected)")}"
                    + $": protected {protectedCount}, rolled {rolledTarget} -> target {targetCount} vs current {currentCount}");
            }

            // Add-only by design: never remove on grow-up, even if the pawn is already above target.
            if (currentCount >= targetCount)
            {
                trace?.AppendLine($"  already at or above target ({currentCount} >= {targetCount}) — add-only path, nothing removed");
                TraitTrace.AppendTraits(trace, "final", new List<Trait>(pawn.story.traits.allTraits), protection);
                TraitTrace.Flush(trace);
                return;
            }

            // Fill remaining slots via vanilla's own real trait picker — no PawnGenerationRequest is
            // available this late (pawn already exists, mid-game), so req is null: this still gets
            // vanilla's commonality weighting, conflict checks, backstory disallows, and mental-break
            // gate, just without the kindDef-specific checks that need a request (disallowedTraits,
            // requiredWorkTags, hostile-spawn allowance) — `disallowed` above already covers the
            // kindDef.disallowedTraits gap for the forced-trait path; the general fill has no
            // equivalent substitute here, an accepted gap versus generation-time.
            int requested = targetCount - currentCount;
            List<Trait> generated = PawnGenerator.GenerateTraitsFor(pawn, requested, null, growthMomentTrait: true);
            foreach (Trait trait in generated)
                pawn.story.traits.GainTrait(trait);

            trace?.AppendLine($"  ADDED {generated.Count} of {requested} requested via vanilla GenerateTraitsFor (growthMomentTrait: true): "
                + (generated.Count == 0 ? "(picker returned none)" : string.Join(", ", generated.Select(TraitTrace.Describe))));
            TraitTrace.AppendTraits(trace, "final", new List<Trait>(pawn.story.traits.allTraits), protection);
            TraitTrace.Flush(trace);
        }
    }
}
