# Measured pawn distributions — source data for listing image 4

Ten 1000-pawn `Roll pawns and dump distribution` runs — two each for Faithful-equivalent,
Distinct, Sovereign, Desperate and Wildcard — against a build deployed from the current tree.
The graphic plots **four** of them; Wildcard is recorded here only, for the reasons in its own
section below.
**Measured, not modelled.** `tools/make-distribution.ps1` draws the listing graphic from the
numbers below; if the numbers are re-measured, change them there.

The Faithful and Distinct runs are from 2026-08-12; the Sovereign and Desperate runs were added
the same day against the same deployed DLL (`PawnVarianceMod.dll`, 2026-08-11 22:41), each with
`activeProfileId` set in `Config/Mod_PawnVarianceMod_PawnVarianceMod.xml` and the game restarted
— **not** via `reload_mod_settings`, which desyncs this mod's static settings reference.

The axis plotted is **per-pawn mean skill** — the average of a pawn's skill levels. That
is the quantity the mod's spread is about: two pawns with the same mean play very differently
from two pawns drawn from a wider band.

> **2026-08-12: the graphic no longer plots the runs in this section.** It plots the eight
> re-runs in *The capable-only re-measure* below, which average over the skills a colonist can
> actually use instead of over all twelve. Everything in this section is still valid and is kept
> as the record it was — but if you are looking for the numbers behind `distribution.png`, they
> are further down. The short reason: `SkillRecord.GetLevel` returns 0 for a disabled skill, so
> "average of twelve" quietly counted every incapability as a zero.

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

### Sovereign, per-pawn mean skill

Resolved `Sovereign x1000 (100.0%)` on both runs, no override warning.

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 5.18 | 1.11 | 1.9 | 3.8 | 5.2 | 6.6 | 8.7 |
| B | 5.15 | 1.09 | 1.9 | 3.7 | 5.2 | 6.5 | 10.0 |

Run A histogram:

```
1.92   7     4.17 136     6.42  70
2.48  18     4.73 213     6.98  41
3.04  58     5.29 194     7.54  14
3.60 115     5.85 130     8.10   4
```

Run B histogram:

```
1.92  11     4.61 241     7.31  21
2.59  31     5.28 234     7.98   1
3.26  97     5.96 129     8.65   0
3.94 166     6.63  68     9.33   1
```

### Desperate, per-pawn mean skill

Resolved `Desperate x1000 (100.0%)` on both runs, no override warning.

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 2.60 | 1.01 | 0.0 | 1.3 | 2.6 | 4.0 | 6.5 |
| B | 2.66 | 0.99 | 0.3 | 1.4 | 2.7 | 3.9 | 6.4 |

Run A histogram:

```
0.00   6     2.17 243     4.33  45
0.54  42     2.71 183     4.88  15
1.08 118     3.25 118     5.42   4
1.63 158     3.79  67     5.96   1
```

Run B histogram:

```
0.25  20     2.31 210     4.36  28
0.76  67     2.82 190     4.88  14
1.28 104     3.33 131     5.39   8
1.79 174     3.85  51     5.90   3
```

### Wildcard, per-pawn mean skill — RECORDED HERE, NOT ON THE GRAPHIC

Resolved `Wildcard x1000 (100.0%)` on both runs, no override warning.

**Measured against a DIFFERENT DLL from the four profiles above.** These two runs are the
verification of the 2026-08-12 Wildcard retune (`PawnVarianceMod.dll` 05:04, `averageQuality`
0.35, `passionSpread` 2.6, `passionCountMin` 0.3, `passionCountMax` 10.5). The other four
profiles were measured against the 04:42-and-earlier build. That is safe to compare — the retune
changed only Wildcard's own constants, and no generation or scoring code — but if the generator
is ever touched, **all five need re-measuring together.**

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 2.74 | 1.24 | 0.3 | 1.3 | 2.6 | 4.4 | 7.5 |
| B | 2.78 | 1.22 | 0.0 | 1.3 | 2.7 | 4.4 | 7.3 |

Run A histogram:

```
0.25  38     2.67 186     5.08  22
0.85 109     3.27 126     5.69  16
1.46 172     3.88  78     6.29   4
2.06 193     4.48  54     6.90   2
```

Run B histogram:

```
0.00  20     2.42 212     4.83  46
0.60  72     3.02 145     5.44   8
1.21 123     3.63 102     6.04   8
1.81 194     4.23  67     6.65   3
```

Per-skill sd 3.44 / 3.46 and traits/pawn sd 1.35 / 1.31 over an observed 0–7 range — both above
`Faithful`'s (3.41–3.43 and ~0.5), which is the gate the preset has to clear. Model deltas +0.040
and −0.017 against a 0.322 tolerance.

**Why it is not a fifth curve on `distribution.png`.** It was plotted as a fifth violet curve on
2026-08-12 and then **removed on the owner's call** — so this has been tried, and the record of
why it came back off matters more than the record of why it was excluded the first time.

**The reason is composition, not colour.** An earlier note here claimed no fifth hue could pass
the dataviz gate. That was wrong. It came from testing four hand-picked candidates, all of which
do genuinely fail — violet `#9085E9` and `#8E7CC3` collide with Faithful's blue (normal-vision ΔE
9.8 and 10.5 against a 15 floor), aqua `#4FB3A0` reads 14.3 against the same blue, and yellow
`#C98500` is ΔE 1.8 from Distinct's amber. But four misses are not a cap. Sweeping sRGB at step 4
against the same `--pairs all` dark-surface gate returns **15,387 passing fifth hues**; the best
is violet **`#8F3FD4`**, which never becomes the worst pair on any check (its own nearest
neighbours are ΔE 26.7 normal vs blue and 16.6 CVD vs green/tritan, roughly double the floors):

```
node scripts/validate_palette.js "#C08628,#3E96DE,#D55181,#008300,#8F3FD4" \
     --mode dark --surface "#1B1C20" --pairs all      -> ALL CHECKS PASS
```

**So if a fifth series is ever wanted on this or any chart in the set: sweep, do not guess.**
Guessing is what produced the wrong conclusion the first time.

What actually keeps Wildcard off is that the five-curve version is a worse image:

1. **It crowds the only busy part of the plot.** Wildcard's mean (2.74) sits close to
   Desperate's (2.60), so both curves live in the already-tight left third where Desperate,
   Faithful and Distinct all peak. Worse, Wildcard's peak density is **0.319** — within 0.001 of
   Faithful's 0.320 — so height cannot separate their direct labels, and the fifth label has to
   be pushed a long way off its own peak to fit at all.
2. **What it adds is a second-order read.** The Wildcard/Desperate difference is real — same low
   centre, sd 1.24 vs 1.01, peak density 0.319 vs 0.449, max 7.5 vs 6.5, i.e. a flatter curve
   running a level further right. But that is a *shape* comparison between two overlapping
   curves, and this image has about one second to land on a store page.

| | Desperate | Wildcard | difference |
|---|---|---|---|
| mean of per-pawn means | 2.60 | 2.74 | +0.14 |
| sd | 1.01 | 1.24 | **+23%** |
| peak density | 0.449 | 0.319 | **flatter** |
| best pawn seen in 1000 | 6.5 | 7.5 | **+1 level** |

Desperate makes colonists who are reliably *poor*; Wildcard makes colonists who are
*unpredictable*. That distinction is worth making somewhere — just not as a fifth line here.

And what most separates Wildcard from everything else is not on this axis at all: *passion
budget* and *trait count* (pips max 12.0, traits 0–7). If Wildcard ever gets its own image, those
are its axes.

### Showcase (custom profile), per-pawn mean skill — NOT A PRESET, NOT ON THE GRAPHIC

Built 2026-08-12 for listing image 2 only. It is a **custom profile**, so it is exempt from Rule 1
and appears in no envelope table; its values and the caption constraint that comes with it are in
`STATUS.md`. One run, resolved `Showcase x1000 (100.0%)`, model delta +0.077 vs 0.361 tolerance.

| run | mean | sd | min | p10 | median | p90 | max |
|---|---|---|---|---|---|---|---|
| A | 4.66 | 1.79 | 0.3 | 2.3 | 4.6 | 7.1 | 10.4 |

```
0.25  12     3.64 176     7.03  59
1.10  49     4.49 185     7.88  32
1.94  89     5.33 136     8.72  11
2.79 151     6.18  96     9.57   4
```

Per-skill sd 3.99, passion pips mean 5.03 (sd 2.73, max 15.0), traits/pawn mean 3.22 over a 1–6
range. The widest per-pawn spread measured on any profile, preset or custom, with the median at
4.6 and **not** censored against the skill floor.

**Why it is not on the graphic.** The graphic compares *shipped presets*, which is what a buyer
can actually select. Adding a hand-built profile would inflate the apparent range of the product.
This reason stands on its own — a free colour slot is available (see the sweep in the Wildcard
section), so colour is not what keeps Showcase off the chart. Honesty about what the dropdown
gives you is.

## The capable-only re-measure — 2026-08-12, and this is what the graphic plots

Eight fresh 1000-pawn runs, two per profile, against `PawnVarianceMod.dll` built 2026-08-12 08:44.
That build adds a second reported series, `per-pawn mean skill (capable)`, alongside the original
`per-pawn mean skill`; it **adds** rather than replaces, so every figure in the sections above
stays reproducible and comparable.

### Why the old axis was misleading

`DebugActions` sums `r.GetLevel(includeAptitudes: false)` over all twelve skills and divides by
twelve. **`SkillRecord.GetLevel` returns 0 for a `TotallyDisabled` skill** — verified against the
shipped `Assembly-CSharp.dll` rather than assumed: the first IL instruction of `GetLevel` is a call
to `get_TotallyDisabled`. So a colonist Incapable of Violent contributed two structural zeros to
its own average.

That is not something this mod does. Incapability comes from backstories, which nothing here
touches, and it lands at a near-constant rate across profiles:

| profile | skills disabled/pawn (A, B) | pawns with none disabled |
|---|---|---|
| Desperate | 1.11, 1.17 | 48%, 47% |
| Faithful | 1.15, 1.16 | 47%, 49% |
| Distinct | 1.14, 1.12 | 48%, 48% |
| Sovereign | 1.18, 1.07 | 46%, 49% |

So the all-12 axis ran about **12% low** for every profile equally. The *comparison* was never
wrong — but the absolute number was, and it was the number a reader scores against RimWorld's
0–20 skill bar. "Sovereign averages 5" reads as a mediocre preset; 5.80 on the skills that
count does not.

### The runs

Every run reports its own `ACTUALLY RESOLVED TO: <profile> x1000 (100.0%)` with no override
warning. **Faithful is a real `preset_faithful` pin this time, not the `Custom 1` stand-in** the
earlier data had to settle for — pinning `activeProfileId` and restarting the game worked on all
four profiles.

| profile | run | all-12 mean/sd | capable mean/sd | capable p10 – p90 | capable max |
|---|---|---|---|---|---|
| Desperate | A | 2.62 / 0.97 | 2.94 / 1.14 | 1.5 – 4.4 | 7.2 |
| Desperate | B | 2.66 / 1.01 | 3.02 / 1.23 | 1.5 – 4.6 | 9.6 |
| Faithful | A | 3.31 / 1.16 | 3.70 / 1.32 | 2.0 – 5.5 | 8.0 |
| Faithful | B | 3.35 / 1.17 | 3.78 / 1.38 | 2.1 – 5.4 | 9.6 |
| Distinct | A | 3.37 / 1.51 | 3.79 / 1.72 | 1.8 – 6.2 | 10.6 |
| Distinct | B | 3.31 / 1.48 | 3.70 / 1.67 | 1.8 – 6.0 | 9.8 |
| Sovereign | A | 5.18 / 1.09 | 5.80 / 1.20 | 4.3 – 7.4 | 10.3 |
| Sovereign | B | 5.24 / 1.12 | 5.80 / 1.22 | 4.3 – 7.3 | 10.6 |

Model deltas all OK: Desperate −0.014 / +0.034 (tol 0.150), Faithful −0.027 / −0.027 (0.156),
Distinct +0.124 / −0.088 (0.206), Sovereign −0.010 / +0.000 (0.161).

Capable-only histograms, bin lower edge → count. **`make-distribution.ps1` plots run A of
Desperate, Faithful and Distinct, and run B of Sovereign** — see the note below.

```
Desperate A          Faithful A           Distinct A           Sovereign B
  0.25  17             0.50  14             0.25  35             2.60  18
  0.83  60             1.13  48             1.11 109             3.26  37
  1.41 117             1.75  91             1.98 180             3.93 100
  1.99 187             2.38 151             2.84 197             4.59 180
  2.57 220             3.00 202             3.70 187             5.26 235
  3.15 177             3.63 158             4.56 123             5.92 177
  3.73 111             4.25 139             5.43  76             6.59 141
  4.30  55             4.88  94             6.29  56             7.25  64
  4.88  30             5.50  59             7.15  23             7.91  25
  5.46  16             6.13  27             8.01   5             8.58  15
  6.04   7             6.75  13             8.88   5             9.24   6
  6.62   3             7.38   4             9.74   4             9.91   2
```

The other four (Desperate C, Faithful B, Distinct B, Sovereign A) are in
`zzz-Do-Not-Commit/capable-shoot/raw-runs.md` with the full dumps.

### Why Sovereign is plotted from run B while the rest are run A

Sovereign run A's three top bins are **217 / 212 / 215** — gaps of 3 and 5 against a Poisson SE of
about 15, which is one flat top sampled three times. Splined, that draws a visible **dent**, and a
reader sees a bimodal Sovereign: *this preset makes two kinds of colonist*. It does not. Run B is
the same distribution (5.80 sd 1.22 against 5.80 sd 1.20) sampled so that it descends
monotonically from a single peak.

This is a choice between two equally valid 1000-pawn samples, **not smoothing** — no count was
altered. If Sovereign is ever re-measured, check its top bins for the same near-tie before
picking a run.

### What the correction did to the two claims — nothing, which is the point

| | Faithful | Distinct | change |
|---|---|---|---|
| mean of capable means | 3.74 | 3.745 | +0.005 (noise) |
| sd | 1.35 | 1.695 | **+26%** |
| p10 – p90 band width | 3.4 | 4.3 | **+26%** |

Both headline numbers come out identical to the all-12 measurement. The correction moved every
curve right; it did not move the comparison between them. Sovereign against Faithful widens
slightly, from +1.8 levels to **+2.1**.

### Method note, worth knowing before repeating this

`click_ui_target` on the `1000 pawns` row **always** returns
`Timed out waiting for main-thread work after 5000ms`, and **the run completes anyway**. The click
lands, the generation then holds the main thread for 20–25 seconds, and the bridge's 5-second
response wait expires in the meantime. It is a false negative: acknowledge the attention item and
read the result out of `rimbridge/list_logs`. Three good runs were nearly thrown away on this
before the log was checked. OS focus is irrelevant — runs completed with RimWorld in the
background, so do not go chasing window focus as the earlier notes might suggest.

## The claim the graphic can honestly make

With four profiles on the chart there are **two** claims, and they must not be blurred together.

*The figures in this section are the original all-12 ones. Both claims were re-checked against the
capable-only re-measure above and both survive with the same numbers; the tables are left as they
were because they are what the surrounding argument was written against.*

**1. Faithful vs Distinct — the colony average does not move; the individuals spread out.**

| | Faithful-equiv | Distinct | change |
|---|---|---|---|
| mean of per-pawn means | 3.36 | 3.33 | −0.03 (noise) |
| sd | 1.18 | 1.49 | **+26%** |
| p10 – p90 band width | 3.0 | 3.75 | **+25%** |
| best pawn seen in 1000 | 7.8 | 10.3 | **+2.5 levels** |

**2. Desperate vs Sovereign — these deliberately DO move the average.** That is their identity,
not a side effect: Desperate's `averageQuality` is 0.37 and Sovereign's 0.55, and Sovereign's
`skillShiftMin` is pinned at 0 specifically to hold its band at or above the vanilla baseline.

| profile | mean of per-pawn means (A, B) | sd (A, B) |
|---|---|---|
| Desperate | 2.60, 2.66 | 1.01, 0.99 |
| Faithful-equiv | 3.34, 3.38 | 1.20, 1.16 |
| Distinct | 3.36, 3.29 | 1.47, 1.50 |
| Sovereign | 5.18, 5.15 | 1.11, 1.09 |
| Wildcard (not plotted) | 2.74, 2.78 | 1.24, 1.22 |

**So the four-curve graphic must NOT say "same colony average."** That headline is true of the
Faithful/Distinct pair only, and putting it over a chart that also shows Sovereign 1.8 levels
higher would be a false claim about the mod. The honest four-curve headline is about *choosing*
which kind of colonist the colony gets. The old two-curve headline is preserved in the git
history of `tools/make-distribution.ps1` if the pair-only framing is ever wanted again.

That is the mod's actual pitch, and it is the honest reading of these eight runs. Note the mean
being flat is **expected and load-bearing** — Distinct's `averageQuality` is 0.32, below
Faithful's 0.50, which is what buys the extra spread without buying extra power. It is also why
Distinct sits *below* Faithful at N=1 (−8.7%) and above it by N=25 (+9.5%) in
`envelope_check.py`.

## Provenance

- Each run's `GENERATOR vs MODEL` block passed: Distinct delta −0.015 and −0.082 against a
  0.206 tolerance; the Faithful-equivalent runs −0.016 and +0.062 against 0.156; Sovereign
  −0.075 and −0.067 against 0.161; Desperate −0.024 and +0.012 against 0.150. So the pawns
  behind these histograms are pawns the scoring model demonstrably describes.
- The Distinct runs report `ACTUALLY RESOLVED TO: Distinct x1000 (100.0%)` with no override
  warning. The Faithful-equivalent runs report `Custom 1 x1000` **with** the
  `^^ NOT the configured active profile` warning, because at that point `activeProfileId` had
  been re-pointed but `RefreshResolved()` had not re-run — the figures are `Custom 1`'s either
  way, which is what the table above claims them to be.
