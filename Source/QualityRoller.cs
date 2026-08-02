using System;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public static class QualityRoller
    {
        // Takes the values explicitly rather than reading the active profile: hostile pawns roll
        // against their own profile's averageQuality, so there is no single "current" shape.
        public static float RollQuality(VarianceProfileValues values)
        {
            values.GetBetaAlphaBeta(out float alpha, out float beta);
            float x = SampleGamma(alpha);
            float y = SampleGamma(beta);

            // Guard against both underflowing to exactly 0 in the same draw (0/0 = NaN)
            while (x == 0f && y == 0f)
            {
                x = SampleGamma(alpha);
                y = SampleGamma(beta);
            }

            float quality = x / (x + y);
            return Mathf.Clamp01(quality);
        }

        // Marsaglia-Tsang for shape >= 1; Stuart's-theorem boost trick for shape < 1.
        private static float SampleGamma(float shape)
        {
            if (shape < 1f)
            {
                float u = (float)Rand.Value;
                // u^(1/shape) can legitimately underflow to 0.0 for small shape — that's
                // treated as a valid extreme draw, not an error (Quality roll numerical-floor note).
                float boost = Mathf.Pow(u, 1f / shape);
                return SampleGammaShapeAtLeastOne(shape + 1f) * boost;
            }
            return SampleGammaShapeAtLeastOne(shape);
        }

        private static float SampleGammaShapeAtLeastOne(float shape)
        {
            float d = shape - 1f / 3f;
            float c = 1f / Mathf.Sqrt(9f * d);

            while (true)
            {
                float x, v;
                do
                {
                    x = NextGaussian();
                    v = 1f + c * x;
                } while (v <= 0f);

                v = v * v * v;
                float u = (float)Rand.Value;

                if (u < 1f - 0.0331f * x * x * x * x)
                    return d * v;
                if (Mathf.Log(u) < 0.5f * x * x + d * (1f - v + Mathf.Log(v)))
                    return d * v;
            }
        }

        private static float NextGaussian()
        {
            // Box-Muller
            float u1 = 1f - (float)Rand.Value; // avoid log(0)
            float u2 = (float)Rand.Value;
            return Mathf.Sqrt(-2f * Mathf.Log(u1)) * Mathf.Sin(2f * Mathf.PI * u2);
        }
    }
}
