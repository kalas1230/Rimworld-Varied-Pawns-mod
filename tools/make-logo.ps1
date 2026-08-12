<#
.SYNOPSIS
    Generates the Varied Pawns logo mark.

.DESCRIPTION
    Writes four files from one deterministic drawing, differing only in whether
    the rounded plate and the baseline stroke are drawn:

      docs\workshop\logo.png                            512  plate + baseline
      docs\workshop\logo-backgroundless.png             512  baseline only
      docs\workshop\logo-backgroundless-nobaseline.png  512  neither
      About\ModIcon.png                                 256  neither  <- SHIPS

    Design constraint: the icon renders at roughly 24-32px, so the mark carries
    NO text and only three silhouettes. Anything finer disappears. Three pawns of
    obviously different heights is the whole idea of the mod, and it is the same
    visual language as About\Preview.png -- keep them in step if either changes.

    WHY ModIcon IS THE PLATE-FREE VARIANT, AND WHY THIS SCRIPT NOW KNOWS THAT.
    Options -> Mod options is the icon's only home, and it draws icons untinted on
    a dark ground. Ours was the only entry there with an opaque rounded plate
    behind it, so it read as a small app tile in a row of transparent glyphs.
    Owner's call, 2026-08-12: drop the plate, and drop the baseline stroke with it,
    since at 24px it is a stray dash under the pawns rather than a ground.

    That change was made to the PNG first, and this script still drew the plated
    version into About\ModIcon.png -- so the next person to regenerate the art
    would have silently reverted the shipped icon and had no reason to look. The
    variants are parameters now, and every checked-in logo file comes out of this
    one script. Do not go back to hand-editing the PNGs.
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

function New-Logo([int]$S, [bool]$WithPlate = $true, [bool]$WithBaseline = $true) {
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

    # The warm wash is CLIPPED TO THE PLATE, so it goes with the plate. Drawing it
    # without one would lay an amber haze across the whole transparent square, which
    # composites as a dirty rectangle on any background the icon is placed on.
    if ($WithPlate) {
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
    }

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
    # Reads as a ground the pawns stand on at 512px, and as a stray amber dash
    # under them at 24px, which is why the shipped icon omits it.
    if ($WithBaseline) {
        $pen = New-Object System.Drawing.Pen((C 210 216 166 86), [single](7 * $u))
        $pen.StartCap = [System.Drawing.Drawing2D.LineCap]::Round
        $pen.EndCap   = [System.Drawing.Drawing2D.LineCap]::Round
        $g.DrawLine($pen, [single](96*$u), $ground, [single](416*$u), $ground)
        $pen.Dispose()
    }

    $plate.Dispose()
    $g.Dispose()
    return $bmp
}

$workshop = Split-Path -Parent $MasterPath

$specs = @(
    @{ path = $MasterPath;                                              size = 512; plate = $true;  baseline = $true  }
    @{ path = (Join-Path $workshop 'logo-backgroundless.png');           size = 512; plate = $false; baseline = $true  }
    @{ path = (Join-Path $workshop 'logo-backgroundless-nobaseline.png'); size = 512; plate = $false; baseline = $false }
    # The shipped icon. Plate-free and baseline-free -- see the header note.
    # DOWNSCALED FROM 512 rather than drawn at 256: rendering the curves at double
    # resolution and resampling down supersamples them, and the pawn shoulders and
    # head circles come out visibly cleaner than drawing them natively at 256, where
    # GDI+ antialiases a 36px circle with far less to work with. This also matches
    # how the shipped icon was actually produced.
    @{ path = $IconPath;                                                size = 256; plate = $false; baseline = $false; from512 = $true }
)

foreach ($spec in $specs) {
    $dir = Split-Path -Parent $spec.path
    if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Force -Path $dir | Out-Null }

    if ($spec.from512) {
        $master = New-Logo 512 $spec.plate $spec.baseline
        $bmp = New-Object System.Drawing.Bitmap($spec.size, $spec.size,
                   [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
        $gg = [System.Drawing.Graphics]::FromImage($bmp)
        $gg.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
        # PixelOffsetMode stays at Default here, and that is not an oversight. Setting
        # it to Half or HighQuality shifts the sample grid by half a pixel, which on a
        # 2:1 downscale of a shape with hard alpha edges flips edge pixels between fully
        # transparent and fully opaque -- 3302 pixels differ from the shipped icon that
        # way, against 1542 differing by a maximum of 4/255 with Default. Default is
        # what reproduces the icon in the tree.
        $gg.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Default
        $gg.CompositingQuality = [System.Drawing.Drawing2D.CompositingQuality]::HighQuality
        $gg.DrawImage($master, 0, 0, $spec.size, $spec.size)
        $gg.Dispose(); $master.Dispose()
    } else {
        $bmp = New-Logo $spec.size $spec.plate $spec.baseline
    }

    $bmp.Save($spec.path, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $fi = Get-Item $spec.path
    Write-Host ("wrote {0}  ({1}x{1}, plate={2}, baseline={3}, {4:N0} bytes)" -f `
        $fi.FullName, $spec.size, $spec.plate, $spec.baseline, $fi.Length)
}
