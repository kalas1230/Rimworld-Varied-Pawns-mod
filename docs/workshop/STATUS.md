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
| 3 | Profile Editor tab | **Done** — `tab-profile-editor.png`, showing `Distinct` |
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
  three tab shots; use as-is. **Re-verified against the build that moved race overrides below
  xenotype overrides: a fresh capture is byte-identical (SHA-256 `148B158B…`).** Both of those
  sections sit below the fold, so the reorder cannot show in this crop. Not stale.
- `tab-general-distinct.png` — General tab with the active profile set to `Distinct`, showing
  the preset's own description line. **Use this one.**
- `tab-general.png` — superseded. Same tab with the active profile reading `Custom 1`, a
  leftover test config. Kept only for comparison; do not upload.
- `tab-profile-editor.png` — **now shows `Distinct`**, with the curve, the Typical line and the
  Best-of-25 line all visible, the preset's own description under the header, and the quality
  slider correctly read-only for a preset. Header and sliders agree, and the agreement is
  checkable rather than eyeballed: the on-screen readouts (`Typical -9%`, `Best of 25 +10%`) match
  `envelope_check.py`'s Distinct row (−8.7% at N=1, +9.5% at N=25), and the sliders show
  Distinct's own values (quality 0.32, skill spread ±0.86, shift −3.3–6.5).

All tab shots are 900x700, captured through the GABS bridge's own `take_screenshot` cropped to
`window:1:RimWorld.Dialog_ModSettings`, so they carry no desktop bleed, tooltips or dev chrome.

## Unfinished, and why

### The FloatMenu limit is real, and here is the way around it

The profile picker **cannot** be driven by the bridge. `DebugActions.cs:62-65` already recorded
why, verified against the Faction Add button on 2026-08-06: a synthetic click activates the
button but the `FloatMenu` does not survive to the next frame, so its rows can never be read or
clicked. Nothing about the foreground fixed this — with no other game running and frames
confirmed live, the click still returns "UI state did not change" and no menu opens.

**What worked instead, and why it is not the same as the attempt that failed.** Setting
`editorProfileId` alone changes the header and leaves `editingValues` holding the old profile —
that is the mismatch that produced the discarded image. But `editingValues` is a *cache*: the
`Editing` getter calls `RefreshEditor()` whenever it is null. So setting **both**
`editorProfileId = preset_distinct` **and** `editingValues = null` (two `update_mod_settings`
calls, `write: false`) drives the same code path `SetEditorProfile` uses, and the next frame
loads Distinct's real values. Header and sliders agree because the mod itself resolved them.

### Item 4 is blocked on the same cache pattern, one layer down

The 1000-pawn dump runs fine — path `Actions\Roll pawns and dump distribution` (backslash; the
`Varied Pawns` category is metadata, not a path segment), then click the `1000 pawns` row in the
`Dialog_DebugOptionListLister`, which *is* clickable. Two runs completed.

**But both resolved to `Custom 1`, not to the preset the run was supposed to measure.** Setting
`activeProfileId` does not re-point generation: `RefreshResolved()` caches the resolved profile
into `Active`/`Hostile` at load, and a direct field write never re-runs it. Setting
`enableOverrides = false` changes nothing, which is what proves it is the cache and not an
override winning. The mod's own diagnostic caught this and said so
(`^^ NOT the configured active profile`) — the instrument worked.

Every remaining route needs a decision the controller has not made:
- **write the config and restore from backup** — `write: true` then `reload_mod_settings` would
  re-run `RefreshResolved`, but it writes the player's real config file;
- **Import from Clipboard** — a plain button the bridge *can* click, and `CopyFrom` calls
  `RefreshResolved`; needs a crafted settings payload on the clipboard;
- **shoot it against `Custom 1` as it stands** — honest, but `Custom 1` is near-`Faithful`
  (shift −3–3, spread 0.49, quality 0.50), so it is a weak advertisement for a variance mod.

Useful discovery while measuring: the dump already prints a **12-bin per-pawn-mean-skill
histogram**, so item 4 does not need new code to get a distribution shape — only a control run to
put beside it.

**The on-disk config was never modified, again.** Every change ran at `write: false`;
`reload_mod_settings` restored in-memory state to disk before exit. Verified by hash, unchanged
across the whole session: SHA-256 `DF224748…`, mtime `2026-08-11 06:41:42`, `activeProfileId`
still `custom_639215913003568941`. A backup sits outside the repo in the session scratchpad.

## To finish

Image 2 (the payoff shot) and image 4 (the distribution graphic) remain — the two that show the
mod's *effect* rather than its settings, and worth more than any tab shot. Image 4 needs the
profile-resolution decision above settled first; image 2 needs two colonists rolled from one
profile and their character sheets shot side by side.

## Regenerating the generated art

```powershell
.\tools\make-preview.ps1   # About\Preview.png       -- Workshop thumbnail / title card
.\tools\make-logo.ps1      # About\ModIcon.png + docs\workshop\logo.png
```

Both are deterministic — no randomness, no hardcoded paths, same bytes every run. Edit colours,
copy or layout in the scripts rather than editing the PNGs, so the assets stay reproducible.
