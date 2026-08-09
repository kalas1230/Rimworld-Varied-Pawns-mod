#!/usr/bin/env python3
"""
Best-of-N envelope + power-tier ordering check for the composite pawn-quality score.

RUN THIS AFTER CHANGING ANY OF:
  - Constants.CompositeSkillWeight / CompositePassionWeight / MaxPassionPips
  - Constants.AssumedVanillaSkillBaseline / AssumedMaxSkillLevel / BetaConcentrationK
  - Constants.MajorPassionCost / MinorPassionCost / PassionLearnRateNone|Minor|Major
  - Constants.MagnitudeLerpLow / MaxMagnitude / PassionBudgetSpreadMin|Max
  - any preset's averageQuality / skillShiftMin/Max / passionCountMin/Max / passionMajorBias
  - any preset's skillSpread / passionSpread -- these ARE scoring inputs, see SCOPE below

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
  The model is DISPERSION-AWARE. grid_moments treats the composite as Normal(mu(q), sigma(q))
  conditional on the quality roll q and integrates that mixture into Best-of-N, so it reads
  averageQuality, skillShiftMin/Max, passionCountMin/Max, passionMajorBias AND both spread
  fields. skillSpread and passionSpread ARE inputs and every percentage in this table responds
  to them -- changing either one is a Rule 6 trigger like any other scoring constant.
  This paragraph used to say the opposite, and that was true only until the dispersion-aware
  scoring work landed: Best-of-N is a MAXIMUM statistic, maxima reward dispersion, and a metric
  blind to spread let Wildcard breach the +-35% envelope while this tool reported PASS.
  The "spread" columns below are a separate matter: raw dispersion is still REPORTED, NOT
  ENFORCED -- there is no Rule 1 equivalent bounding spread as its own axis.

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
          # SCORED. grid_moments reads both to build sigma(q), so every percentage responds to
          # them. They also feed the reported-not-enforced spread columns. This comment used to
          # say "not scored"; that was true only before the dispersion-aware work.
          "skillSpread", "passionSpread")

# SCORED, and mirrored from CalculateCompositeScore / DispersionModel.Moments as of 2026-08-09.
# No shipped preset assigns either one -- VarianceProfile.cs:77/79 default both to true -- so
# every figure this tool prints is unchanged by their addition. They are parsed and honoured
# anyway because the C# sides read them, and a mirror that silently assumes `true` is exactly the
# omission that let the per-axis-toggle defect (Q-01) sit behind a green 32/32 gate: both
# implementations agreed with each other while neither agreed with the generator. If a future
# preset ever turns an axis off, this tool follows it instead of quietly scoring the wrong thing.
BOOL_FIELDS = ("enableSkillVariance", "enablePassionVariance")


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
                # Vanilla's own budget and Major flip. These anchor the printed exchange rate AND,
                # since 2026-08-09, they are the passion axis's value for a profile with passion
                # variance switched off -- so they are read, not merely quoted. (They used to be
                # computed into a `passionNorm` that CalculateCompositeScore then multiplied by a
                # zeroed weight, i.e. they were dead on both sides.)
                "VanillaMajorBias", "VanillaPassionBudget",
                # Skill-noise Lerp endpoints. These DO enter the score now: grid_moments builds
                # the per-skill excursion from them via SkillNoiseScalar. They were spread-column
                # only before the dispersion-aware work.
                "MagnitudeLerpLow", "MaxMagnitude",
                "PassionBudgetSpreadMin", "PassionBudgetSpreadMax",
                # Read since 2026-08-09 by make_spend, which needs the widest budget a caller can
                # present in order to size its table. Q-06 notes this tool used to hardcode the
                # +-4 sigma window as a literal while dispersion_mc.py derived it from here; the
                # quadrature nodes in _gauss_nodes still hardcode it, so that half of Q-06 is open.
                "PassionBudgetClampFactor"]
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
        for f in BOOL_FIELDS:
            # Absent is the normal case: the field initialiser in VarianceProfile.cs is `true` and
            # no preset overrides it. Only an explicit `= false` turns an axis off.
            hits = re.findall(rf"\b{f}\s*=\s*(true|false)\s*[,;]", block)
            vals[f] = (hits[-1] == "true") if hits else True
        out[NAMES[var]] = vals
    missing = set(NAMES.values()) - set(out)
    if missing:
        sys.exit(f"ERROR: profiles not found: {', '.join(sorted(missing))}")
    return out


def make_spend(C):
    """Mirror of PassionVarianceApplier's spend loop. Returns `outcomes(budget, bias)` giving the
    EXACT distribution of pips the generator actually hands over, as a tuple of (pips, weight).

    The generator does not spend a continuous budget (PassionVarianceApplier.cs:81-93): it buys
    whole passions at MajorPassionCost / MinorPassionCost until it can no longer afford a Minor,
    and the remainder in [0, MinorPassionCost) is DISCARDED. Until 2026-08-09 every model side --
    this tool, DispersionModel.Moments, CalculateCompositeScore and dispersion_mc.py -- scored the
    continuous budget instead, i.e. scored a pawn richer than any the generator rolls. Faithful:
    5.000 pips assumed against 4.551 delivered, which is the ~0.45-pip gap the 1000-pawn in-game
    dump recorded as 4.59 (HANDOVER.md "The Faithful baseline"). Audit finding Q-14.

    Why this can be exact rather than sampled. Every branch in the loop tests the remaining budget
    against MinorPassionCost or MajorPassionCost, so the whole outcome distribution depends on
    `budget` ONLY through which of those thresholds it sits between. Between two consecutive
    breakpoints the distribution is literally constant, so a table indexed by breakpoint is not a
    discretisation of the answer -- it IS the answer. Breakpoints are enumerated from the reachable
    spend totals rather than assumed to lie on a fixed grid, so the two costs are not required to
    be commensurable.

    The table is also tiny: the loop exits holding less than one Minor, so delivered pips always
    lie in (budget - MinorPassionCost, budget] and at most three distinct totals carry any mass.
    That is what lets the callers apply the capacity and Clamp01 limits per OUTCOME instead of to
    a mean, keeping the second moment exact as well.
    """
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    # Widest budget the callers can present: the passion band is clamped to MaxPassionPips
    # (VarianceProfile.ClampAndSwap) and the Gaussian is truncated at PassionBudgetClampFactor
    # sigma with sigma at most PassionBudgetSpreadMax.
    max_budget = (C["MaxPassionPips"]
                  + C["PassionBudgetSpreadMax"] * C["PassionBudgetClampFactor"])
    cache = {}

    def dist(budget, bias):
        """One exact run of the loop's state machine, as a forward pass over (majors, minors)."""
        out = {}
        cur = {(0, 0): 1.0}
        while cur:
            nxt = {}
            for (m, n), w in cur.items():
                left = budget - major * m - minor * n
                if left < minor:
                    total = major * m + minor * n
                    out[total] = out.get(total, 0.0) + w
                elif left >= major:
                    nxt[(m + 1, n)] = nxt.get((m + 1, n), 0.0) + w * bias
                    nxt[(m, n + 1)] = nxt.get((m, n + 1), 0.0) + w * (1.0 - bias)
                else:
                    # Cannot afford a Major: the coin is not even flipped.
                    nxt[(m, n + 1)] = nxt.get((m, n + 1), 0.0) + w
            cur = nxt
        return tuple(sorted(out.items()))

    def build(bias):
        # Reachable spend totals, and the budget values at which the loop's decisions change.
        totals = {0.0}
        frontier = [0.0]
        while frontier:
            nxt = []
            for t in frontier:
                for step in (major, minor):
                    v = round(t + step, 9)
                    if v <= max_budget and v not in totals:
                        totals.add(v)
                        nxt.append(v)
            frontier = nxt
        cuts = sorted({round(t + s, 9) for t in totals for s in (minor, major)
                       if t + s <= max_budget + major})
        # Evaluate once per interval, at its left endpoint -- the interval is [cut, next_cut).
        return cuts, [dist(c, bias) for c in cuts]

    def outcomes(budget, bias):
        if budget < minor:
            return ((0.0, 1.0),)
        entry = cache.get(bias)
        if entry is None:
            entry = cache[bias] = build(bias)
        cuts, table = entry
        # Rightmost cut <= budget. bisect_right - 1, written out to avoid an import.
        lo, hi = 0, len(cuts)
        while lo < hi:
            mid = (lo + hi) // 2
            if cuts[mid] <= budget:
                lo = mid + 1
            else:
                hi = mid
        return table[lo - 1]

    return outcomes


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
    """Mirror of PawnVarianceSettings.CalculateCompositeScore.

    Both enable flags are honoured. A disabled axis is NOT dropped from the weighted average --
    it contributes vanilla's own level, because "passion variance off" means the pawn keeps
    vanilla's passions, not that it has none. The weights therefore stay unconditional. With both
    axes off the score is exactly the Faithful baseline (0.250709), which is the check that fixes
    the semantics. See the note on the weights in CalculateCompositeScore.
    """
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    # Derived, not a constant of its own -- MaxPassionPips is 12 skills x a Major, so dividing it
    # back out gives the skill count and cannot drift out of step with the ceiling.
    skills = pdiv / major

    efficiency = make_efficiency(C)
    outcomes = make_spend(C)

    def passion_from(budget, bias):
        """Pips -> normalised axis, through the generator's own spend loop.

        The loop is applied BEFORE the capacity limit, which is the generator's order: it spends
        the whole budget into whole passions and only then discovers how many eligible skills
        exist, discarding the surplus at assignment time. Capacity and Clamp01 are applied per
        OUTCOME rather than to the mean, so the second moment stays exact for grid_moments.
        """
        eff = efficiency(bias)
        acc = 0.0
        for pips, w in outcomes(budget, bias):
            u = min(pips, capacity_for(bias)) * eff / pdiv
            acc += w * min(1.0, max(0.0, u))
        return acc

    def capacity_for(bias):
        return skills * (minor + (major - minor) * bias)

    def composite(q, p):
        if p.get("enableSkillVariance", True):
            shift = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
            skill_norm = min(1.0, max(0.0, min(max(base + shift, 0.0), top) / top))
        else:
            skill_norm = base / top
        # The budget is already in pips. No per-Major premium on top of it -- the
        # `* (1 + 0.25 * passionMajorBias)` that stood here until 2026-08-06 was a unit error
        # inherited from the era when the denominator was 12 skills instead of 18 pips. Capacity
        # is the real cap: 12 skills, one passion each, at the bias's average price.
        if p.get("enablePassionVariance", True):
            budget = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
            passion_norm = passion_from(budget, p["passionMajorBias"])
        else:
            # Vanilla's own budget at vanilla's own 50/50 Major flip, scored through the same
            # efficiency term as every other profile -- AND through the same spend loop, because
            # vanilla's generator discretizes exactly the way ours does (HANDOVER.md "The Faithful
            # baseline"). Scoring the fallback continuously while scoring the live axis discretely
            # would put the two branches on different scales and break the both-axes-off invariant
            # that Q-16 turns on: Faithful's band at q = 0.50 is VanillaPassionBudget pips at
            # VanillaMajorBias, so the two paths must agree term for term.
            passion_norm = passion_from(C["VanillaPassionBudget"], C["VanillaMajorBias"])
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
    outcomes = make_spend(C)
    TS, TW = _tri_nodes()
    ZS, ZW = _gauss_nodes()
    wsum = wS + wP

    def moments(p, q, with_noise=True):
        # Both enable flags honoured, mirroring DispersionModel.Moments. A disabled axis is a
        # zero-variance CONSTANT at vanilla's level, at full weight -- not a dropped term. See
        # make_composite's docstring for why dropping it is the wrong reading.
        s1 = s2 = 0.0
        if p.get("enableSkillVariance", True):
            mag = (p["skillSpread"] * math.sqrt(6.0)) if with_noise else 0.0
            baseline = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
            for t, w in zip(TS, TW):
                lvl = base + baseline + t * mag
                lvl = 0.0 if lvl < 0.0 else (top if lvl > top else lvl)
                u = lvl / top
                s1 += w * u
                s2 += w * u * u
            # The pawn's AVERAGE over n_skills iid draws: variance divides by n_skills (CLT).
            s_var = max(0.0, s2 - s1 * s1) / n_skills
        else:
            s1, s_var = base / top, 0.0

        p1 = p2 = 0.0
        if p.get("enablePassionVariance", True):
            sig = p["passionSpread"] if with_noise else 0.0
            bmean = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
            capacity = n_skills * (minor + (major - minor) * p["passionMajorBias"])
            eff = efficiency(p["passionMajorBias"])
            for z, w in zip(ZS, ZW):
                b = bmean + z * sig
                # Vanilla's floor. NOT gated on sig -- PassionVarianceApplier applies it whenever the
                # budget lands under 1 and passionCountMin > 0, spread or no spread. Must stay
                # identical to DispersionModel.cs and dispersion_mc.py, and to the applier itself.
                if b < 1.0 and p["passionCountMin"] > 0.0:
                    b = 1.0
                if b < 0.0:
                    b = 0.0
                # The generator's spend loop: whole passions only, remainder discarded. Applied
                # BEFORE the capacity limit because that is the generator's order -- it spends the
                # whole budget into counts and only discovers how many eligible skills exist at
                # assignment, discarding the surplus there. Both limits are applied per OUTCOME,
                # so p2 is the exact second moment and picks up the dispersion the discretization
                # removes, not merely the shift in the mean. See make_spend (audit finding Q-14).
                for pips, pw in outcomes(b, p["passionMajorBias"]):
                    u = min(pips, capacity) * eff / pdiv
                    if u > 1.0:
                        u = 1.0
                    p1 += w * pw * u
                    p2 += w * pw * u * u
            p_var = max(0.0, p2 - p1 * p1)
            if not with_noise:
                # The spend loop flips a coin per passion, so it is a dispersion source that
                # SURVIVES zeroing both spread fields -- "zero noise" stopped meaning "zero
                # variance" when the loop was modelled (Q-14). with_noise=False exists for exactly
                # one caller, main()'s self-check, which asks whether the Normal-mixture Best-of-N
                # machinery reduces to the analytic composite when there is no dispersion to
                # integrate. Keeping the loop's variance here would make that comparison a test of
                # a Normal approximation to a two-point distribution instead, and it fails by
                # 4.1e-3. The MEAN above still runs through the loop, so the check keeps its teeth:
                # the analytic side averages the same outcomes and the two must still agree exactly.
                p_var = 0.0
        else:
            # Vanilla's fallback runs through the same loop -- see make_composite's else branch.
            vb, vbias = C["VanillaPassionBudget"], C["VanillaMajorBias"]
            veff = efficiency(vbias)
            p1 = sum(pw * min(1.0, pips * veff / pdiv)
                     for pips, pw in outcomes(vb, vbias))
            p_var = 0.0

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
    # TWO different baselines, printed together because conflating them was audit finding Q-03.
    #
    #   readout   - what PawnVarianceSettings.FaithfulBaseline() returns and what the in-game
    #               "Typical" row and the distribution curve are measured against. Dispersion-aware,
    #               so it matches the numerators it divides.
    #   mean-band - what a ZERO-VARIANCE vanilla pawn scores, i.e. the value the both-axes-off
    #               invariant (Q-16) turns on. NOT the readout's denominator.
    #
    # These agreed to six decimals until the spend loop was modelled (Q-14) and now differ by
    # ~3.4%: E[spent] is a staircase and the mean band lands just above one of its jumps, so
    # f(E[X]) and E[f(X)] separate. Quoting either one as "the Faithful baseline" without saying
    # which is how the readout ended up dividing one estimator by the other.
    mu_readout, _ = grid_moments(C)(P["Faithful"], 0.50)
    print(f"Faithful baseline @ q=0.50: {mu_readout:.4f} readout (dispersion-aware), "
          f"{composite(0.50, P['Faithful']):.4f} mean-band (both-axes-off invariant)\n")

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
