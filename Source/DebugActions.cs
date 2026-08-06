using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using LudeonTK;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Dev-mode only, by construction: RimWorld never surfaces a DebugAction outside the debug
    // menu, which is itself behind Prefs.DevMode. Nothing here is visible to a normal player, and
    // deliberately so -- a player should not have to care whether the mod is working. These exist
    // for the owner's in-game verification pass and for turning a vague bug report into numbers.
    //
    // This is the project's test harness. It is NOT a unit-test project, and that is a decision,
    // not a gap: the interesting code is Pawn-coupled, so the only way to exercise the real path
    // (Harmony postfix -> ValuesFor resolution -> the three appliers) is to actually generate
    // pawns inside a running game. An out-of-game test double would be testing a copy of the
    // logic rather than the logic.
    public static class DebugActions
    {
        private const string Category = "Varied Pawns";

        // ------------------------------------------------------------------------------------
        // 1. Cross-check the mod's live integrator against docs/tools/envelope_check.py.
        // ------------------------------------------------------------------------------------
        // The mod and the Python tool each implement E[composite(max(q1..qn))] independently --
        // the tool at 20000 nodes, the mod at Constants.BestOfNIntegrationNodes (1024), because
        // custom profiles need a live figure no precomputed table can cover. Until now the only
        // thing holding them together was a comment reading "if you change one, change both".
        //
        // That contract has already failed once: Task 4's first commit compared a Best-of-25
        // score against Faithful's N=1 baseline, putting every figure ~36pp too high and flipping
        // Desperate and Scavenger positive -- precisely inverting the fact the second anchor
        // exists to convey. It compiled, it looked plausible, and it shipped. This action is what
        // makes that class of divergence fail loudly instead of looking self-consistent.
        // PlayingOnMap ALONE, and deliberately not `Entry | ...`. Verified empirically in-game
        // 2026-08-06, because this is the opposite of what it looks like:
        //
        //   declared Entry|Playing     (3), current PlayingOnMap (6) -> HIDDEN
        //   declared Entry|PlayingOnMap(7), current PlayingOnMap (6) -> HIDDEN
        //   declared PlayingOnMap      (6), current PlayingOnMap (6) -> visible
        //
        // The gate is (current & declared) == declared: the declared set must be a SUBSET of the
        // current state. So ORing in another state makes an action LESS visible, not more, and
        // "visible at the main menu AND on a map" is not expressible in one attribute at all.
        //
        // This action was originally Entry|Playing and was therefore invisible in the debug menu
        // whenever a colony was loaded -- the exact situation HANDOVER tells you to run it in. It
        // was only ever reachable from the main menu. The map case is the one that matters, so it
        // wins; the bridge can still execute it from Entry, where visibility does not apply.
        [DebugAction(Category, "Verify Best-of-N against envelope_check.py",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void VerifyBestOfN()
        {
            // Two thresholds, because there are two different questions here and conflating them
            // is what let a real defect hide.
            //
            // The one that decides pass/fail is whether a DISPLAYED digit can move. The readout
            // never shows a raw score -- it shows deviation from Faithful AT THE SAME N, rendered
            // "F0" (FormatPowerPercent) -- so that is what gets the 0.5 percentage-POINT threshold,
            // below which no digit on screen can change.
            //
            // Raw scores are still compared, but at a deliberately wide 3% relative. Both
            // implementations share a first-order-accurate right-edge CDF -- envelope_check.py's
            // beta_grid does `run += v * dq` before appending, and CalculateBestOfNScoreCore does
            // the same -- and that scheme's error is proportional to dq. So the mod's 1024 nodes
            // and the tool's 20000 do NOT converge to the same raw number; they differ by up to
            // ~0.9% at N=50. That gap is real, but it CANCELS in the ratio to Faithful and moves no
            // digit on screen.
            //
            // Gating the raw score at 0.5% (as this did originally, while its comment claimed to be
            // measuring percentage points) failed 16 times: 15 on that invisible shared bias, and
            // one on the genuine n == 1 shortcut defect in CalculateBestOfNScoreCore -- which was
            // indistinguishable from the noise precisely because the noise was so loud.
            const float DisplayTolerancePp = 0.5f;
            const float RawToleranceRelPct = 3.0f;

            var sb = new StringBuilder();
            sb.AppendLine($"[PawnVarianceMod] Best-of-N cross-check vs {EnvelopeFigures.Tool}");
            sb.AppendLine($"  reference {EnvelopeFigures.ReferenceNodes} nodes, "
                + $"live {Constants.BestOfNIntegrationNodes} nodes; "
                + $"readout tolerance {DisplayTolerancePp:F2}pp, raw {RawToleranceRelPct:F2}%");

            int failures = 0;

            // Stale-table check first. If a scoring constant moved without the tool being re-run,
            // every figure below is measuring against the wrong reference, and a table that is
            // merely SELF-consistent would otherwise pass while being wrong -- which is exactly
            // the failure mode a golden file is supposed to prevent.
            failures += CheckConstant(sb, "CompositeSkillWeight",
                Constants.CompositeSkillWeight, EnvelopeFigures.GenCompositeSkillWeight);
            failures += CheckConstant(sb, "CompositePassionWeight",
                Constants.CompositePassionWeight, EnvelopeFigures.GenCompositePassionWeight);
            failures += CheckConstant(sb, "MaxPassionPips",
                Constants.MaxPassionPips, EnvelopeFigures.GenMaxPassionPips);
            failures += CheckConstant(sb, "AssumedVanillaSkillBaseline",
                Constants.AssumedVanillaSkillBaseline, EnvelopeFigures.GenAssumedVanillaSkillBaseline);
            failures += CheckConstant(sb, "AssumedMaxSkillLevel",
                Constants.AssumedMaxSkillLevel, EnvelopeFigures.GenAssumedMaxSkillLevel);
            failures += CheckConstant(sb, "BetaConcentrationK",
                Constants.BetaConcentrationK, EnvelopeFigures.GenBetaConcentrationK);

            if (failures > 0)
            {
                sb.AppendLine("  ^^ Constants.cs has moved since the reference was generated.");
                sb.AppendLine("     Re-run `python docs/tools/envelope_check.py` and commit "
                    + "Source/EnvelopeFigures.g.cs before trusting anything below.");
            }

            // Every displayed figure is measured against Faithful at the same N, so without it
            // there is nothing to compare and the run is meaningless rather than merely failing.
            int faithfulIdx = Array.IndexOf(EnvelopeFigures.Profiles, "Faithful");
            VarianceProfile faithfulPreset = VarianceProfiles.Presets
                .FirstOrDefault(x => x.label == "Faithful");
            if (faithfulIdx < 0 || faithfulPreset == null)
            {
                sb.AppendLine("  ABORT: Faithful is missing from the reference table or from "
                    + "VarianceProfiles.Presets. Every readout is relative to it.");
                Log.Error(sb.ToString().TrimEnd());
                Messages.Message("Varied Pawns: Best-of-N cross-check could not run — see log.",
                    MessageTypeDefOf.NegativeEvent, historical: false);
                return;
            }

            // Precomputed rather than fetched inside the loop: CalculateBestOfNScore has a
            // single-entry cache, so alternating between Faithful and the profile under test would
            // evict on every call and never hit.
            VarianceProfileValues faithfulValues = faithfulPreset.MakeValues();
            var liveBaseline = new float[EnvelopeFigures.Batches.Length];
            for (int b = 0; b < EnvelopeFigures.Batches.Length; b++)
                liveBaseline[b] = PawnVarianceSettings.CalculateBestOfNScore(
                    faithfulValues, EnvelopeFigures.Batches[b]);

            sb.AppendLine($"  {"profile",-12}{"N",4}{"reference",12}{"live",12}"
                + $"{"raw",9}{"ref%",9}{"live%",9}{"shown",9}");

            for (int p = 0; p < EnvelopeFigures.Profiles.Length; p++)
            {
                string label = EnvelopeFigures.Profiles[p];
                VarianceProfile preset = VarianceProfiles.Presets
                    .FirstOrDefault(x => x.label == label);

                // A preset present in the reference but absent from the code means one was
                // renamed or removed without regenerating -- report it rather than skipping,
                // since a silent skip is how a preset drops out of coverage unnoticed.
                if (preset == null)
                {
                    sb.AppendLine($"  {label,-12}  MISSING from VarianceProfiles.Presets");
                    failures++;
                    continue;
                }

                VarianceProfileValues v = preset.MakeValues();
                for (int b = 0; b < EnvelopeFigures.Batches.Length; b++)
                {
                    int n = EnvelopeFigures.Batches[b];
                    float expected = EnvelopeFigures.Scores[p][b];
                    float actual = PawnVarianceSettings.CalculateBestOfNScore(v, n);

                    float rawRelPct = Mathf.Abs(actual - expected) / expected * 100f;

                    // The quantity the player actually reads, computed the same way on both sides.
                    float refDev = (expected / EnvelopeFigures.Scores[faithfulIdx][b] - 1f) * 100f;
                    float liveDev = (actual / liveBaseline[b] - 1f) * 100f;
                    float shownPp = Mathf.Abs(liveDev - refDev);

                    bool badShown = shownPp > DisplayTolerancePp;
                    bool badRaw = rawRelPct > RawToleranceRelPct;
                    if (badShown || badRaw) failures++;

                    string flag = badShown ? "  *** READOUT MISMATCH ***"
                        : badRaw ? "  *** RAW MISMATCH ***"
                        : string.Empty;

                    sb.AppendLine($"  {label,-12}{n,4}{expected,12:F6}{actual,12:F6}"
                        + $"{rawRelPct,8:F2}%{refDev,8:F2}%{liveDev,8:F2}%{shownPp,7:F2}pp{flag}");
                }
            }

            if (failures == 0)
            {
                sb.AppendLine("  PASS: the live integrator agrees with the reference everywhere.");
                Log.Message(sb.ToString().TrimEnd());
                Messages.Message("Varied Pawns: Best-of-N cross-check PASSED (see log).",
                    MessageTypeDefOf.PositiveEvent, historical: false);
            }
            else
            {
                sb.AppendLine($"  FAIL: {failures} mismatch(es). The UI readout and HANDOVER's "
                    + "table now disagree.");
                Log.Error(sb.ToString().TrimEnd());
                Messages.Message($"Varied Pawns: Best-of-N cross-check FAILED ({failures}) — see log.",
                    MessageTypeDefOf.NegativeEvent, historical: false);
            }
        }

        private static int CheckConstant(StringBuilder sb, string name, float live, float generated)
        {
            if (Mathf.Approximately(live, generated)) return 0;
            sb.AppendLine($"  STALE: Constants.{name} is {live:G} but the reference was "
                + $"generated at {generated:G}");
            return 1;
        }

        // ------------------------------------------------------------------------------------
        // 2. Roll a batch of pawns and dump the distribution.
        // ------------------------------------------------------------------------------------
        // Answers the questions the composite score cannot: what the population actually looks
        // like once skillNoise, the passion spend loop, trait protection and the age cap have all
        // had their say. Every one of those is invisible to the +-35% envelope, which is a
        // MEAN-POWER model -- it sees averageQuality, the skill-shift band and the passion budget,
        // and nothing else. This is the only place dispersion can be observed rather than derived.
        //
        // Goes through PawnGenerator.GeneratePawn deliberately, so the real Harmony postfix,
        // ValuesFor override resolution and all three appliers run exactly as they do in play.
        [DebugAction(Category, "Roll pawns and dump distribution",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RollPawnDistribution()
        {
            var options = new List<DebugMenuOption>();
            foreach (int count in new[] { 50, 200, 1000 })
            {
                int n = count;
                options.Add(new DebugMenuOption($"{n} pawns", DebugMenuOptionMode.Action,
                    () => DumpDistribution(n)));
            }
            Find.WindowStack.Add(new Dialog_DebugOptionListLister(options));
        }

        private static void DumpDistribution(int count)
        {
            Faction faction = Faction.OfPlayerSilentFail;
            if (faction == null)
            {
                Log.Error("[PawnVarianceMod] No player faction — load a game first.");
                return;
            }

            var settings = PawnVarianceMod.Settings;
            var levels = new List<int>();       // every skill of every pawn, flattened
            var perPawnMeans = new List<float>();
            var traitCounts = new List<int>();
            var passionPips = new List<float>();
            int majors = 0, minors = 0, nones = 0, passionless = 0;

            // Verbose logging would emit a full trace per pawn — hundreds of them here, which
            // would bury the summary this action exists to produce. Suppressed for the batch and
            // restored in the finally, so an exception mid-run cannot leave it flipped.
            bool verboseWas = settings.verboseLogging;
            settings.verboseLogging = false;

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Pawn pawn = null;
                    try
                    {
                        // canGeneratePawnRelations: false — relation generation spawns ADDITIONAL
                        // pawns (parents, siblings), which would both skew the sample with pawns
                        // nobody asked for and make the run drastically slower.
                        var request = new PawnGenerationRequest(
                            PawnKindDefOf.Colonist,
                            faction,
                            PawnGenerationContext.NonPlayer,
                            forceGenerateNewPawn: true,
                            canGeneratePawnRelations: false,
                            allowDowned: true,
                            mustBeCapableOfViolence: false);

                        pawn = PawnGenerator.GeneratePawn(request);
                        if (pawn?.skills == null) continue;

                        float sum = 0f;
                        foreach (SkillRecord r in pawn.skills.skills)
                        {
                            // Raw learned level, not Level: Level folds in the Biotech aptitude
                            // bonus, which is not something this mod set and would misattribute
                            // gene effects to the profile's tuning.
                            int lv = r.GetLevel(includeAptitudes: false);
                            levels.Add(lv);
                            sum += lv;

                            switch (r.passion)
                            {
                                case Passion.Major: majors++; break;
                                case Passion.Minor: minors++; break;
                                default: nones++; break;
                            }
                        }

                        perPawnMeans.Add(sum / pawn.skills.skills.Count);
                        traitCounts.Add(pawn.story?.traits?.allTraits?.Count ?? 0);

                        // Priced the same way PassionVarianceApplier's spend loop prices them:
                        // Major 1.5, Minor 1. Counting passions instead would understate any
                        // Major-biased profile by a third.
                        float pips = pawn.skills.skills.Sum(
                            r => r.passion == Passion.Major ? 1.5f
                               : r.passion == Passion.Minor ? 1f : 0f);
                        passionPips.Add(pips);
                        if (pips <= 0f) passionless++;
                    }
                    finally
                    {
                        // Unspawned throwaway pawns must be discarded explicitly or they leak into
                        // the world pawn pool and show up in later events.
                        pawn?.Discard(true);
                    }
                }
            }
            finally
            {
                settings.verboseLogging = verboseWas;
            }

            if (perPawnMeans.Count == 0)
            {
                Log.Error("[PawnVarianceMod] Generated no usable pawns.");
                return;
            }

            var sb = new StringBuilder();
            sb.AppendLine($"[PawnVarianceMod] Distribution over {perPawnMeans.Count} generated "
                + $"{PawnKindDefOf.Colonist.defName} pawns");
            sb.AppendLine($"  active profile: {settings.LabelFor(settings.activeProfileId)}"
                + $"   hostile profile: {settings.LabelFor(settings.hostileProfileId)}");
            sb.AppendLine("  (player-faction colonists, so this samples the ACTIVE profile; "
                + "overrides are not exercised here)");
            sb.AppendLine();
            sb.AppendLine(Describe("per-skill level", levels.Select(x => (float)x).ToList()));
            sb.AppendLine(Describe("per-pawn mean skill", perPawnMeans));
            sb.AppendLine(Describe("passion pips/pawn", passionPips));
            sb.AppendLine(Describe("traits/pawn", traitCounts.Select(x => (float)x).ToList()));
            sb.AppendLine($"  passions: {majors} Major, {minors} Minor, {nones} None"
                + $"   passionless pawns: {passionless} "
                + $"({100f * passionless / perPawnMeans.Count:F1}%)");
            sb.AppendLine();

            // The whole point of the run: an observed sd to hold the tool's DERIVED sd against.
            // envelope_check.py predicts per-skill sd = magnitude/sqrt(6) from skillNoise alone;
            // the observed figure also carries the quality-driven spread of the baseline, so it
            // should sit ABOVE that prediction. If it sits below, the noise term is not reaching
            // the pawns and something upstream is clamping it.
            sb.AppendLine("  Compare 'per-skill level' sd against the 'per-skill sd' column in");
            sb.AppendLine("  `python docs/tools/envelope_check.py`. Observed should exceed the");
            sb.AppendLine("  predicted figure — the tool models noise only, this also carries the");
            sb.AppendLine("  spread of the quality roll itself.");
            sb.AppendLine(Histogram("per-pawn mean skill", perPawnMeans, 12));

            Log.Message(sb.ToString().TrimEnd());
            Messages.Message($"Varied Pawns: rolled {perPawnMeans.Count} pawns — see log.",
                MessageTypeDefOf.TaskCompletion, historical: false);
        }

        private static string Describe(string label, List<float> xs)
        {
            if (xs.Count == 0) return $"  {label,-22} (no data)";

            var sorted = xs.OrderBy(x => x).ToList();
            float mean = xs.Average();
            // Population sd, matching the tool's closed-form figure (which is also a population
            // quantity, not a sample estimate) so the two are directly comparable.
            float sd = Mathf.Sqrt(xs.Sum(x => (x - mean) * (x - mean)) / xs.Count);

            return $"  {label,-22} mean {mean,6:F2}  sd {sd,5:F2}  min {sorted[0],5:F1}  "
                + $"p10 {Quantile(sorted, 0.10f),5:F1}  median {Quantile(sorted, 0.50f),5:F1}  "
                + $"p90 {Quantile(sorted, 0.90f),5:F1}  max {sorted[sorted.Count - 1],5:F1}";
        }

        private static float Quantile(List<float> sorted, float q)
        {
            if (sorted.Count == 1) return sorted[0];
            float pos = q * (sorted.Count - 1);
            int lo = Mathf.FloorToInt(pos);
            int hi = Mathf.Min(lo + 1, sorted.Count - 1);
            return Mathf.Lerp(sorted[lo], sorted[hi], pos - lo);
        }

        private static string Histogram(string label, List<float> xs, int bins)
        {
            float lo = xs.Min(), hi = xs.Max();
            if (hi - lo < 0.0001f) return $"  {label}: all values at {lo:F2}";

            var counts = new int[bins];
            foreach (float x in xs)
            {
                int b = Mathf.Clamp(Mathf.FloorToInt((x - lo) / (hi - lo) * bins), 0, bins - 1);
                counts[b]++;
            }

            int peak = counts.Max();
            var sb = new StringBuilder();
            sb.AppendLine($"  {label} histogram:");
            for (int b = 0; b < bins; b++)
            {
                float edge = lo + (hi - lo) * b / bins;
                int bar = peak == 0 ? 0 : Mathf.RoundToInt(40f * counts[b] / peak);
                sb.AppendLine($"    {edge,6:F2} |{new string('#', bar),-40} {counts[b]}");
            }
            return sb.ToString().TrimEnd();
        }
    }
}
