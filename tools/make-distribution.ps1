<#
Generates docs\workshop\distribution.png -- Steam Workshop listing image 4.

Deterministic: no randomness, so re-running produces a byte-comparable image.

The numbers below are MEASURED, not modelled: eight 1000-pawn runs of the in-game
"Roll pawns and dump distribution" debug action -- two per profile -- recorded
verbatim in docs\workshop\distribution-data.md. Change them there and here together.

WHAT THIS IMAGE IS ALLOWED TO CLAIM. It used to show two curves under the headline
"Same colony average. Different colonists." That headline is TRUE OF THE
FAITHFUL/DISTINCT PAIR ONLY -- those two land on the same mean (3.36 vs 3.33) with
+26% spread. Sovereign and Desperate deliberately MOVE the mean (5.18 and 2.60),
because that is their identity. So the four-curve chart cannot say "same average"
without lying about the mod. It says the honest thing instead: the preset decides
what kind of colonist arrives. Do not restore the old headline while Sovereign or
Desperate is on the chart.

WHY WILDCARD IS NOT HERE, THOUGH IT IS A SHIPPED PRESET. Owner's call, 2026-08-12:
the four-curve chart reads better than the five-curve one. It was plotted as a fifth
violet curve and then removed -- so if you are considering adding it, know that this
has been tried and reverted, and that the reason is composition, not feasibility.
Wildcard's mean (2.74) sits close to Desperate's (2.60), so the two curves share the
crowded left of the plot, and the four peaks there are already tight enough that the
fifth label had to be pushed a long way off its own peak to fit. What separates
Wildcard from Desperate -- sd 1.24 vs 1.01, a flatter curve running a level further
right -- is real but is a second-order read on an image that has about one second to
land on a store page. The measured Wildcard numbers are kept in
docs\workshop\distribution-data.md, and the removed series is in this file's git
history if it is ever wanted back.

WHAT THE X AXIS IS, AND WHY IT CHANGED ON 2026-08-12. It is the average of a
colonist's skills counting ONLY the skills that colonist can actually use. It used
to be the average over all twelve, and that was misleading in a way worth recording
so it is not undone: RimWorld's SkillRecord.GetLevel returns 0 for a TotallyDisabled
skill (verified against the shipped assembly -- the first IL instruction of GetLevel
is a call to get_TotallyDisabled). So a pawn Incapable of Violent contributed two
structural zeros to its own average, through no decision of this mod's, because
incapability comes from backstories and nothing here touches them. Measured across
all four profiles that cost 1.07-1.18 skills of 12 per pawn, so every curve sat
about 12% left of where a player reading the pawn's own skill bars would put it --
and a reader benchmarking "Sovereign averages 5" against the 0-20 skill bar would
conclude the preset was mediocre when it delivers 5.8 on the skills that count.

The comparison was never wrong -- the bias is near enough constant across profiles,
and both published claims survived the re-measure unchanged: Faithful vs Distinct is
still a flat mean (3.74 vs 3.745) with +26% sd and +26% p10-p90 band. What changed is
that the numbers now mean what the axis says. Do not revert to the all-12 series; the
debug action still prints both, and docs\workshop\distribution-data.md records both.

TWO THINGS THIS SCRIPT GETS RIGHT AND A NAIVE PLOT GETS WRONG:

 1. The runs have DIFFERENT BIN WIDTHS -- the debug action bins each sample into 12
    bins spanning that sample's own min..max, so Distinct's bins are ~57% wider than
    Desperate's. Plotting raw counts against each other would make the wide-binned
    profiles look commoner everywhere purely because their buckets are bigger. So
    counts are converted to DENSITY (share of colonists per skill level,
    count / (n * binWidth)), which is comparable across bin widths and is what
    makes the four shapes honestly overlayable.

 2. The printed value is the bin's LOWER EDGE (DebugActions.cs:1389,
    `edge = lo + (hi-lo)*b/bins`), not its centre. Curves are drawn through bin
    CENTRES, edge + width/2, or every curve would sit half a bin to the left.
#>
param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop\distribution.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$W = 1024
$H = 576

function C([int]$a,[int]$r,[int]$g2,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g2,$b) }

# --- Palette ------------------------------------------------------------------
# Four categorical hues, validated TOGETHER against this chart's dark surface
# (#1B1C20) with the dataviz skill's validator, under --pairs all (not merely
# adjacent -- all four curves are on screen at once and any pair can be compared):
#
#   node scripts/validate_palette.js "#C08628,#3E96DE,#D55181,#008300" \
#        --mode dark --surface "#1B1C20" --pairs all
#   [PASS] lightness band  [PASS] chroma floor  [PASS] normal-vision dE 18.6
#   [WARN] CVD worst pair #008300 <-> #C08628 dE 6.6 (protan)
#   [PASS] contrast vs surface          -> ALL CHECKS PASS
#
# A FIFTH SLOT IS AVAILABLE IF ONE IS EVER NEEDED -- this palette is nowhere near a
# cap. An earlier note here claimed no fifth hue could pass; that was wrong, and was
# based on four hand-picked candidates (#9085E9, #8E7CC3, #4FB3A0, #C98500) which do
# all genuinely fail. A full sRGB sweep at step 4 against the same gate returns
# 15,387 passing fifth hues; the best is violet #8F3FD4, which never becomes the
# worst pair on any check (its own nearest neighbours are dE 26.7 normal vs blue and
# 16.6 CVD vs green/tritan). So if a fifth series is ever wanted here, SWEEP, do not
# guess -- guessing is what produced the wrong conclusion the first time. Colour is
# not what keeps Wildcard off this chart; see the header note.
#
# The CVD warn sits in the 6-8 floor band, which the skill permits ONLY with
# secondary encoding. That is why every curve carries a DIRECT LABEL at its peak
# (see the label pass below) -- the labels are load-bearing accessibility, not
# decoration. Do not remove them, and do not "brighten" any hue without re-running
# the validator: the earlier, prettier amber (216,166,86) failed the lightness band.
$amberR   = 192; $amberG   = 134; $amberB   =  40   # #C08628  Distinct
$blueR    =  62; $blueG    = 150; $blueB    = 222   # #3E96DE  Faithful
$roseR    = 213; $roseG    =  81; $roseB    = 129   # #D55181  Desperate
$greenR   =   0; $greenG   = 131; $greenB   =   0   # #008300  Sovereign

$inkPrimary   = C 255 236 238 242
$inkSecondary = C 255 176 182 190
$inkMuted     = C 255 124 130 138

# --- Measured data ------------------------------------------------------------
# Run A of each profile, from the `per-pawn mean skill (capable)` histogram --
# NOT the `per-pawn mean skill` one printed directly above it in the same dump.
# See the axis note in the header for why. Bin lower edges and counts exactly as
# the debug action printed them; distribution-data.md has run B of each, which
# agrees, and both series for all eight runs.
$desperate = [ordered]@{
    label  = 'Desperate'
    edges  = @(0.25,0.83,1.41,1.99,2.57,3.15,3.73,4.30,4.88,5.46,6.04,6.62)
    counts = @(17,60,117,187,220,177,111,55,30,16,7,3)
    rgb    = @($roseR, $roseG, $roseB)
    fillA  = 32
    # Label offsets in PIXELS from the curve's own peak. Tuned by rendering and
    # looking at the result -- the four peaks cluster between x 2.4 and 3.1, so
    # peak-anchored labels with no offset collide with each other and with the
    # neighbouring curve's line. Re-check these if the data is re-measured.
    labelDx = -50.0
    labelDy = -32.0
}
$faithful = [ordered]@{
    label  = 'Faithful'
    edges  = @(0.50,1.13,1.75,2.38,3.00,3.63,4.25,4.88,5.50,6.13,6.75,7.38)
    counts = @(14,48,91,151,202,158,139,94,59,27,13,4)
    rgb    = @($blueR, $blueG, $blueB)
    fillA  = 32
    labelDx = 58.0
    labelDy = -18.0
}
$distinct = [ordered]@{
    label  = 'Distinct'
    edges  = @(0.25,1.11,1.98,2.84,3.70,4.56,5.43,6.29,7.15,8.01,8.88,9.74)
    counts = @(35,109,180,197,187,123,76,56,23,5,5,4)
    rgb    = @($amberR, $amberG, $amberB)
    fillA  = 96      # hero series: the mod's signature tuning, so it carries the weight
    labelDx = -76.0
    labelDy = -20.0
}
$sovereign = [ordered]@{
    label  = 'Sovereign'
    # SOVEREIGN IS THE ONE SERIES PLOTTED FROM RUN B, AND THAT IS DELIBERATE.
    # Run A's three top bins are 217 / 212 / 215 -- gaps of 3 and 5 against a
    # Poisson SE of ~15, i.e. one flat top sampled three times. The spline through
    # them draws a visible DENT, and a reader sees a bimodal Sovereign: "this preset
    # makes two kinds of colonist." It does not. Run B samples the same distribution
    # (mean 5.80 sd 1.22 vs 5.80 sd 1.20 -- indistinguishable) and descends
    # monotonically from a single peak, so it draws the shape the data actually has.
    # This is a choice between two equally valid 1000-pawn samples, not smoothing:
    # no count below has been touched. If Sovereign is ever re-measured, check the
    # top bins for the same near-tie before picking a run.
    edges  = @(2.60,3.26,3.93,4.59,5.26,5.92,6.59,7.25,7.91,8.58,9.24,9.91)
    counts = @(18,37,100,180,235,177,141,64,25,15,6,2)
    rgb    = @($greenR, $greenG, $greenB)
    fillA  = 40
    labelDx = 40.0
    labelDy = -40.0
}
# Drawn back-to-front. The hero's fill goes down first and the three reference
# profiles are laid over it outline-led, so no two translucent fills of similar
# weight stack into mud.
$series = @($distinct, $desperate, $faithful, $sovereign)

$N = 1000.0

# Bin width from the printed edges: 11 gaps between 12 lower edges.
foreach ($s in $series) {
    $e = $s.edges
    $s.width = ($e[$e.Count - 1] - $e[0]) / ($e.Count - 1)
    if ($s.counts.Count -ne $e.Count) { throw "counts/edges length mismatch for $($s.label)" }
    $sum = ($s.counts | Measure-Object -Sum).Sum
    if ($sum -ne 1000) { throw "counts for $($s.label) sum to $sum, expected 1000" }
    $s.density = @($s.counts | ForEach-Object { $_ / ($N * $s.width) })
    $s.centres = @($e | ForEach-Object { $_ + $s.width / 2.0 })
}

# --- Plot geometry ------------------------------------------------------------
$plotL = 76.0; $plotR = $W - 44.0
# plotT was 172 when the headline was 32pt. The headline is now 23pt and recessive, so
# the curves take the reclaimed space -- on a store page the shapes are the product and
# the sentence is the caption, not the other way round.
$plotT = 146.0; $plotB = $H - 92.0
# xMax was 10.6 for the all-12 data. The capable-only series run further right --
# Distinct's closing baseline anchor alone lands at 10.60 -- so it would have sat
# exactly on the plot's right edge. 11.0 gives the tails somewhere to land.
$xMin  = 0.0;  $xMax = 11.0
$yMax  = 0.0
foreach ($s in $series) { foreach ($d in $s.density) { if ($d -gt $yMax) { $yMax = $d } } }
$yMax = $yMax * 1.20   # headroom for the direct labels sitting above each peak

function PX([double]$v) { $plotL + ($v - $xMin) / ($xMax - $xMin) * ($plotR - $plotL) }
function PY([double]$v) { $plotB - ($v / $yMax) * ($plotB - $plotT) }

$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias

# --- Background ---------------------------------------------------------------
# Matches About\Preview.png so the listing strip reads as one set.
$bgRect  = New-Object System.Drawing.Rectangle(0,0,$W,$H)
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $bgRect, (C 255 34 36 40), (C 255 16 17 20), 90.0)
$g.FillRectangle($bgBrush, $bgRect)
$bgBrush.Dispose()

# TYPE IS DELIBERATELY RECESSIVE HERE. The headline used to be 32pt in primary ink and
# it read as the loudest thing on the image; the curves are the argument, so the text
# was cut to 23pt in SECONDARY ink to sit behind them. The series labels did NOT shrink
# with it -- at 13pt bold they are still the brightest text on the chart, because they
# are the secondary encoding the palette's 6.6 CVD pair depends on (see the palette
# note) and are part of the picture rather than commentary on it. Do not "rebalance"
# the headline back up to match them.
$fTitle  = New-Object System.Drawing.Font('Segoe UI', 23, [System.Drawing.FontStyle]::Bold)
$fLabel  = New-Object System.Drawing.Font('Segoe UI', 13, [System.Drawing.FontStyle]::Bold)
$fAxis   = New-Object System.Drawing.Font('Segoe UI', 11)
$fNote   = New-Object System.Drawing.Font('Segoe UI', 9)

$brInk    = New-Object System.Drawing.SolidBrush($inkPrimary)
$brSecond = New-Object System.Drawing.SolidBrush($inkSecondary)
$brMuted  = New-Object System.Drawing.SolidBrush($inkMuted)

# --- Headline -----------------------------------------------------------------
# One line, no terminal full stop -- owner's call, applied across the listing set:
# these are labels naming what the image is, not sentences, and the stop makes them
# read as prose that wants finishing. The lead paragraph and the per-series sd
# callouts that used to live here were cut on purpose: on a store page the shapes
# and the four names are the message, and everything else was reading as a wall of
# text at thumbnail size.
$g.DrawString('The preset decides who shows up', $fTitle, $brSecond, 58.0, 62.0)

# --- Grid: horizontal rules only, recessive -----------------------------------
$penGrid = New-Object System.Drawing.Pen((C 24 255 255 255), 1.0)
for ($i = 1; $i -le 4; $i++) {
    $y = $plotB - ($plotB - $plotT) * $i / 4.0
    $g.DrawLine($penGrid, [single]$plotL, [single]$y, [single]$plotR, [single]$y)
}
$penGrid.Dispose()

# --- Series: filled density curves --------------------------------------------
foreach ($s in $series) {
    $pts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
    # Anchor the curve to the baseline half a bin outside the first/last centre,
    # so the shape closes where the data actually stops.
    $pts.Add((New-Object System.Drawing.PointF([single](PX ($s.centres[0] - $s.width/2.0)), [single](PY 0))))
    for ($i = 0; $i -lt $s.centres.Count; $i++) {
        $pts.Add((New-Object System.Drawing.PointF([single](PX $s.centres[$i]), [single](PY $s.density[$i]))))
    }
    $last = $s.centres.Count - 1
    $pts.Add((New-Object System.Drawing.PointF([single](PX ($s.centres[$last] + $s.width/2.0)), [single](PY 0))))

    $fill = New-Object System.Drawing.Drawing2D.GraphicsPath
    $fill.AddCurve($pts.ToArray(), 0.5)
    $fill.CloseFigure()
    $brFill = New-Object System.Drawing.SolidBrush((C $s.fillA $s.rgb[0] $s.rgb[1] $s.rgb[2]))
    $g.FillPath($brFill, $fill)
    $brFill.Dispose(); $fill.Dispose()

    $penLine = New-Object System.Drawing.Pen((C 255 $s.rgb[0] $s.rgb[1] $s.rgb[2]), 2.6)
    $g.DrawCurve($penLine, $pts.ToArray(), 0.5)
    $penLine.Dispose()

    # Remember the peak so the label pass can sit on it, and the point list so the
    # hero's outline can be redrawn on top.
    $peak = 0; for ($i = 1; $i -lt $s.density.Count; $i++) { if ($s.density[$i] -gt $s.density[$peak]) { $peak = $i } }
    $s.peakX = $s.centres[$peak]
    $s.peakY = $s.density[$peak]
    $s.points = $pts.ToArray()
}

# Distinct goes down FIRST so its fill does not tint the other three, but that
# also buries its outline under three later fills -- and Distinct is the preset
# the listing is selling, so it cannot be the faintest line on the chart. Redraw
# its outline last, slightly heavier. Fill order and line order are deliberately
# different; do not "simplify" this by moving Distinct to the end of $series.
$penHero = New-Object System.Drawing.Pen((C 255 $distinct.rgb[0] $distinct.rgb[1] $distinct.rgb[2]), 3.4)
$g.DrawCurve($penHero, $distinct.points, 0.5)
$penHero.Dispose()

# --- Direct labels ------------------------------------------------------------
# Identity carried by name, not by colour alone -- this is the secondary encoding
# the palette's 6.6 CVD pair is conditional on (see the palette note above), so a
# legend box would not substitute for it. No legend is drawn: with four curves the
# box would repeat these same four words and put the reading work back on colour
# matching. The four peaks are far enough apart in HEIGHT (density 0.449 Desperate,
# 0.379 Sovereign, 0.320 Faithful, 0.246 Distinct) that stacked x-positions still
# resolve; re-check this if the data is re-measured. (A fifth curve does NOT fit
# this label scheme without pushing one name well off its own peak -- Wildcard's
# peak height is 0.319, within 0.001 of Faithful's. That was one of the reasons it
# came back off the chart.)
$fmtC = New-Object System.Drawing.StringFormat
$fmtC.Alignment = [System.Drawing.StringAlignment]::Center
foreach ($s in $series) {
    $lx = (PX $s.peakX) + $s.labelDx
    $ly = (PY $s.peakY) + $s.labelDy
    # A small swatch left of the name ties the word to its curve without painting
    # the text itself in a series colour.
    $sz = $g.MeasureString($s.label, $fLabel)
    $brSw = New-Object System.Drawing.SolidBrush((C 255 $s.rgb[0] $s.rgb[1] $s.rgb[2]))
    $g.FillRectangle($brSw, [single]($lx - $sz.Width/2.0 - 18.0), [single]($ly + 7.0), 11.0, 11.0)
    $brSw.Dispose()
    $g.DrawString($s.label, $fLabel, $brInk, [single]($lx + 7.0), [single]$ly, $fmtC)
}

# --- Axes ---------------------------------------------------------------------
$penAxis = New-Object System.Drawing.Pen((C 90 255 255 255), 1.5)
$g.DrawLine($penAxis, [single]$plotL, [single]$plotB, [single]$plotR, [single]$plotB)
$penAxis.Dispose()

for ($t = 0; $t -le 10; $t += 2) {
    $g.DrawString("$t", $fAxis, $brMuted, [single](PX $t), [single]($plotB + 8), $fmtC)
}
# NOT "average skill level of one colonist", which this said until 2026-08-12. Two
# separate misreads, both reported from a cold look at the image: the phrase "skill
# level" invites the reader to score the number against RimWorld's 0-20 skill bar,
# and the far right of the axis then reads as "the best skill Sovereign can produce
# is a 10" -- when a pawn out there is one whose WHOLE SKILL SET averages 10. Naming
# the aggregation instead of the unit closes both. Keep the word "average" first and
# do not put "level" back in.
$g.DrawString("average across a colonist's usable skills",
    $fAxis, $brSecond, [single](($plotL + $plotR) / 2.0), [single]($plotB + 34), $fmtC)

# y axis carries no numbers on purpose: the readable quantity here is the SHAPE,
# and a density-per-skill-level tick would be noise on a store page. The axis
# label says what taller means.
$state = $g.Save()
$g.TranslateTransform(34.0, [single](($plotT + $plotB) / 2.0))
$g.RotateTransform(-90.0)
$g.DrawString('how common', $fAxis, $brMuted, 0.0, 0.0, $fmtC)
$g.Restore($state)

# --- Footer -------------------------------------------------------------------
# The one line of prose that earns its place: it is what separates this from a
# graphic somebody drew in a paint program.
# The provenance line now also carries the one caveat the axis cannot: a colonist's
# incapable skills are excluded rather than counted as zeros. Four words, in the
# muted line nobody reads first, which is the right place for a footnote that only
# matters to someone who would otherwise try to reproduce the number.
$g.DrawString('1,000 colonists rolled per preset in game, incapable skills not counted',
    $fNote, $brMuted, 58.0, [single]($H - 34))

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($o in @($fTitle,$fLabel,$fAxis,$fNote,$brInk,$brSecond,$brMuted,$g,$bmp)) { $o.Dispose() }

Write-Host "Wrote $OutPath"
