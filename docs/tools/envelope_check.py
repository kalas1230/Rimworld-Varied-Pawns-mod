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
"""

import math
import os
import re
import sys

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
CONSTANTS = os.path.join(ROOT, "Source", "Constants.cs")
PROFILES = os.path.join(ROOT, "Source", "VarianceProfile.cs")

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
          "passionCountMin", "passionCountMax", "passionMajorBias")


def read(path):
    with open(path, "r", encoding="utf-8-sig") as fh:
        return fh.read()


def parse_constants(src):
    out = {}
    for m in re.finditer(r"public\s+const\s+float\s+(\w+)\s*=\s*(-?[\d.]+)f?\s*;", src):
        out[m.group(1)] = float(m.group(2))
    required = ["CompositeSkillWeight", "CompositePassionWeight", "MaxPassionPips",
                "AssumedVanillaSkillBaseline", "AssumedMaxSkillLevel", "BetaConcentrationK"]
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


def make_composite(C):
    """Mirror of PawnVarianceSettings.CalculateCompositeScore. Presets all enable both axes."""
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])

    def composite(q, p):
        shift = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
        skill_norm = min(1.0, max(0.0, min(max(base + shift, 0.0), top) / top))
        budget = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
        pips = budget * (1.0 + 0.25 * p["passionMajorBias"])
        passion_norm = min(1.0, max(0.0, pips / pdiv))
        return min(1.0, (wS * skill_norm + wP * passion_norm) / (wS + wP))

    return composite


GRID = 20000


def beta_grid(m, K):
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
    grids = {n: beta_grid(p["averageQuality"], C["BetaConcentrationK"]) for n, p in P.items()}

    R = (C["AssumedMaxSkillLevel"] / C["MaxPassionPips"]) * \
        (C["CompositePassionWeight"] / C["CompositeSkillWeight"])
    print(f"wS={C['CompositeSkillWeight']:g}  wP={C['CompositePassionWeight']:g}  "
          f"pips/{C['MaxPassionPips']:g}  skill/{C['AssumedMaxSkillLevel']:g}  "
          f"K={C['BetaConcentrationK']:g}")
    print(f"Exchange rate R = ({C['AssumedMaxSkillLevel']:g}/{C['MaxPassionPips']:g}) * "
          f"({C['CompositePassionWeight']:g}/{C['CompositeSkillWeight']:g}) = "
          f"{R:.2f} skill levels per passion pip")
    print(f"Faithful baseline @ q=0.50: {composite(0.50, P['Faithful']):.4f}\n")

    score = {(n, N): expected_best_of_n(P[n], N, grids[n], composite)
             for n in P for N in BATCHES}
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
