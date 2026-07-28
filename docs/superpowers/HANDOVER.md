# Handover — Pawn Variance Mod

Date: 2026-07-28 (rewritten — the previous version of this file predated the additive trait
model and the profile system, and its open-issues list was almost entirely stale)
Repo: `C:\Users\gokal\Desktop\Rimworld-mod\Rimworld-Pawn-variance-mod`
Branch: `main` (working directly on main, no worktree, per user's choice)

## What this project is

A RimWorld Harmony-patched C# mod adding continuous, quality-driven variance to pawn
skills/traits/passions at generation time, a Biotech growth-up patch for children who age to
adulthood, and a cosmetic "quality tier" hover tooltip.

- Original design spec: `docs/superpowers/specs/2026-07-27-pawn-variance-mod-design.md`
- Additive trait model spec: `docs/superpowers/specs/2026-07-28-additive-trait-model-design.md`
  (supersedes the trait-generation portion of the original)
- Plans: `docs/superpowers/plans/` (both marked complete)

## Guiding philosophy (read before touching trait/skill/passion code)

The user's stated intent, verbatim: **"What this mod achieves to do is while still using the
same systems, lets the user change the randomness values."** Prefer calling vanilla's own real
generation methods and exposing their random inputs as settings, over inventing a parallel
algorithm that approximates vanilla. Traits and passions both delegate to vanilla now; skills
already did.

The standing "keep auditing PawnGenerator for more delegation opportunities" request is
**closed** — five agent passes plus this session's work covered redress, passion trait/gene
rules, gene passion-mod erasure, gene-forced-trait `sourceGene` loss, backstory-forced traits,
request-forced traits, sexuality traits, name generation, and patch-ordering safety. Treat this
area as a clean slate rather than assuming more bugs are waiting.

## Current status: everything implemented, playtested, committed and pushed

`dotnet build` clean (0 warnings/errors), deployed to the user's Mods folder, and validated
across four in-game logs totalling **778 traced pawns** with zero exceptions, zero ordering
violations and zero crashes.

Working tree is clean and `main` is pushed to `origin`. **Standing rule: this repo's code is
committed only when the user explicitly asks** — do not commit unprompted just because work is
finished.

## This session's work

### 1. Profile system (`Source/VarianceProfile.cs`, new)

Six profiles: **Custom** plus five presets — **Faithful** (vanilla-like), **Distinct** (the
mod's original defaults), **Wildcard** (max spread), **Gifted** (power fantasy), **Desperate**
(grim). Only Custom is editable; preset sliders render read-only via `GUI.enabled = false`, with
a "Copy this profile to Custom" button to fork one.

Key design points, all deliberate:
- `VarianceProfileValues` holds only generation-tuning fields. The housekeeping toggles
  (`applyToHostilePawns`, `applyVarianceOnGrowUp`, `verboseLogging`, `showQualityTier`) sit
  outside the profile system so switching profiles never silently changes them.
- Custom's values scribe under the **pre-profile node names**, and `activeProfile` defaults to
  `Custom`, so an existing settings file loads intact into Custom. No migration needed, and the
  old "click Reset to Defaults after updating" advice is now actively wrong — it would discard
  the user's tuning.
- `PawnVarianceSettings` keeps its flat public fields as the *effective* values mirroring the
  active profile, so no applier code needed changing.
- A constructor calls `ApplyActiveProfile()` because a fresh install never reaches
  `PostLoadInit`.

Verified in-game: switching, copying to Custom, and persistence across save/quit all work.

### 2. Settings UI restructure (`Source/PawnVarianceSettings.cs`)

User reported sliders bleeding into each other. Now split into titled sections (Overall quality
/ Skills / Traits / Passions / General), each with gap + rule + medium-font heading. The min/max
pairs were the worst offender — one label between two identical sliders — so each slider carries
its own "Lowest-quality pawn" / "Highest-quality pawn" label with the range as a dimmed caption
above the pair.

### 3. Two real passion bugs found and fixed

Both found by adding a **verbose-logging passion trace** (gated behind `verboseLogging`) that
dumps per pawn: raw vs shown level per skill, exclusions and why, rolled Major/Minor counts,
forced-trait grants, gene bumps, and where the budget ran out. This diagnostic is the single
most useful tool in the repo for this area — use it before theorising.

**Bug: passion queue ordered by the wrong level.** Vanilla's walk uses
`GetLevel(includeAptitudes: false)` (decompiled `PawnGenerator.GenerateSkills` ~line 1911) — the
raw learned level. We used `SkillRecord.Level`, which *includes* Biotech aptitude bonuses, so an
aptitude gene both jumped the queue and got its separate `passionMod` bump. Fixed.

**Bug: gene passion bump stacked on top of the walk.** Vanilla applies `GeneDef.passionMod`
`AddOneLevel` in `Pawn_GeneTracker.AddGene`, which runs *before* `GenerateSkills` — so the walk
overwrites it, and a gene-granted Minor becomes Major only if the skill ranks high enough on its
own. We re-applied the bump *after* our walk, promoting low-ranked skills to Major. Fixed to
restore-only-if-`None`, which reproduces all three vanilla outcomes.

Confirmed decisively in a Yttakin-heavy raid: **126 `AptitudeRemarkable_Animals` carriers, 32 of
which had the walk assign Animals a Minor, and 0 promotions** — versus 13 promotions out of 35
carriers before the fix.

### 4. Passion budget floor (user decision)

Vanilla's budget is `5 + clamp(Gaussian, -4, 4)`, so it bottoms out at 1 and every adult gets at
least one passion. Our lower mean plus settable spread let the tail land under 1 and produce
passionless pawns. Now floored to 1 pip — **except** when `passionCountMin` is 0 (an explicit
request for passionless pawns; Wildcard and Desperate rely on this) or when
`alreadyCommittedPips > 0` (grow-up top-up, pawn already has passions). Verified: Distinct
floored 5 with zero passionless pawns; Wildcard/Desperate produced 18 and 45 untouched.

### 5. Preset retuning from a 352-pawn four-profile comparison

- **Gifted** `skillShiftMax` 12 → **8**. At 12, top skill per pawn averaged 19.4 and 29 slots
  clipped the level cap, so gifted pawns stopped being distinguishable at the high end. This was
  also the likely cause of the old "skills hitting 20" report.
- **Desperate** `skillShiftMin` -10 → **-8**. At -10, mean skill level was 0.54 — most skills on
  most pawns floored at 0, leaving colonies unable to do skilled work at all.

### 6. Two known divergences from vanilla, deliberately kept (user decision)

Do not "fix" these without asking:
- **Our walk skips skills that already have a passion; vanilla does not.** Vanilla re-visits
  them and spends a second unit of budget on a skill that already has one. Ours spreads the same
  budget across more skills. Matching vanilla here would break grow-up, where the skip is what
  protects growth-moment passions.
- **Vanilla's forced-passion pass can push `minorPassions` to -1** (the `force` branch
  decrements unconditionally), silently cancelling a later Minor. Ours clamps at 0.

## Testing status

Validated across four logs (`temp/temp_log_output*.md`, gitignored): 70 + 152 + 204 + 352 pawns.
Zero exceptions, zero ordering violations, no VTE crash, quality means 0.496–0.535 against a
0.50 setting. Profile behaviour matches preset definitions to two decimals on budget means.

### NOT tested — item 1 is the highest priority for the next session

**1. Child / Biotech developmental content has had no coverage at all.** Everything above was
adult raid pawns. Specifically untested:

   a. **`GrowthUpPatch` genuine Child→Adult transition** — a child aging up in a real colony. The
   patch tracks each pawn's own last-observed `DevelopmentalStage` in an in-memory dictionary to
   tell a real transition from vanilla's load-resync noise (`Source/GrowthUpPatch.cs:39-52`).
   Confirm variance applies exactly once, at the real moment.
   b. **`alreadyCommittedPips`** — a child who accumulated growth-moment passions should be topped
   *up* toward the quality budget, not rerolled. Verify existing passions survive and are not
   downgraded. The verbose trace prints `committed pips N` — use it.
   c. **`TraitAgeCap.MaxRolledTraitsFor`** — caps rolled traits by growth birthdays
   (`GrowthUtility.GrowthMomentAges`, default 0 below age 7, 1 at 7-9, 2 at 10-12, 3 from 13).
   Verify a young child does not receive an adult trait load, and that the cap stops binding
   after the last growth moment.
   d. **`applyVarianceOnGrowUp` toggle off** — confirm it actually suppresses the grow-up path.
   e. **Generation of child pawns** — `GeneratePawn_Postfix` early-returns for any non-Adult
   `DevelopmentalStage` (`Source/HarmonyPatches.cs:27`). Confirm children generate untouched.
   f. **The growth-moment letter** — `ChoiceLetter_GrowthMoment` choices should be unaffected.

### Other untested areas, in suggested order

2. **`countProtectedTraits` = on.** Only the default off-path has been exercised in play. This
   is the branch that changes what the trait sliders *mean* (total traits vs traits this mod
   rolls), so it deserves direct confirmation rather than inference.
3. **Write a trait diagnostic.** Traits have **no equivalent** to the passion trace. That trace
   is what turned two hand-waved theories into two confirmed, decompile-verified bugs in a
   single pass — reading the code had failed to find either. If trait counts ever look wrong
   again, the first move is writing the diagnostic, not reasoning about the code. Worth doing
   **before** the child testing above, since grow-up trait behaviour is exactly what that
   testing inspects. Model it on `PassionVarianceApplier`'s trace: gate on `verboseLogging`,
   build a `StringBuilder`, and dump per pawn the protected-trait set and its source
   (kindDef / gene / backstory / request / scenario), the age cap from
   `TraitAgeCap.MaxRolledTraitsFor`, the quality-derived target, how many were actually rolled,
   and the final list.
4. **`applyToHostilePawns` = off** — cheap toggle, never exercised. Raiders should generate
   completely untouched.
5. **Running without Biotech active.** Several code paths are gated on `ModsConfig.BiotechActive`
   (gene passion mods, gene-forced traits, the grow-up patch, the grow-up settings checkbox).
   None have been run with the DLC disabled.
6. **Redress on a long save.** A world pawn reused by `GenerateOrRedressPawnInternal` must not
   be re-rolled — this is the entire reason the patch targets the private
   `GenerateNewPawnInternal` rather than public `GeneratePawn`. Verified by decompile, never
   directly observed in play. Needs a save with a large world-pawn pool (the redress chance
   climbs from 2% toward 80% as that pool grows), so it only shows up in a mature colony.
7. **Tier tooltip alignment** on the colonist bar / character card — never confirmed by the user
   across any session. Note the user has said this feature may be cut entirely, so don't invest
   in polishing it; just confirm it isn't visibly broken.

## Build & deploy loop

```bash
cd "C:\Users\gokal\Desktop\Rimworld-mod\Rimworld-Pawn-variance-mod"
dotnet build Source/PawnVarianceMod.csproj
cp Assemblies/PawnVarianceMod.dll Assemblies/PawnVarianceMod.pdb \
   "/c/Program Files (x86)/Steam/steamapps/common/RimWorld/Mods/PawnVarianceMod/Assemblies/"
```

- The copy **fails if RimWorld is running** (DLL locked). Guard with
  `tasklist //FI "IMAGENAME eq RimWorldWin64.exe"` first.
- `Source/PawnVarianceMod.csproj` uses MSBuild `RimWorldDir`/`HarmonyModDir` properties pointing
  at the user's real Steam install. Override with `-p:RimWorldDir=...` on another machine.

## Decompiling vanilla

`ilspycmd` works if pinned to an older version (the default install fails on bad package
metadata):

```bash
dotnet tool install -g ilspycmd --version 8.2.0.7535
export PATH="$PATH:$HOME/.dotnet/tools"
ilspycmd -t Namespace.TypeName -r "<RimWorld>/RimWorldWin64_Data/Managed" \
  "<RimWorld>/RimWorldWin64_Data/Managed/Assembly-CSharp.dll" > out.cs
```

Existing decompiles live in `zzz-Do-Not-Commit/decompile/` (gitignored via
`.git/info/exclude`), including `PawnGenerator.cs`, `SkillRecord.cs`, `Pawn_GeneTracker.cs`,
`PassionMod.cs` and the Vanilla Traits Expanded patches. Use these first — every fix this
session was verified against real decompiled source, not guessed.

## Explicit user decisions (don't re-litigate)

- Trait degree: real-degree fallback (`FirstValidDegree`) over vanilla's hardcoded 0, kept even
  where it diverges from vanilla — this was an in-game-verified bug fix.
- Ideology disallowed-traits: dropped entirely.
- Tier label: hover tooltip, no save-file footprint, low investment.
- Custom profile starts as a copy of Faithful and is the profile on first launch.
- Passion budget floor: conditional on `passionCountMin > 0` (see §4).
- The two vanilla divergences in §6 stay as they are.
- Combat Extended duplicate-`packageId` error in the logs is pre-existing and unrelated — ignore.

## Commit history and git identity

All work is committed. The session's five commits, most recent last:

```
9d86840 chore: add .gitignore
d1dd4b9 fix: reconcile traits in place instead of clearing and rebuilding
b9bdc0f feat: variance profiles and a restructured settings page
c0df509 fix: passion queue ordering, gene passion-mod stacking, and a budget floor
77a7946 docs: rewrite handover against current state
```

Note the intermediate commits do **not** individually build: `PawnVarianceSettings.cs` carries
changes from two workstreams (the additive model's `countProtectedTraits` and the profile
system) and splitting one file's changes across commits was judged riskier than the benefit.
The final state builds clean.

All 34 commits were rewritten to a single identity — `kalas1230 <gokalpxd@gmail.com>`, author
and committer — via `git filter-branch`, so every hash from the initial commit onward differs
from what any older clone or notes may reference. `user.name`/`user.email` are set
**`--local`** so other projects on this machine keep their own identity. Backup refs
(`refs/original/`, the `pre-email-rewrite-backup` tag) have been deleted and the old
`gokalp.albayrak@ug.bilkent.edu.tr` identity no longer appears anywhere in the object database.

`temp/` and `zzz-Do-Not-Commit/` are ignored — via `.gitignore` and `.git/info/exclude`
respectively — so playtest logs and decompiles never enter the repo.

## How to resume

1. Read this file. The passion/trait areas are in good shape and well evidenced — resist
   re-auditing them.
2. `git log --oneline -10` and `git status` to confirm nothing moved.
3. **Start with the child/growth-up test list above** — it is the only substantial untested
   surface left, and `GrowthUpPatch` is the most intricate code in the mod.
4. Same loop as always: user reports behavior → diagnose via decompile + the verbose trace, not
   guessing → fix → rebuild → deploy → user retests → commit only when explicitly asked.
