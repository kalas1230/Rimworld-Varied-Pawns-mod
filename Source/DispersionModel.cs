using UnityEngine;

namespace PawnVarianceMod
{
    // Deterministic, dispersion-aware Best-of-N. Mirrors make_grid_score / grid_moments in
    // docs/tools/envelope_check.py -- IF YOU CHANGE ONE, CHANGE BOTH. The in-game
    // "Verify Best-of-N" action enforces that mechanically.
    //
    // Monte Carlo deliberately does NOT live here: the gate cross-checks this against the Python
    // at 0.5pp, which only works while both sides are reproducible.
    public static class DispersionModel
    {
        public const int QNodes = 256;
        public const int XNodes = 512;
        public const int TriNodes = 65;
        public const int GaussNodes = 65;

        // Drag-time resolution. Measured drift vs the full grid is 0.001pp, i.e. free, so this is
        // taken unconditionally rather than gated on a profiling result.
        public const int QNodesDrag = 64;
        public const int XNodesDrag = 128;

        private static float[] triT, triW, gaussZ, gaussW;
        private static float[] densityWq, densityMus, densitySds;

        private static void EnsureNodes()
        {
            if (triT != null) return;

            triT = new float[TriNodes];
            triW = new float[TriNodes];
            float dt = 2f / TriNodes, ttot = 0f;
            for (int i = 0; i < TriNodes; i++)
            {
                float t = -1f + (i + 0.5f) * dt;
                triT[i] = t;
                triW[i] = (1f - Mathf.Abs(t)) * dt;   // triangular density 1-|t|
                ttot += triW[i];
            }
            for (int i = 0; i < TriNodes; i++) triW[i] /= ttot;

            gaussZ = new float[GaussNodes];
            gaussW = new float[GaussNodes];
            // The truncation window is DERIVED from PassionBudgetClampFactor, not a literal that
            // happens to equal it. PassionVarianceApplier clamps its Gaussian draw to
            // +-(spread * PassionBudgetClampFactor), i.e. +-factor sigma, so this quadrature has to
            // cover exactly that support. Until 2026-08-09 this read `8f / GaussNodes` and `-4f`
            // with a comment naming the coupling, and envelope_check.py did the same, while
            // dispersion_mc.py derived its window from the constant -- so retuning the constant
            // would have moved the independent Monte Carlo and left both quadratures behind, and
            // the resulting disagreement would have read as "the Monte Carlo says the quadrature
            // is wrong" when the truth was the reverse. Audit finding Q-06.
            float zmax = Constants.PassionBudgetClampFactor;
            float dz = 2f * zmax / GaussNodes, gtot = 0f;
            for (int i = 0; i < GaussNodes; i++)
            {
                float z = -zmax + (i + 0.5f) * dz;
                gaussZ[i] = z;
                gaussW[i] = Mathf.Exp(-0.5f * z * z) * dz;
                gtot += gaussW[i];
            }
            for (int i = 0; i < GaussNodes; i++) gaussW[i] /= gtot;
        }

        // Mean and sd of the composite CONDITIONAL on q. The two axes are independent given q, so
        // their means and variances combine under the composite weights.
        public static void Moments(VarianceProfileValues v, float q, out float mu, out float sd)
        {
            EnsureNodes();

            float wS = Constants.CompositeSkillWeight;
            float wP = Constants.CompositePassionWeight;
            float wsum = wS + wP;
            float top = Constants.AssumedMaxSkillLevel;
            float pdiv = Constants.MaxPassionPips;
            int nSkills = Mathf.RoundToInt(pdiv / Constants.MajorPassionCost);

            // Both axes honour their enable flag, exactly as CalculateCompositeScore does -- a
            // disabled axis is the pawn keeping VANILLA's skills/passions, so it contributes its
            // vanilla fallback as a CONSTANT (zero variance), at full weight. It is not dropped
            // from the average: see the long note on the weights in CalculateCompositeScore for
            // why zeroing the weight is wrong, and keep the two functions in step.
            //
            // Until 2026-08-09 neither flag was read here at all -- a grep for either identifier in
            // this file returned nothing -- so Typical, Best of N and the distribution curve all
            // scored an axis the generator does not roll. Worth up to 18.3pp of the displayed
            // "vs Faithful" figure against a gate whose tolerance is 0.5pp, and invisible to that
            // gate because envelope_check.py shared the omission. Presets all enable both axes,
            // which is why 32/32 never caught it.
            float s1 = 0f, s2 = 0f;
            float sVar;
            if (v.enableSkillVariance)
            {
                float mag = Mathf.Lerp(Constants.MagnitudeLerpLow, Constants.MaxMagnitude,
                                       v.SkillNoiseScalar);
                float baseline = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, q);

                for (int i = 0; i < TriNodes; i++)
                {
                    float lvl = Mathf.Clamp(Constants.AssumedVanillaSkillBaseline
                                            + baseline + triT[i] * mag, 0f, top);
                    float u = lvl / top;
                    s1 += triW[i] * u;
                    s2 += triW[i] * u * u;
                }
                // Pawn's AVERAGE over nSkills iid draws -> variance divides by nSkills.
                sVar = Mathf.Max(0f, s2 - s1 * s1) / nSkills;
            }
            else
            {
                // Mirrors CalculateCompositeScore's `float skillNorm = 0.25f` fallback: vanilla's
                // AssumedVanillaSkillBaseline / AssumedMaxSkillLevel = 5/20.
                s1 = Constants.AssumedVanillaSkillBaseline / Constants.AssumedMaxSkillLevel;
                sVar = 0f;
            }

            float p1 = 0f, p2 = 0f;
            float pVar;
            if (v.enablePassionVariance)
            {
                float sig = Mathf.Lerp(Constants.PassionBudgetSpreadMin,
                                       Constants.PassionBudgetSpreadMax, v.PassionNoiseScalar);
                float bmean = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
                float capacity = nSkills * (Constants.MinorPassionCost
                    + (Constants.MajorPassionCost - Constants.MinorPassionCost) * v.passionMajorBias);
                float eff = PawnVarianceSettings.PassionPipEfficiency(v.passionMajorBias);

                for (int i = 0; i < GaussNodes; i++)
                {
                    float b = bmean + gaussZ[i] * sig;
                    // Vanilla's floor. NOT gated on sig: PassionVarianceApplier applies it whenever
                    // the budget lands under 1 and passionCountMin > 0, spread or no spread. Gating it
                    // here diverged from the generator on any profile with passionCountMin > 0 and a
                    // mean budget under 1 pip at zero spread -- reachable in two slider moves and worth
                    // ~6.6pp on the readout, which the in-game gate could not see because every model
                    // side shared the gate. Keep this condition identical to the applier's.
                    if (b < 1f && v.passionCountMin > 0f) b = 1f;
                    if (b < 0f) b = 0f;

                    // The generator's spend loop: whole passions only, remainder discarded. Run
                    // BEFORE the capacity limit because that is the generator's order -- it spends
                    // the whole budget into counts and only discovers how many eligible skills
                    // exist at assignment, discarding the surplus there.
                    //
                    // Both limits are applied per OUTCOME, not to a mean, so p2 stays the exact
                    // second moment. That matters more than it looks: discretization removes a
                    // little dispersion as well as shifting the mean, and Best-of-N is a maximum
                    // statistic, so it reads dispersion directly. See PassionSpend (finding Q-14).
                    var outcomes = PassionSpend.Outcomes(b, v.passionMajorBias);
                    for (int k = 0; k < outcomes.Length; k++)
                    {
                        float pips = outcomes[k].Pips;
                        if (pips > capacity) pips = capacity;
                        float u = Mathf.Min(1f, pips * eff / pdiv);
                        float w = gaussW[i] * outcomes[k].Weight;
                        p1 += w * u;
                        p2 += w * u * u;
                    }
                }
                pVar = Mathf.Max(0f, p2 - p1 * p1);
            }
            else
            {
                // Mirrors CalculateCompositeScore's passion-off fallback exactly: vanilla's own
                // 5-pip budget at vanilla's 50/50 Major coin flip, scored through the same
                // efficiency term as every other profile -- AND through the same spend loop, since
                // vanilla's generator discretizes exactly the way ours does. Scoring the fallback
                // continuously while scoring the live axis discretely would put the two branches
                // on different scales and break the both-axes-off invariant Q-16 turns on:
                // Faithful's band at q = 0.50 is VanillaPassionBudget pips at VanillaMajorBias, so
                // the two paths have to agree term for term.
                float veff = PawnVarianceSettings.PassionPipEfficiency(Constants.VanillaMajorBias);
                var vOutcomes = PassionSpend.Outcomes(Constants.VanillaPassionBudget,
                                                      Constants.VanillaMajorBias);
                p1 = 0f;
                for (int k = 0; k < vOutcomes.Length; k++)
                    p1 += vOutcomes[k].Weight * Mathf.Min(1f, vOutcomes[k].Pips * veff / pdiv);
                pVar = 0f;
            }

            mu = (wS * s1 + wP * p1) / wsum;
            sd = Mathf.Sqrt((wS * wS * sVar + wP * wP * pVar) / (wsum * wsum));
        }

        // F(x) on a midpoint grid over [0,1]. The [0,1] domain IS the composite's Clamp01 --
        // integrating the unclamped normal CDF over [0,1] is exact for the clamped variable.
        // Do not widen the range or pre-clamp F.
        private static float[] BuildCdf(VarianceProfileValues v, int qNodes, int xNodes)
        {
            v.GetBetaAlphaBeta(out float alpha, out float beta);
            float dq = 1f / qNodes;

            var qs = new float[qNodes];
            var wq = new float[qNodes];
            float total = 0f;
            for (int i = 0; i < qNodes; i++)
            {
                float q = (i + 0.5f) * dq;
                qs[i] = q;
                wq[i] = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                total += wq[i] * dq;
            }
            for (int i = 0; i < qNodes; i++) wq[i] = wq[i] * dq / total;

            var mus = new float[qNodes];
            var sds = new float[qNodes];
            for (int i = 0; i < qNodes; i++) Moments(v, qs[i], out mus[i], out sds[i]);

            float dx = 1f / xNodes;
            var F = new float[xNodes];
            for (int j = 0; j < xNodes; j++)
            {
                float x = (j + 0.5f) * dx;
                float acc = 0f;
                for (int i = 0; i < qNodes; i++)
                {
                    // sd == 0 at zero noise; NormalCdf would divide by zero. Step function there.
                    acc += wq[i] * (sds[i] > 1e-12f
                        ? MathUtil.NormalCdf((x - mus[i]) / sds[i])
                        : (x >= mus[i] ? 1f : 0f));
                }
                F[j] = acc;
            }
            return F;
        }

        public static float BestOfN(VarianceProfileValues v, int n, bool lowRes = false)
        {
            int xNodes = lowRes ? XNodesDrag : XNodes;
            float[] F = BuildCdf(v, lowRes ? QNodesDrag : QNodes, xNodes);
            float dx = 1f / xNodes;
            float acc = 0f;
            for (int j = 0; j < xNodes; j++) acc += (1f - Mathf.Pow(F[j], n)) * dx;
            return acc;
        }

        // E[composite | q] -- the dispersion-aware "typical pawn" at a given quality.
        public static float TypicalAt(VarianceProfileValues v, float q)
        {
            Moments(v, q, out float mu, out _);
            return mu;
        }

        // The realised-outcome density for the header curve. Analytic Gaussian mixture rather than
        // finite differences of F -- same inputs, visibly smoother line.
        //
        // Moments() is hoisted out of the x loop, exactly as BuildCdf does it. Calling it inside
        // would evaluate it qNodes*xNodes times (131k per frame), each doing its own tri+gauss
        // quadrature internally -- ~500x the work for an identical result, since mu and sd do not
        // depend on x. This is the single most expensive thing on the editor's per-frame path.
        public static void OutcomeDensity(VarianceProfileValues v, float[] into)
        {
            v.GetBetaAlphaBeta(out float alpha, out float beta);
            int qNodes = QNodes, xNodes = into.Length;
            float dq = 1f / qNodes;

            // Reused across calls rather than reallocated: this runs every IMGUI frame the editor
            // tab is open. Same pattern as ProfileEditorTab's curveDensityScratch. qNodes is the
            // QNodes const, so these are allocated once and never resized.
            if (densityWq == null || densityWq.Length != qNodes)
            {
                densityWq = new float[qNodes];
                densityMus = new float[qNodes];
                densitySds = new float[qNodes];
            }
            float[] wq = densityWq, mus = densityMus, sds = densitySds;
            float total = 0f;
            for (int i = 0; i < qNodes; i++)
            {
                float q = (i + 0.5f) * dq;
                wq[i] = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                total += wq[i] * dq;
                Moments(v, q, out mus[i], out sds[i]);
            }
            for (int i = 0; i < qNodes; i++) wq[i] = wq[i] * dq / total;

            float invSqrt2Pi = 1f / Mathf.Sqrt(2f * Mathf.PI);
            float dx = 1f / xNodes;
            for (int j = 0; j < xNodes; j++)
            {
                float x = (j + 0.5f) / xNodes;
                float acc = 0f;
                for (int i = 0; i < qNodes; i++)
                {
                    float sd = sds[i];
                    if (sd <= 1e-12f)
                    {
                        // Zero-dispersion q-node (e.g. both spread sliders at 0): there is no
                        // Gaussian to spread, so the node's whole weight collapses to a point
                        // mass at mus[i] instead of vanishing. Deposit it only into the one
                        // x-bin that contains mus[i], scaled by 1/dx so it is expressed as a
                        // density -- the same units the Gaussian branch below produces -- and
                        // integrates back to wq[i] once multiplied by dx, so a mixed profile
                        // (some q-nodes degenerate, some not) still normalises consistently.
                        int bin = Mathf.Clamp(Mathf.FloorToInt(mus[i] * xNodes), 0, xNodes - 1);
                        if (j == bin) acc += wq[i] / dx;
                        continue;
                    }
                    float z = (x - mus[i]) / sd;
                    acc += wq[i] * invSqrt2Pi / sd * Mathf.Exp(-0.5f * z * z);
                }
                into[j] = acc;
            }
        }
    }
}
