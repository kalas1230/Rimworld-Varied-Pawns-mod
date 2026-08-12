<#
Generates docs\workshop\tabs-showcase.png -- the three settings tabs in one image.

Deterministic: no randomness, and every source is blitted at NATIVE SIZE, so re-running
produces a byte-comparable image.

WHY THIS IS A WIDE ROW. Three 900x700 captures side by side need 2700px of width. The
previous version tried to fit them into a 1600px frame by placing them at x = 40 / 350 /
660, which overlaps each card by about 61% -- two of the three tabs were almost entirely
hidden behind the third, and the script reported success. So the choice is between
scaling the captures down and fitting the frame to the content, and this file takes the
same answer make-payoff.ps1 took: FIT THE FRAME. Scaling resamples 11px in-game text into
mush, and the whole point of a settings screenshot is that it is readable.

A CASCADE WAS TRIED FIRST AND DOES NOT WORK -- do not re-propose it. Stacking the three
cards with a vertical step, so each one's top strip shows above the next, is the obvious
way to get three 900px windows into a compact frame. It fails for a reason specific to
this dialog: every card's top strip is the SAME title and the SAME three tab buttons. The
only thing distinguishing them up there is which tab is drawn raised, which is a few
pixels of shading, so the image reads as three copies of one window rather than as three
different tabs. What identifies a tab is its CONTENT, and content is exactly what a
cascade hides. Adding a sideways step to reveal more only made it worse: the sliver falls
on the dialog's left-hand label column, so the image grew a ragged strip of chopped
half-words down one side.

So each card is shown whole. The order is the tab order in the game -- General, Profile
Editor, Overrides -- and the middle card is lifted rather than the row being flat, so the
group reads as three windows rather than as a spec sheet. That lift is the one thing
borrowed from the payoff shot's layout notes, along with its two hard rules: NO rotation
(tilted screenshots of a square UI look broken), and integer offsets at native size so
there is no resampling anywhere.

WHAT THIS SCRIPT WILL NOT DO SILENTLY. The previous version looked for two background
files that do not exist in the repo, and when it found neither it fell through to a flat
gradient and reported success -- so the "custom colony backdrop" in its own header
comment had never once been drawn. It also force-scaled each capture through
DrawImage(img, x, y, w, h) with no size check. Both are now hard failures: a missing
background throws, and a capture that is not exactly 900x700 throws rather than being
quietly resampled.
#>
param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop\tabs-showcase.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$docs = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop'

function C([int]$a,[int]$r,[int]$g2,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g2,$b) }

# --- Sources ------------------------------------------------------------------
# Left to right, in the order the tabs appear in the dialog itself. That ordering is
# the point: a reader who has seen the tab row can map the three cards onto it
# without a caption telling them which is which.
$TAB_W = 900
$TAB_H = 700
$cards = @(
    'tab-general-distinct.png',   # NOT tab-general.png, which is the superseded capture
                                  # showing a leftover 'Custom 1' profile
    'tab-profile-editor.png',     # lifted -- the only card carrying a picture
    'tab-overrides.png'
)
# Vertical lift per card. The middle one rides above the other two; a dead-flat row
# of three identical rectangles reads as a machined spec sheet and the eye slides
# off it. Kept small: a big stagger carves empty wedges out of the frame corners,
# which is what made an early version of the payoff shot read as unfinished.
$lift = @(36, 0, 36)

$bgName = 'bg-blurred.png'

# --- Layout -------------------------------------------------------------------
# Gaps, not overlap. These are three separate windows rather than a stack of cards, so
# they read better with air between them -- and it sidesteps the payoff shot's hard-won
# overlap ceiling entirely (a character card has only ~18px of internal padding before
# its labels start, and an early payoff version at 25px of overlap was silently eating
# the leading letter of every label underneath).
$gap = 24
$n   = $cards.Count

$blockW = $TAB_W * $n + $gap * ($n - 1)
$blockH = $TAB_H + ($lift | Measure-Object -Maximum).Maximum

# Generous, and that is the whole reason the background exists. At a 44px margin the
# three cards cover about 95% of the frame and the colony behind them is reduced to a
# dark rim nobody registers -- at which point the blur is costing file size and buying
# nothing, and a flat fill would be honest. The margin is what turns the backdrop from
# a rim into the room the dialog is sitting in.
$marginX  = 116
$headline = 96     # band above the cards
$footer   = 76     # band below them

$W = $blockW + $marginX * 2
$H = $headline + $blockH + $footer
$x0 = $marginX
$y0 = $headline

$inkSecondary = C 255 176 182 190
$inkMuted     = C 255 124 130 138

$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
$g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::HighQuality

# --- Background ---------------------------------------------------------------
# Painted onto opaque black FIRST. bg-blurred.png carries a soft vignette whose
# corner pixels are only ~40% opaque; composited straight onto a fresh 32bpp
# bitmap those corners stay semi-transparent in the PNG, and whatever views the
# image supplies its own colour behind them.
$bgPath = Join-Path $docs $bgName
if (-not (Test-Path $bgPath)) {
    throw "Missing background: $bgPath -- this script does not fall back to a gradient, " +
          "because the version that did shipped images nobody realised had lost their backdrop."
}

$g.FillRectangle((New-Object System.Drawing.SolidBrush((C 255 0 0 0))),
                 (New-Object System.Drawing.Rectangle(0,0,$W,$H)))

$bgImg = [System.Drawing.Image]::FromFile($bgPath)
try {
    # COVER, not stretch: scale by the larger ratio and centre-crop the overflow, so
    # the colony keeps its proportions whatever frame the card block ends up needing.
    $scale = [Math]::Max($W / $bgImg.Width, $H / $bgImg.Height)
    $dw = [int][Math]::Ceiling($bgImg.Width  * $scale)
    $dh = [int][Math]::Ceiling($bgImg.Height * $scale)
    $g.DrawImage($bgImg, [int](($W - $dw) / 2), [int](($H - $dh) / 2), $dw, $dh)
} finally { $bgImg.Dispose() }

# A light scrim, to knock the blur's brightest patches down so the dark UI panels
# still read as objects in front of something rather than as holes in it. Measured,
# the blur runs luminance 20-29 across most of its area with local peaks near 46.
#
# KEEP THIS LOW. The first attempt used alpha 118 and was a mistake worth recording:
# the source is already darker than the chart surface used elsewhere in this set
# (median luminance 23 against 28), so a heavy scrim did not tame it, it erased it --
# the result was flat black and the colony the image exists to sit in front of was
# simply gone. If the cards ever look like holes, add a shadow, do not add scrim.
$g.FillRectangle((New-Object System.Drawing.SolidBrush((C 30 8 9 11))),
                 (New-Object System.Drawing.Rectangle(0,0,$W,$H)))

# --- Cards --------------------------------------------------------------------
for ($i = 0; $i -lt $n; $i++) {
    $path = Join-Path $docs $cards[$i]
    if (-not (Test-Path $path)) { throw "Missing source tab: $path" }

    $img = [System.Drawing.Image]::FromFile($path)
    try {
        if ($img.Width -ne $TAB_W -or $img.Height -ne $TAB_H) {
            throw ("$($cards[$i]) is $($img.Width)x$($img.Height), expected ${TAB_W}x${TAB_H}. " +
                   "Recapture it rather than letting this script rescale it -- resampling " +
                   "turns the 11px in-game type into mush, which is the one thing a " +
                   "settings screenshot cannot afford.")
        }

        $x = $x0 + ($TAB_W + $gap) * $i
        $y = $y0 + $lift[$i]

        # Faked soft shadow: concentric translucent rectangles, drawn as an outline
        # ring per step rather than a filled rect, so the alpha does not accumulate
        # into a black slab under a card that is about to be drawn over it anyway.
        for ($s = 22; $s -ge 1; $s--) {
            $pen = New-Object System.Drawing.Pen((C 9 0 0 0), 2.0)
            $g.DrawRectangle($pen, ($x + 7 - $s), ($y + 11 - $s),
                                   ($TAB_W + 2 * $s), ($TAB_H + 2 * $s))
            $pen.Dispose()
        }

        # Integer offsets, native size: no resampling at all, so the in-game numbers
        # are pixel-identical to the captures.
        $g.DrawImageUnscaled($img, $x, $y)
    } finally { $img.Dispose() }
}

# --- Text ---------------------------------------------------------------------
# Recessive, and pure ASCII with no terminal full stops -- both are house rules for
# this listing set. ASCII also closes an encoding trap: these .ps1 files carry no
# BOM, Windows PowerShell 5.1 reads a BOM-less script as ANSI, and a literal em dash
# once rendered as mojibake. Build any non-ASCII character from its code point.
$fTitle = New-Object System.Drawing.Font('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
$fNote  = New-Object System.Drawing.Font('Segoe UI', 11)
$brSec  = New-Object System.Drawing.SolidBrush($inkSecondary)
$brMut  = New-Object System.Drawing.SolidBrush($inkMuted)

$g.DrawString('Presets, a profile editor, and per-faction overrides',
              $fTitle, $brSec, [single]$marginX, 24.0)
$g.DrawString('The mod settings dialog, captured in game at full size',
              $fNote, $brMut, [single]$marginX, [single]($H - 38))

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($o in @($fTitle,$fNote,$brSec,$brMut,$g,$bmp)) { $o.Dispose() }

Write-Host "Wrote $OutPath  (${W}x${H})"
