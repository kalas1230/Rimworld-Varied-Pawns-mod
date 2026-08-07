using UnityEngine;

namespace PawnVarianceMod
{
    // .NET Framework 4.7.2 (this project's TargetFramework) has no Math.Erf -- it arrived in
    // .NET Core. The dispersion model needs a normal CDF, so we carry our own.
    //
    // envelope_check.py uses Python's math.erf, which is near machine precision, so THIS
    // approximation sets the accuracy floor for the whole two-implementation contract. A&S 7.1.26
    // is good to 1.5e-7 absolute -- about 2600x tighter than the gate's 0.5pp tolerance -- and the
    // error averages rather than accumulating across the grid, because every use is inside a
    // normalised weighted sum.
    public static class MathUtil
    {
        public static float Erf(float x)
        {
            // Abramowitz & Stegun 7.1.26.
            float sign = x < 0f ? -1f : 1f;
            x = Mathf.Abs(x);

            const float a1 = 0.254829592f;
            const float a2 = -0.284496736f;
            const float a3 = 1.421413741f;
            const float a4 = -1.453152027f;
            const float a5 = 1.061405429f;
            const float p = 0.3275911f;

            float t = 1f / (1f + p * x);
            float y = 1f - ((((a5 * t + a4) * t + a3) * t + a2) * t + a1) * t * Mathf.Exp(-x * x);
            return sign * y;
        }

        public static float NormalCdf(float z)
        {
            return 0.5f * (1f + Erf(z / Mathf.Sqrt(2f)));
        }
    }
}
