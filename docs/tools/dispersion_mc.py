"""Independent Monte-Carlo ground truth for the dispersion model.

Deliberately a DIFFERENT numerical method from the quadrature in envelope_check.py, so it can
catch shared quadrature errors. It does NOT make verification fully independent: this and the
quadrature both substitute a flat AssumedVanillaSkillBaseline for each skill's real vanilla level.
Only `Roll pawns and dump distribution` sees real baselines. Do not describe this as independent
verification without that qualifier.
"""
import math
import os
import random
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import envelope_check as ec

NS = (1, 5, 25, 50)
SEED = int(sys.argv[1]) if len(sys.argv) > 1 else 20260807
M = int(sys.argv[2]) if len(sys.argv) > 2 else 200_000


def make_realised(C):
    """The composite, taking REALISED per-skill shifts and a REALISED budget.

    Mirrors envelope_check.make_composite term for term; only the inputs differ.
    """
    wS, wP = C["CompositeSkillWeight"], C["CompositePassionWeight"]
    base, top, pdiv = (C["AssumedVanillaSkillBaseline"],
                       C["AssumedMaxSkillLevel"], C["MaxPassionPips"])
    major, minor = C["MajorPassionCost"], C["MinorPassionCost"]
    skills = pdiv / major
    efficiency = ec.make_efficiency(C)

    def realised(shifts, budget, p):
        acc = 0.0
        for s in shifts:
            lvl = base + s
            if lvl < 0.0:
                lvl = 0.0
            elif lvl > top:
                lvl = top
            acc += lvl
        skill_norm = (acc / len(shifts)) / top
        if skill_norm > 1.0:
            skill_norm = 1.0

        capacity = skills * (minor + (major - minor) * p["passionMajorBias"])
        eff = efficiency(p["passionMajorBias"])
        b = budget if budget < capacity else capacity
        passion_norm = b * eff / pdiv
        if passion_norm < 0.0:
            passion_norm = 0.0
        elif passion_norm > 1.0:
            passion_norm = 1.0

        c = (wS * skill_norm + wP * passion_norm) / (wS + wP)
        return c if c < 1.0 else 1.0

    return realised


def expected_max_of_n(sorted_xs, N):
    """E[max of N] under the empirical distribution, via order statistics."""
    m = len(sorted_xs)
    acc = 0.0
    prev = 0.0
    for k in range(1, m + 1):
        cur = (k / m) ** N
        acc += sorted_xs[k - 1] * (cur - prev)
        prev = cur
    return acc


def simulate(p, C, realised, n_skills, with_noise):
    eps = C["QualityClampEpsilon"]
    K = C["BetaConcentrationK"]
    m = min(max(p["averageQuality"], eps), 1.0 - eps)
    a, b = m * K, (1.0 - m) * K

    mag = (p["skillSpread"] * math.sqrt(6.0)) if with_noise else 0.0
    sig = p["passionSpread"] if with_noise else 0.0
    window = sig * C["PassionBudgetClampFactor"]

    smin, smax = p["skillShiftMin"], p["skillShiftMax"]
    bmin, bmax = p["passionCountMin"], p["passionCountMax"]

    rnd = random.Random(SEED)
    out = []
    for _ in range(M):
        q = rnd.betavariate(a, b)
        baseline = smin + (smax - smin) * q
        if mag:
            shifts = [baseline + (rnd.random() + rnd.random() - 1.0) * mag
                      for _ in range(n_skills)]
        else:
            shifts = [baseline] * n_skills

        budget = bmin + (bmax - bmin) * q
        if sig:
            g = rnd.gauss(0.0, sig)
            if g > window:
                g = window
            elif g < -window:
                g = -window
            budget += g
            if budget < 1.0 and bmin > 0.0:
                budget = 1.0
            if budget < 0.0:
                budget = 0.0
        out.append(realised(shifts, budget, p))

    out.sort()
    return out


def main():
    C = ec.parse_constants(ec.read(ec.CONSTANTS))
    P = ec.parse_profiles(ec.read(ec.PROFILES))
    composite = ec.make_composite(C)
    realised = make_realised(C)
    n_skills = int(round(C["MaxPassionPips"] / C["MajorPassionCost"]))

    worst = 0.0
    for name, p in P.items():
        for i in range(21):
            q = i / 20.0
            baseline = p["skillShiftMin"] + (p["skillShiftMax"] - p["skillShiftMin"]) * q
            budget = p["passionCountMin"] + (p["passionCountMax"] - p["passionCountMin"]) * q
            got = realised([baseline] * n_skills, budget, p)
            want = composite(q, p)
            worst = max(worst, abs(got - want))
    print(f"self-check: max |realised - analytic| at zero noise = {worst:.2e}")
    if worst >= 1e-12:
        print("FAIL: realised composite does not reproduce the analytic one")
        return 1
    print(f"skills={n_skills}  pawns/preset={M:,}  seed={SEED}\n")

    print(f"{'profile':<12}{'N':>4}  {'best-of-N':>10}")
    for name, p in P.items():
        s = simulate(p, C, realised, n_skills, with_noise=True)
        for N in NS:
            print(f"{name:<12}{N:>4}  {expected_max_of_n(s, N):10.4f}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
