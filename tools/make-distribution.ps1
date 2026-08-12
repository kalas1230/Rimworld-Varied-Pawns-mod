<#
Generates docs\workshop\distribution.png -- Steam Workshop listing image 4.

Deterministic: no randomness, so re-running produces a byte-comparable image.

The numbers below are MEASURED, not modelled: two 1000-pawn runs of the in-game
"Roll pawns and dump distribution" debug action, recorded verbatim in
docs\workshop\distribution-data.md. Change them there and here together.

TWO THINGS THIS SCRIPT GETS RIGHT AND A NAIVE PLOT GETS WRONG:

 1. The two runs have DIFFERENT BIN WIDTHS -- the debug action bins each sample
    into 12 bins spanning that sample's own min..max, so Distinct's bins are ~37%
    wider than Faithful's. Plotting raw counts against each other would make
    Distinct look commoner everywhere purely because its buckets are bigger. So
    counts are converted to DENSITY (share of colonists per skill level,
    count / (n * binWidth)), which is comparable across bin widths and is what
    makes the two shapes honestly overlayable.

 2. The printed value is the bin's LOWER EDGE (DebugActions.cs:1389,
    `edge = lo + (hi-lo)*b/bins`), not its centre. Curves are drawn through bin
    CENTRES, edge + width/2, or both would sit half a bin to the left.
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
# Amber and blue, validated together against the dark chart surface with the
# dataviz skill's validator: lightness band, chroma floor, CVD separation
# (worst adjacent dE 24.3 protan / 22.7 tritan), normal-vision separation and
# contrast all PASS. Do not "brighten" either one without re-running it -- the
# earlier, prettier amber (216,166,86) failed the lightness band.
$amberR = 192; $amberG = 134; $amberB = 40      # #C08628  Distinct
$blueR  =  62; $blueG  = 150; $blueB  = 222     # #3E96DE  Faithful

$inkPrimary   = C 255 236 238 242
$inkSecondary = C 255 176 182 190
$inkMuted     = C 255 124 130 138

# --- Measured data ------------------------------------------------------------
# Run A of each profile. Bin lower edges and counts exactly as the debug action
# printed them; see distribution-data.md for run B, which agrees.
$faithful = [ordered]@{
    label  = 'Faithful  -- closest to unmodded'
    edges  = @(0.17,0.78,1.40,2.02,2.64,3.26,3.88,4.49,5.11,5.73,6.35,6.97)
    counts = @(11,28,98,161,198,187,145,108,32,20,10,2)
    sd     = 1.20
    rgb    = @($blueR, $blueG, $blueB)
    fillA  = 34      # reference series: outline-led, so the overlap stays readable
    z      = 1
}
$distinct = [ordered]@{
    label  = 'Distinct  -- the mod''s signature tuning'
    edges  = @(0.08,0.93,1.78,2.63,3.47,4.32,5.17,6.01,6.86,7.71,8.56,9.40)
    counts = @(26,112,207,208,195,133,67,39,7,4,1,1)
    sd     = 1.47
    rgb    = @($amberR, $amberG, $amberB)
    fillA  = 104     # hero series: the shape the listing is selling
    z      = 0
}
# Drawn back-to-front. Two translucent fills of similar weight mix into mud where
# they overlap, so the hero is filled, the reference is drawn outline-led on top.
$series = @($distinct, $faithful)

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
$plotL = 92.0; $plotR = $W - 56.0
$plotT = 214.0; $plotB = $H - 96.0
$xMin  = 0.0;  $xMax = 10.6
$yMax  = 0.0
foreach ($s in $series) { foreach ($d in $s.density) { if ($d -gt $yMax) { $yMax = $d } } }
$yMax = $yMax * 1.18

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

$fTitle  = New-Object System.Drawing.Font('Segoe UI', 34, [System.Drawing.FontStyle]::Bold)
$fLead   = New-Object System.Drawing.Font('Segoe UI', 15)
$fLegend = New-Object System.Drawing.Font('Segoe UI', 14, [System.Drawing.FontStyle]::Bold)
$fAxis   = New-Object System.Drawing.Font('Segoe UI', 12)
$fNote   = New-Object System.Drawing.Font('Segoe UI', 11)

$brInk    = New-Object System.Drawing.SolidBrush($inkPrimary)
$brSecond = New-Object System.Drawing.SolidBrush($inkSecondary)
$brMuted  = New-Object System.Drawing.SolidBrush($inkMuted)

# --- Headline -----------------------------------------------------------------
$g.DrawString('Same colony average. Different colonists.', $fTitle, $brInk, 60.0, 52.0)
$g.DrawString('Every colonist rolls one hidden quality value. Distinct spreads that roll wider, so the',
    $fLead, $brSecond, 62.0, 112.0)
$g.DrawString('colony still averages the same -- but the individuals in it stop being interchangeable.',
    $fLead, $brSecond, 62.0, 136.0)

# --- Grid: horizontal rules only, recessive -----------------------------------
$penGrid = New-Object System.Drawing.Pen((C 26 255 255 255), 1.0)
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

    $penLine = New-Object System.Drawing.Pen((C 255 $s.rgb[0] $s.rgb[1] $s.rgb[2]), 2.5)
    $g.DrawCurve($penLine, $pts.ToArray(), 0.5)
    $penLine.Dispose()
}

# --- Axes ---------------------------------------------------------------------
$penAxis = New-Object System.Drawing.Pen((C 90 255 255 255), 1.5)
$g.DrawLine($penAxis, [single]$plotL, [single]$plotB, [single]$plotR, [single]$plotB)
$penAxis.Dispose()

$fmtC = New-Object System.Drawing.StringFormat
$fmtC.Alignment = [System.Drawing.StringAlignment]::Center
for ($t = 0; $t -le 10; $t += 2) {
    $g.DrawString("$t", $fAxis, $brMuted, [single](PX $t), [single]($plotB + 8), $fmtC)
}
$g.DrawString('average skill level of one colonist  (mean of their 12 skills)',
    $fAxis, $brMuted, [single](($plotL + $plotR) / 2.0), [single]($plotB + 34), $fmtC)

# y axis carries no numbers on purpose: the readable quantity here is the SHAPE,
# and a density-per-skill-level tick would be noise on a store page. The axis
# label says what taller means.
$state = $g.Save()
$g.TranslateTransform(38.0, [single](($plotT + $plotB) / 2.0))
$g.RotateTransform(-90.0)
$g.DrawString('how common', $fAxis, $brMuted, 0.0, 0.0, $fmtC)
$g.Restore($state)

# --- Legend + direct labels ---------------------------------------------------
# Both: a legend for identity, and the sd called out per series, so the claim is
# not carried by colour alone.
$legX = $plotR - 340.0
$legY = $plotT - 4.0
foreach ($s in $series) {
    $brSw = New-Object System.Drawing.SolidBrush((C 255 $s.rgb[0] $s.rgb[1] $s.rgb[2]))
    $g.FillRectangle($brSw, [single]$legX, [single]($legY + 4), 22.0, 12.0)
    $brSw.Dispose()
    $g.DrawString($s.label, $fLegend, $brInk, [single]($legX + 32), [single]$legY)
    $g.DrawString("spread: sd $([string]::Format('{0:F2}', $s.sd)) skill levels",
        $fNote, $brSecond, [single]($legX + 32), [single]($legY + 24))
    $legY += 62.0
}

# --- Footer -------------------------------------------------------------------
$g.DrawString('1,000 generated colonists per profile, rolled in game. Mean 3.36 vs 3.33 -- unchanged. Spread +26%.',
    $fNote, $brMuted, 62.0, [single]($H - 42))

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($o in @($fTitle,$fLead,$fLegend,$fAxis,$fNote,$brInk,$brSecond,$brMuted,$g,$bmp)) { $o.Dispose() }

Write-Host "Wrote $OutPath"
