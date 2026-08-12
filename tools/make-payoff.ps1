<#
Generates docs\workshop\payoff-showcase.png -- Steam Workshop listing image 2.

Deterministic: no randomness, no hardcoded absolute paths, so re-running produces a
byte-comparable image.

WHAT THIS IMAGE IS. Three real colonist cards, captured in game through the GABS
bridge from colonies rolled on the `Showcase` custom profile, composited onto the
same surface as the rest of the listing strip. The three source PNGs are checked in
beside this script's output and are NOT redrawn here -- this script only masks UI
chrome, places them and adds two lines of text. If a card needs to change, re-capture
it (method in docs\workshop\STATUS.md) and drop the new PNG in place.

THE IMAGE DOES THE TALKING. Owner's direction: minimal text, and the only thing text
is allowed to convey is WHAT THE PHOTO IS. So there is one headline and one
provenance line, and there are deliberately NO per-card labels, captions, arrows,
callouts or annotations -- the cards already carry the colonists' names, and the
three skill columns are the argument. Do not add "Excellent / Specialist / Unlucky"
style labels: naming the shapes tells the reader what to think instead of letting
them see it, and it is the exact thing this image was rebuilt to avoid.

TYPE IS RECESSIVE AND CARRIES NO FULL STOPS. The headline was 40pt in primary ink and
was the loudest thing in the frame, which is backwards -- it is now 24pt in SECONDARY
ink and the provenance line is 11pt muted. Terminal full stops are dropped across the
listing set on the owner's call: these are labels naming what the image is, not
sentences. The headline also uses an em dash rather than a mid-line stop, so the whole
string stays stop-free.

THE LAYOUT IS OFF-GRID BUT THE CARDS ARE SQUARE. An early version placed the three
cards on an exact row with identical gaps and it read as a spec sheet -- machined, and
the eye slid straight off it. The next version fixed that by tilting each card a
degree or two; that was rejected too, and correctly, because tilted screenshots of a
square UI just look broken rather than casual.

So the looseness comes ENTIRELY from staggered heights and overlap, and every card is
axis-aligned. That is not a compromise, it is strictly better: with no rotation the
cards are blitted at integer offsets, which means NO RESAMPLING AT ALL and the in-game
skill numbers stay exactly as crisp as they were captured. DO NOT REINTRODUCE
ROTATION -- it was tried, it was rejected, and it costs image quality to do.

The stagger is HAND-PICKED AND FIXED, not random: this script stays deterministic, and
the numbers below were chosen by rendering and looking.

WHY THESE THREE, IN THIS ORDER. Left to right they descend in overall quality while
the middle card breaks the pattern on a second axis, so the eye gets range AND shape
rather than a simple ladder:

  Madeline Miller  four MAJOR passions (Mining 15, Construction 13, Plants 13,
                   Cooking 10), Incapable of: None, and no skill below 6.
  Mason Lindsey    brilliant at three things (Social 14, Medical 12, Melee 10, all
                   Major) and INCAPABLE of three others -- five blank rows.
  Kathryn Harley   best skill on the card is a 3, with Shooting and Melee dashed out.

Selection rule, and it is not "biggest number": a colonist reads as GOOD when the
PASSIONS are good, ideally passions and skills together. A high skill with no passion
behind it reads as a fluke rather than a lucky roll. That rule is why a measured
Melee 20 pawn (one minor passion, two incapabilities) is NOT in this image. See
docs\workshop\STATUS.md.

THE CAPTION CONSTRAINT TRAVELS WITH THIS IMAGE. `Showcase` is a CUSTOM profile built
in the Profile Editor, not a shipped preset. The footer line below says so, and the
Steam listing caption must not imply that picking `Distinct` from the dropdown gives
these three colonists.
#>
param(
    [string]$OutPath = (Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop\payoff-showcase.png')
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$docs = Join-Path (Split-Path -Parent $PSScriptRoot) 'docs\workshop'

# 16:9, matching the rest of the strip's aspect. Sized so the three 514x489 cards sit
# at their NATIVE PIXEL SIZE on integer offsets -- never scaled, never rotated, so the
# skill numbers are pixel-identical to the captures. Card size drives canvas size.
#
# NOT 16:9, and that is the point. A single row of 489px-tall cards inside a 16:9 frame
# leaves ~300px of vertical slack no matter where the cards go, and the stagger turns
# that slack into obvious voids at the corners. Two ways out: shrink the frame to the
# content, or stack two rows of cards and scale them down. Scaling resamples the small
# in-game text, so the frame shrank instead. 1600x790 is sized so the card block plus
# one headline and one footer very nearly fills it.
#
# The card block is 1518 wide, leaving 41px either side. Do not pad this back out; the
# earlier 1680x945 read as unfinished for exactly that reason.
$W = 1600
$H = 790

# Sources are payoff-card-*.png; the composite this script writes is payoff-showcase.png.
# Keep that naming split -- an earlier round had the ingredients and the deliverable
# sharing a prefix and it was genuinely unclear which file the listing uploads.
$cards = @(
    (Join-Path $docs 'payoff-card-madeline.png'),   # Madeline Miller
    (Join-Path $docs 'payoff-card-mason.png'),      # Mason Lindsey
    (Join-Path $docs 'payoff-card-harley.png')      # Kathryn Harley
)

function C([int]$a,[int]$r,[int]$g2,[int]$b) { [System.Drawing.Color]::FromArgb($a,$r,$g2,$b) }

$inkSecondary = C 255 176 182 190
$inkMuted     = C 255 124 130 138

# The card's own flat background, sampled from the captures. Used to paint out the
# window chrome below; if RimWorld's UI theme ever changes, re-sample it.
$cardBg = C 255 21 25 29

$bmp = New-Object System.Drawing.Bitmap($W, $H, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
$g   = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode      = [System.Drawing.Drawing2D.SmoothingMode]::HighQuality
$g.TextRenderingHint  = [System.Drawing.Text.TextRenderingHint]::AntiAlias
$g.InterpolationMode  = [System.Drawing.Drawing2D.InterpolationMode]::NearestNeighbor
$g.PixelOffsetMode    = [System.Drawing.Drawing2D.PixelOffsetMode]::Half

# --- Background ---------------------------------------------------------------
# A blurred colony behind the cards: these are in-game screenshots, so a game behind
# them is the honest setting for them, and it stops three panels floating on nothing.
#
# THIS USED TO FAIL SILENTLY. The path here was 'bg-blurred-entrails-shorter.png',
# which is not in the repo and never has been, and the else branch quietly painted a
# flat gradient and reported success -- so the backdrop this block describes had never
# once been drawn. Missing background is now a hard error. If you rename the file,
# you will be told.
$bgPath = Join-Path $docs 'bg-blurred.png'
if (-not (Test-Path $bgPath)) {
    throw "Missing background: $bgPath -- no gradient fallback, see the note above."
}

# Opaque black first: bg-blurred.png has a soft vignette whose corner pixels are only
# ~40% opaque, and composited onto a fresh 32bpp bitmap those corners would stay
# semi-transparent in the saved PNG.
$g.FillRectangle((New-Object System.Drawing.SolidBrush((C 255 0 0 0))),
                 (New-Object System.Drawing.Rectangle(0,0,$W,$H)))

$bgImg = [System.Drawing.Image]::FromFile($bgPath)
try {
    # COVER, not stretch. The source is 1600x900 and this frame is 1600x790 -- a
    # straight DrawImage to $W x $H would squash the colony by 12% vertically.
    $scale = [Math]::Max($W / $bgImg.Width, $H / $bgImg.Height)
    $dw = [int][Math]::Ceiling($bgImg.Width  * $scale)
    $dh = [int][Math]::Ceiling($bgImg.Height * $scale)
    $old = $g.InterpolationMode
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($bgImg, [int](($W - $dw) / 2), [int](($H - $dh) / 2), $dw, $dh)
    # Restored immediately: the CARDS must stay on NearestNeighbor, which is what keeps
    # the in-game skill numbers pixel-exact. Only the blur wants a smooth resample.
    $g.InterpolationMode = $old
} finally { $bgImg.Dispose() }

# Light scrim. Keep it low -- the source is already very dark (median luminance 23),
# so a heavy scrim does not tame the backdrop, it erases it.
$g.FillRectangle((New-Object System.Drawing.SolidBrush((C 30 8 9 11))),
                 (New-Object System.Drawing.Rectangle(0,0,$W,$H)))

# --- Cards --------------------------------------------------------------------
$cardW = 514; $cardH = 489

# The scatter. One entry per card, in the order the $cards array lists them: the
# CENTRE the card is pinned at, and the angle it is rotated by. Cards are 514 wide and
# the centres are 489 apart, so consecutive cards overlap by 25px -- they touch and
# stack instead of sitting in separate columns. Heights differ by up to 65px so no two
# tops line up, and the angles alternate in sign so the row never resolves into a
# straight edge. Hand-tuned by rendering; adjust by eye, not by formula.
#
# THE OVERLAP HAS A HARD CEILING OF ABOUT 15px AND HERE IS WHY. A RimWorld character
# card has only ~18px of internal padding before its left-hand labels start, and its
# skill bars run to within ~17px of the right edge. The first attempt overlapped by
# 25px and Mason's card ate the leading letter of Harley's "Childhood", "Traits" and
# "Incapable of" -- the pile looked right and was quietly destroying content. 12px
# reads as a stack and cannot touch a glyph. Do not open it up again.
# Top-left corner of each card, in the order $cards lists them. X steps by 502 so
# consecutive cards overlap by 12px. Y is the whole of the looseness, and it is a
# BALANCING ACT: too little and the row reads as machined, too much and the stagger
# carves empty wedges out of the corners. 90px was too much once the frame tightened;
# 55px still breaks the rule line without opening holes.
$place = @(
    @{ x = 41;   y = 128 },   # Madeline
    @{ x = 543;  y = 173 },   # Mason -- dropped lowest, and drawn on top
    @{ x = 1045; y = 118 }    # Harley -- rides highest
)
# Paint order, as indices into $place. Deliberately NOT left-to-right: drawing the
# middle card last puts it on top of both neighbours, which is what makes the group
# read as a pile rather than as three overlapping rectangles in reading order.
$paintOrder = @(2, 0, 1)

$brCardBg = New-Object System.Drawing.SolidBrush($cardBg)
# Window chrome to paint out: RimWorld's close button and the two character-card
# action icons. They are real game UI, but three copies of an X button in a store
# image is noise that says nothing about the colonists. Coordinates are card-local
# and were measured off the captures, not guessed -- close button x 490..510 y 6..24,
# action icons x 374..437 y 22..42; the rects below cover both with margin. Nothing
# here touches a skill, passion, trait or name.
$chromeRects = @(
    (New-Object System.Drawing.Rectangle(370, 4, 142, 44))
)

foreach ($i in $paintOrder) {
    $path = $cards[$i]
    if (-not (Test-Path $path)) { throw "missing source card: $path" }
    $img = [System.Drawing.Image]::FromFile($path)
    try {
        if ($img.Width -ne $cardW -or $img.Height -ne $cardH) {
            throw "card $path is $($img.Width)x$($img.Height), expected ${cardW}x${cardH} -- re-crop it rather than letting this script scale it"
        }
        $x = $place[$i].x
        $y = $place[$i].y

        # Soft drop shadow. System.Drawing has no blur, so it is faked with nested
        # translucent rectangles fading outward -- the card is opaque and drawn on top,
        # so only the halo escaping down and right is ever seen. Without this the
        # overlaps read as flat collage; with it the cards read as physically stacked,
        # which is what carries the loose feel now that nothing is tilted.
        for ($s = 18; $s -ge 0; $s--) {
            $sh = New-Object System.Drawing.SolidBrush((C 3 0 0 0))
            $g.FillRectangle($sh, ($x + 5 - $s), ($y + 9 - $s),
                                  ($cardW + 2 * $s), ($cardH + 2 * $s))
            $sh.Dispose()
        }

        # Integer destination, native size, no transform in effect: a straight blit.
        $g.DrawImage($img, $x, $y, $cardW, $cardH)
        foreach ($r in $chromeRects) {
            $g.FillRectangle($brCardBg, ($x + $r.X), ($y + $r.Y), $r.Width, $r.Height)
        }
    } finally { $img.Dispose() }
}
$brCardBg.Dispose()

# --- Text: one headline, one provenance line, nothing else --------------------
$fTitle = New-Object System.Drawing.Font('Segoe UI', 24, [System.Drawing.FontStyle]::Bold)
$fNote  = New-Object System.Drawing.Font('Segoe UI', 11)
$brSec  = New-Object System.Drawing.SolidBrush($inkSecondary)
$brMut  = New-Object System.Drawing.SolidBrush($inkMuted)

# Set at x=76 rather than flush with any card edge -- the cards no longer share a left
# edge to align to, and picking one of them to align against would re-introduce exactly
# the ruled-off look the scatter exists to break.
# Both strings are pure ASCII on purpose. No terminal full stop (owner's call, applies
# across the listing set), and NO EM DASH -- one was tried as a stop-free way to join
# the two halves and was rejected; a plain comma does the same job and keeps the file
# encoding-proof. That matters here: this .ps1 has no BOM, Windows PowerShell 5.1 reads
# a BOM-less script as ANSI, and the literal em dash rendered as "a€"" mojibake. Keep
# the drawn strings ASCII and the problem cannot come back.
$g.DrawString('Same settings, three very different colonists', $fTitle, $brSec, 41.0, 34.0)
$g.DrawString('Rolled in game on one custom profile, built in the mod''s Profile Editor',
    $fNote, $brMut, 41.0, [single]($H - 42))

$dir = Split-Path -Parent $OutPath
if (-not (Test-Path $dir)) { New-Item -ItemType Directory -Path $dir -Force | Out-Null }
$bmp.Save($OutPath, [System.Drawing.Imaging.ImageFormat]::Png)

foreach ($o in @($fTitle,$fNote,$brSec,$brMut,$g,$bmp)) { $o.Dispose() }

Write-Host "Wrote $OutPath"
