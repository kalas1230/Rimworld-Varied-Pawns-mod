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
        // varies" around the quality-derived mean rather than being fixed like vanilla's flat roll.
        public const float PassionBudgetSpreadMin = 0.25f;
        public const float PassionBudgetSpreadMax = 4f;
        public const float PassionBudgetClampFactor = 4f; // matches vanilla's own spread:clamp ratio (widthFactor 1 : clamp 4)

        // Trait desirability scoring (Core Algorithms > Trait desirability scoring)
        public const float SkillOffsetReferenceMagnitude = 6f;      // category 1
        public const float StatReferenceMagnitude = 1.0f;           // category 2
        public const float WorkTagDisablePenalty = 0.15f;           // category 3, per disabled tag
        public const float SocialReferenceMagnitude = 20f;          // category 4
        public const float ZMultiplier = 2f;                        // observedMinScore/MaxScore bound width

        // Tier bio label (Core Algorithms > Tier bio label)
        public const float AssumedVanillaSkillBaseline = 5f;
    }
}
