# Measured pawn distributions — source data for listing image 4

Four 1000-pawn `Roll pawns and dump distribution` runs, 2026-08-12, against a build deployed
from the current tree. **Measured, not modelled.** `tools/make-distribution.ps1` draws the
listing graphic from the numbers below; if the numbers are re-measured, change them there.

The axis plotted is **per-pawn mean skill** — the average of a pawn's twelve skill levels. That
is the quantity the mod's spread is about: two pawns with the same mean play very differently
from two pawns drawn from a wider band.

## What "vanilla-like" means here, exactly

The control is **not** a mod-disabled run. It is the profile named `Custom 1` in the test config,
which is `Faithful` — the mod's closest-to-unmodded preset — in every field but one:

| field | `Custom 1` | `Faithful` |
|---|---|---|
| averageQuality | 0.4995024 | 0.5 |
| skillSpread | 0.489898 | 0.489898 |
| passionSpread | 1.0 | 1.0 |
| passionMajorBias | 0.5 | 0.5 |
| skillShiftMin / Max | −3 / 3 | −3 / 3 |
| childSkillShiftMin / Max | −1 / 2 | −1 / 2 |
| traitCountMin / Max | 2 / 3 | 2 / 3 |
| passionCountMin / Max | 3 / 7 | 3 / 7 |

One field differs, by 0.0005 on a 0–1 dial. So the control is Faithful to within slider
quantisation, and the graphic must say **"Faithful (closest to unmodded)"** rather than
"vanilla" — a literal vanilla control needs a run with all three axes disabled, which was not
taken. See STATUS.md.

## The runs

Two runs per profile, so the shape can be told apart from run-to-run noise. Bin edges are chosen
per run by the debug action, so **runs must not be summed** — plot one, or plot both separately.

### Faithful-equivalent (`Custom 1`), per-pawn mean skill

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 3.34 | 1.20 | 0.2 | 1.9 | 3.3 | 4.9 | 7.6 |
| B | 3.38 | 1.16 | 0.4 | 1.9 | 3.3 | 4.9 | 7.8 |

Run A histogram (bin lower edge → count):

```
0.17  11     2.64 198     5.11  32
0.78  28     3.26 187     5.73  20
1.40  98     3.88 145     6.35  10
2.02 161     4.49 108     6.97   2
```

Run B histogram:

```
0.42  14     2.89 229     5.36  35
1.03  43     3.51 169     5.98  12
1.65 115     4.13 128     6.60   2
2.27 172     4.74  80     7.22   1
```

### Distinct, per-pawn mean skill

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 3.36 | 1.47 | 0.1 | 1.6 | 3.3 | 5.3 | 10.3 |
| B | 3.29 | 1.50 | 0.2 | 1.5 | 3.1 | 5.3 | 9.3 |

Run A histogram:

```
0.08  26     3.47 195     6.86   7
0.93 112     4.32 133     7.71   4
1.78 207     5.17  67     8.56   1
2.63 208     6.01  39     9.40   1
```

Run B histogram:

```
0.17  33     3.22 184     6.28  19
0.93 105     3.99 127     7.04   7
1.69 180     4.75  81     7.81   5
2.46 206     5.51  49     8.57   4
```

## The claim the graphic can honestly make

**The colony average does not move; the individuals spread out.**

| | Faithful-equiv | Distinct | change |
|---|---|---|---|
| mean of per-pawn means | 3.36 | 3.33 | −0.03 (noise) |
| sd | 1.18 | 1.49 | **+26%** |
| p10 – p90 band width | 3.0 | 3.75 | **+25%** |
| best pawn seen in 1000 | 7.8 | 10.3 | **+2.5 levels** |

That is the mod's actual pitch, and it is the honest reading of these four runs. Note the mean
being flat is **expected and load-bearing** — Distinct's `averageQuality` is 0.32, below
Faithful's 0.50, which is what buys the extra spread without buying extra power. It is also why
Distinct sits *below* Faithful at N=1 (−8.7%) and above it by N=25 (+9.5%) in
`envelope_check.py`.

## Provenance

- Each run's `GENERATOR vs MODEL` block passed: Distinct delta −0.015 and −0.082 against a
  0.206 tolerance; the Faithful-equivalent runs −0.016 and +0.062 against 0.156. So the pawns
  behind these histograms are pawns the scoring model demonstrably describes.
- The Distinct runs report `ACTUALLY RESOLVED TO: Distinct x1000 (100.0%)` with no override
  warning. The Faithful-equivalent runs report `Custom 1 x1000` **with** the
  `^^ NOT the configured active profile` warning, because at that point `activeProfileId` had
  been re-pointed but `RefreshResolved()` had not re-run — the figures are `Custom 1`'s either
  way, which is what the table above claims them to be.
