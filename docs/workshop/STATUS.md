# Workshop image set — status and unfinished work

Everything the Steam Workshop listing needs, and what is still missing. The player-facing
text lives in `../workshop-description.txt`; this file is only about the images.

## Upload order — the owner's call, 2026-08-12

**This is the order to upload in. It supersedes the earlier "strongest first" ordering**, which put
the payoff shot second and the distribution graphic fourth; that arrangement is recorded here only
so nobody re-derives it and "corrects" the list back.

| # | Image | File | State |
|---|---|---|---|
| 1 | Title card (Workshop thumbnail) | `../../About/Preview.png` | **Done** |
| 2 | Distribution graphic, plotted from real 1000-pawn dumps | `distribution.png` | **Done** |
| 3 | The payoff shot: colonists rolled by the mod, side by side | `payoff-showcase.png` | **Done** — three-card composite from `tools/make-payoff.ps1` |
| 4 | General tab | `tab-general-distinct.png` | **Done** — note the filename; plain `tab-general.png` is the superseded Custom 1 shot |
| 5 | Overrides tab | `tab-overrides.png` | **Done** |
| 6 | Profile Editor tab | `tab-profile-editor.png` | **Done** — showing `Distinct` |
| — | All three tabs in one frame | `tabs-showcase.png` | **Done** — not in the upload set; see below |

### Corrections applied 2026-08-12 (second pass)

This table said image 2 was "done and in the tree" while **`payoff-showcase.png` did not
exist** — only the `payoff-card-*.png` ingredients did, and `tools/make-payoff.ps1` was
untracked. It has now been generated. Treat "Done" in this table as a claim to verify with
`Get-ChildItem docs\workshop` rather than as a fact, because it was wrong once.

Also corrected in this pass:

- **`tab-general.png` is superseded** by `tab-general-distinct.png`. A 2026-08-12 deletion of it did not stick; see the file list below.
- **`ModIcon.png` no longer has the opaque rounded plate.** It is now the backgroundless,
  baseline-free drawing (verified: a 256px downscale of `logo-backgroundless-nobaseline.png`
  matches the shipped icon pixel for pixel). The note further down about ours being the only
  opaque tile in the Mod options grid is therefore **stale** — that was the reason to change it,
  and it has been changed.
- **`distribution.png` now plots a different quantity.** See the section on it below.

## Logo

`../../About/ModIcon.png` (256x256) is the Mod options icon; `logo.png` here is the
512x512 master for GitHub or anywhere else. All of it comes from one drawing — three pawns of
obviously different heights, the same visual language as the title card.

`tools/make-logo.ps1` emits **four** files from that one drawing, differing only in whether the
rounded plate and the baseline stroke are drawn: `logo.png` (both), `logo-backgroundless.png`
(baseline only), `logo-backgroundless-nobaseline.png` (neither), and `ModIcon.png` (neither, at
256). The icon is **downscaled from a 512 render** rather than drawn at 256 — supersampling gives
visibly cleaner head circles and shoulders.

**This was a live regression trap until 2026-08-12.** The plate was dropped from `ModIcon.png` by
editing the PNG, while the script still drew the plated version into that same path — so the next
person to regenerate the art would have silently reverted the shipped icon with nothing to warn
them. The variants are parameters now. Verified: the three 512px files regenerate **byte-identical**
to the ones in the tree, and the icon to a maximum channel delta of 4.

**No text, by design.** The icon renders at roughly 24–32px; checked at 32/24/16px, the three
distinct heights survive and anything finer would not. If the title card's look changes, change
the logo with it. **Edit the script, not the PNGs** — see the trap immediately above for what
happens otherwise.

### Where each image actually appears — verified in game 2026-08-12

Confirmed live against RimWorld 1.6.4871, not assumed. There are **three** surfaces and they use
**two different files**:

| Surface | File shown | Notes |
|---|---|---|
| In-game **mod list** (`Page_ModsConfig`) detail panel | `About/Preview.png` | Full-width banner beside the description |
| In-game **mod list** row glyph | *neither* | This is RimWorld's **content-source** icon (`ContentSourceIcon_ModsFolder`) — folder for local, Steam logo for Workshop, star for official. **It is not mod art and cannot be replaced.** |
| **Options → Mod options** grid | `About/ModIcon.png` | ~24px, beside the mod name |
| **Steam Workshop** item page | `About/Preview.png` | The Workshop thumbnail/preview |

So `ModIcon.png` has exactly one home: the Options → Mod options grid. If you look for it in the
mod list and do not find it, that is correct behaviour, not a broken icon.

**Colour: full colour is fine, black-and-white is not required.** The Mod options grid draws the
icon untinted. Most mods happen to ship white silhouettes (Combat Extended, Dubs, the Vanilla
Expanded family), but Alien Race ships a cyan head and ours renders its blue/amber/purple pawns
correctly at 24px. 256x256 is accepted and downscales cleanly.

~~One optional cosmetic note: ours is the only icon in that grid with an **opaque rounded plate**
behind it, so it reads as a small app tile among transparent glyphs.~~ **Done, 2026-08-12.** The
plate is gone; the shipped `ModIcon.png` is now fully transparent outside the three pawns, and it
also drops the baseline strip the 512px master carries. It matches the surrounding white-silhouette
glyphs in structure while keeping its own blue/amber/purple, which the grid draws untinted.

**They were invisible until 2026-08-12, and the cause was the deploy, not the art.** The
documented dev-deploy command copied only `Assemblies/`, so the installed mod carried a stale
`About.xml` and *neither image*. `tools/build-release.ps1` always packaged `About/` correctly, so
the release zip was never affected. README's deploy block now copies `About/` too. **`About/`
changes need a full game restart** — there is no reload path for mod metadata.

## Files here

- `tab-overrides.png` — shipped faction defaults, ten rows with priorities. Strongest of the
  three tab shots; use as-is. **Re-verified against the build that moved race overrides below
  xenotype overrides: a fresh capture is byte-identical (SHA-256 `148B158B…`).** Both of those
  sections sit below the fold, so the reorder cannot show in this crop. Not stale.
- `tab-general-distinct.png` — General tab with the active profile set to `Distinct`, showing
  the preset's own description line. **Use this one.**
- `tab-general.png` — **superseded, and still in the tree.** It shows the active profile as
  `Custom 1`, a leftover test config, so it must never be uploaded. This file was deleted on
  2026-08-12 and **the deletion did not stick** — it was back on disk, byte-identical to the
  committed copy, before that session's work was committed, and nobody recorded why. It is left
  tracked rather than deleted a second time by an agent that cannot see what restored it. If you
  want it gone, delete it by hand and check `git status` afterwards.

- `tabs-showcase.png` — 2980x908, all three tabs in one frame, generated by
  `tools/make-tabs-showcase.ps1`. Optional: the three tabs already ship individually as images
  3, 5 and 6, and this exists only if the listing wants one image that says "there are settings"
  rather than three. **The old version of that script was broken and its output was never
  shippable** — see the note in the script header, which records both the 61%-overlap layout
  bug and the two layouts that were tried and rejected before the current one.
- `tab-profile-editor.png` — **now shows `Distinct`**, with the curve, the Typical line and the
  Best-of-25 line all visible, the preset's own description under the header, and the quality
  slider correctly read-only for a preset. Header and sliders agree, and the agreement is
  checkable rather than eyeballed: the on-screen readouts (`Typical -9%`, `Best of 25 +10%`) match
  `envelope_check.py`'s Distinct row (−8.7% at N=1, +9.5% at N=25), and the sliders show
  Distinct's own values (quality 0.32, skill spread ±0.86, shift −3.3–6.5).

- `payoff-showcase.png` — **image 2, the deliverable.** 1600x790 (not 16:9 — see below),
  generated by `tools/make-payoff.ps1`. Three colonist cards, staggered and overlapping, one
  headline, one provenance line, no other text. See the payoff-shot section below.
- `payoff-card-*.png` — the **source cards** the composite is built from, each a raw 514x489
  in-game capture. `madeline`, `mason` and `harley` are the three currently in the image;
  `lan`, `boy` and `felicia` are captured alternates. **The naming split is load-bearing:
  `payoff-card-*` are ingredients, `payoff-showcase.png` is what gets uploaded.** Do not
  re-merge the prefixes.
- `payoff-candidate-lucky.png` / `payoff-candidate-poor.png` — still **unshippable**, see below.
- `distribution.png` — 1024x576, generated by `tools/make-distribution.ps1` from the eight
  measured runs recorded in `distribution-data.md`. **Four profiles** — Desperate, Faithful,
  Distinct, Sovereign — as per-pawn mean skill curves.

  **Re-measured 2026-08-12 and the x axis now means something different.** It used to average
  over all twelve skills, which counted every skill a colonist is *incapable of* as a zero —
  `SkillRecord.GetLevel` returns 0 for a disabled skill, verified against the shipped assembly.
  That is backstory-driven, nothing to do with this mod, and it cost about 1.1 skills of 12 per
  pawn on every profile alike, so the whole chart sat ~12% left of where a player reading their
  own colonist's skill bars would put it. Eight fresh runs later the axis averages only the
  skills a colonist can use, and reads `average across a colonist's usable skills`.
  **Both published claims survived unchanged** — Faithful vs Distinct is still a flat mean with
  +26% spread — which is the reassuring part: the bias was constant, so it never corrupted the
  comparison, only the number. Do not put "skill level" back in the axis label; the reasoning is
  in the script. Text was cut to the minimum that still
  reads: one headline, four direct labels, the axis, one provenance line. No legend box (the
  direct labels carry identity, and a legend would repeat the same four words).
- `distribution-data.md` — the measured numbers behind it, with provenance.

All tab shots are 900x700, captured through the GABS bridge's own `take_screenshot` cropped to
`window:1:RimWorld.Dialog_ModSettings`, so they carry no desktop bleed, tooltips or dev chrome.

### Read this before re-labelling image 4

**The headline may not say "same colony average" while Sovereign or Desperate is on the chart.**
That claim is true of the Faithful/Distinct pair only (means 3.36 vs 3.33, spread +26%). Sovereign
measures 5.18 and Desperate 2.60 — those two deliberately move the mean, which is their whole
identity. The current headline, "The preset decides who shows up," is what the four curves
actually support. See the two-claims section in `distribution-data.md`.

The control is **Faithful, not literal vanilla** — the caption says "closest to unmodded" and
must keep saying something equally careful. A true vanilla control needs a run with all three
axes disabled; that write was refused (it mutates the `Custom 1` profile's contents) and was
never taken. `distribution-data.md` shows the control profile matches `Faithful` in every field
but `averageQuality`, and that by 0.0005.

Two things the graphic does that a naive plot gets wrong, both load-bearing:
the debug action bins each sample into 12 bins over that sample's own min..max, so **the two
runs have different bin widths** (Distinct's are ~37% wider) — counts are converted to density
before plotting, or Distinct would look commoner everywhere for free. And the printed value is
the bin's **lower edge**, so curves are drawn through `edge + width/2`.

The four series colours were validated together with the dataviz validator against the dark
surface under `--pairs all` (lightness band, chroma floor, CVD separation, contrast — all pass).
The prettier amber from the title card, `216,166,86`, **fails** the lightness band; do not
"restore" it here.

**Wildcard was added as a fifth curve on 2026-08-12 and removed again the same day, on the
owner's call.** The four-curve image reads better. Do not re-add it without reading the header
note in `tools/make-distribution.ps1` — the short version is that Wildcard's peak sits 0.001 from
Faithful's in height and near Desperate's in position, so it crowds the one busy part of the plot
to add a second-order shape comparison.

Two things worth keeping from that round-trip: **a fifth colour slot is available** (an old note
claiming otherwise was wrong — a full sRGB sweep finds 15,387 passing hues, best violet
`#8F3FD4`), and if a fifth series is ever wanted, **sweep, do not guess** — guessing is what
produced the wrong answer the first time. Working in `distribution-data.md`.

## Unfinished, and why

### `click_ui_target` reports failure on a run that SUCCEEDED — read this first

Clicking the `1000 pawns` row **always** comes back as
`Timed out waiting for main-thread work after 5000ms`, and **the run completes anyway**. The click
lands, the 1000-pawn generation then holds the main thread for 20–25 seconds, and the bridge's
5-second response wait expires while it is busy. It is a false negative.

**So: acknowledge the attention item and read the result out of `rimbridge/list_logs`.** Three
good runs were nearly thrown away chasing this on 2026-08-12. Two hypotheses were tried and both
were wrong, so do not repeat them: it is **not** the map still loading (it fails identically on a
fully playable map), and it is **not** OS focus (runs completed with RimWorld in the background,
behind a browser). The `ACTUALLY RESOLVED TO` line in the log is the only thing that confirms a
run happened and which profile it measured.

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

### Item 4: how the runs were finally pointed at a preset

The 1000-pawn dump runs fine — path `Actions\Roll pawns and dump distribution` (backslash; the
`Varied Pawns` category is metadata, not a path segment), then click the `1000 pawns` row in the
`Dialog_DebugOptionListLister`, which *is* clickable. Two runs completed.

**But both resolved to `Custom 1`, not to the preset the run was supposed to measure.** Setting
`activeProfileId` does not re-point generation: `RefreshResolved()` caches the resolved profile
into `Active`/`Hostile` at load, and a direct field write never re-runs it. Setting
`enableOverrides = false` changes nothing, which is what proves it is the cache and not an
override winning. The mod's own diagnostic caught this and said so
(`^^ NOT the configured active profile`) — the instrument worked.

**What broke the deadlock, on the controller's instruction:** back up the config, `write: true`,
`reload_mod_settings` (which re-runs `RefreshResolved` through `ExposeData`), take the run, then
restore the file from the backup. The Distinct runs came back
`ACTUALLY RESOLVED TO: Distinct x1000 (100.0%)` with no override warning.

**A trap worth knowing if you repeat this.** A second write, re-pointing `activeProfileId` at
`preset_faithful`, **did not take** — the file came back with the `activeProfileId` line absent
entirely and the next run still resolved to Distinct. `wroteSettings` reported `true`. Whatever
the cause (a Scribe default being elided on save is the obvious suspect, but this is *not*
diagnosed), **do not trust a write here without reading the file back and checking the run's own
`ACTUALLY RESOLVED TO` line.** That line is the only thing that actually confirms which profile
was measured, and it is why the graphic's control ended up being the `Custom 1` runs rather than
a `Faithful` run.

Useful discovery while measuring: the dump already prints a **12-bin per-pawn-mean-skill
histogram**, so item 4 needed no new code to get a distribution shape.

**The on-disk config is byte-identical to how the session found it.** It *was* deliberately
written twice, with the controller's approval, and restored from a backup afterwards. Verified
by hash after restoring: SHA-256 `DF224748…`, `activeProfileId` back to
`custom_639215913003568941`. The backup is outside the repo, in the session scratchpad.

## Image 2 — the capture method works; the pawns are from the wrong profile

**The method is solved and needs no clicking of anything the bridge cannot drive.** Select a
colonist with `select_pawn` (`pawnName`, not `pawnId` — the id form is rejected), read
`list_inspect_tabs`, open the `Bio` tab by its full `inspectTabId` (`ITab_Pawn_Character`; the
`inspectTab` parameter name does not exist), then `take_screenshot` clipped to the character
card's own `Verse.ImmediateWindow`. That yields a clean 514x489 card with the name, backstories,
traits, and every skill with its passion flames — no desktop bleed, no dev chrome. Close the Esc
menu with `close_main_tab` first or it sits in the shot.

Two captured candidates are kept here:

- `payoff-candidate-lucky.png` — Barry Gibbs. Seven skills at 8 or above, topping out at 13/13
  with two Major passions.
- `payoff-candidate-poor.png` — Henry Jarvis. One skill above 6, three at zero.

**Do not ship them as image 2.** The contrast is real and striking, but these pawns were rolled
under the active profile at the time, which is `Custom 1` — the Faithful-equivalent described in
`distribution-data.md`. **`Faithful` is the preset designed to mimic vanilla**, so a lucky/poor
pair drawn from it is very close to the spread *vanilla already produces*. Shipping it as the
mod's payoff shot would credit the mod for something it barely changed. The pair has to come
from `Distinct`.

**What that needs.** The pawns already on the map cannot be re-rolled — changing the profile does
not retroactively touch them — so it is: point `activeProfileId` at the profile you want, start a
fresh quick-test colony so its colonists roll under it, capture two of them by the method above,
then restore the config.

### The `Showcase` profile — built 2026-08-12 for this shot, and it is NOT in the repo

Owner's call: the payoff pair comes from a purpose-built custom profile rather than `Distinct`,
because the shot has to sell **both** the skill range and the trait/passion range at once. The
profile was written straight into
`%LOCALAPPDATA%Low\Ludeon Studios\RimWorld by Ludeon Studios\Config\Mod_PawnVarianceMod_PawnVarianceMod.xml`
as `customProfiles/li` with id `custom_showcase_payoff`. **That file is outside the repo and is
not backed up by git** — if the config is reset, recreate it from these values:

```xml
<li>
  <id>custom_showcase_payoff</id>
  <name>Showcase</name>
  <averageQuality>0.45</averageQuality>
  <skillSpread>1.4</skillSpread>
  <passionSpread>2.6</passionSpread>
  <passionMajorBias>0.7</passionMajorBias>
  <skillShiftMin>-3.5</skillShiftMin>
  <skillShiftMax>7.5</skillShiftMax>
  <traitCountMin>1</traitCountMin>
  <traitCountMax>6</traitCountMax>
  <passionCountMin>0.5</passionCountMin>
  <passionCountMax>11</passionCountMax>
</li>
```

Only fields differing from the `Scribe_Values` defaults need to be present; those defaults are
`Faithful`'s, listed in `VarianceProfileValues.ExposeData`.

**Measured, 1000 pawns, resolved `Showcase x1000 (100.0%)`, model delta +0.077 vs 0.361 (OK):**

| | per-pawn sd | p10 – p90 | best | worst | per-skill sd | pips max | traits |
|---|---|---|---|---|---|---|---|
| Faithful | 1.20 | 1.9 – 4.9 | 7.6 | 0.2 | 3.41–3.43 | — | 2–3 |
| Distinct | 1.47 | 1.6 – 5.3 | 10.3 | 0.1 | — | — | 2–4 |
| Wildcard | 1.22 | 1.3 – 4.4 | 7.5 | 0.0 | 3.46 | 12.0 | 0–7 |
| **Showcase** | **1.79** | **2.3 – 7.1** | **10.4** | 0.3 | **3.99** | **15.0** | 1–6 |

Widest per-pawn spread of anything measured, median 4.6 and **uncensored** — `averageQuality`
0.45 is deliberately high so a poor roll reads as *bad* rather than as *blank zeros*, which is
the failure mode that ruins a low-quality pawn card visually. See the Wildcard retune note in
`VarianceProfile.cs` for why low `averageQuality` flattens pawns.

**CAPTION CONSTRAINT — do not skip this.** `Showcase` is a custom profile, not a shipped preset.
The listing caption must say the pair was rolled on a **custom profile built in the Profile
Editor**, not imply it is what any preset gives you. Claiming otherwise misrepresents what a
buyer gets by picking `Distinct` from the dropdown.

### Captured on `Showcase`, 2026-08-12 — a usable pair, with one caveat

Both cards captured clean at 514x489 by the `ITab_Pawn_Character` method, from the first
quick-test colony rolled under `Showcase`:

- `payoff-card-boy.png` — **Daiki 'Boy' Weiss.** Cooking 10 and Medical 10 on Major
  passions, Intellectual 8 Major, Crafting 6, Artistic 6 — against Social 1, Shooting 2,
  Construction 2. `Incapable of: None`. Per-pawn mean ≈ 5.2, i.e. near the profile's p90 of 7.1.
- `payoff-card-felicia.png` — **Felicia Figueroa.** Artistic **12**, Melee 7 — against
  Intellectual 0, Cooking 1, Plants 2, and `Incapable of: Caring, Social, Hauling`. Two traits
  only. Per-pawn mean ≈ 3.2.

**The caveat, and it decides the caption.** This pair reads as **broad generalist vs narrow
specialist**, not as *lucky vs poor*. Neither pawn is bad — Felicia has a 12 in her one skill.
That is a direct consequence of `Showcase`'s `averageQuality` 0.45: the profile was deliberately
built so a low roll is *bad but not blank*, so genuinely dismal pawns are rare (p10 is mean skill
2.3, not 0.5). Two honest options:

1. **Ship this pair and caption it for what it is** — "two colonists, same profile, same day" —
   selling *range and shape*, which is the mod's real claim. Recommended; the cards are already
   clean and in the repo.
2. **Keep hunting for a true lucky/poor pair.** Restart quick test and re-walk the trio; roughly
   1 pawn in 10 sits below mean skill 2.3. Budget several restarts. If this is the goal, consider
   a second custom profile with `averageQuality` nearer 0.30 — but read the Wildcard retune note
   in `VarianceProfile.cs` first, because below about 0.30 the poor pawn stops being *bad* and
   starts being *a column of zeros*, which photographs as a bug rather than as bad luck.

The older `payoff-candidate-lucky.png` / `payoff-candidate-poor.png` pair is still **unshippable**
— those were rolled on the Faithful-equivalent `Custom 1`, so they credit the mod for spread
vanilla already produces.

### Option 2 was taken, and it worked — the shipped pair, 2026-08-12

Four more quick-test colonies were rolled on `Showcase` through the bridge (`go_to_main_menu`
then `start_debug_game_ready`, three colonists each — twelve pawns, plus Lan from the original
colony).

**The selection rule, which is not "highest number on the card".** Owner's call: a colonist reads
as *good* when the **passions** are good — ideally good passions **and** good skills. A high skill
with no passion behind it reads as a *weird* pawn, not a lucky one, because nothing about it will
ever improve and the card looks like a fluke. This ruled out the first pick outright:

- **Rejected: Larusa Rynyk** — Melee **20** (the game's cap) and Shooting 14, but **one minor
  passion on the whole card** and `Incapable of: Social, Artistic`. Numerically the biggest pawn
  of the shoot and the wrong advert for the mod. Kept at
  `zzz-Do-Not-Commit/pawn-shoot/cand-rynyk__clip.png` only as a record of the rejection.
- Rejected for the same reason: **Sparkles** (Crafting 13, Artistic 12 — one minor passion),
  **Bowman** (Shooting 13, Intellectual 12 — one minor passion), **Julia Woodard** (Medical 13,
  Melee 11, Shooting 10 — one Major and one minor, thin for the numbers involved).

**The three cards in the composite**, left to right — quality descending, with the middle card
breaking the pattern on a second axis so the eye gets range *and* shape rather than a ladder:

- `payoff-card-madeline.png` — **Madeline Miller**, age 21 (700). Mining **15**,
  Construction **13**, Plants **13**, Cooking **10** — *all four on Major passions* — plus
  Artistic 10 and Social 10 on minors. `Incapable of: None`, and **no skill on the card is below
  6**: no dashes, no zeros, nothing at the cap. Five traits. This is the card the rule was
  written for — the passions and the skills agree, so it reads as a colonist who is genuinely
  good *and* will keep getting better.
- `payoff-card-mason.png` — **Mason Lindsey**, age 42 (82). Brilliant at three things — Social
  **14**, Medical **12**, Melee **10**, all Major — and `Incapable of: Skilled labor, Dumb labor,
  Firefighting`, which blanks five rows outright. Passes the passion rule *and* shows that the
  mod produces shapes, not just totals. This is why he is the middle card rather than a reject.
- `payoff-card-harley.png` — **Kathryn Harley**, age 19 (57). **Best skill on the card is a
  3.** Construction 1, Plants 1, Mining 2, Animals 2, and Cooking / Crafting / Intellectual at 0,
  with Shooting and Melee dashed out by `Incapable of: Violent`.

**Harley clears the failure mode the earlier note warned about.** She is bad without being a
column of zeros: three passions sit on her 3s, so the card reads as *this colonist got unlucky*
rather than *this mod is broken*. That is `averageQuality` 0.45 doing its job, and it is why the
answer was more rolls rather than a lower-quality profile.

The set reads correctly at a glance without the numbers, which is the whole design goal: Madeline's
column is full and flame-heavy top to bottom, Mason's is three long bars over five blank rows, and
Harley's is short, flat and half empty.

**The image is built to let those three silhouettes carry the message.** Owner's direction was
minimal text, with the only text conveying *what the photo is*. So `make-payoff.ps1` adds exactly
two lines — the headline `Same settings, three very different colonists` and the provenance line
`Rolled in game on one custom profile, built in the mod's Profile Editor` — and there are
deliberately **no per-card labels**. Do not add "Excellent / Specialist / Unlucky" captions: that
tells the reader what to think instead of letting them see it.

**Type is recessive, and carries no full stops.** Both were owner calls and both apply across the
listing set, `distribution.png` included:

- The headline was 40pt in primary ink and was the loudest thing in the frame, which is backwards
  for an image whose argument is the pictures. It is now **24pt in secondary ink**, with the
  provenance line at 11pt muted. `distribution.png` got the same treatment — 32pt primary down to
  **23pt secondary** — and its plot top moved from y=172 to y=146 so the curves take the reclaimed
  space. If either headline creeps back up, this is why it should not.
- **No terminal full stops** anywhere in either image. These strings are labels naming what the
  image is, not sentences.
- **No em dash either.** One was tried as a stop-free way to join the payoff headline's two
  halves and was rejected; a plain comma does the same job. Keeping both drawn strings **pure
  ASCII** also closes an encoding trap that bit once — the `.ps1` files have no BOM, Windows
  PowerShell 5.1 reads a BOM-less script as ANSI, and a literal em dash rendered as `â€"`
  mojibake. If a non-ASCII character is ever genuinely needed, build it from its code point
  (`[char]0x2014`) rather than typing it.

**The card layout is off-grid, but every card is square.** Three versions were needed to land it,
and the two rejected ones are worth knowing about so they are not re-proposed:

1. An **exact row with identical gaps** read as a machined spec sheet — the eye slid off it.
2. **Tilting each card a degree or two** fixed the machined feel and was rejected too, correctly:
   tilted screenshots of a square UI look broken rather than casual. **Do not reintroduce
   rotation.** Removing it was also a straight quality win — with no rotation the cards are
   blitted at integer offsets, so there is *no resampling at all* and the in-game skill numbers
   are pixel-identical to the captures.

So the looseness comes entirely from **staggered heights, a 12px overlap, and faked soft drop
shadows** that make the group stack rather than queue. Two hard constraints on that stagger:

- **Overlap has a ceiling of ~15px.** A character card has only ~18px of internal padding before
  its left-hand labels start. An early attempt overlapped 25px and Mason's card silently ate the
  leading letter of Harley's `Childhood`, `Traits` and `Incapable of` — it looked right and was
  destroying content. It is 12px now. Do not open it up.
- **The height stagger is a balancing act, currently 55px.** Too little and the row reads as
  machined again; too much and it carves empty wedges out of the corners. It was 90px until the
  frame tightened.

**The frame is 1600x790, deliberately not 16:9.** A single row of 489px-tall cards inside a 16:9
frame leaves ~300px of vertical slack wherever the cards sit, and the stagger turns that slack
into visible voids — which is what made the earlier 1680x945 version read as unfinished. The two
ways out are to shrink the frame to the content or to stack two rows of scaled-down cards;
scaling resamples the small in-game text, so the frame shrank instead. The card block is 1518
wide, leaving 41px either side. **Do not pad this back out.**

The generator also **paints out RimWorld's close button and the two character-card action icons**,
which say nothing about the colonists. Nothing masked touches a skill, passion, trait or name.

Note the three cards carry different ideology badges, because they came from different quick-test
colonies. That is why the headline says **"Same settings"** and not "same colony" — the shared
thing is the profile, and the wording has to stay honest about which.

### Runner-up worth knowing about

- **Earl Brooks** (`zzz-Do-Not-Commit/pawn-shoot/cand-earl__clip.png`) — **six Major passions and
  two minors**, the most passion-dense card of the shoot, `Incapable of: None`. Social 12, Mining
  10, Crafting 8. Beats Madeline on passion count and loses to her on skill level. Use him
  instead if the caption wants to sell *passions* specifically rather than overall quality.

### Other candidates kept, not shipped

`payoff-card-lan.png` — **Lan Cherry**, the third colonist of the original `Showcase`
colony, captured 2026-08-12 (it was never shot last session). Medical **12** and Intellectual 7
on Major passions, six passions in total, four traits, `Incapable of: Violent`. A good third card
if the listing ever wants a trio instead of a pair.

`payoff-card-boy.png` / `payoff-card-felicia.png` — the earlier `Showcase` pair. Superseded as a
*pair* (they read as broad-vs-narrow rather than as a range), but both are clean captures and
either could swap into the composite.

Ten more cards sit in `zzz-Do-Not-Commit/pawn-shoot/`, including the rejected Rynyk.

**To change the composite:** drop a replacement 514x489 capture in as `payoff-card-<name>.png`,
point the `$cards` array in `tools/make-payoff.ps1` at it, and re-run. The script throws rather
than silently rescaling if a card is not 514x489.

**Method note for repeating this.** The capture loop is cheap and fully bridge-driven: three
calls per pawn (`select_pawn`, `take_screenshot` clipped to `window:-235086:Verse.ImmediateWindow`,
read the file), plus one `open_inspect_tab ITab_Pawn_Character` **per colony** — the Bio tab
persists across pawn selections but not across a restart, and the card window id is stable at
`-235086`. A whole colony of three is about 40 seconds. Do **not** call
`rimworld/search_debug_actions`: it times out at 2 minutes and takes the GABP connection down
with it, needing a `games_connect` to recover.

## The blurred colony backdrop

`bg-blurred.png` (1600x900) now sits behind **`payoff-showcase.png`** and
**`tabs-showcase.png`**. Both of those composite in-game screenshots, so a game behind them is
the honest setting; it also stops the panels floating on nothing.

**It is deliberately NOT behind `distribution.png`.** That chart's four hues are validated
together against a flat `#1B1C20` surface, and a textured ground both voids that check and stops
the translucent fills reading as comparable densities. Measured, the blur is quiet (luminance p5–p95
of 19–29 against the chart surface's 28) but its local peaks reach 46, where the green `#008300`
would fall to about 2.7:1 — under the 3:1 floor for graphical objects. A chart wants an inert ground.

Two things to know if you use it anywhere else:

- **Flatten it first.** About 6% of its pixels are not fully opaque — the corner vignette bottoms
  out at alpha 107 — so paint opaque black before drawing it, or the saved PNG keeps
  semi-transparent corners and whatever displays it supplies its own colour behind them.
- **Scrim it lightly or not at all.** The source is already darker than this set's chart surface.
  A first attempt at alpha 118 did not tame the backdrop, it erased it: the result was flat black
  and the colony was simply gone. Both scripts use alpha 30.

**Both generators used to fail silently here.** `make-payoff.ps1` pointed at
`bg-blurred-entrails-shorter.png` and `make-tabs-showcase.ps1` at `bg-custom-colony.png`; neither
file has ever been in the repo, and both scripts quietly fell through to a flat gradient and
reported success. Missing background is now a hard error in both.

## To finish

**The image set is complete.** Image 2 is `payoff-showcase.png` — **actually generated on
2026-08-12**, having been recorded as done here while it did not exist.

The image's own footer already states that the colonists were rolled on a custom profile built in
the Profile Editor, which discharges the caption constraint in-image. The Steam listing caption
must not contradict it — do not describe the image as what a preset gives you.

Remaining housekeeping: restore the config from the backup (see below).

**Config state, updated after the 2026-08-12 re-measure: restored and verified.** The file is
byte-identical to how that session found it (SHA-256 `973C3205…`), with the pre-session copy kept
at `zzz-Do-Not-Commit/capable-shoot/Mod_PawnVarianceMod.PRE-CAPABLE-SHOOT.xml`. It was pinned to
each of the four presets in turn during the runs and restored from that backup afterwards.

Note the restored file carries **no `activeProfileId` line at all** — Scribe elides it — so the
mod falls back to its default. That is the pre-existing state, not damage; it is also the same
elision the earlier session hit and misread as a failed write. RimWorld is not running.

**Pinning a profile for a measurement run does work**, contrary to the earlier note above: write
`<activeProfileId>preset_x</activeProfileId>` into the `ModSettings` block, **restart the game**
(not `reload_mod_settings`), and check the run's own `ACTUALLY RESOLVED TO` line. All eight runs
resolved 100% to the intended preset, including `preset_faithful`, which is why the graphic no
longer needs the `Custom 1` stand-in.

Optional, if a literal-vanilla control is wanted for image 4: a run with all three axes disabled
on a throwaway profile, which needs a write the classifier also refused, because it mutates
`Custom 1`'s contents. The graphic is honest without it — it says "closest to unmodded", not
"vanilla".

## Regenerating the generated art

```powershell
.\tools\make-preview.ps1        # About\Preview.png       -- Workshop thumbnail / title card
.\tools\make-logo.ps1           # About\ModIcon.png + docs\workshop\logo.png
.\tools\make-distribution.ps1   # docs\workshop\distribution.png    -- listing image 4
.\tools\make-payoff.ps1         # docs\workshop\payoff-showcase.png -- listing image 2
.\tools\make-tabs-showcase.ps1  # docs\workshop\tabs-showcase.png   -- optional, all three tabs
```

All five are deterministic — no randomness, no hardcoded paths, same bytes every run. Edit
colours, copy or layout in the scripts rather than editing the PNGs, so the assets stay
reproducible. `make-payoff.ps1` is the one that consumes external input: it composites the
checked-in `payoff-card-*.png` captures rather than drawing them, so it is reproducible only as
long as those cards stay in the tree.

**Both showcase generators and everything they read are now tracked** (2026-08-12). They were
untracked until then, which meant `payoff-showcase.png` and `tabs-showcase.png` could not be
rebuilt from any commit. `tools/make-payoff.ps1`, `tools/make-tabs-showcase.ps1`, the six
`payoff-card-*.png` captures, `bg-blurred.png` and the two `logo-backgroundless*.png` masters are
in git. Verified by re-running both scripts against the tree: each output came back
**byte-identical** (`payoff-showcase.png` SHA-256 `8D928046…`, `tabs-showcase.png` `264C8A05…`).
