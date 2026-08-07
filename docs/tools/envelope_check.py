#!/usr/bin/env python3
"""
Best-of-N envelope + power-tier ordering check for the composite pawn-quality score.

RUN THIS AFTER CHANGING ANY OF:
  - Constants.CompositeSkillWeight / CompositePassionWeight / MaxPassionPips
  - Constants.AssumedVanillaSkillBaseline / AssumedMaxSkillLevel / BetaConcentrationK
  - any preset's averageQuality / skillShiftMin/Max / passionCountMin/Max / passionMajorBias

    python docs/tools/envelope_check.py

Exits non-zero if HANDOVER.md's Rule 1 (+-35% envelope vs Faithful at every N) or Rule 2
(monotonic power-tier ordering at every N) is violated, so it can gate a commit.

It PARSES Source/Constants.cs and Source/VarianceProfile.cs rather than hardcoding, so it
cannot silently drift from the shipped values. No third-party dependencies (no numpy).

Method (mirrors HANDOVER.md "How the percentages are derived"):
  q ~ Beta(m*K, (1-m)*K) with m = averageQuality, K = BetaConcentrationK.
  CalculateCompositeScore is monotonic in q, so Best-of-N score = composite(max(q_1..q_N)).
  E[score] is computed by deterministic integration against the density of the max,
  N * F(q)^(N-1) * f(q) -- not by sampling, so results are exactly reproducible.

SCOPE -- READ THIS BEFORE TRUSTING THE TABLE:
  Every figure here is a MEAN-POWER figure. The model treats a pawn as fully determined by
  its quality roll q, so it sees averageQuality, skillShiftMin/Max, passionCountMin/Max and
  passionMajorBias -- and NOTHING ELSE. skillSpread and passionSpread are not inputs to
  CalculateCompositeScore and no percentage in this table responds to them, even though
  skillSpread drives up to +-6 levels of per-skill excursion (Constants.MaxMagnitude).
  The "spread" columns below exist to make that invisible axis visible. They are REPORTED,
  NOT ENFORCED -- there is no Rule 1 equivalent for dispersion.

SIDE EFFECT: regenerates Source/EnvelopeFigures.g.cs (checked in). That file is what the
in-game "Verify Best-of-N against envelope_check.py" debug action diffs the mod's own
integrator against, which is what turns "if you change one, change both" from a comment
into a detectable failure. If `git status` shows it dirty after a run, the shipped figures
were stale -- commit it.
"""

import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONSTANTS = os.path.join(ROOT, "Source", "Constants.cs")
PROFILES = os.path.join(ROOT, "Source", "VarianceProfile.cs")
GENERATED = os.path.join(ROOT, "Source", "EnvelopeFigures.g.cs")

# Display name -> C# variable name in VarianceProfiles (see HANDOVER "What each profile represents").
NAMES = {
    "VanillaLike": "Faithful",
    "Hardscrabble": "Desperate",
    "Scavenger": "Scavenger",
    "BalancedVariance": "Distinct",
    "WildSpread": "Wildcard",
    "Specialist": "Specialist",
    "Elite": "Elite",
    "Sovereign": "Sovereign",
}
# Rule 2 applies to power tiers only. Distinct/Wildcard are variance presets and are exempt
# from ordering (but NOT from the Rule 1 envelope).
TIERS = ["Desperate", "Scavenger", "Faithful", "Specialist", "Elite", "Sovereign"]
NOT_ENFORCED = set()
BATCHES = (1, 5, 25, 50)
ENVELOPE = 35.0
FIELDS = ("averageQuality", "skillShiftMin", "skillShiftMax",
          "passionCountMin", "passionCountMax", "passionMajorBias",
          # Not scored -- read only for the reported-not-enforced spread columns. See SCOPE
          # in the module docstring for why these are absent from the composite.
          "skillSpread", "passionSpread")


def read(path):
    with open(path, "r", encoding="utf-8-sig") as fh:
        return fh.read()


def parse_constants(src):
    out = {}
    # Scientific notation is accepted because QualityClampEpsilon is written `1e-3f`. The rest of
    # the constraint still holds and is documented in HANDOVER.md: the right-hand side must be a
    # LITERAL, not an expression -- `12f * MajorPassionCost` makes this parse fail, not evaluate.
    for m in re.finditer(
            r"public\s+const\s+float\s+(\w+)\s*=\s*(-?[\d.]+(?:[eE][-+]?\d+)?)f?\s*;", src):
        out[m.group(1)] = float(m.group(2))
    required = ["CompositeSkillWeight", "CompositePassionWeight", "MaxPassionPips",
                "AssumedVanillaSkillBaseline", "AssumedMaxSkillLevel", "BetaConcentrationK",
                # Domain guard on averageQuality, mirrored from VarianceProfile.GetBetaAlphaBeta.
                "QualityClampEpsilon",
                # Passion capacity and pip-efficiency terms (see make_composite).
                "MajorPassionCost", "MinorPassionCost",
                "PassionLearnRateNone", "PassionLearnRateMinor", "PassionLearnRateMajor",
                # Only used to anchor the printed exchange rate. R is bias-dependent, so a single
                # quoted figure has to say which bias it is at, and vanilla's 50/50 is the anchor.
                "VanillaMajorBias",
                # Spread columns only -- these never enter the composite score.
                "MinMagnitudeFloor", "MaxMagnitude",
                "PassionBudgetSpreadMin", "PassionBudgetSpreadMax"]
    missing = [r for r in required if r not in out]
    if missing:
        sys.exit(f"ERROR: {CONSTANTS} is missing: {', '.join(missing)}")
    return out


def parse_profiles(src):
    """Slice the file at each `public static readonly VarianceProfile <Var>` and read the
    last assignment of each field inside that slice."""
    starts = [(m.start(), m.group(1)) for m in
              re.finditer(r"public\s+static\s+readonly\s+VarianceProfile\s+(\w+)\s*=", src)]
    if not starts:
        sys.exit(f"ERROR: no VarianceProfile declarations found in {PROFILES}")
    out = {}
    for i, (pos, var) in enumerate(starts):
        if var not in NAMES:
            continue
        end = starts[i + 1][0] if i + 1 < len(starts) else len(src)
        block = src[pos:end]
        vals = {}
        for f in FIELDS:
            hits = re.findall(rf"\b{f}\s*=\s*(-?[\d.]+)f?\s*[,;]", block)
            if not hits:
                sys.exit(f"ERROR: {var} has no '{f}' assignment")
            vals[f] = float(hits[-1])
        out[NAMES[var]] = vals
    missing = set(NAMES.values()) - set(out)
    if missing:
        sys.exit(f"ERROR: profiles not found: {', '.join(sorted(missing))}")
    return out


def make_efficiency(C):
    """Mirror of PawnVarianceSettings.PassionPipEfficiency. A Major costs 1.5 pips but is worth
    1.769 Minors in XP-rate increment over having no passion, so pips bought at a low Major bias
    are worth less. Normalised at bias 1.0 so that 18 all-Major pips stay exactly a saturated
    axis. Read the C# comment before changing anything here.

    Module-level rather than nested in make_composite because main() needs it too: the exchange
    rate R carries this factor, so printing R without it reproduces exactly the stale-figure defect
    (audit P-03) where R was quoted as a scalar built from three constants.
    """
    minor, major = C["MinorPassionCost"], C["MajorPassionCost"]
    minor_gain = C["PassionLearnRateMinor"] - C["PassionLearnRateNone"]
    major_gain = C["PassionLearnRateMajor"] - C["PassionLearnRateNone"]

    def efficiency(bias):
        price = minor + (major - minor) * bias
        gain = minor_gain + (major_gain - minor_gain) * bias
        return (gain / price) / (major_gain / major)

    return efficiency


def make_composite(C):
    """Mirror of PawnVarianceSettings.CalculateCompositeScore. Presets all enable both axes."""
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    # Derived, not a constant of its own -- MaxPassionPips is 12 skills x a Major, so dividing it
    # back out gives the skill count and cannot drift out of step with the ceiling.
    skills = pdiv / major

    efficiency = make_efficiency(C)

    def composite(q, p):
        shift = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
        skill_norm = min(1.0, max(0.0, min(max(base + shift, 0.0), top) / top))
        # The budget is already in pips. No per-Major premium on top of it -- the
        # `* (1 + 0.25 * passionMajorBias)` that stood here until 2026-08-06 was a unit error
        # inherited from the era when the denominator was 12 skills instead of 18 pips. Capacity
        # is the real cap: 12 skills, one passion each, at the bias's average price.
        budget = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
        capacity = skills * (minor + (major - minor) * p["passionMajorBias"])
        eff = efficiency(p["passionMajorBias"])
        passion_norm = min(1.0, max(0.0, min(budget, capacity) * eff / pdiv))
        return min(1.0, (wS * skill_norm + wP * passion_norm) / (wS + wP))

    return composite


def make_spread(C):
    """Dispersion figures the composite score cannot see. REPORTED, NOT ENFORCED.

    Per-skill shift is `baseline(q) + T * magnitude` (SkillVarianceApplier.Shift), where T is
    the `TriangularSample() * 2 - 1` term: triangular on [-1, 1], peaked at 0, variance 1/6.
    So the within-pawn, per-skill standard deviation is magnitude / sqrt(6), in skill levels.
    This is the axis the +-35% envelope is completely blind to -- two profiles with identical
    percentages in the table above can differ by 3x here.

    Passion spread is the budget Gaussian's sigma (PassionVarianceApplier), already in pips.
    """
    root6 = math.sqrt(6.0)

    def spread(p):
        magnitude = p["skillSpread"] * math.sqrt(6.0)
        budget_sigma = p["passionSpread"]
        return magnitude / root6, budget_sigma

    return spread


QGRID, XGRID, TGRID, GGRID = 256, 512, 65, 65


def _phi(z):
    return 0.5 * (1.0 + math.erf(z / math.sqrt(2.0)))


def _tri_nodes():
    """Triangular density on [-1,1]: f(t) = 1-|t|. Mirrors (Rand.Value+Rand.Value)/2*2-1."""
    dt = 2.0 / TGRID
    ts, ws, tot = [], [], 0.0
    for i in range(TGRID):
        t = -1.0 + (i + 0.5) * dt
        w = (1.0 - abs(t)) * dt
        ts.append(t)
        ws.append(w)
        tot += w
    return ts, [w / tot for w in ws]


def _gauss_nodes():
    """Standard normal truncated to +-4, matching PassionBudgetClampFactor."""
    lo, hi = -4.0, 4.0
    dz = (hi - lo) / GGRID
    zs, ws, tot = [], [], 0.0
    for i in range(GGRID):
        z = lo + (i + 0.5) * dz
        w = math.exp(-0.5 * z * z) * dz
        zs.append(z)
        ws.append(w)
        tot += w
    return zs, [w / tot for w in ws]


def grid_moments(C):
    """Mean and sd of the composite CONDITIONAL on q. Mirror of DispersionModel.Moments."""
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    n_skills = int(round(pdiv / major))
    efficiency = make_efficiency(C)
    TS, TW = _tri_nodes()
    ZS, ZW = _gauss_nodes()
    wsum = wS + wP

    def moments(p, q, with_noise=True):
        mag = (p["skillSpread"] * math.sqrt(6.0)) if with_noise else 0.0
        sig = p["passionSpread"] if with_noise else 0.0

        baseline = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
        s1 = s2 = 0.0
        for t, w in zip(TS, TW):
            lvl = base + baseline + t * mag
            lvl = 0.0 if lvl < 0.0 else (top if lvl > top else lvl)
            u = lvl / top
            s1 += w * u
            s2 += w * u * u
        # The pawn's AVERAGE over n_skills iid draws: variance divides by n_skills (CLT).
        s_var = max(0.0, s2 - s1 * s1) / n_skills

        bmean = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
        capacity = n_skills * (minor + (major - minor) * p["passionMajorBias"])
        eff = efficiency(p["passionMajorBias"])
        p1 = p2 = 0.0
        for z, w in zip(ZS, ZW):
            b = bmean + z * sig
            if sig > 0.0 and b < 1.0 and p["passionCountMin"] > 0.0:
                b = 1.0
            if b < 0.0:
                b = 0.0
            if b > capacity:
                b = capacity
            u = b * eff / pdiv
            if u > 1.0:
                u = 1.0
            p1 += w * u
            p2 += w * u * u
        p_var = max(0.0, p2 - p1 * p1)

        mu = (wS * s1 + wP * p1) / wsum
        var = (wS * wS * s_var + wP * wP * p_var) / (wsum * wsum)
        return mu, math.sqrt(var)

    return moments


def make_grid_score(C):
    """Dispersion-aware Best-of-N. Mirror of DispersionModel.BestOfN."""
    moments = grid_moments(C)

    def best_of_n(p, with_noise=True):
        eps, K = C["QualityClampEpsilon"], C["BetaConcentrationK"]
        m = min(max(p["averageQuality"], eps), 1.0 - eps)
        a, b = m * K, (1.0 - m) * K
        lb = math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
        dq = 1.0 / QGRID
        qs, dens, tot = [], [], 0.0
        for i in range(QGRID):
            q = (i + 0.5) * dq
            d = math.exp(lb + (a - 1.0) * math.log(q) + (b - 1.0) * math.log(1.0 - q))
            qs.append(q)
            dens.append(d)
            tot += d * dq
        wq = [d * dq / tot for d in dens]
        ms = [moments(p, q, with_noise) for q in qs]

        dx = 1.0 / XGRID
        F = []
        for j in range(XGRID):
            x = (j + 0.5) * dx
            acc = 0.0
            for (mu, sd), w in zip(ms, wq):
                # sd == 0 is the zero-noise self-check path; Phi would divide by zero.
                acc += w * (_phi((x - mu) / sd) if sd > 1e-12 else (1.0 if x >= mu else 0.0))
            F.append(acc)

        # The [0,1] bound IS the Clamp01 on the composite. Do not widen it.
        return {N: sum((1.0 - F[j] ** N) * dx for j in range(XGRID)) for N in (1, 5, 25, 50)}

    return best_of_n


GEN_CONSTANTS = ("CompositeSkillWeight", "CompositePassionWeight", "MaxPassionPips",
                 "AssumedVanillaSkillBaseline", "AssumedMaxSkillLevel", "BetaConcentrationK",
                 "QualityClampEpsilon",
                 "MajorPassionCost", "MinorPassionCost",
                 "PassionLearnRateNone", "PassionLearnRateMinor", "PassionLearnRateMajor")

GEN_HEADER = """// <auto-generated>
//     Regenerated by {tool}. DO NOT EDIT BY HAND.
//
//     These are the REFERENCE Best-of-N figures, integrated at {grid} nodes. The mod ships its
//     own {nodes}-node integrator (PawnVarianceSettings.CalculateBestOfNScoreCore) because
//     custom profiles need a live figure that no precomputed table can cover -- so there are
//     genuinely two implementations of the same integral, and they must agree.
//
//     The in-game debug action "Varied Pawns > Verify Best-of-N against envelope_check.py"
//     diffs the live integrator against this file. That is what makes the divergence
//     DETECTABLE rather than a comment someone has to remember to read. It is the exact bug
//     class that shipped once already: a Best-of-25 score compared against an N=1 baseline,
//     ~36pp wrong, with Desperate and Scavenger flipped positive.
//
//     The Gen* constants below snapshot what Constants.cs held at generation time. The debug
//     action compares them against the live values, so "changed a constant, forgot to re-run
//     the tool" is caught too -- a stale table cannot pass by looking self-consistent.
// </auto-generated>

namespace PawnVarianceMod
{{
    public static class EnvelopeFigures
    {{
        public const string Tool = "{tool}";
        public const int ReferenceNodes = {grid};

        // Constants.cs values these figures were generated from. Mismatch against the live
        // Constants means this file is stale -- re-run the tool, do not adjust these.
{gen_consts}

        public static readonly int[] Batches = {{ {batches} }};

        // Index-aligned with Batches. Display names, matching VarianceProfiles' labels.
        public static readonly string[] Profiles =
        {{
{profiles}
        }};

        public static readonly float[][] Scores =
        {{
{scores}
        }};
    }}
}}
"""


def write_generated(C, P, score, order):
    gen_consts = "\n".join(
        f"        public const float Gen{k} = {C[k]:g}f;" for k in GEN_CONSTANTS)
    profiles = ",\n".join(f'            "{n}"' for n in order)
    scores = ",\n".join(
        "            new[] { " + ", ".join(f"{score[(n, N)]:.6f}f" for N in BATCHES) + " }"
        for n in order)
    text = GEN_HEADER.format(
        tool="docs/tools/envelope_check.py", grid=GRID, nodes="1024",
        gen_consts=gen_consts, batches=", ".join(str(N) for N in BATCHES),
        profiles=profiles, scores=scores)

    # Only rewrite on a real change, so an unchanged run leaves the file (and its mtime) alone.
    existed = os.path.exists(GENERATED)
    if existed and read(GENERATED) == text:
        return "unchanged"
    with open(GENERATED, "w", encoding="utf-8", newline="\n") as fh:
        fh.write(text)
    return "rewritten" if existed else "created"


GRID = 20000


def beta_grid(m, K, eps):
    # Mirror of VarianceProfile.GetBetaAlphaBeta's clamp. Without it this tool and the C# it is
    # the reference for disagree for any preset outside [eps, 1-eps] -- and the in-game gate would
    # fail with no bug in either implementation, which looks identical to the two real gate
    # failures this project has had. At m = 0 it is worse than a disagreement: a = 0, lgamma(0)
    # is inf, every density underflows to 0 and the renormalisation divides by zero.
    m = min(max(m, eps), 1.0 - eps)
    a, b = m * K, (1.0 - m) * K
    lb = math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
    dq = 1.0 / GRID
    xs = [(i + 0.5) * dq for i in range(GRID)]
    f = [math.exp(lb + (a - 1.0) * math.log(x) + (b - 1.0) * math.log(1.0 - x)) for x in xs]
    F, run = [], 0.0
    for v in f:
        run += v * dq
        F.append(run)
    total = F[-1]  # renormalise away discretisation error so F(1) == 1 exactly
    return xs, [v / total for v in f], [v / total for v in F], dq


def expected_best_of_n(profile, N, grid, composite):
    xs, f, F, dq = grid
    acc = 0.0
    for i in range(GRID):
        acc += composite(xs[i], profile) * N * (F[i] ** (N - 1)) * f[i] * dq
    return acc


def main():
    C = parse_constants(read(CONSTANTS))
    P = parse_profiles(read(PROFILES))
    composite = make_composite(C)
    grids = {n: beta_grid(p["averageQuality"], C["BetaConcentrationK"], C["QualityClampEpsilon"])
             for n, p in P.items()}

    grid_score = make_grid_score(C)
    worst_selfcheck = 0.0
    for n, p in P.items():
        got = grid_score(p, with_noise=False)
        for N in (1, 5, 25, 50):
            want = expected_best_of_n(p, N, grids[n], composite)
            worst_selfcheck = max(worst_selfcheck, abs(got[N] - want))
    print(f"dispersion model self-check (zero noise vs analytic): {worst_selfcheck:.2e}")
    if worst_selfcheck > 1e-3:
        print("FAIL: the dispersion model does not reduce to the analytic score at zero noise")
        return 1

    # R carries the pip-efficiency factor, so it is a FUNCTION of the profile's Major bias, not a
    # scalar. Printing the bare weight ratio is what audit P-03 was: a figure that is only true for
    # an all-Major profile, quoted as though it were the general rate. Anchor it at vanilla's bias
    # and print the span so the bias-dependence is visible rather than implied.
    eff = make_efficiency(C)
    ratio = (C["AssumedMaxSkillLevel"] / C["MaxPassionPips"]) * \
        (C["CompositePassionWeight"] / C["CompositeSkillWeight"])
    anchor = C["VanillaMajorBias"]
    print(f"wS={C['CompositeSkillWeight']:g}  wP={C['CompositePassionWeight']:g}  "
          f"pips/{C['MaxPassionPips']:g}  skill/{C['AssumedMaxSkillLevel']:g}  "
          f"K={C['BetaConcentrationK']:g}")
    print(f"Exchange rate R(bias) = ({C['AssumedMaxSkillLevel']:g}/{C['MaxPassionPips']:g}) * "
          f"({C['CompositePassionWeight']:g}/{C['CompositeSkillWeight']:g}) * eff(bias)")
    print(f"  R = {ratio * eff(anchor):.2f} skill levels per passion pip at vanilla bias "
          f"{anchor:g}   (range {ratio * eff(0.0):.2f} at bias 0 .. "
          f"{ratio * eff(1.0):.2f} at bias 1)")
    print(f"Faithful baseline @ q=0.50: {composite(0.50, P['Faithful']):.4f}\n")

    dispersed = {n: grid_score(p, with_noise=True) for n, p in P.items()}
    score = {(n, N): dispersed[n][N] for n in P for N in BATCHES}
    dev = {(n, N): (score[(n, N)] - score[("Faithful", N)]) / score[("Faithful", N)] * 100.0
           for n in P for N in BATCHES}

    order = ["Faithful"] + [n for n in P if n != "Faithful"]
    print(f"{'profile':<12}" + "".join(f"{'N=' + str(N):>19}" for N in BATCHES))
    failures = []
    for n in order:
        cells = []
        for N in BATCHES:
            d = dev[(n, N)]
            bad = abs(d) > ENVELOPE
            if bad and n not in NOT_ENFORCED:
                failures.append(f"Rule 1: {n} at N={N} is {d:+.1f}% (limit +-{ENVELOPE:.0f}%)")
            mark = "!" if bad else " "
            cells.append(f"{score[(n, N)]:.4f} {d:+6.1f}%{mark}")
        note = "  (variance)" if n in ("Distinct", "Wildcard") else \
               "  (not enforced)" if n in NOT_ENFORCED else ""
        print(f"{n:<12}" + "".join(f"{c:>19}" for c in cells) + note)

    print("\nRule 2 - power-tier ordering at the same N:")
    for N in BATCHES:
        vals = [(t, score[(t, N)]) for t in TIERS]
        bad = [(vals[i][0], vals[i + 1][0]) for i in range(len(vals) - 1)
               if not vals[i][1] < vals[i + 1][1]]
        for lo, hi in bad:
            failures.append(f"Rule 2: at N={N}, {lo} is not below {hi}")
        print(f"  N={N:<4}" + " < ".join(f"{t}({v:.3f})" for t, v in vals) +
              ("   OK" if not bad else "   *** INVERSION ***"))

    tight = sorted(((ENVELOPE - abs(dev[(n, N)]), n, N) for n in P
                    for N in BATCHES if n not in NOT_ENFORCED))[:3]
    print("\nTightest envelope margins:")
    for margin, n, N in tight:
        print(f"  {n} @ N={N}: {dev[(n, N)]:+.1f}%  ({margin:.1f}pp of headroom)")

    # Dispersion. Deliberately printed AFTER the envelope table and deliberately unenforced:
    # the point is to make the axis visible to whoever reaches for skillSpread, not to add an
    # eighth architectural rule. Nothing above responds to either column.
    spread = make_spread(C)
    ref_skill, ref_passion = spread(P["Faithful"])
    print("\nWithin-pawn dispersion (REPORTED, NOT ENFORCED -- invisible to every % above):")
    print(f"  {'profile':<12}{'skillSpread':>11}{'per-skill sd':>15}{'vs Faithful':>13}"
          f"{'passionSpread':>14}{'budget sd':>12}")
    for n in order:
        s_sd, p_sd = spread(P[n])
        print(f"  {n:<12}{P[n]['skillSpread']:>11.2f}{s_sd:>12.2f} lv"
              f"{s_sd / ref_skill:>12.2f}x{P[n]['passionSpread']:>14.2f}"
              f"{p_sd:>9.2f} pips")
    print("  A profile can be flat in the table above and 3x wider here. Wildcard is exactly")
    print("  that case: its 2026-08-04 retune narrowed skillShift (the mean band), not skillSpread.")

    state = write_generated(C, P, score, order)
    notes = {
        "unchanged": "unchanged.",
        "created": "CREATED -- commit it.",
        "rewritten": "REWRITTEN -- the shipped figures were stale, commit it.",
    }
    print(f"\nSource/EnvelopeFigures.g.cs: {notes[state]}")

    if failures:
        print("\nFAIL:")
        for f in failures:
            print(f"  - {f}")
        print("\nUpdate the table in HANDOVER.md and fix the calibration before committing.")
        return 1
    print("\nPASS: Rule 1 and Rule 2 hold at every N for all enforced presets.")
    print("If any number moved, update the table in HANDOVER.md "
          '"The skill <-> passion exchange rate".')
    return 0


if __name__ == "__main__":
    sys.exit(main())
