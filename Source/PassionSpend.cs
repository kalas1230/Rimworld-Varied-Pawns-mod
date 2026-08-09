using System.Collections.Generic;

namespace PawnVarianceMod
{
    // The generator's passion spend loop, as an EXACT distribution instead of a sample.
    // Mirrors make_spend in docs/tools/envelope_check.py -- IF YOU CHANGE ONE, CHANGE BOTH.
    //
    // WHY THIS EXISTS. PassionVarianceApplier does not spend a continuous budget
    // (PassionVarianceApplier.cs:81-93): it buys whole passions at MajorPassionCost /
    // MinorPassionCost until it can no longer afford a Minor, and the remainder in
    // [0, MinorPassionCost) is DISCARDED. A pawn whose budget rolls 4.8 pips receives 4.
    // Until 2026-08-09 every model side -- DispersionModel.Moments, CalculateCompositeScore,
    // envelope_check.py and dispersion_mc.py -- scored the continuous budget, i.e. scored a pawn
    // richer than any the generator rolls. Faithful: 5.000 pips assumed against 4.551 delivered.
    // That ~0.45-pip gap is the figure the 1000-pawn in-game dump recorded as 4.59 and which
    // HANDOVER filed as evidence of vanilla parity -- true, but it is ALSO evidence that the
    // scoring model assumed a budget no pawn ever gets. Audit finding Q-14.
    //
    // The loss is ~0.45 pips on EVERY profile, so most of it cancels in the ratio to Faithful.
    // What does not cancel is signed by tier: it pushes each profile further from Faithful in the
    // direction it already sits, so the real tiers are further apart than the envelope said.
    //
    // WHY THIS CAN BE EXACT RATHER THAN SAMPLED. Every branch in the loop tests the remaining
    // budget against MinorPassionCost or MajorPassionCost, so the outcome distribution depends on
    // the budget ONLY through which pair of consecutive thresholds it falls between. Between two
    // such breakpoints the distribution is literally constant, so a table indexed by breakpoint is
    // not an approximation of the answer -- it IS the answer. Breakpoints are enumerated from the
    // reachable spend totals rather than assumed to lie on a fixed grid, so the two costs are not
    // required to be commensurable.
    //
    // The table is also tiny. The loop exits holding less than one Minor, so delivered pips always
    // lie in (budget - MinorPassionCost, budget] and at most three distinct totals can carry mass
    // (two, at the shipped costs). That is what lets callers apply the capacity limit and Clamp01
    // per OUTCOME rather than to a mean, which keeps the SECOND moment exact too -- the
    // discretization removes a little dispersion as well as shifting the mean, and Best-of-N is a
    // maximum statistic that reads dispersion directly.
    public static class PassionSpend
    {
        // One reachable outcome of the loop: a whole number of pips, and how often it happens.
        public readonly struct Outcome
        {
            public readonly float Pips;
            public readonly float Weight;
            public Outcome(float pips, float weight) { Pips = pips; Weight = weight; }
        }

        private sealed class Table
        {
            public double[] Cuts;        // interval left endpoints, ascending
            public Outcome[][] Outcomes; // Outcomes[i] applies to budgets in [Cuts[i], Cuts[i+1])
        }

        // Keyed by Major bias, which is per-profile and constant across every node of a Moments
        // sweep. A single slot would thrash the moment two profiles are compared in a loop, which
        // is exactly what the Verify action and the envelope table both do.
        private static readonly Dictionary<float, Table> Tables = new Dictionary<float, Table>();

        private static readonly Outcome[] Nothing = { new Outcome(0f, 1f) };

        // Built in double, not float, because the Python mirror is double throughout. The cut
        // values are small multiples of the two costs reached by repeated addition, so matching
        // the accumulation width is what keeps the two tables' breakpoints identical.
        private static Table Build(float bias)
        {
            double major = Constants.MajorPassionCost;
            double minor = Constants.MinorPassionCost;

            // Widest budget a caller can present: the passion band is clamped to MaxPassionPips
            // (VarianceProfile.ClampAndSwap) and the Gaussian is truncated at
            // PassionBudgetClampFactor sigma, with sigma at most PassionBudgetSpreadMax.
            double maxBudget = Constants.MaxPassionPips
                + Constants.PassionBudgetSpreadMax * Constants.PassionBudgetClampFactor;

            var totals = new HashSet<double> { 0.0 };
            var frontier = new List<double> { 0.0 };
            while (frontier.Count > 0)
            {
                var next = new List<double>();
                foreach (double t in frontier)
                {
                    for (int s = 0; s < 2; s++)
                    {
                        double v = System.Math.Round(t + (s == 0 ? major : minor), 9);
                        if (v <= maxBudget && totals.Add(v)) next.Add(v);
                    }
                }
                frontier = next;
            }

            var cutSet = new HashSet<double>();
            foreach (double t in totals)
            {
                double a = System.Math.Round(t + minor, 9);
                double b = System.Math.Round(t + major, 9);
                if (a <= maxBudget + major) cutSet.Add(a);
                if (b <= maxBudget + major) cutSet.Add(b);
            }
            var cuts = new List<double>(cutSet);
            cuts.Sort();

            var table = new Table
            {
                Cuts = cuts.ToArray(),
                Outcomes = new Outcome[cuts.Count][],
            };
            // Evaluate once per interval, at its left endpoint -- the interval is [cut, nextCut).
            for (int i = 0; i < cuts.Count; i++) table.Outcomes[i] = Distribution(cuts[i], bias);
            return table;
        }

        // One exact run of the loop's state machine, as a forward pass over (majors, minors).
        private static Outcome[] Distribution(double budget, float bias)
        {
            double major = Constants.MajorPassionCost;
            double minor = Constants.MinorPassionCost;

            var done = new Dictionary<double, double>();
            var cur = new Dictionary<int, double> { { 0, 1.0 } };   // key: majors << 8 | minors
            while (cur.Count > 0)
            {
                var next = new Dictionary<int, double>();
                foreach (var kv in cur)
                {
                    int m = kv.Key >> 8, n = kv.Key & 0xFF;
                    double w = kv.Value;
                    double spent = major * m + minor * n;
                    double left = budget - spent;

                    if (left < minor)
                    {
                        done.TryGetValue(spent, out double acc);
                        done[spent] = acc + w;
                        continue;
                    }
                    if (left >= major)
                    {
                        // Rand.Chance is short-circuited away when a Major is unaffordable, so the
                        // coin is only flipped on this branch. Keep that; flipping it regardless
                        // would consume a different number of random values than the generator.
                        int km = ((m + 1) << 8) | n;
                        next.TryGetValue(km, out double a);
                        next[km] = a + w * bias;

                        int kn = (m << 8) | (n + 1);
                        next.TryGetValue(kn, out double b);
                        next[kn] = b + w * (1f - bias);
                    }
                    else
                    {
                        int kn = (m << 8) | (n + 1);
                        next.TryGetValue(kn, out double b);
                        next[kn] = b + w;
                    }
                }
                cur = next;
            }

            var keys = new List<double>(done.Keys);
            keys.Sort();
            var outcomes = new Outcome[keys.Count];
            for (int i = 0; i < keys.Count; i++)
                outcomes[i] = new Outcome((float)keys[i], (float)done[keys[i]]);
            return outcomes;
        }

        /// <summary>
        /// The exact distribution of pips PassionVarianceApplier's spend loop hands over for this
        /// budget and Major bias. Weights sum to 1. Never returns null or an empty array.
        /// </summary>
        public static Outcome[] Outcomes(float budget, float bias)
        {
            if (budget < Constants.MinorPassionCost) return Nothing;

            if (!Tables.TryGetValue(bias, out Table t))
            {
                t = Build(bias);
                Tables[bias] = t;
            }

            // Rightmost cut <= budget.
            double[] cuts = t.Cuts;
            int lo = 0, hi = cuts.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (cuts[mid] <= budget) lo = mid + 1; else hi = mid;
            }
            if (lo <= 0) return Nothing;
            return t.Outcomes[lo - 1 < cuts.Length ? lo - 1 : cuts.Length - 1];
        }

        /// <summary>
        /// Expected delivered pips. Convenience for the single-value callers; the moment-accurate
        /// callers must walk <see cref="Outcomes"/> themselves so the clamps land per outcome.
        /// </summary>
        public static float ExpectedPips(float budget, float bias)
        {
            float acc = 0f;
            Outcome[] os = Outcomes(budget, bias);
            for (int i = 0; i < os.Length; i++) acc += os[i].Pips * os[i].Weight;
            return acc;
        }
    }
}
