namespace PawnVarianceMod
{
    public static class Constants
    {
        // Quality roll (Core Algorithms > Quality roll)
        public const float QualityClampEpsilon = 1e-3f;
        public const float BetaConcentrationK = 8f; // fixed population-spread constant

        // Noise-floor / max constants (Settings Schema)
        public const float MinMagnitudeFloor = 0.5f;
        public const float MaxMagnitude = 6f;
        public const float SmallRandomJitter = 0.5f;

        // Passion budget spread (Core Algorithms > Passion variance): mirrors vanilla
        // PawnGenerator.GenerateSkills' own passion-budget roll — `5f + clamp(Rand.Gaussian(), -4f,
        // 4f)` — but with the Gaussian's width factor and clamp window driven by passionNoise instead
        // of vanilla's hardcoded 1 and 4, so the setting controls "how much the total passion budget
        public const float PassionBudgetSpreadMin = 0.25f;
        public const float PassionBudgetSpreadMax = 4f;
        public const float PassionBudgetClampFactor = 4f; // matches vanilla's own spread:clamp ratio (widthFactor 1 : clamp 4)

        // Quality distribution graph curve normalization
        public const float AssumedVanillaSkillBaseline = 5f;
        public const float AssumedMaxSkillLevel = 20f;

        // Composite-score passion normalizer. 18 = 12 skills x 1.5 pips (the cost of a Major in
        // PassionVarianceApplier's spend loop), i.e. the true saturation point: every skill Major.
        // It was 12, which is the SKILL COUNT, not the pip ceiling — an off-by-a-Major-cost error
        // that made passionNorm saturate a third early. (The preset that exposed this, Gifted,
        // was removed 2026-08-04: it sat at +152% vs Faithful and was unreachable by default.)
        // Happy consequence: at q=0.50 the Faithful baseline is now 5/20 = 4.5/18 = 0.25 on BOTH
        // axes, so the reference score is exactly 0.2500 and no longer moves when the weights below
        // are retuned. Do not "simplify" this back to 12.
        public const float MaxPassionPips = 18f;

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
