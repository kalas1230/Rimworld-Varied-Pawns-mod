namespace PawnVarianceMod
{
    public static class Constants
    {
        // Quality roll (Core Algorithms > Quality roll)
        public const float QualityClampEpsilon = 1e-3f;
        public const float BetaConcentrationK = 8f; // fixed population-spread constant

        // Noise-floor / max constants (Settings Schema)
        // 0f, not 0.5f, as of 2026-08-06: the noise sliders now mean literally what they say, and
        // a slider at 0 produces genuinely zero spread. NOTE this is a Lerp LOW ENDPOINT, not a
        // floor applied after the fact — dropping it rescales magnitude at EVERY noise setting,
        // not only at 0. Lerp(0.5, 6, t) vs Lerp(0, 6, t) diverge most at low t: skillNoise 0.2
        // went 1.60 -> 1.20 (-25%), 0.85 went 5.18 -> 5.10 (-1.4%). Every preset except Wildcard
        // sits in the 0.20-0.35 band, so this narrowed per-skill dispersion across the board.
        public const float MinMagnitudeFloor = 0f;
        public const float MaxMagnitude = 6f;
        public const float SmallRandomJitter = 0.5f;

        // Passion budget spread (Core Algorithms > Passion variance): mirrors vanilla
        // PawnGenerator.GenerateSkills' own passion-budget roll — `5f + clamp(Rand.Gaussian(), -4f,
        // 4f)` — but with the Gaussian's width factor and clamp window driven by passionNoise instead
        // of vanilla's hardcoded 1 and 4, so the setting controls "how much the total passion budget
        // 0f as of 2026-08-06, same reasoning as MinMagnitudeFloor above: a Lerp low endpoint, so
        // passionNoise 0.25 went sigma 1.19 -> 1.00 (-16%), not just the zero case. At
        // passionNoise = 0 the budget is now exactly its quality-lerped mean, with no roll at all.
        public const float PassionBudgetSpreadMin = 0f;
        public const float PassionBudgetSpreadMax = 4f;
        public const float PassionBudgetClampFactor = 4f; // matches vanilla's own spread:clamp ratio (widthFactor 1 : clamp 4)

        // Quality distribution graph curve normalization
        public const float AssumedVanillaSkillBaseline = 5f;
        public const float AssumedMaxSkillLevel = 20f;

        // SkillRecord.LearnRateFactor, verbatim from the decompiled assembly 2026-08-06. These are
        // XP-RATE MULTIPLIERS, not pip prices -- the two are different currencies and the whole
        // "Major premium" below exists because they disagree. PassionLearnRateMinor and
        // MinorPassionCost are both 1f by coincidence; they are not the same quantity.
        public const float PassionLearnRateNone = 0.35f;
        public const float PassionLearnRateMinor = 1f;
        public const float PassionLearnRateMajor = 1.5f;

        // Vanilla's Major/Minor coin flip is `Rand.Bool` -- exactly 50/50, no weighting. This mod
        // makes it settable as passionMajorBias; this constant is what "vanilla's own bias" means
        // when the composite has to score a profile that has passion variance switched off.
        public const float VanillaMajorBias = 0.5f;

        // Vanilla's own passion budget, read off the decompiled assembly 2026-08-06:
        // PawnGenerator.GenerateSkills does `5f + Mathf.Clamp(Rand.Gaussian(), -4f, 4f)`, so the
        // mean is 5 pips and the range is [1, 9]. Rand.Gaussian(centerX, widthFactor) multiplies a
        // standard normal, i.e. widthFactor IS the sd -- so vanilla's clamp is exactly 4 sigma,
        // which is what PassionBudgetClampFactor mirrors.
        //
        // Used as the composite's passion axis when a profile has passion variance switched OFF:
        // that pawn gets vanilla's assignment, so vanilla's budget is what the axis should report.
        // It read a flat 0.25 until 2026-08-06, which was the SKILL axis's baseline (5/20) copied
        // across -- correct there, coincidental here. Scored like any other profile:
        // 5 pips x PassionPipEfficiency(VanillaMajorBias = 0.5) / 18 = 5 x 0.9391 / 18 = 0.2609.
        public const float VanillaPassionBudget = 5f;

        // Vanilla skips the passion budget entirely below this age: GenerateSkills returns early
        // at `pawn.ageTracker.AgeBiologicalYears < 13`, so a child's passions come only from
        // forced traits and growth birthdays (GrowthUtility.GrowthMomentAges = 7, 10, 13).
        // An AGE, deliberately, not DevelopmentalStage: that is the rule vanilla itself applied to
        // this pawn moments earlier, and the two disagree for any race declaring a Child life
        // stage past 13 -- exactly the HAR races this mod supports.
        public const int VanillaAdultPassionAge = 13;

        // The passion price list, verbatim from PawnGenerator.GenerateSkills' spend loop
        // (decompiled 2026-08-06): `if (num >= 1.5f && Rand.Bool) { major++; num -= 1.5f; }
        // else { minor++; num -= 1f; }`. PassionVarianceApplier's spend loop, GrowUpVariance's
        // "pips already committed" re-derivation and DebugActions' sampler all price passions, and
        // until 2026-08-06 all three carried their own inline 1.5f/1f. Price passions through
        // these two constants and nowhere else.
        //
        // ⚠️ WHERE THE 24 KEEPS COMING FROM — read before "correcting" a Major to cost 2.
        // Vanilla contains TWO passion scales, and only one of them is the spend cost:
        //   * GenerateSkills' budget      -- Major 1.5, Minor 1.   THIS ONE. It runs.
        //   * Pawn_SkillTracker.PassionCount -- Major 2, Minor 1, with `MinorPassionWeight = 1`
        //     and `MajorPassionWeight = 2` beside it. It is public API with ZERO consumers
        //     anywhere in Assembly-CSharp -- a valuation vanilla declares and never uses.
        // 12 x 2 = 24 is a correct reading of the SECOND scale. That is almost certainly where
        // this project's "24-pip era" came from, and why it recurred twice: it was never an
        // invented number, it was the right arithmetic on the wrong scale. If a future reader
        // finds Major = 2 in vanilla and concludes this file is wrong, they have found
        // PassionCount. The budget is what generation spends. Do not switch.
        public const float MajorPassionCost = 1.5f;
        public const float MinorPassionCost = 1f;

        // Composite-score passion normalizer. 18 = 12 skills x MajorPassionCost, i.e. the true
        // saturation point: every skill Major.
        // MUST STAY A NUMERIC LITERAL, not `12f * MajorPassionCost`: docs/tools/envelope_check.py
        // parses this file with a regex that only accepts `public const float X = <number>f;`, and
        // an expression here makes the tool exit with "Constants.cs is missing: MaxPassionPips".
        // It was 12, which is the SKILL COUNT, not the pip ceiling — an off-by-a-Major-cost error
        // that made passionNorm saturate a third early. (The preset that exposed this, Gifted,
        // was removed 2026-08-04: it sat at +152% vs Faithful and was unreachable by default.)
        // Do not "simplify" this back to 12.
        //
        // It used to be claimed here that at q=0.50 Faithful landed 5/20 = 4.5/18 = 0.25 on BOTH
        // axes, making the reference score exactly 0.2500. That 4.5 was not Faithful's budget —
        // its budget at q=0.50 is 4.0. The extra 0.5 came entirely from the `(1 + 0.25 * majorBias)`
        // factor CalculateCompositeScore applied to the budget, which was a 24-pip-era unit error
        // (see the note on that line) and was removed 2026-08-06. The baseline is now 0.2237 and
        // the two axes no longer coincide. Restoring the coincidence is a PRESET question, not a
        // constants one: with the pip-efficiency term in place it needs a Faithful budget band
        // whose q=0.50 midpoint is 4.79 pips (0.25 x 18 / 0.9391), against 4.0 today. Vanilla's
        // own flat 5 pips would land the axis at 0.2609. Left for the retune -- and note the
        // round 0.2500 is cosmetic; nothing depends on it, which is exactly why the old comment
        // claiming it could go unchallenged for so long.
        public const float MaxPassionPips = 18f;

        // NOTE: the assumed skill count (12) is NOT a constant of its own. It is already inside
        // MaxPassionPips — 18 / MajorPassionCost = 12, exactly — and the composite's capacity term
        // derives it there rather than holding a second copy. A separate `AssumedSkillCount = 12f`
        // was written on 2026-08-06 and removed the same day: two constants encoding one number,
        // with an unenforced invariant between them (capacity at bias 1.0 must equal
        // MaxPassionPips), is the precise shape of the 24-pip-era bugs this file now warns about
        // three times over. Do not reintroduce it.

        // Composite-score axis weights. The exchange rate they encode is
        //     R = (AssumedMaxSkillLevel / MaxPassionPips) * (CompositePassionWeight / CompositeSkillWeight)
        //       = (20/18) * (1.4/0.8) = 1.94 skill levels per passion pip.
        // Set 2026-08-03 after a four-agent review (2 Claude, 2 Gemini). Passion is an XP-RATE
        // multiplier (None 0.35x / Minor 1.0x / Major 1.5x), not an additive gift, so its value in
        // skill-levels is time-dependent: ~0 on day 1, peaking near 4.8 around day 30, saturating
        // near 3.2 once skill decay reaches equilibrium. A generation-time score has no time axis
        // and can only carry a colony-lifetime average; ~2.0 is that average after discounting for
        // the ~40-60% chance a passion lands on a skill the colony never assigns.
        // NOTE: R depends on MaxPassionPips as much as on these weights. Changing either without
        // the other silently moves the exchange rate — recompute R before touching them.
        public const float CompositeSkillWeight = 0.8f;
        public const float CompositePassionWeight = 1.4f;

        // The Profile Editor shows power at two anchors: the typical pawn (N=1) and the best of
        // N rerolls. 25 rather than 50: at 50 Wildcard would display +21.5%, and a UI that
        // advertises how close a preset sits to the +-35% envelope invites players to treat the
        // limit as a target.
        public const int BestOfNSampleCount = 25;

        // Midpoint-rule nodes for the Best-of-N integral. Measured against the 20000-node
        // reference in docs/tools/envelope_check.py across all seven presets: 512 nodes lands
        // 0.35pp off, which can flip a whole-percent readout; 1024 lands 0.17pp. Do not lower it.
        public const int BestOfNIntegrationNodes = 1024;
    }
}
