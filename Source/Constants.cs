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
        public const float MinSpreadFloor = 0.05f;
        public const float MaxSpread = 2f;
        public const float MinTemperatureFloor = 0.5f;
        public const float MaxTemperature = 8f;
        public const float SmallRandomJitter = 0.5f;

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
