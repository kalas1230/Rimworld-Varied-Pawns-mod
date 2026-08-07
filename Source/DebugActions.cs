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
        // ------------------------------------------------------------------------------------
        // 0. Dump the Race Overrides Add-menu contents.
        // ------------------------------------------------------------------------------------
        // The acceptance check for the race-overrides feature is "the Add menu lists exactly the
        // installed humanlike races, with no mechanoid alien races in it". That cannot be checked
        // through UI automation: the menu is a FloatMenu opened from Listing_Standard.ButtonText,
        // and a synthetic click activates the button without the menu surviving to the next frame,
        // so the bridge can never read its rows. Verified 2026-08-06 against the Faction Add button
        // too, so it is a limit of the automation and not of this section.
        //
        // Calling PawnVarianceSettings.SelectableRaces() directly is the point: a reimplementation
        // of the filter here would pass while the menu regressed. It also prints the mechanoid
        // ThingDef_AlienRace count that the Humanlike filter is there to exclude, so a regression
        // to something like AllDefs.Where(d => d.race != null) shows up as that count leaking in.
        [DebugAction(Category, "Dump Add-menu race list",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpSelectableRaces()
        {
            var races = PawnVarianceSettings.SelectableRaces().ToList();

            int humanlikeRaceDefs = 0;
            int nonHumanlikeAlienRaces = 0;
            foreach (var d in DefDatabase<ThingDef>.AllDefs)
            {
                if (d.race == null) continue;
                if (d.race.Humanlike) humanlikeRaceDefs++;
                else if (d.GetType().Name == "ThingDef_AlienRace") nonHumanlikeAlienRaces++;
            }

            var sb = new StringBuilder();
            sb.AppendLine("[PawnVarianceMod] Race Overrides -- Add menu contents");
            sb.AppendLine($"  {races.Count} selectable race(s); the menu shows exactly these rows:");
            foreach (var d in races)
            {
                sb.AppendLine($"    {d.LabelCap,-24} {d.defName}");
            }
            sb.AppendLine($"  filtered out: {humanlikeRaceDefs - races.Count} humanlike race def(s) " +
                          "with no PawnKindDef referencing them, and " +
                          $"{nonHumanlikeAlienRaces} non-humanlike alien race def(s) (mechanoids, drones, float units).");

            // The excluded humanlike defs are printed because "which races are missing" is the
            // half of this check that a PASS on the mechanoid filter says nothing about.
            var excluded = DefDatabase<ThingDef>.AllDefs
                .Where(d => d.race != null && d.race.Humanlike && !races.Contains(d))
                .OrderBy(d => d.defName)
                .ToList();
            foreach (var d in excluded)
            {
                sb.AppendLine($"    excluded: {d.LabelCap,-24} {d.defName}");
            }

            var leaked = races.Where(d => d.defName.IndexOf("Mechanoid", StringComparison.OrdinalIgnoreCase) >= 0
                                       || d.defName.IndexOf("Drone", StringComparison.OrdinalIgnoreCase) >= 0
                                       || d.defName.IndexOf("FloatUnit", StringComparison.OrdinalIgnoreCase) >= 0)
                              .ToList();
            if (leaked.Count > 0)
            {
                sb.AppendLine($"  FAIL: {leaked.Count} mechanoid-looking def(s) reached the menu: " +
                              string.Join(", ", leaked.Select(d => d.defName)));
                Log.Warning(sb.ToString().TrimEnd());
                return;
            }

            sb.Append("  PASS: no mechanoid, drone or float-unit def reached the menu.");
            Log.Message(sb.ToString());
        }

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

            // Modded skills change what the passion axis MEANS, and nothing else can detect it.
            // The score's capacity term assumes MaxPassionPips / MajorPassionCost skills, because
            // CalculateCompositeScore describes a profile and has no pawn to ask -- while the
            // applier itself reads the live list and is already correct. Deliberately NOT a
            // failure: the figures are still internally consistent and the envelope tool has no
            // way to know a player's modlist. Reported so a modded install knows the readout is an
            // approximation, instead of the tool and the game disagreeing in silence.
            int liveSkills = DefDatabase<SkillDef>.AllDefsListForReading.Count;
            float assumedSkills = Constants.MaxPassionPips / Constants.MajorPassionCost;
            if (Mathf.Abs(liveSkills - assumedSkills) > 0.001f)
            {
                sb.AppendLine($"  NOTE  skill count: live {liveSkills}, score assumes {assumedSkills:F0} "
                    + $"(a mod adds skills). Real capacity is {liveSkills * Constants.MajorPassionCost:F1} pips, "
                    + $"not {Constants.MaxPassionPips:F1} -- the passion axis understates high budgets. "
                    + "Pawn generation is unaffected: PassionVarianceApplier reads the live skill list.");
            }
            else
            {
                sb.AppendLine($"  skill count {liveSkills} matches the score's assumption.");
            }

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
            // Not a weight, but it sets the DOMAIN both integrators run over: GetBetaAlphaBeta
            // clamps averageQuality into [eps, 1-eps] before deriving alpha/beta. The tool only
            // started mirroring that clamp on 2026-08-07; before then the two sides disagreed for
            // any preset outside the window, and this gate would have reported a failure with no
            // bug in either implementation.
            failures += CheckConstant(sb, "QualityClampEpsilon",
                Constants.QualityClampEpsilon, EnvelopeFigures.GenQualityClampEpsilon);
            // These two entered the composite on 2026-08-06, as the passion-capacity term that
            // replaced the stale `(1 + 0.25 * majorBias)` premium. They are scoring constants now,
            // so they are drift-checked like the rest -- not merely the price list. Together with
            // MaxPassionPips above they also pin the derived skill count, so it needs no check of
            // its own.
            failures += CheckConstant(sb, "MajorPassionCost",
                Constants.MajorPassionCost, EnvelopeFigures.GenMajorPassionCost);
            failures += CheckConstant(sb, "MinorPassionCost",
                Constants.MinorPassionCost, EnvelopeFigures.GenMinorPassionCost);
            // The XP rates entered the composite on 2026-08-06 via PassionPipEfficiency. They are
            // vanilla's numbers, so they only move if a RimWorld update moves them -- which is
            // precisely the drift this check exists to catch, since nothing else in this mod would
            // notice SkillRecord.LearnRateFactor changing under it.
            failures += CheckConstant(sb, "PassionLearnRateNone",
                Constants.PassionLearnRateNone, EnvelopeFigures.GenPassionLearnRateNone);
            failures += CheckConstant(sb, "PassionLearnRateMinor",
                Constants.PassionLearnRateMinor, EnvelopeFigures.GenPassionLearnRateMinor);
            failures += CheckConstant(sb, "PassionLearnRateMajor",
                Constants.PassionLearnRateMajor, EnvelopeFigures.GenPassionLearnRateMajor);

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
        // like once skillSpread, the passion spend loop, trait protection and the age cap have all
        // had their say. Every one of those is invisible to the +-35% envelope, which is a
        // MEAN-POWER model -- it sees averageQuality, the skill-shift band and the passion budget,
        // and nothing else. This is the only place dispersion can be observed rather than derived.
        //
        // Goes through PawnGenerator.GeneratePawn deliberately, so the real Harmony postfix,
        // ValuesFor override resolution and all three appliers run exactly as they do in play.
        // ------------------------------------------------------------------------------------
        // 3. Override resolution matrix — faction vs race vs xenotype, under both toggle states.
        // ------------------------------------------------------------------------------------
        // Nothing in this repo exercised override resolution at runtime. The Python mirror
        // (zzz-Do-Not-Commit/test_race_resolution.py, 19 cases) validates the RULE TABLE but not
        // the C# that implements it, and the UI path cannot be automated because adding an
        // override goes through a FloatMenu the bridge cannot read.
        //
        // So this generates a REAL pawn per case and calls the REAL PawnVarianceSettings.ValuesFor
        // with the same PawnGenerationRequest the Harmony postfix passes, then prints every
        // candidate source with its priority next to the winner. It deliberately reports rather
        // than asserts a recomputed expectation: re-deriving the expected winner here would be a
        // second copy of the rule, and a copy agreeing with itself proves nothing.
        //
        // factionOverridesTakePrecedence is flipped in memory and restored in a finally. It is
        // never persisted — no Mod.WriteSettings call happens on this path.
        [DebugAction(Category, "Dump override resolution matrix",
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpOverrideResolution()
        {
            var settings = PawnVarianceMod.Settings;
            var sb = new StringBuilder();
            sb.AppendLine("[PawnVarianceMod] Override resolution matrix");
            sb.AppendLine($"  enableOverrides={settings.enableOverrides}   " +
                          $"factionOverridesTakePrecedence={settings.factionOverridesTakePrecedence} (live value)");
            sb.AppendLine("  'winner' columns are what ValuesFor actually returned at each toggle state.");
            sb.AppendLine();

            Faction Fac(string defName)
            {
                FactionDef fd = DefDatabase<FactionDef>.GetNamedSilentFail(defName);
                return fd == null || Find.FactionManager == null
                    ? null
                    : Find.FactionManager.FirstFactionOfDef(fd);
            }

            PawnKindDef KindForRace(string raceDefName) =>
                DefDatabase<PawnKindDef>.AllDefs.FirstOrDefault(
                    k => k.race != null && k.race.defName == raceDefName && k.RaceProps?.Humanlike == true);

            XenotypeDef Xeno(string defName) =>
                ModsConfig.BiotechActive ? DefDatabase<XenotypeDef>.GetNamedSilentFail(defName) : null;

            // label, race def, faction def, forced xenotype
            var cases = new List<(string label, string race, string faction, string xeno)>
            {
                ("player colonist, no xeno",        "Human",        null,     null),
                ("faction Highest vs race Normal",  "Human",        "Empire", null),
                ("faction Normal vs race Normal",   "Human",        "Pirate", null),
                ("xeno Normal vs race Normal",      "Human",        null,     "Waster"),
                ("xeno High vs race Normal",        "Human",        null,     "Hussar"),
                ("all three, Highest faction",      "Human",        "Empire", "Waster"),
                ("Milira race",                     "Milira_Race",  null,     null),
                ("Wolfein race",                    "Wolfein_Race", null,     null),
            };

            bool toggleWas = settings.factionOverridesTakePrecedence;
            bool verboseWas = settings.verboseLogging;
            settings.verboseLogging = false;

            try
            {
                foreach (var c in cases)
                {
                    PawnKindDef kind = KindForRace(c.race);
                    if (kind == null)
                    {
                        sb.AppendLine($"  {c.label,-32} SKIPPED: no humanlike PawnKindDef for race {c.race}");
                        continue;
                    }

                    Faction faction = c.faction == null ? Faction.OfPlayerSilentFail : Fac(c.faction);
                    if (c.faction != null && faction == null)
                    {
                        sb.AppendLine($"  {c.label,-32} SKIPPED: faction {c.faction} not present in this world");
                        continue;
                    }

                    XenotypeDef xeno = c.xeno == null ? null : Xeno(c.xeno);
                    if (c.xeno != null && xeno == null)
                    {
                        sb.AppendLine($"  {c.label,-32} SKIPPED: xenotype {c.xeno} unavailable");
                        continue;
                    }

                    Pawn pawn = null;
                    try
                    {
                        var request = new PawnGenerationRequest(
                            kind,
                            faction,
                            PawnGenerationContext.NonPlayer,
                            forceGenerateNewPawn: true,
                            canGeneratePawnRelations: false,
                            allowDowned: true,
                            mustBeCapableOfViolence: false,
                            forcedXenotype: xeno);

                        pawn = PawnGenerator.GeneratePawn(request);
                        if (pawn == null)
                        {
                            sb.AppendLine($"  {c.label,-32} SKIPPED: pawn generation returned null");
                            continue;
                        }

                        string raceKey = pawn.def?.defName;
                        string facKey = (pawn.Faction ?? faction)?.def?.defName;
                        string xenoKey = xeno?.defName ?? pawn.genes?.Xenotype?.defName;

                        string Candidate(string kindLabel, string key,
                            Dictionary<string, string> map, Dictionary<string, OverridePriority> prios)
                        {
                            if (key == null || !map.TryGetValue(key, out string pid)) return $"{kindLabel}: —";
                            var prio = prios.TryGetValue(key, out var p) ? p : OverridePriority.Normal;
                            return $"{kindLabel}: {settings.LabelFor(pid)}@{prio}";
                        }

                        settings.factionOverridesTakePrecedence = false;
                        string winnerRaceFirst = settings.ValuesFor(pawn, request).profileLabel;
                        settings.factionOverridesTakePrecedence = true;
                        string winnerFactionFirst = settings.ValuesFor(pawn, request).profileLabel;

                        sb.AppendLine($"  {c.label}");
                        sb.AppendLine($"    keys      race={raceKey ?? "-"}  faction={facKey ?? "-"}  xenotype={xenoKey ?? "-"}");
                        sb.AppendLine("    candidates " +
                            Candidate("faction", facKey, settings.factionOverrides, settings.factionPriorities) + "   " +
                            Candidate("race", raceKey, settings.raceOverrides, settings.racePriorities) + "   " +
                            Candidate("xenotype", xenoKey, settings.xenotypeOverrides, settings.xenotypePriorities));
                        sb.AppendLine($"    winner    takePrecedence=false -> {winnerRaceFirst,-12} " +
                                      $"takePrecedence=true -> {winnerFactionFirst}");
                    }
                    finally
                    {
                        pawn?.Discard(true);
                    }
                }

                // ----------------------------------------------------------------------------
                // Priority sweep: race vs faction at Low / Normal / High against a fixed
                // Normal-priority faction. The equal-priority row is the only one where the
                // toggle can change the winner; the other two must be decided by priority alone
                // and must therefore read the same in both columns.
                //
                // The faction is whichever one with an override actually exists in this world --
                // the owner's Pirate rows do not exist in a quicktest map, and a SKIPPED row is
                // not a passing test. Priorities are moved in memory and restored below.
                // ----------------------------------------------------------------------------
                PawnKindDef humanKind = KindForRace("Human");
                string probeFaction = null;
                Faction probeFac = null;
                foreach (var key in settings.factionOverrides.Keys)
                {
                    Faction f = Fac(key);
                    if (f != null) { probeFaction = key; probeFac = f; break; }
                }

                sb.AppendLine();
                if (humanKind == null || probeFac == null)
                {
                    sb.AppendLine("  priority sweep SKIPPED: no overridden faction exists in this world");
                }
                else
                {
                    var facPrioWas = settings.factionPriorities.TryGetValue(probeFaction, out var fpw)
                        ? (OverridePriority?)fpw : null;
                    var racePrioWas = settings.racePriorities.TryGetValue("Human", out var rpw)
                        ? (OverridePriority?)rpw : null;

                    try
                    {
                        settings.factionPriorities[probeFaction] = OverridePriority.Normal;

                        string raceLabel = settings.LabelFor(settings.raceOverrides["Human"]);
                        string facLabel = settings.LabelFor(settings.factionOverrides[probeFaction]);
                        sb.AppendLine($"  priority sweep — race Human ({raceLabel}) " +
                                      $"vs faction {probeFaction} ({facLabel}) held at Normal");

                        foreach (var racePrio in new[]
                                 { OverridePriority.Low, OverridePriority.Normal, OverridePriority.High })
                        {
                            settings.racePriorities["Human"] = racePrio;

                            Pawn p = null;
                            try
                            {
                                // Baseliner is FORCED, and that is the whole point of this line.
                                // Without it the pawn rolls its pawnkind's xenotypeSet -- Empire
                                // kinds produce Genie and Hussar, both overridden at High -- and a
                                // third candidate outranks the two Normals being compared. The
                                // first run of this sweep returned Specialist for the tie row for
                                // exactly that reason. Baseliner has no override, so race vs
                                // faction is the only comparison left.
                                var req = new PawnGenerationRequest(
                                    humanKind, probeFac, PawnGenerationContext.NonPlayer,
                                    forceGenerateNewPawn: true, canGeneratePawnRelations: false,
                                    allowDowned: true, mustBeCapableOfViolence: false,
                                    forcedXenotype: Xeno("Baseliner"));
                                p = PawnGenerator.GeneratePawn(req);
                                if (p == null) continue;

                                settings.factionOverridesTakePrecedence = false;
                                string wRace = settings.ValuesFor(p, req).profileLabel;
                                settings.factionOverridesTakePrecedence = true;
                                string wFac = settings.ValuesFor(p, req).profileLabel;

                                sb.AppendLine($"    race@{racePrio,-6} vs faction@Normal   " +
                                              $"takePrecedence=false -> {wRace,-12} takePrecedence=true -> {wFac}");
                            }
                            finally
                            {
                                p?.Discard(true);
                            }
                        }
                    }
                    finally
                    {
                        if (facPrioWas.HasValue) settings.factionPriorities[probeFaction] = facPrioWas.Value;
                        else settings.factionPriorities.Remove(probeFaction);
                        if (racePrioWas.HasValue) settings.racePriorities["Human"] = racePrioWas.Value;
                        else settings.racePriorities.Remove("Human");
                    }
                }
            }
            finally
            {
                settings.factionOverridesTakePrecedence = toggleWas;
                settings.verboseLogging = verboseWas;
            }

            sb.Append($"  toggle restored to {settings.factionOverridesTakePrecedence}; nothing was written to disk.");
            Log.Message(sb.ToString());
        }

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
            // What each pawn ACTUALLY resolved to, tallied per label. This action used to print
            // settings.activeProfileId and assert "overrides are not exercised here" -- which is
            // false the moment any override matches a player-faction colonist. On 2026-08-07 a
            // Human race override at Normal priority silently outranked the Active Colony Profile
            // and this action reported "Wildcard" across two consecutive 1000-pawn runs that had
            // in fact generated Faithful pawns. Nothing flagged it; the numbers were simply the
            // wrong preset's. Ask the resolver instead of trusting the configured id.
            var resolved = new Dictionary<string, int>();

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

                        // The same call the generation postfix makes, so this reports the profile
                        // that actually produced the pawn rather than the one the settings name.
                        string label = settings.ValuesFor(pawn, request)?.profileLabel ?? "(null)";
                        resolved.TryGetValue(label, out int seen);
                        resolved[label] = seen + 1;

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
                            r => r.passion == Passion.Major ? Constants.MajorPassionCost
                               : r.passion == Passion.Minor ? Constants.MinorPassionCost : 0f);
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
            sb.AppendLine($"  configured active profile: {settings.LabelFor(settings.activeProfileId)}"
                + $"   hostile profile: {settings.LabelFor(settings.hostileProfileId)}");

            // RESOLVED, not configured. Read this line, not the one above it: an override on the
            // pawn's faction, race or xenotype beats the Active Colony Profile, so these can differ
            // and the numbers below belong to whatever is named here.
            var byCount = resolved.OrderByDescending(kv => kv.Value).ToList();
            sb.AppendLine("  ACTUALLY RESOLVED TO: " + string.Join(", ",
                byCount.Select(kv => $"{kv.Key} x{kv.Value} "
                    + $"({100f * kv.Value / perPawnMeans.Count:F1}%)")));
            if (byCount.Count > 1)
            {
                sb.AppendLine("  ^^ MIXED SAMPLE -- the figures below average across DIFFERENT "
                    + "profiles and are not a valid reading of any one of them.");
            }
            else if (byCount.Count == 1 && byCount[0].Key != settings.LabelFor(settings.activeProfileId))
            {
                sb.AppendLine($"  ^^ NOT the configured active profile. An override on faction, race "
                    + $"or xenotype outranked it, so these figures are {byCount[0].Key}'s.");
            }
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
            // envelope_check.py predicts per-skill sd = skillSpread directly (the field now STORES
            // that sd rather than deriving it from a 0-1 scalar); the observed figure also carries
            // the quality-driven spread of the baseline, so it
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

        [DebugAction(Category, "Dump dispersion-aware Best-of-N",
                     allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void DumpDispersionBestOfN()
        {
            var sb = new StringBuilder();
            sb.AppendLine("[PawnVarianceMod] Dispersion-aware Best-of-N (C# side)");
            sb.AppendLine($"  grid q={DispersionModel.QNodes} x={DispersionModel.XNodes} "
                          + $"tri={DispersionModel.TriNodes} gauss={DispersionModel.GaussNodes}");
            sb.AppendLine("  profile           N=1      N=5     N=25     N=50");
            foreach (var preset in VarianceProfiles.Presets)
            {
                var v = preset.MakeValues();
                sb.AppendLine($"  {preset.label,-12}"
                    + $"{DispersionModel.BestOfN(v, 1),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 5),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 25),9:F4}"
                    + $"{DispersionModel.BestOfN(v, 50),9:F4}");
            }
            Log.Message(sb.ToString());
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
