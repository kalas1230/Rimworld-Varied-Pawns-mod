<#
Generates About\Preview.png -- the Steam Workshop thumbnail / mod-list preview.
Deterministic: no randomness, so re-running produces a byte-comparable image.
#>
param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'About\Preview.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$W = 1024
$H = 576

$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic

function C([int]$a,[int]$r,[int]$g2,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g2,$b) }

# --- Background: vertical gradient, dark neutral ------------------------------
$bgRect = New-Object System.Drawing.Rectangle(0,0,$W,$H)
$bgBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
    $bgRect, (C 255 34 36 40), (C 255 16 17 20), 90.0)
$g.FillRectangle($bgBrush, $bgRect)
$bgBrush.Dispose()

# Warm glow behind the title, so the type does not sit on flat black.
$glow = New-Object System.Drawing.Drawing2D.GraphicsPath
$glow.AddEllipse(-180, -320, $W + 360, 760)
$glowBrush = New-Object System.Drawing.Drawing2D.PathGradientBrush($glow)
$glowBrush.CenterColor = (C 46 216 166 86)
$glowBrush.SurroundColors = @((C 0 216 166 86))
$g.FillPath($glowBrush, $glow)
$glowBrush.Dispose(); $glow.Dispose()

# --- Distribution curve behind the pawn row ----------------------------------
# A wide, flat bell: the mod's whole premise is that the band should be wider.
$curvePts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
$mu = $W / 2.0
$sd = 250.0
$baseY = 512.0
$amp = 150.0
for ($x = 0; $x -le $W; $x += 8) {
    $y = $baseY - $amp * [math]::Exp( - (($x - $mu) * ($x - $mu)) / (2.0 * $sd * $sd) )
    $curvePts.Add((New-Object System.Drawing.PointF([single]$x, [single]$y)))
}
$curvePen = New-Object System.Drawing.Pen((C 54 216 166 86), 2.0)
$g.DrawCurve($curvePen, $curvePts.ToArray())
$curvePen.Dispose()

# Fill under the curve, very faint.
$fillPts = New-Object System.Collections.Generic.List[System.Drawing.PointF]
$fillPts.AddRange($curvePts)
$fillPts.Add((New-Object System.Drawing.PointF([single]$W, [single]$baseY)))
$fillPts.Add((New-Object System.Drawing.PointF(0.0, [single]$baseY)))
$fillBrush = New-Object System.Drawing.SolidBrush((C 18 216 166 86))
$g.FillPolygon($fillBrush, $fillPts.ToArray())
$fillBrush.Dispose()

# --- Pawn row -----------------------------------------------------------------
# Hand-picked, not random: heights and builds spread deliberately so the row
# reads as "these are not the same person" at thumbnail size.
$ground = 512.0
$pawns = @(
    @{ x =  92; h = 0.74; w = 0.86; col = @(122,134,150) }
    @{ x = 196; h = 1.05; w = 1.00; col = @( 90,125,155) }
    @{ x = 300; h = 0.88; w = 1.14; col = @(128,140, 90) }
    @{ x = 404; h = 1.22; w = 0.92; col = @(196,152, 82) }
    @{ x = 508; h = 0.68; w = 1.02; col = @(150, 92, 68) }
    @{ x = 612; h = 1.12; w = 1.10; col = @(120, 98,142) }
    @{ x = 716; h = 0.94; w = 0.88; col = @( 96,140,126) }
    @{ x = 820; h = 1.30; w = 1.04; col = @(176,116, 84) }
    @{ x = 924; h = 0.80; w = 0.96; col = @(108,116,132) }
)

function New-RoundedPath([single]$x,[single]$y,[single]$w,[single]$h,[single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x,             $y,             $d, $d, 180, 90)
    $p.AddArc($x + $w - $d,   $y,             $d, $d, 270, 90)
    $p.AddArc($x + $w - $d,   $y + $h - $d,   $d, $d,   0, 90)
    $p.AddArc($x,             $y + $h - $d,   $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

foreach ($p in $pawns) {
    $bodyH = [single](146.0 * $p.h)
    $bodyW = [single]( 52.0 * $p.w)
    $headR = [single]( 22.5 * $p.w)
    $cx    = [single]$p.x

    $bodyTop = [single]($ground - $bodyH)
    $headTop = [single]($bodyTop - $headR * 1.55)

    $c     = $p.col
    $main  = C 255 $c[0] $c[1] $c[2]
    $shade = C 255 ([int]($c[0]*0.72)) ([int]($c[1]*0.72)) ([int]($c[2]*0.72))

    # soft contact shadow
    $shadow = New-Object System.Drawing.SolidBrush((C 60 0 0 0))
    $g.FillEllipse($shadow, [single]($cx - $bodyW*0.85), [single]($ground - 7), [single]($bodyW*1.7), 14.0)
    $shadow.Dispose()

    # torso
    $torso = New-RoundedPath ([single]($cx - $bodyW/2)) $bodyTop $bodyW $bodyH ([single]($bodyW*0.36))
    $tb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(([single]($cx - $bodyW/2)), ([single]$bodyTop))),
        (New-Object System.Drawing.PointF(([single]($cx + $bodyW/2)), ([single]($bodyTop + $bodyH)))),
        $main, $shade)
    $g.FillPath($tb, $torso)
    $tb.Dispose(); $torso.Dispose()

    # head
    $hb = New-Object System.Drawing.SolidBrush($main)
    $g.FillEllipse($hb, [single]($cx - $headR), $headTop, [single]($headR*2), [single]($headR*2))
    $hb.Dispose()
}

# ground line
$groundPen = New-Object System.Drawing.Pen((C 70 232 226 213), 1.5)
$g.DrawLine($groundPen, 60.0, [single]$ground, [single]($W - 60), [single]$ground)
$groundPen.Dispose()

# --- Type ---------------------------------------------------------------------
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment     = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center

$titleFont = New-Object System.Drawing.Font('Segoe UI Black', 96, [System.Drawing.FontStyle]::Bold, [System.Drawing.GraphicsUnit]::Pixel)

$titleRect = New-Object System.Drawing.RectangleF(0, 124, $W, 110)
$shadowBrush = New-Object System.Drawing.SolidBrush((C 150 0 0 0))
$g.DrawString('VARIED PAWNS', $titleFont, $shadowBrush,
    (New-Object System.Drawing.RectangleF(3, 127, $W, 110)), $sf)
$shadowBrush.Dispose()
$titleBrush = New-Object System.Drawing.SolidBrush((C 255 240 233 219))
$g.DrawString('VARIED PAWNS', $titleFont, $titleBrush, $titleRect, $sf)
$titleBrush.Dispose()

# No subtitle, no rule, no version tag: this is a thumbnail, and at Workshop
# browse size body copy is unreadable anyway. The mod name and a row of pawns
# that are visibly not the same pawn is the entire message. Version and
# description live in About.xml and the Workshop description.

$titleFont.Dispose(); $sf.Dispose()

$g.Dispose()
$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)
$bmp.Dispose()

$fi = Get-Item $OutPath
Write-Host ("wrote {0}  ({1}x{2}, {3:N0} bytes)" -f $fi.FullName, $W, $H, $fi.Length)
