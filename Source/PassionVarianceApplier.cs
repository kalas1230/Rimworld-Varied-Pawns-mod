using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Mirrors vanilla PawnGenerator.GenerateSkills' real passion-assignment algorithm (decompiled
    // and re-verified against this RimWorld version's Assembly-CSharp.dll on 2026-08-06, at
    // GenerateSkills:1846-1955) — its INTENDED algorithm. There are three places where vanilla's
    // implementation contradicts its own design and we deliberately do not copy it; each is
    // marked "DELIBERATE DEPARTURE" below with the vanilla line range and the reason. Do not
    // "restore fidelity" at any of them without reading the argument there first.
    // The budget roll, the 1.5/1 price list, the 4-sigma clamp, the level-ordered walk by
    // GetLevel(includeAptitudes: false), the trait/gene exclusions and the gene bump ARE exact.
    //
    // The algorithm, rather than a custom
    // scheme: roll a random "passion budget", spend it turn-by-turn as Major (costs 1.5) or Minor
    // (costs 1) via a weighted coin flip, then hand out ALL rolled Majors to the pawn's
    // highest-level eligible skills first (in strict descending-level order) before any Minors —
    // vanilla's own CreatePassion local function checks `majorPassions > 0` before `minorPassions`,
    // so the top-ranked skill(s) always get first claim on Major. This is what makes "proficient
    // skills get Major far more often" happen for free, as a consequence of vanilla's own shape,
    // rather than a bolted-on bias. The only two numbers this mod makes tunable that vanilla hardcodes
    // are: the budget's mean (vanilla: flat 5, here: quality-lerped between passionCountMin/Max) and
    // spread (vanilla: fixed widthFactor 1 / clamp 4, here: passionNoise-driven), and the coin flip's
    // probability of Major (vanilla: flat Rand.Bool = 50%, here: passionMajorBias).
    public static class PassionVarianceApplier
    {
        public static void Apply(Pawn pawn, float quality, VarianceProfileValues v)
        {
            // Callers gate on this too; repeated here because pawn.skills is optional on a
            // Humanlike race (HAR can switch it off) and this is a public entry point.
            //
            // MUST match AssignPassions' guard exactly, story tracker included. This method wipes
            // every passion to None before delegating, so a guard that admits a pawn AssignPassions
            // will then reject leaves that pawn permanently passionless — the wipe happens, the
            // reassignment does not, and nothing logs. A narrower guard here is strictly worse than
            // no guard at all, which at least threw where the postfix could catch and report it.
            if (pawn?.skills?.skills == null || pawn.story?.traits == null) return;

            foreach (SkillRecord record in pawn.skills.skills)
                record.passion = Passion.None;

            AssignPassions(pawn, quality, alreadyCommittedPips: 0f, v: v);
        }

        // alreadyCommittedPips lets GrowUpVariance top up a pawn's existing growth-moment passions
        // toward the same quality-derived budget, rather than rolling a whole second budget on top.
        // Float, not int, because the spend loop below prices a Major at 1.5 — so a caller counting
        // existing passions has to be able to express 1.5 too, or Majors get over-charged.
        public static void AssignPassions(Pawn pawn, float quality, float alreadyCommittedPips, VarianceProfileValues v)
        {
            // Public entry point in its own right (GrowUpVariance calls it directly), and the
            // eligibility checks below read pawn.story.traits as well as pawn.skills.
            if (pawn?.skills?.skills == null || pawn.story?.traits == null) return;

            var settings = PawnVarianceMod.Settings;
            var trace = settings.verboseLogging ? new System.Text.StringBuilder() : null;

            float budgetMean = Mathf.Lerp(v.passionCountMin, v.passionCountMax, quality);
            float spread = Mathf.Lerp(Constants.PassionBudgetSpreadMin, Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
            float clampWindow = spread * Constants.PassionBudgetClampFactor;
            float budget = budgetMean + Mathf.Clamp(Rand.Gaussian(0f, spread), -clampWindow, clampWindow) - alreadyCommittedPips;

            // Vanilla's budget is `5 + clamp(Gaussian, -4, 4)`, so it bottoms out at 1 and every
            // adult ends up with at least one passion — a structural guarantee, not a lucky roll.
            // Our mean is lower and our spread is settable, so the Gaussian tail can land under 1
            // and produce a passionless pawn (observed: quality 0.29 against a 2-6 range, mean 3.15,
            // rolled 0 Major + 0 Minor). Restore vanilla's floor so the range's minimum reads as a
            // real minimum — except when the player has deliberately set that minimum to 0, which is
            // an explicit request for passionless pawns to be possible (the Desperate profile does
            // exactly this). Skipped when pips are already committed: the growth-up path only tops
            // up a pawn who by definition already has passions, so no guarantee is at stake.
            float rolledBudget = budget;
            bool flooredBudget = budget < 1f && v.passionCountMin > 0f && alreadyCommittedPips <= 0f;
            if (flooredBudget) budget = 1f;

            int minorPassions = 0;
            int majorPassions = 0;
            while (budget >= Constants.MinorPassionCost)
            {
                if (budget >= Constants.MajorPassionCost && Rand.Chance(v.passionMajorBias))
                {
                    majorPassions++;
                    budget -= Constants.MajorPassionCost;
                }
                else
                {
                    minorPassions++;
                    budget -= Constants.MinorPassionCost;
                }
            }

            // Vanilla excludes a skill from ever receiving a passion if any of the pawn's traits
            // declare TraitDef.conflictingPassions for it (e.g. Brawler vs. Shooting), or if an
            // active Biotech gene's GeneDef.passionMod is PassionModType.DropAll for it. This was a
            // real gap versus vanilla: our re-implementation used to only exclude TotallyDisabled
            // skills, meaning it could (and would) hand a Brawler a Shooting passion, which vanilla
            // structurally never does.
            bool IsConflicting(SkillDef def)
            {
                if (pawn.story.traits.allTraits.Any(t => t.def.ConflictsWithPassion(def))) return true;
                if (ModsConfig.BiotechActive && pawn.genes != null)
                {
                    foreach (Gene gene in pawn.genes.GenesListForReading)
                        if (gene.Active && gene.def.passionMod != null
                            && gene.def.passionMod.modType == PassionMod.PassionModType.DropAll
                            && gene.def.passionMod.skill == def)
                            return true;
                }
                return false;
            }

            var eligible = pawn.skills.skills
                .Where(r => !r.TotallyDisabled && !IsConflicting(r.def))
                .ToList();

            if (trace != null)
            {
                trace.AppendLine($"[PawnVarianceMod] Passion assignment for {pawn.LabelShortCap} (quality {quality:F2}, profile {v.profileLabel})");
                trace.AppendLine($"  budget mean {budgetMean:F2}, spread {spread:F2}, committed pips {alreadyCommittedPips:F2} (Minor 1, Major 1.5), rolled {rolledBudget:F2}"
                    + (flooredBudget ? $" -> FLOORED to 1.00 (minimum is {v.passionCountMin:F1}, not 0)" : string.Empty)
                    + $" -> {majorPassions} Major + {minorPassions} Minor");
                foreach (SkillRecord r in pawn.skills.skills)
                {
                    string state = r.TotallyDisabled ? "DISABLED"
                        : IsConflicting(r.def) ? "EXCLUDED (trait/gene conflict)"
                        : "eligible";
                    // The incoming passion, printed before anything below touches it. Without this the
                    // trace cannot prove the grow-up top-up actually preserved a child's existing
                    // growth-moment passions — `committed pips N` gives the total but never says which
                    // skills held them, so a lost or downgraded passion would be invisible here.
                    trace.AppendLine($"  {r.def.defName,-14} raw {r.GetLevel(includeAptitudes: false),2}  shown {r.Level,2}  had {r.passion,-5}  {state}");
                }
            }

            // Vanilla also force-guarantees at least a Minor passion (upgradeable to Major if budget
            // allows) on any skill a trait's TraitDef.forcedPassions names (e.g. Tortured Artist
            // always gets Artistic) — independent of that skill's level ranking. Real vanilla content
            // uses this (Tortured Artist), so silently never guaranteeing it was a real gap, not a
            // theoretical one. Only applies to skills with no passion yet, so an existing
            // growth-moment passion (GrowthUpPatch) is never downgraded or overwritten.
            //
            // TWO DELIBERATE DEPARTURES FROM VANILLA HERE, both kept because vanilla's version is
            // defective. Verified against the decompiled GenerateSkills 2026-08-06, lines 1871-1884:
            //
            //   1. Vanilla's forced pass skips only TotallyDisabled. It does NOT check
            //      conflictingPassions or DropAll genes, so it will hand a Brawler a forced
            //      Shooting passion — the exact outcome its own level-ordered walk goes out of its
            //      way to prevent twenty lines later. We iterate `eligible`, which excludes both.
            //      No shipped vanilla trait both forces and conflicts on one skill, so this is
            //      unreachable in vanilla content but reachable the moment a mod adds one.
            //
            //   2. Vanilla's inner `foreach (Trait ...)` has no `break` after a match, so a pawn
            //      with two traits that both force the same skill calls CreatePassion TWICE and is
            //      charged twice for one passion — the second call overwrites the first on the same
            //      SkillRecord, so the budget is silently burned for nothing. `.Any(...)` charges
            //      once, which is what the budget arithmetic everywhere else in this mod assumes.
            //
            // Reproducing either would mean reproducing a bug, and the composite score, the
            // envelope tool and the pip accounting in GrowUpVariance all assume one charge buys
            // one passion. "Mirrors vanilla" in this file means mirrors its INTENDED algorithm.
            foreach (SkillRecord record in eligible)
            {
                if (record.passion != Passion.None) continue;
                if (!pawn.story.traits.allTraits.Any(t => t.def.RequiresPassion(record.def))) continue;

                if (majorPassions > 0)
                {
                    record.passion = Passion.Major;
                    majorPassions--;
                }
                else
                {
                    record.passion = Passion.Minor;
                    if (minorPassions > 0) minorPassions--;
                }

                trace?.AppendLine($"  FORCED BY TRAIT: {record.def.defName} -> {record.passion} (jumps the level queue, matching vanilla)");
            }

            // Order by the RAW learned level, not SkillRecord.Level. Vanilla's own walk uses
            // GetLevel(includeAptitudes: false) (decompiled PawnGenerator.GenerateSkills), so a
            // Biotech aptitude gene deliberately does NOT push its skill up the passion queue —
            // the gene grants its passion bump separately, through GeneDef.passionMod, which is
            // re-applied at the end of this method. Ordering by the aptitude-inclusive Level here
            // double-counted the gene: it both jumped the queue and got its bump.
            // THIRD DELIBERATE DEPARTURE: `.Where(r => r.passion == Passion.None)`.
            // Vanilla's walk (GenerateSkills:1911-1941) calls CreatePassion on every eligible
            // skill in level order WITHOUT checking whether it already holds a passion, so a
            // trait-forced Minor assigned moments earlier gets overwritten to Major and charged a
            // second time. Net effect in vanilla: a Tortured Artist ends up with FEWER distinct
            // passions than an identical pawn without the trait, because part of the budget was
            // spent twice on Artistic. That is plainly not the intent of a trait whose whole
            // purpose is to GRANT a passion. We skip already-assigned skills, so every unit of
            // budget buys one distinct passion.
            var candidates = eligible
                .Where(r => r.passion == Passion.None)
                .OrderByDescending(r => r.GetLevel(includeAptitudes: false))
                .ToList();

            foreach (SkillRecord record in candidates)
            {
                if (majorPassions > 0)
                {
                    record.passion = Passion.Major;
                    majorPassions--;
                }
                else if (minorPassions > 0)
                {
                    record.passion = Passion.Minor;
                    minorPassions--;
                }
                else
                {
                    trace?.AppendLine($"  budget exhausted at {record.def.defName} (raw {record.GetLevel(includeAptitudes: false)}) — everything from here down gets nothing");
                    break;
                }

                trace?.AppendLine($"  by level: {record.def.defName} (raw {record.GetLevel(includeAptitudes: false)}) -> {record.passion}");
            }

            // Ran out of skills before running out of budget — the loop above just ends, so without
            // this the trace looks identical to a budget that came out exactly even.
            //
            // Still reachable, but NOT for the reason this comment used to give. It said "a 24-pip
            // budget buys 16 Majors but there are only 12 skills", which was true when a Major cost
            // 2 pips and the cap was 24. At MaxPassionPips = 18 (= 12 x 1.5) an all-Major budget
            // buys exactly 12 Majors for exactly 12 skills — dead even, no surplus. The surplus now
            // comes from the other two directions:
            //   - Minor-heavy rolls. A Minor costs 1 pip, so 18 pips can buy up to 18 passions for
            //     at most 12 skills.
            //   - `eligible` is often smaller than 12 — conflicting passions (Brawler vs Shooting),
            //     TotallyDisabled skills, and DropAll passion genes all shrink it.
            // Do not delete this guard on the grounds that the budget can no longer exceed 12.
            if (trace != null && (majorPassions > 0 || minorPassions > 0))
                trace.AppendLine($"  ran out of eligible skills with {majorPassions} Major + {minorPassions} Minor unspent (budget exceeds what {pawn.skills.skills.Count} skills can hold)");

            // Vanilla's Biotech gene passion bonus (GeneDef.passionMod, PassionModType.AddOneLevel —
            // real content: the AptitudeRemarkable_* genes, e.g. referenced in real XenotypeDefs) is
            // applied exactly once, permanently, at gene-add time (Pawn_GeneTracker.AddGene), which
            // for a freshly generated pawn runs before GenerateSkills ever touches passions — and
            // vanilla's own budget loop never resets passions to None first, so that bump can never
            // be lost in real vanilla play. Our Apply() above does reset every passion to None before
            // rerolling, which would otherwise silently erase this gene's bonus whenever our own
            // reroll's budget runs dry before reaching that skill. Re-apply it here as a floor
            // (upgrade only, never downgrade) so it survives our reroll the same way it always
            // survives vanilla's. Also refresh Gene.passionPreAdd, since AddGene's original snapshot
            // (whatever the passion was before its bump) is now stale after our reroll — leaving it
            // stale would make a later gene removal (Gene.NewPassionForOnRemoval) restore the wrong
            // pre-bump value.
            if (ModsConfig.BiotechActive && pawn.genes != null)
            {
                foreach (Gene gene in pawn.genes.GenesListForReading)
                {
                    if (!gene.Active || gene.def.passionMod == null
                        || gene.def.passionMod.modType != PassionMod.PassionModType.AddOneLevel)
                        continue;

                    SkillRecord record = pawn.skills.GetSkill(gene.def.passionMod.skill);

                    // Restore-if-lost, NOT upgrade-if-present. Vanilla applies this bump in
                    // Pawn_GeneTracker.AddGene, which runs BEFORE GenerateSkills — so the bump is
                    // already in place when vanilla's level-ordered walk runs, and the walk simply
                    // overwrites it (a gene-granted Minor becomes Major only if that skill ranks
                    // high enough to earn a Major on its own). Re-applying the bump after our walk
                    // instead let it stack on top of what the walk had just assigned, so a low-level
                    // skill that earned a Minor got promoted to Major — an outcome vanilla cannot
                    // produce through this path. Confirmed in a 70-pawn raid trace: 13 pawns had
                    // AptitudeRemarkable_Animals push a bottom-ranked Animals skill to Major.
                    // Only filling a None keeps all three vanilla outcomes intact: walk reached the
                    // skill (walk's value stands), or it didn't (the gene's Minor survives).
                    // BEFORE the bump, never after. passionPreAdd means "what this skill's passion
                    // was before this gene touched it", and Gene.NewPassionForOnRemoval puts that
                    // value back when the gene is removed (xenogerm implant, gene surgery, xenotype
                    // change). Snapshotting after the bump would record the gene's own grant as the
                    // pre-add value, so removal would restore the bonus instead of undoing it and
                    // the gene's effect would outlive the gene permanently. Vanilla's
                    // Pawn_GeneTracker.AddGene writes it in this same order for the same reason.
                    gene.passionPreAdd = record.passion;

                    if (record.passion == Passion.None)
                    {
                        Passion bumped = gene.def.passionMod.NewPassionFor(record);
                        trace?.AppendLine($"  GENE BUMP: {gene.def.defName} restored {record.def.defName} to {bumped} (walk never reached it)");
                        record.passion = bumped;
                    }
                }
            }

            if (trace != null) Log.Message(trace.ToString().TrimEnd());
        }
    }
}
