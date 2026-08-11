<#
.SYNOPSIS
    Generates the Varied Pawns logo mark.

.DESCRIPTION
    Writes two files from one deterministic drawing:
      About\ModIcon.png        256x256 -- RimWorld's mod-list row icon
      docs\workshop\logo.png   512x512 -- master, for GitHub / anywhere else

    Design constraint: the mod-list icon renders at roughly 32px, so the mark
    carries NO text and only three silhouettes. Anything finer disappears.
    Three pawns of obviously different heights is the whole idea of the mod,
    and it is the same visual language as About\Preview.png -- keep them in
    step if either changes.
#>
[CmdletBinding()]
param(
    [string]$IconPath   = (Join-Path (Split-Path -Parent $PSScriptRoot) 'About\ModIcon.png'),
    [string]$MasterPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop\logo.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

function C([int]$a,[int]$r,[int]$g,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g,$b) }

function New-RoundedPath([single]$x,[single]$y,[single]$w,[single]$h,[single]$r) {
    $p = New-Object System.Drawing.Drawing2D.GraphicsPath
    $d = $r * 2
    $p.AddArc($x,           $y,           $d, $d, 180, 90)
    $p.AddArc($x + $w - $d, $y,           $d, $d, 270, 90)
    $p.AddArc($x + $w - $d, $y + $h - $d, $d, $d,   0, 90)
    $p.AddArc($x,           $y + $h - $d, $d, $d,  90, 90)
    $p.CloseFigure()
    return $p
}

function New-Logo([int]$S) {
    $bmp = New-Object System.Drawing.Bitmap($S, $S, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode     = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality

    # Everything below is expressed as a fraction of the canvas, so the icon and
    # the master are the same drawing at two resolutions rather than two designs.
    $u = $S / 512.0

    # --- Rounded-square ground -------------------------------------------
    $inset = [single](10 * $u)
    $side  = [single]($S - $inset * 2)
    $plate = New-RoundedPath $inset $inset $side $side ([single](76 * $u))

    $plateBrush = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        (New-Object System.Drawing.PointF(0.0, 0.0)),
        (New-Object System.Drawing.PointF([single]$S, [single]$S)),
        (C 255 40 43 48), (C 255 17 18 21))
    $g.FillPath($plateBrush, $plate)
    $plateBrush.Dispose()

    # Warm wash from the top. Deliberately a LINEAR gradient, not a radial one:
    # PathGradientBrush bands into visible concentric rings at 8 bits per
    # channel, which is glaring on a flat dark plate. Do not "improve" this
    # back into a radial glow.
    $oldClip = $g.Clip
    $g.SetClip($plate)
    # Full height on purpose: a wash that stops short leaves a visible seam
    # where the rectangle ends. Let it fade to zero alpha at the bottom edge.
    $washRect = New-Object System.Drawing.Rectangle(0, 0, $S, $S)
    $wash = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
        $washRect, (C 30 216 166 86), (C 0 216 166 86), 90.0)
    $g.FillRectangle($wash, $washRect)
    $wash.Dispose()
    $g.Clip = $oldClip

    # --- Three pawns ------------------------------------------------------
    # Heights deliberately far apart: at 32px the ONLY surviving signal is
    # that the three shapes are not the same shape.
    $ground = [single](406 * $u)
    $pawns = @(
        @{ cx = 150; h = 0.76; col = @(122,150,176) }
        @{ cx = 256; h = 1.18; col = @(222,170, 88) }
        @{ cx = 362; h = 0.94; col = @(146,116,168) }
    )

    foreach ($p in $pawns) {
        $bodyH = [single](212 * $u * $p.h)
        $bodyW = [single]( 82 * $u)
        $headR = [single]( 36 * $u)
        $cx    = [single]($p.cx * $u)

        $bodyTop = [single]($ground - $bodyH)
        $headTop = [single]($bodyTop - $headR * 1.62)

        $c     = $p.col
        $main  = C 255 $c[0] $c[1] $c[2]
        $shade = C 255 ([int]($c[0]*0.68)) ([int]($c[1]*0.68)) ([int]($c[2]*0.68))

        $torso = New-RoundedPath ([single]($cx - $bodyW/2)) $bodyTop $bodyW $bodyH ([single]($bodyW*0.40))
        $tb = New-Object System.Drawing.Drawing2D.LinearGradientBrush(
            (New-Object System.Drawing.PointF(([single]($cx - $bodyW/2)), ([single]$bodyTop))),
            (New-Object System.Drawing.PointF(([single]($cx + $bodyW/2)), ([single]($bodyTop + $bodyH)))),
            $main, $shade)
        $g.FillPath($tb, $torso)
        $tb.Dispose(); $torso.Dispose()

        $hb = New-Object System.Drawing.SolidBrush($main)
        $g.FillEllipse($hb, [single]($cx - $headR), $headTop, [single]($headR*2), [single]($headR*2))
        $hb.Dispose()
    }

    # --- Baseline ---------------------------------------------------------
    $pen = New-Object System.Drawing.Pen((C 210 216 166 86), [single](7 * $u))
    $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
    $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
    $g.DrawLine($pen, [single](96*$u), $ground, [single](416*$u), $ground)
    $pen.Dispose()

    $plate.Dispose()
    $g.Dispose()
    return $bmp
}

foreach ($spec in @(@{ path = $MasterPath; size = 512 }, @{ path = $IconPath; size = 256 })) {
    $dir = Split-Path -Parent $spec.path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }
    $bmp = New-Logo $spec.size
    $bmp.Save($spec.path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $fi = Get-Item $spec.path
    Write-Host ("wrote {0}  ({1}x{1}, {2:N0} bytes)" -f $fi.FullName, $spec.size, $fi.Length)
}
