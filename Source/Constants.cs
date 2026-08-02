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
    }
}
