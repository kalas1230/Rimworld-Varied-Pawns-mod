# Workshop image set — status and unfinished work

Everything the Steam Workshop listing needs, and what is still missing. The player-facing
text lives in `../workshop-description.txt`; this file is only about the images.

## Planned set, in listing order

Strongest first — Workshop truncates the strip, and the settings tabs are the weakest sales
material, so they go last.

| # | Image | State |
|---|---|---|
| 1 | Title card (Workshop thumbnail) | **Done** — `../../About/Preview.png` |
| 2 | The payoff shot: two colonists, same kind, same profile, one lucky roll and one poor | **Not started** |
| 3 | Profile Editor tab | **Done, but shows a test profile** — see below |
| 4 | Vanilla-vs-varied distribution graphic, plotted from a real 1000-pawn dump | **Not started** |
| 5 | General tab | **Done** — `tab-general-distinct.png` |
| 6 | Overrides tab | **Done** — `tab-overrides.png` |

## Logo

`../../About/ModIcon.png` (256x256) is RimWorld's mod-list row icon; `logo.png` here is the
512x512 master for GitHub or anywhere else. Both come from one drawing — three pawns of
obviously different heights on a rounded plate, the same visual language as the title card.

**No text, by design.** The row icon renders at roughly 32px; checked at 32/24/16px, the three
distinct heights survive and anything finer would not. If the title card's look changes, change
the logo with it.

## Files here

- `tab-overrides.png` — shipped faction defaults, ten rows with priorities. Strongest of the
  three tab shots; use as-is.
- `tab-general-distinct.png` — General tab with the active profile set to `Distinct`, showing
  the preset's own description line. **Use this one.**
- `tab-general.png` — superseded. Same tab with the active profile reading `Custom 1`, a
  leftover test config. Kept only for comparison; do not upload.
- `tab-profile-editor.png` — correct and clean (curve, Typical Baseline, Best-of-25 line all
  visible) but the selected profile is `Custom 1`, a nameless test profile, rather than a
  preset a player would recognise.

All tab shots are 900x700, captured through the GABS bridge's own `take_screenshot` cropped to
`window:1:RimWorld.Dialog_ModSettings`, so they carry no desktop bleed, tooltips or dev chrome.

## Unfinished, and why

**The Profile Editor re-shoot against `Distinct` failed and the image was discarded.** Two
separate problems, both worth knowing before retrying:

1. Setting `editorProfileId` directly through `update_mod_settings` changes the header but does
   **not** reload `editingValues`. The capture showed "Distinct" above `Custom 1`'s sliders and
   curve — an image that would have misrepresented the mod on the Workshop. The profile picker
   has to be driven through the real UI so the values actually load.
2. Driving it through the UI then hit a hard stop: **Dying Light 2 held the Windows foreground**,
   `SetForegroundWindow` returned `False`, and RimWorld neither renders nor processes queued
   clicks while unfocused. Every capture after that point was a byte-identical stale frame — no
   error, no warning. Diagnosed and stopped there rather than killing the other game.

**Item 4, the distribution graphic, is not started** — it needs a 1000-pawn
`Roll pawns and dump distribution` run, which needs the same working game session.

The mod's on-disk config was never modified: every settings change ran at `write: false`, and
`reload_mod_settings` discarded them before exit. Verified afterwards —
`activeProfileId` still `custom_639215913003568941`.

## To finish

Close any other fullscreen game first, then it is roughly five minutes of bridge work:
the Profile Editor re-shoot against `Distinct` (via the real profile picker), plus the
1000-pawn dump behind image 4. Images 2 and 4 are the two that show the mod's *effect*
rather than its settings, and are worth more than any tab shot.

## Regenerating the generated art

```powershell
.\tools\make-preview.ps1   # About\Preview.png       -- Workshop thumbnail / title card
.\tools\make-logo.ps1      # About\ModIcon.png + docs\workshop\logo.png
```

Both are deterministic — no randomness, no hardcoded paths, same bytes every run. Edit colours,
copy or layout in the scripts rather than editing the PNGs, so the assets stay reproducible.
