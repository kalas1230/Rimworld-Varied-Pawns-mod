# Problem Register — Varied Pawns audit, 2026-08-09

**Purpose.** A standing register of *defects and risks found*. Each entry states what is wrong, why
it is wrong, when it bites, and what was actually verified versus inferred. Same contract as
[`AUDIT-2026-08-06-problem-register.md`](AUDIT-2026-08-06-problem-register.md), which is **closed** —
every row in its index is ✅ or ⛔. This file starts a new numbering series (`Q-nn`) so the two
registers can never be confused; nothing here re-reports a `P-nn` entry unless it has **recurred**,
and where that happens the entry says so.

**Ground state this audit was taken against**

| | |
|---|---|
| Branch | `main` |
| `HEAD` | `6c93279` (`fix: close every remaining finding in the 2026-08-06 audit register`) |
| Working tree | **NOT clean** — 3 files modified (`HANDOVER.md`, `Source/Constants.cs`, `docs/AUDIT-2026-08-06-problem-register.md`), 120 insertions / 57 deletions |
| `envelope_check.py` | **PASS** — Rule 1 and Rule 2 at N = 1, 5, 25, 50; `Source/EnvelopeFigures.g.cs: unchanged`; exit 0. Run at the start of this audit, against this tree. |

> [!NOTE]
> **Branch and tree state are recorded as context, not as a finding.** The previous register opened
> with P-01 (Major) on uncommitted and unpushed work. That does not apply here: the owner is the
> only person working on this repo, the mod is unreleased, and pushing is explicitly not a goal.
> **Do not file tree state as a defect in this or any future register.**

**How to read severity**

| Level | Meaning |
|---|---|
| **Critical** | Wrong pawns, lost data, or a crash on a normal path. |
| **Major** | Wrong numbers a player or a future agent will act on; or a live process risk. |
| **Minor** | Real but bounded — bad hygiene, latent trap, cosmetic-to-player. |
| **Cosmetic** | Wording, naming, tidiness. |

**Confidence** is stated per finding, along with what could *not* be checked. Every line citation in
this file was opened and read before being written down — the two occasions where an unchecked
citation sent someone editing the wrong code are exactly what **Q-02** is about.

**Method.** Five parallel read-only sweeps (scoring/model mirror, generation path, growth and
persistence, UI and tooling, docs-vs-code) plus an independent adversarial pass, each required to
cite `path:line` and to report what it checked and found clean. Every finding below was then
re-verified by hand against the tree, and every candidate Major was **measured numerically** rather
than argued (Q-01, Q-03, Q-14). The measurements earned their place: one downgraded a finding from
Major to Minor (Q-03, measured at 0.00pp), one replaced an adversarial estimate that was off by
~40% (Q-14), and one turned up an in-game measurement already sitting in `HANDOVER.md` that
confirms a finding nobody had connected to it (Q-14 again).

---

## Index

| # | Area | Finding | Severity |
|---|---|---|---|
| [Q-01](#q-01) | Scoring | The dispersion model ignores `enableSkillVariance` / `enablePassionVariance`, so every dispersion-aware readout scores an axis the generator does not roll — up to **18.3pp** | **Major** — ✅ **FIXED 2026-08-09** |
| [Q-16](#q-16) | Scoring | `CalculateCompositeScore` zeroes the weight of a disabled axis, making both of its own vanilla fallbacks dead code — the root cause under Q-01 | **Major** — ✅ **FIXED 2026-08-09** |
| [Q-14](#q-14) | Scoring | The passion spend loop discretizes the budget into whole purchases and discards the remainder; no model side does — **1.60pp** on the enforcing metric | **Major** — ⏳ open |
| [Q-02](#q-02) | Docs | Six `file.cs:NNN` citations in `HANDOVER.md` point at the wrong line; two are off by a consistent 66 | **Major** — ✅ **FIXED 2026-08-09** (a seventh was found and fixed in the same sweep) |
| [Q-03](#q-03) | Scoring | The `Typical` readout divides a dispersion-aware numerator by a mean-band denominator — two different estimators of the same quantity | **Minor** (measured: **0.00pp** today) |
| [Q-04](#q-04) | Model drift | `CalculateCompositeScore` omits the vanilla passion floor that all four dispersion sites carry | **Minor** |
| [Q-05](#q-05) | Model drift | Six constants that enter the score are outside the golden-file drift check | **Minor** |
| [Q-06](#q-06) | Model drift | The ±4σ truncation window is hardcoded in two mirrors and derived from the constant in the third | **Minor** |
| [Q-07](#q-07) | Docs | The two documents modified in the same uncommitted batch disagree about which file is next for review | **Minor** |
| [Q-08](#q-08) | Docs | The uncommitted `Constants.cs` diff deletes the only surviving record of the passion-spread rescale figures | **Minor** |
| [Q-09](#q-09) | Comments | The verify gate's tolerance rationale describes an integrator that no longer exists | **Cosmetic** |
| [Q-10](#q-10) | Packaging | `LoadFolders.xml` still maps `v1.5`, which `About.xml` deliberately dropped | **Cosmetic** |
| [Q-11](#q-11) | Naming | Dead identifiers (`passionNoise`, `skillNoise`, `MinMagnitudeFloor`) survive in comments and docs | **Cosmetic** |
| [Q-12](#q-12) | Comments | `VarianceProfile.cs:31` cites `PawnVarianceSettings.cs:1096/1106`; the real sites are `:1171/:1181` | **Cosmetic** — ✅ **FIXED 2026-08-09** |
| [Q-15](#q-15) | Growth | A throw anywhere in the life-stage postfix locks that pawn out of adult variance permanently, because the stage is recorded before the work | **Minor** |
| [Q-13](#q-13) | UI | The curve draw allocates an array and sorts already-sorted data every frame | **Cosmetic** |

*(Numbering is by discovery order; the index is grouped by area. `Q-14` and `Q-15` came from the
adversarial pass, which ran last.)*

### What was checked and found clean

Recorded so this audit is not silently re-run. Verified against **this** tree:

- **The generation path is clean as an implementation of its own stated intent.** All eight files
  (`HarmonyPatches`, `QualityRoller`, the three appliers, `TraitAgeCap`, `TraitProtection`,
  `TraitTrace`) were read in full and every invariant the handover claims was checked against code.
  **This is not a claim that the generator matches the model** — Q-01 and Q-14 are both cases where
  the generator is right and the *model* is missing something the generator does. Specifically
  confirmed: the passion floor condition
  reads exactly `budget < 1f && v.passionCountMin > 0f && alreadyCommittedPips <= 0f`
  (`PassionVarianceApplier.cs:76`); `gene.passionPreAdd` is now snapshotted **before** the bump
  (`PassionVarianceApplier.cs:281` ahead of `:283-288`), i.e. P-15 has not regressed; the null
  guards added for P-16 are present and — the part that matters — `Apply` and its delegate
  `AssignPassions` carry the **byte-identical** guard `pawn?.skills?.skills == null ||
  pawn.story?.traits == null` (`PassionVarianceApplier.cs:40` and `:56`), so the "narrower guard at
  a mutating entry point" failure that the first P-16 attempt introduced cannot recur; quality is
  rolled once (`HarmonyPatches.cs:55`) and shared by all three appliers; `Shift` is private and
  reachable only through `ShiftAroundBand` / `ShiftWithinBounds`.
- **Growth, settings and persistence are clean.** `Scribe.ForceStop()` is in a `finally` on both
  transfer paths (`SettingsTransfer.cs:53-60`, `:128-132`); import parses into a throwaway object
  and commits only on full success (`:108-141`); the pending-growth component's parallel lists drop
  together with a logged warning on a count mismatch (`GrowUpPendingComponent.cs:45-51`) and prune
  null pawns after load (`:53-58`); `Deregister` is idempotent and no path re-adds a removed pawn,
  so double-processing is impossible; a dangling profile id falls back to a **clone**
  (`PawnVarianceSettings.cs:240-244`), i.e. P-14 has not regressed; the delete path scrubs
  `activeProfileId`, `hostileProfileId` and all three override maps.
- **`applyVarianceToChildren` and `applyChildSkillShift` are `false` in all three places each** —
  field initialiser, `Scribe` default, and `ResetToDefaults` (`PawnVarianceSettings.cs:24`, `:409`,
  `:1301`; `VarianceProfile.cs:69`, `:163`). No preset overrides either. Rule 4 holds.
- **Every range control's tooltip states which kind of range it is**, and the Row 3 power readout
  keeps its exclusion clause (`ProfileEditorTab.cs:289-293`, `:394-400`, `:420-428`, `:482-490`,
  `:513-518`). The slider double-guard pattern is followed by every editable control in the file.
- **Slider bounds match their downstream clamps exactly** — skill spread against
  `MaxMagnitude / √6`, passion spread against `PassionBudgetSpreadMax`, passion budget against
  `MaxPassionPips` (`ProfileEditorTab.cs:358-361`, `:441`, `:481` vs `VarianceProfile.cs:129-131`,
  `:149-150`). The 24-pip ceiling is gone from the UI as well as the maths.
- **The Beta cache is correctly invalidated** by the only control that moves its input
  (`ProfileEditorTab.cs:245-256` → `MarkDistributionParamsDirty()`), and the class re-invalidates on
  `Clone()`, `ClampAndSwap()` and `ExposeData()`. P-28 has not regressed.
- **The Best-of-N cache key is complete** — every field `Moments`/`BuildCdf` read is in the key
  (`PawnVarianceSettings.cs:1533-1544`), *including* the two enable flags. Q-01 is not a cache bug;
  the flags are cached and then never consulted.
- **The C#/Python mirror is faithful everywhere the two sides both model something**: composite
  formula, skill-norm clamp chain, capacity cap, pip efficiency and its bias-1.0 anchor, Beta
  α/β derivation, the `QualityClampEpsilon` domain clamp, and the `sd > 1e-12` guard. The
  `SkillNoiseScalar`/`PassionNoiseScalar` round-trip reduces exactly to `skillSpread·√6` and
  `passionSpread`, matching the Python side with no drift.
- **The vanilla passion floor is identical in all four places it is written** —
  `PassionVarianceApplier.cs:76`, `DispersionModel.cs:101`, `envelope_check.py:282-283`,
  `dispersion_mc.py:111-112` — with the generator's extra `alreadyCommittedPips <= 0f` being the one
  legitimate, documented difference. The 2026-08-08 fix held. (But see **Q-04** for the fifth site
  nobody counted.)
- **`EnvelopeFigures.g.cs` is not stale**: every `Gen*` constant matches the live `Constants.cs`
  value, and every figure in the pasted envelope table in `HANDOVER.md` matches the `Scores` array
  to four decimals across all 8 presets × 4 batch sizes, including the headroom figures.
- **`Roll pawns and dump distribution` resolves each pawn's actual profile** via
  `settings.ValuesFor(pawn, request)` and flags a mixed sample (`DebugActions.cs:632-636`,
  `:696-709`) — the 2026-08-07 defect there is genuinely fixed. Its mean/sd/quantile/histogram
  arithmetic is correct (`:758-804`).
- **Mandatory rules 1–8 are uniquely numbered** and every in-document `Rule N` cross-reference
  points at the rule it means. P-04 has not recurred.
- **`verboseLogging` is gated on `Prefs.DevMode`** (`PawnVarianceSettings.cs:1221`) and the rethrow
  requires *both* it and dev mode (`HarmonyPatches.cs:69`). P-17 is closed and the gate is real.

### Where to start

The register is ordered by area, not priority. If these are worked rather than filed:

> [!NOTE]
> **Superseded 2026-08-09.** Q-01 (with Q-16 under it), Q-02 and Q-12 are fixed; the advice below is
> kept because its *reasoning* about sequencing is what the remaining work still follows. The one
> correction worth carrying forward: this list said to fix Q-01 and Q-14 "in one edit". That would
> have been a mistake. Q-01 moves no published figure and Q-14 moves all of them, so landing them
> together means the golden-file regeneration cannot be attributed to either one. They were split,
> Q-01 first, and the `EnvelopeFigures.g.cs: unchanged` line is what proves Q-01 was figure-neutral.
> **Q-14 is now the only open Major.**

1. ~~**Q-01 and Q-14 together**~~ — see the note above. Q-01 is done; **Q-14 is next**, and it is
   the entry that still puts a wrong number in front of the player (1.60pp re-measured, against a
   gate built to catch 0.5pp). **Q-14 moves shipped figures**, so Rule 6 applies: re-run
   `envelope_check.py`, regenerate `EnvelopeFigures.g.cs`, repaste every table.
2. ~~**Q-02**~~ — done, and the reasoning held: the Q-01 work read those citations.
3. **Q-04, Q-05, Q-06** as one sweep. They are three faces of the same question — *what does the
   verify gate actually prove?* — and fixing them piecemeal means re-deriving that answer three
   times. None of them moves a shipped figure.
4. **Q-03** is deliberately *not* in the urgent list, and the reason is worth reading: it was
   measured, found to be worth `0.000pp` on all eight presets, and downgraded from Major to Minor on
   the measurement. It is a trap for a future retune, not a live defect. Fix it when Faithful is next
   touched, not before.
5. **Q-07 and Q-08** should be resolved *before the working tree is committed*, since both are
   defects in the uncommitted diff itself.

> **In-game exercise status.** ⚠️ **Nothing in this register has been exercised in game.** Every
> finding is argued from source, and the three measured ones (Q-01, Q-03, Q-14) were measured
> **offline**,
> from scratch scripts driving the shipped `envelope_check.py` (Q-01, Q-03) or a verbatim port of the
> generator's own spend loop calibrated against it (Q-14). All three therefore carry a version of the
> blind spot Q-01 describes: they measure models and ports, not rolled pawns. Q-01 and Q-14 both
> predict specific, checkable in-game symptoms. Q-01's has not been run — its entry gives the exact
> profile configuration. **Q-14's already has been**, unknowingly: the 1000-pawn dump quoted at
> `HANDOVER.md:111-113` measures precisely the gap it describes, agreeing with the simulation to
> ~0.04 pips. See its entry.

---

<a id="q-01"></a>
## Q-01 — The dispersion model ignores the per-axis enable toggles, so the readout scores an axis the generator does not roll

**Severity: Major.  Confidence: high (structure observed; magnitude measured offline, not in game).**

### What

`VarianceProfileValues` has two per-profile switches, both live checkboxes in the Profile Editor
(`ProfileEditorTab.cs:352` "Skills", `:430` "Passions"), defaulting to `true`
(`VarianceProfile.cs:77`, `:79`).

**Pawn generation honours them.** `HarmonyPatches.cs:59-60`:

```csharp
if (v.enableSkillVariance) SkillVarianceApplier.Apply(pawn, quality, v);
if (v.enablePassionVariance) PassionVarianceApplier.Apply(pawn, quality, v);
```

— and so does the grow-up path (`GrowUpVariance.cs:68-69`).

**`CalculateCompositeScore` honours them,** carefully and with a documented rationale.
`PawnVarianceSettings.cs:1416-1417`, `:1441-1443`, `:1492-1493`:

```csharp
float skillNorm = 0.25f;
if (v.enableSkillVariance) { … }
…
float passionNorm = Constants.VanillaPassionBudget
    * PassionPipEfficiency(Constants.VanillaMajorBias) / Constants.MaxPassionPips;
if (v.enablePassionVariance) { … }
…
float wS = v.enableSkillVariance ? Constants.CompositeSkillWeight : 0f;
float wP = v.enablePassionVariance ? Constants.CompositePassionWeight : 0f;
```

**`DispersionModel` does not.** A grep for either identifier across `Source/DispersionModel.cs`
returns **zero hits**. `Moments` (`DispersionModel.cs:57-112`) opens with the weights taken
unconditionally from `Constants`:

```csharp
float wS = Constants.CompositeSkillWeight;
float wP = Constants.CompositePassionWeight;
```

and then builds both axes from the profile's bands regardless of whether either is switched on.
`docs/tools/envelope_check.py`'s `grid_moments` (`:246-299`) has the identical omission — a grep for
the flags there also returns zero — and its docstring for `make_composite` states the assumption
outright at `:164`: *"Presets all enable both axes."* That is true of presets and false of the
custom profiles the editor exists to create.

Everything dispersion-aware flows through `Moments`: the **`Typical`** readout
(`ProfileEditorTab.cs:278` → `DispersionModel.TypicalAt`), the **`Best of N`** readout (`:318` →
`CalculateBestOfNScore` → `DispersionModel.BestOfN`), and the **distribution curve** (`:595-596`).

### Why it matters — measured

A profile with an axis switched off still has that axis scored, at full weight, from a band the
generator will never apply. Measured at `q = 0.50` by driving the shipped `envelope_check.py`
directly, comparing what the dispersion model returns against what `CalculateCompositeScore`'s own
rules say the answer should be, expressed in percentage points of the displayed `vs Faithful` figure:

> [!WARNING]
> **The table below was measured at a fixed `q = 0.50`, which is not what the readout evaluates.**
> `ProfileEditorTab.cs:278` calls `TypicalAt(v, v.averageQuality)`, and `averageQuality` ranges
> 0.32–0.55 across the presets. The `q = 0.50` column was re-derived independently on 2026-08-09
> and reproduces digit-for-digit, so the arithmetic was right — but only `Faithful` and
> `Specialist` actually sit at 0.50. **The right-hand pair is the player-visible error.** Two
> presets change sign and `Distinct` falls by 8pp. Corrected figures:

| Profile | `avgQ` | Skills OFF @ 0.50 | Passions OFF @ 0.50 | **Skills OFF @ `avgQ`** | **Passions OFF @ `avgQ`** |
|---|---|---|---|---|---|
| Faithful | 0.50 | −1.47pp | +2.76pp | **−1.47pp** | **+2.76pp** |
| Desperate | 0.37 | +0.48pp | −0.90pp | **−0.61pp** | **+1.14pp** |
| Scavenger | 0.43 | +0.95pp | −1.77pp | **+0.06pp** | **−0.11pp** |
| Specialist | 0.50 | +3.71pp | −6.96pp | **+3.71pp** | **−6.96pp** |
| Elite | 0.53 | +5.33pp | −9.99pp | **+5.50pp** | **−10.32pp** |
| Distinct | 0.32 | +5.67pp | −10.64pp | **+1.39pp** | **−2.60pp** |
| **Sovereign** | 0.55 | +7.63pp | −14.31pp | **+7.33pp** | **−13.75pp** |
| **Wildcard** | 0.37 | −10.26pp | +19.23pp | **−9.77pp** | **+18.33pp** |

**The in-game gate's tolerance is 0.5pp** (`DebugActions.cs:145`). The worst real case is
**18.3pp, ~37× that**.

### Why no existing check can see it

Three independent reasons, and they compose:

1. **All eight shipped presets leave both flags `true`,** so `Verify Best-of-N`'s 32/32 never
   exercises the branch, and `EnvelopeFigures.g.cs` is unaffected — the golden file would be
   byte-unchanged by the fix.
2. **The gate compares C# against Python, and both sides share the omission.** They agree with each
   other to ~0.000pp while both disagree with the generator. The gate would stay green through the
   entire defect.
3. **The flags *are* in the Best-of-N cache key** (`PawnVarianceSettings.cs:1542-1543`), so toggling
   a checkbox correctly invalidates the cache and recomputes — producing the same wrong number
   again. The one mechanism that looks like it is tracking the flags is tracking them for a purpose
   that never reads them.

> [!CAUTION]
> **This is the §0 failure recurring, and this audit found it twice more in one pass (here and
> Q-14), so the pattern should be treated as structural rather than as a run of unlucky bugs.**
> First: `Wildcard` breached the ±35% envelope while
> the gate read green, because the metric was blind to dispersion. Second: the vanilla passion floor
> was gated behind `spread > 0` in all three model sides and unconditionally in the generator — all
> three models agreed with each other and all three disagreed with the pawns being rolled. Third:
> this. Fourth: **Q-14**, found in the same pass. Every instance has the same shape — **the
> generator does something no model mirrors, and "both implementations agree" is mistaken for "the
> metric is right."** `HANDOVER.md` already states this in the passion-floor CAUTION; the statement
> did not prevent the next two instances, which is the argument that the missing artifact is not
> another warning but a **checklist of every branch in the generator and which model sites mirror
> it**. Q-14's entry sketches what that checklist would have to contain.

### When it triggers

Any custom profile with either section unchecked, and any preset a player unchecks a section on
(the checkbox is drawn for presets too — it simply cannot be saved there). Two clicks from the
Profile Editor. No log line, no visible marker; the readout looks entirely plausible.

### What was not verified

The magnitude table was produced offline from the Python mirror, not from a running game, so it
inherits the mirror's own approximations. The **direction and structure** are certain from the code.
**Not verified in game** — the reproduction to run is: create a custom profile, set it to
`Sovereign`'s values, uncheck **Passions**, and read Row 3. The prediction is that the figure moves
by ~14pp *less* than it should, i.e. the readout barely changes when it should drop sharply.

### ✅ Fixed 2026-08-09

**The proposed fix in the paragraph above was wrong, and acting on it would have mirrored a bug.**
It assumed `CalculateCompositeScore` was the correct reference to port from. It is not — see
**Q-16**, found while verifying this entry. `CalculateCompositeScore` zeroed the weight of a
disabled axis, which made its own two fallbacks unreachable, so "mirror the flags into
`DispersionModel`" would have propagated the weight-zeroing into the dispersion path.

The open question this entry raised — *what does σ mean on a disabled axis?* — needed no Rule 5
consultation once Q-16 was understood. A disabled axis is a **zero-variance constant at vanilla's
level**, at full weight. It is not dropped from the weighted average.

What changed:

| Site | Change |
|---|---|
| `PawnVarianceSettings.cs` | weights made unconditional; `skillNorm` literal `0.25f` replaced by the derived `AssumedVanillaSkillBaseline / AssumedMaxSkillLevel`; dead `totalW <= 0 ⇒ return q` branch removed (Q-16) |
| `DispersionModel.cs` `Moments` | both axes now branch on their flag; a disabled axis contributes its vanilla fallback with zero variance |
| `envelope_check.py` | `BOOL_FIELDS` parsing (absent ⇒ `true`), both flags honoured in `make_composite` and `grid_moments`, `VanillaPassionBudget` added to `required` |
| `DebugActions.cs` | new per-axis-toggle gate — see below |

**No shipped figure moves.** All eight presets leave both flags `true`, so `envelope_check.py`
still PASSes Rule 1 and Rule 2 at N = 1/5/25/50 and reports `Source/EnvelopeFigures.g.cs:
unchanged`. `FaithfulBaseline` is `0.257089` before and after. Build is clean, 0 warnings.

**The new gate does not compare against the reference table, and could not** — every preset has
both flags on, so the golden file has no column for a disabled axis and the 32/32 cross-check can
never reach this branch. Cross-checking two mirrors cannot catch a branch both mirrors are missing,
which is the whole §0 lesson. It checks two standalone invariants derived from `Constants`:

1. **Both axes off ⇒ the score is exactly vanilla's**, for every preset at `q` = 0.10/0.50/0.90.
   Verified exact (deviation `0.000e+00`) on all eight.
2. **On `Sovereign`, switching an axis off must move the score.** If the flags are ignored again the
   delta is exactly zero. Measured 0.0266 (passions) and 0.0368 (skills) against a 1e-3 threshold.

**Still not verified in game.** The reproduction above should still be run, and the gate itself has
only been predicted through the Python mirror, not executed in RimWorld.

---

<a id="q-16"></a>
## Q-16 — `CalculateCompositeScore` multiplies both of its disabled-axis fallbacks by zero

**Severity: Major.  Confidence: high — proved by an exact invariant, not argued.  ✅ Fixed 2026-08-09.**

*Found on 2026-08-09 while verifying Q-01. Not produced by any of the five sweeps or the
adversarial pass — all six read the two functions as consistent with each other.*

### What

`CalculateCompositeScore` computed a fallback for each disabled axis and then discarded it:

```csharp
float skillNorm = 0.25f;                                   // :1416
if (v.enableSkillVariance) { … }

float passionNorm = Constants.VanillaPassionBudget         // :1441-1442
    * PassionPipEfficiency(Constants.VanillaMajorBias) / Constants.MaxPassionPips;
if (v.enablePassionVariance) { … }

float wS = v.enableSkillVariance ? Constants.CompositeSkillWeight : 0f;      // :1492
float wP = v.enablePassionVariance ? Constants.CompositePassionWeight : 0f;  // :1493
float totalW = wS + wP;
if (totalW <= 0f) return q;
return Mathf.Clamp01((wS * skillNorm + wP * passionNorm) / totalW);
```

If an axis is disabled its weight is `0`, so its fallback contributes `0 * fallback`. **Both
fallbacks were unreachable.** `VanillaPassionBudget` and `VanillaMajorBias` had no other consumer
in `Source/` — a grep returns only `Constants.cs` and these two lines — so both constants were dead,
along with the seven-line comment at `:1436-1442` arguing why the passion fallback is `0.2609` and
"not 0.25", and the derivation in `Constants.cs:45-56`.

### Why it matters, and why the fallbacks were right and the weights wrong

Dropping the axis is not a harmless alternative reading. A disabled axis does not mean the pawn has
no passions; it means the pawn keeps **vanilla's**, which is exactly what the fallbacks measure.
Zeroing the weight instead rescales the composite so the surviving axis is 100% of it, while the
result is still divided by the same `Faithful` baseline — two different scales compared against one
reference.

**The invariant that settles it.** With both axes off the mod changes nothing about the pawn, so the
score must be vanilla's own — the `Faithful` baseline. Keeping the weights gives

```
(0.8 × 0.25 + 1.5 × 0.260870) / 2.3 = 0.257089  ==  FaithfulBaseline()
```

to nine decimal places, for every preset and at every quality. The old code returned `q` — the raw
quality roll, on no meaningful scale at all. A second, independent confirmation: under the fallback
semantics `Faithful` scores `0.257089` in **all four** flag combinations, which is what the
vanilla-mimicking preset must do and which the weight-zeroing broke (it read −1.47pp with skills
off).

### Fix

Weights made unconditional; the now-dead `totalW <= 0f` branch removed; the literal `0.25f` replaced
by `AssumedVanillaSkillBaseline / AssumedMaxSkillLevel` so the same fallback is not hardcoded twice
across the C# mirror. Mirrored into `DispersionModel.Moments` and `envelope_check.py`. No shipped
figure moves — every preset enables both axes.

### What was not verified

Not exercised in game. The claim that the two constants were dead is from a grep over `Source/`
plus reading every use; `Source/obj/` binaries were not decompiled to confirm.

---

<a id="q-02"></a>
## Q-02 — Six `file.cs:NNN` citations in `HANDOVER.md` point at the wrong line

**Severity: Major.  Confidence: high — every row below was opened on both sides.**

### What

| `HANDOVER.md` | Claims the cited line is | What is actually there | Real location |
|---|---|---|---|
| `:624` | *"Quality is rolled ONCE per pawn (`HarmonyPatches.cs:36`)"* | a comment: *"one here would hand a child something vanilla structurally never gives."* | `HarmonyPatches.cs:55` |
| `:641` | *"`SkillVarianceApplier.cs:47` is `RoundToInt(record.levelInt + shift)`"* | a comment: *"ambiguity was flagged as the most likely future bug in this file…"* | `SkillVarianceApplier.cs:73` |
| `:1143` | *"`PawnVarianceSettings.cs:614` is `Math.Max(overridesViewHeight, 1000f)`"* | `public void DoWindowContents(Rect inRect)` | `PawnVarianceSettings.cs:680` |
| `:1144` | *"`:656` recomputes `listing.CurHeight + 40f` each frame"* | `if (listing.ButtonText(LabelFor(activeProfileId)))` | `PawnVarianceSettings.cs:722` |
| `:912` | *"`budget` is rolled at `PassionVarianceApplier.cs:42` but `eligible` is not built until ~`:79`"* | `:42` is `foreach (SkillRecord record in pawn.skills.skills)` — the wipe loop | budget `:64`, eligible `:115` |
| `:650` | *"`GrowUpVariance.cs:58` calls `RollQuality` again"* | a comment: *"Same per-pawn profile resolution as generation…"* | `GrowUpVariance.cs:62` |

Two further near-misses, listed for completeness but not counted above because the cited line is
within the same short block and a reader would find the real statement by scrolling: `:1192`
(`TraitVarianceApplier.cs:72` is `{`; the `GenerateTraitsFor` call is `:76`) and `:1193`
(`GrowUpVariance.cs:209` is a comment; the call is `:215`).

### Why it matters

This is the failure mode `HANDOVER.md` names in its own text and that the previous register's
preamble cites as the reason every citation must be opened before it is written down. Two of the
six are worse than a stale pointer:

- **`PawnVarianceSettings.cs:614` and `:656` are off by exactly 66 lines, in the same direction.**
  That is not two typos, it is one un-swept insertion (the General-tab scroll-height block at
  `:642-680`). **Any other line citation into that file should be treated as suspect** — this audit
  found no others, but the sweep proves the file's citations were not maintained.
- **The `PassionVarianceApplier.cs:42 / ~:79` pair is an instruction for future work**, not a
  description. It reads *"either needs a reorder"* — i.e. it tells the next agent which two
  statements to move. Both line numbers are wrong, and `:42` points at a loop that mutates every
  skill on the pawn. An agent that reorders around that line is editing the passion-wipe loop.

### ✅ Fixed 2026-08-09

All six corrected, plus both "near-misses" (`TraitVarianceApplier.cs:72`→`:76`,
`GrowUpVariance.cs:209`→`:215`). The `PassionVarianceApplier` pair now reads `:64` / `:115` and
carries an explicit warning that `:42` is the passion-wipe loop, since that citation was an
instruction to a future agent to reorder code.

Rather than fix only the six, every `File.cs:NNN` citation in `HANDOVER.md` was swept
mechanically. **That found a seventh the audit had missed**: `GrowUpVariance.cs:70-79`, cited as
the evidence that the growth-moment trait pass is add-only, actually spans a `catch` block; the
rationale is at `:77-83`. Three other range citations (`TraitVarianceApplier.cs:43-48`, `:19-22`,
`SkillVarianceApplier.cs:14-22`) were checked and are correct.

The sweep script matches `File.cs:NNN` only, so bare `:NNN` continuation citations (the `:656` and
`~:79` halves of two rows above) were still fixed by hand.

### What was not verified

Whether each citation was correct when written or drifted later; only the current state was checked.
`TRAIT-DESIRABILITY-RESEARCH.md` and the plan/spec documents under `docs/superpowers/` were **not**
swept for citations — the sweep covered `HANDOVER.md` only, so those may hold more.

---

<a id="q-03"></a>
## Q-03 — The `Typical` readout divides a dispersion-aware numerator by a mean-band denominator

**Severity: Minor.  Confidence: high on the structure; the magnitude is measured and is currently zero.**

### What

The numerator and the denominator of the headline percentage are computed by two different
functions that estimate the same quantity two different ways.

**Numerator** — `ProfileEditorTab.cs:278`:

```csharp
float meanComposite = DispersionModel.TypicalAt(v, v.averageQuality);
```

`TypicalAt` → `Moments` integrates the noise through the same clamps the generator applies
(`Clamp(level, 0, 20)`, the `b < 1` passion floor, the capacity cap). It is `E[f(X)]`.

**Denominator** — `PawnVarianceSettings.cs:1575-1580`, reached from `FormatPowerReadout` (`:1323`)
and from `MapToCenteredX` (`:1605`, which sets the curve's centre line):

```csharp
cachedFaithfulBaseline = CalculateCompositeScore(0.50f, VarianceProfiles.VanillaLike.MakeValues());
```

`CalculateCompositeScore` reads the mean band and applies no noise at all. It is `f(E[X])`.

### Why it matters — and why it is Minor

`f(E[X]) == E[f(X)]` only where `f` is linear across the range the noise actually reaches, i.e. only
while no clamp engages. **This was measured rather than argued.** Comparing the two estimators at
`q = 0.50` for all eight presets:

| Profile | `CalculateCompositeScore` | `TypicalAt` | divergence |
|---|---|---|---|
| Faithful | 0.257089 | 0.257089 | **+0.000%** |
| Wildcard | 0.304378 | 0.304434 | +0.019% |
| Distinct | 0.302637 | 0.302650 | +0.004% |
| Desperate | 0.230188 | 0.230189 | +0.001% |
| every other preset | — | — | ≤ 0.001% |

For the **denominator specifically** — Faithful at `q = 0.50` — the two agree to six decimal places,
so the mismatch contributes **`0.00pp` to every displayed percentage today**, on all eight presets.

That measurement is the whole reason this is Minor and not Major. It agrees to six places *because
Faithful's noise band never touches a clamp at that quality* — which is a property of Faithful's
current tuning, not a guarantee either function makes. The latent failure is specific and easy to
trigger: **any retune that pushes Faithful's band against the skill clamp or the passion floor
desynchronises the shared denominator**, and because `baseC` is the denominator for *every* profile's
readout and the centre line of the curve, every figure on screen moves at once. Note that Wildcard
already shows the mechanism biting at 0.019% — small, but non-zero, and it is the preset whose band
reaches a clamp.

**Nothing cross-checks this pair.** `Verify Best-of-N` compares `CalculateBestOfNScore` against
`DispersionModel.BestOfN`; it never evaluates `CalculateCompositeScore`, `TypicalAt`, or
`FaithfulBaseline`, so the gate cannot notice these two drifting apart.

### What was not verified

Measured through the Python mirror, not the shipped C#; the two are a faithful mirror everywhere
both model something (see "checked and clean"), but this is a mirror figure. Whether the intended
fix is to move `FaithfulBaseline` onto `TypicalAt` was not decided — it would change the published
`0.2571` baseline by a currently-immeasurable amount but is a Rule 5 / Rule 6 action regardless,
since it touches the reference every percentage is measured against.

---

<a id="q-04"></a>
## Q-04 — `CalculateCompositeScore` omits the vanilla passion floor that all four dispersion sites carry

**Severity: Minor.  Confidence: high.**

### What

The 2026-08-08 fix established that the floor `budget < 1 && passionCountMin > 0 ⇒ budget = 1` must
appear identically in four places, and it does:

| Site | Line |
|---|---|
| Generator | `PassionVarianceApplier.cs:76-77` |
| `DispersionModel.Moments` | `DispersionModel.cs:101` |
| `envelope_check.py` `grid_moments` | `:282-283` |
| `dispersion_mc.py` `simulate` | `:111-112` |

There is a **fifth** site that computes a passion budget and it does not have the floor.
`PawnVarianceSettings.cs:1479-1486`:

```csharp
budget = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
…
passionNorm = Mathf.Clamp01(Mathf.Min(budget, capacity) * efficiency / Constants.MaxPassionPips);
```

Its Python mirror `make_composite` (`envelope_check.py:182-185`) omits it too — so this is **not** a
C#/Python drift. Both sides of the mirror are consistently missing what the other four sites have.

### Why it matters

`CalculateCompositeScore` is not dead: it computes `FaithfulBaseline()` — the denominator of every
displayed percentage (see Q-03) — and `MapToCenteredX`. On a custom profile with
`passionCountMin < 1`, the generator floors the pawn's budget to 1 pip, the dispersion model models
that floor, and this function does not. It is the same defect the 2026-08-08 fix closed, surviving in
the one function the fix did not enumerate, and it survives for the same reason: the enumeration was
"the model sides", and this function was not counted as one.

Reachability is the same two slider moves the previous fix documented: no shipped preset has
`passionCountMin < 2.2`, so this is custom-profile-only and no shipped figure moves.

### What was not verified

The magnitude at a specific low `passionCountMin` was not computed. Because `FaithfulBaseline` is
always evaluated on `VanillaLike` (whose `passionCountMin` is well above 1), **the denominator is
never affected** — the reachable consequence is confined to `MapToCenteredX` placing a low-budget
custom profile's marker slightly wrong on the curve. That narrowness is why this is Minor rather
than a repeat of the Major it descends from.

---

<a id="q-05"></a>
## Q-05 — Six constants that enter the score are outside the golden-file drift check

**Severity: Minor.  Confidence: high.**

### What

`envelope_check.py`'s `required` list (`:94-108`) and its `GEN_CONSTANTS` tuple (`:338-342`) are
different sets, and the gap is not accidental leftovers — it includes constants the tool's own
comment says are scoring inputs. `required` contains, with this comment at `:104-107`:

> Skill-noise Lerp endpoints. **These DO enter the score now**: `grid_moments` builds the per-skill
> excursion from them via `SkillNoiseScalar`. They were spread-column only before the
> dispersion-aware work.

`GEN_CONSTANTS` — the 12 values baked into `EnvelopeFigures.g.cs` and diffed in game by
`DebugActions.cs:181-218` — contains none of them:

| Constant | In `required` | In `GEN_CONSTANTS` / drift check | Enters the score via |
|---|---|---|---|
| `MagnitudeLerpLow` | ✅ | ❌ | `DispersionModel.cs:68` skill-noise Lerp |
| `MaxMagnitude` | ✅ | ❌ | `DispersionModel.cs:68` skill-noise Lerp |
| `PassionBudgetSpreadMin` | ✅ | ❌ | `DispersionModel.cs:84` passion-noise Lerp |
| `PassionBudgetSpreadMax` | ✅ | ❌ | `DispersionModel.cs:84` passion-noise Lerp |
| `VanillaMajorBias` | ✅ | ❌ | `PawnVarianceSettings.cs:1442` passion-off fallback |
| `VanillaPassionBudget` | ❌ → ✅ | ❌ | `PawnVarianceSettings.cs:1441` passion-off fallback |

> [!WARNING]
> **The last two rows were wrong when written, and are now right for a different reason.** At the
> time this entry was filed neither constant entered the score at all: the passion-off fallback they
> feed was multiplied by a zeroed weight, so both were dead code (**Q-16**). The Q-16 fix made the
> fallback live, so they genuinely are scoring inputs **now**, and `VanillaPassionBudget` was added
> to `envelope_check.py`'s `required` list as part of that fix. Both are still outside
> `GEN_CONSTANTS`, so the drift-check gap this entry describes is real and still open for them —
> and the paragraph below, which says `envelope_check.py` "does not model the fallback at all", is
> also now out of date: it does.

### Why it matters

The stale-table check exists because *"a table that is merely SELF-consistent would otherwise pass
while being wrong"* (`DebugActions.cs:177-180`). Four of these six are exactly that case: change
`MaxMagnitude` without re-running the tool and every dispersion-aware figure is measured against the
wrong reference, while the check that was built to name the cause stays silent and the failure
surfaces — if at all — as an unexplained raw-score mismatch.

The last two are a smaller, different gap: `VanillaPassionBudget` and `VanillaMajorBias` drive the
passion-variance-off fallback, which `envelope_check.py` does not model at all (see Q-01) — so there
is no reference figure for them to be checked against. Listing them here is a note that the gap is
one layer deeper than a missing tuple entry.

### What was not verified

Whether a change to one of the four spread constants would still be caught downstream by the raw
3% / displayed 0.5pp comparison. It probably would for a large change and probably would not for a
small one, but this was not computed — so the claim is "the diagnostic is missing", not "the change
is undetectable".

---

<a id="q-06"></a>
## Q-06 — The ±4σ truncation window is hardcoded in two mirrors and derived in the third

**Severity: Minor.  Confidence: high.**

### What

The passion budget's Gaussian is truncated at ±4σ, matching `Constants.PassionBudgetClampFactor = 4f`
(`Constants.cs:26`). Three places implement it, two by literal:

- `DispersionModel.cs:44` — `float dz = 8f / GaussNodes;   // +-4 sigma, matching PassionBudgetClampFactor`, with nodes built from `-4f + (i + 0.5f) * dz` at `:47`. The comment names the coupling; the code does not read the constant.
- `envelope_check.py:232-234` — `lo, hi = -4.0, 4.0`, docstring *"matching PassionBudgetClampFactor"*. Same shape. `PassionBudgetClampFactor` is not even in the tool's `required` list, so the tool never reads it.
- `dispersion_mc.py:84` — `window = sig * C["PassionBudgetClampFactor"]`. **Derived from the live constant.**

### Why it matters

Numerically identical today. The exposure is specific: `dispersion_mc.py` exists to be an
*independent* validation of the two production integrators. If `PassionBudgetClampFactor` is ever
retuned, the Monte Carlo follows it and the two quadratures do not — so the one tool whose job is to
catch modelling error would start disagreeing with them **because of the retune, not because of a
bug**, and the natural reading of that disagreement ("the Monte Carlo says the quadrature is wrong")
would be backwards. Two hardcodes that agree with a constant are fine; two hardcodes and one
derivation is a trap set for whoever changes the constant.

### What was not verified

Whether any test or assertion elsewhere pins the three together — none was found in the audited
files, but the search was not exhaustive across the repo.

---

<a id="q-07"></a>
## Q-07 — The two documents in the same uncommitted batch disagree about the review backlog

**Severity: Minor.  Confidence: high.**

`HANDOVER.md`'s working-tree diff flips `Source/GrowUpVariance.cs` from `[ ] — **NEXT UP**` to `[x]`,
along with five other files, and moves **NEXT UP** to `Source/DebugActions.cs`.

`docs/AUDIT-2026-08-06-problem-register.md:183-187` — modified in the *same* uncommitted batch —
still reads:

> The next work is not here — it is the review backlog in `HANDOVER.md` "Code review status": …
> and `GrowUpVariance.cs` marked **NEXT UP**.

`HANDOVER.md` is the authority by its own statement, so the resolution is not in doubt. What makes it
worth filing is that both files were edited in the same batch and left contradicting each other, and
the newer-looking of the two is the wrong one. A reader following the register would treat
`GrowUpVariance.cs` as unreviewed and Rule-8-gated, and `DebugActions.cs` as fair game — precisely
inverted.

**What was not verified:** whether the six files flipped to `[x]` in that diff were in fact reviewed.
That is not checkable from the tree; only the disagreement is.

---

<a id="q-08"></a>
## Q-08 — The uncommitted `Constants.cs` diff deletes the only surviving record of the passion-spread rescale figures

**Severity: Minor.  Confidence: high.**

The working-tree diff shortens two comments. One deletion is harmless, one is not.

**Harmless:** the `MagnitudeLerpLow` comment loses its `skillNoise 0.2 → 1.60/1.20 (−25%)` table.
`HANDOVER.md:666-671` carries that same table, so nothing is lost.

**Not harmless:** the `PassionBudgetSpreadMin` comment loses

> `passionNoise 0.25 went sigma 1.19 -> 1.00 (-16%)`, not just the zero case

and grepping `HANDOVER.md` for `1.19`, `−16%` or any restatement of it finds **nothing**. The
handover's rescale table covers the *skill* constant only. This figure exists nowhere else in the
repo, and it is tuning history of exactly the kind the handover elsewhere insists on keeping —
`HANDOVER.md` states the noise floors *"rescaled dispersion at every setting"* and tells the reader
to consult the current table rather than remembered figures; for the passion axis, after this diff,
there is no table to consult.

The same diff also *fixes* a real defect in the committed baseline — the old comment contained an
unterminated quotation and a sentence that never resolved (`so the setting controls "how much the
total passion budget` followed by an unrelated line). So the diff should not be reverted wholesale;
the figure should be preserved, in `HANDOVER.md` if not in the comment.

**What was not verified:** whether the figure survives in a 2026-08-06 commit message. Even if it
does, a commit message is not where the handover tells readers to look.

---

<a id="q-09"></a>
## Q-09 — The verify gate's tolerance rationale describes an integrator that no longer exists

**Severity: Cosmetic.  Confidence: high.**

`DebugActions.cs:133-139` justifies the deliberately wide 3% raw tolerance:

> Both implementations share a first-order-accurate right-edge CDF — `envelope_check.py`'s
> `beta_grid` does `run += v * dq` before appending, **and `CalculateBestOfNScoreCore` does the
> same** …

`CalculateBestOfNScoreCore` (`PawnVarianceSettings.cs:1568-1571`) is now a one-line delegate to
`DispersionModel.BestOfN`, and `BuildCdf` (`DispersionModel.cs:117-154`) builds each `F[j]` as a
direct weighted sum `acc += wq[i] * NormalCdf(…)`, not an incremental running total. A grep for
`run +=` across `Source/*.cs` returns nothing.

The Python half of the claim is still true (`envelope_check.py:427-430`). The C# half describes the
pre-dispersion-model implementation. Nothing computes wrongly — but the stated *reason* for trusting
a 3% tolerance can no longer be verified against the code, which matters because that tolerance is
the one number standing between this gate and the class of defect it was built after. Worth noting
that the carried right-edge-CDF decision itself is unaffected and remains correctly documented
elsewhere; this is only the comment that explains the tolerance.

**What was not verified:** what the actual source and magnitude of raw disagreement between
`BuildCdf` and `beta_grid` now is, i.e. whether 3% is still the right number.

---

<a id="q-10"></a>
## Q-10 — `LoadFolders.xml` still maps `v1.5`

**Severity: Cosmetic.  Confidence: high.**

`About/About.xml:6-11` deliberately dropped 1.5, with the reasoning in a comment — this is P-07's
fix. `About/LoadFolders.xml:3-8` still declares both:

```xml
<v1.5><li>/</li></v1.5>
<v1.6><li>/</li></v1.6>
```

`supportedVersions` is what actually gates loading, so 1.5 is not advertised to players and nothing
is broken. It is a vestigial declaration of the exact claim the sibling file's comment explains was
removed, and a maintainer reading `LoadFolders.xml` alone would conclude 1.5 is still targeted.

---

<a id="q-11"></a>
## Q-11 — Dead identifiers survive in comments and docs

**Severity: Cosmetic.  Confidence: high.**

The rename `skillNoise`/`passionNoise` → `skillSpread`/`passionSpread` (accessors
`SkillNoiseScalar`/`PassionNoiseScalar`) and `MinMagnitudeFloor` → `MagnitudeLerpLow` (P-11's fix)
left references behind:

| Location | Dead name | Note |
|---|---|---|
| `Source/Constants.cs`, `PassionBudgetSpreadMin` comment (working tree) | `passionNoise` | *"At passionNoise = 0 the budget is exactly its quality-lerped mean"*. Predates the current diff, which touched this very comment without fixing it. |
| `HANDOVER.md:657` | `MinMagnitudeFloor` | Phrased as history (*"used to be"*), so arguably intentional, but the identifier is ungreppable. |
| `HANDOVER.md:666` | `skillNoise` (table header) | The table deliberately reports pre-rename 0–1 scalar values, but unlike the comparable passage at `:722` it never says so. |

Nothing computes wrongly. Filed because grepping for the name a comment uses is how a reader finds
the field, and all three return zero hits in `Source/`.

---

<a id="q-12"></a>
## Q-12 — `VarianceProfile.cs:31` cites the wrong creation sites

**Severity: Cosmetic.  Confidence: high.**

`VarianceProfile.cs:31` reads *"every creation path passes explicit values
(`PawnVarianceSettings.cs:1096/1106`, `Clone`)"*. The explicit-value sites are
`PawnVarianceSettings.cs:1171` (`CreateNewCustomProfile`) and `:1181` (`DuplicateCurrentProfile`).

Same class as Q-02 but inside a source comment rather than the handover. It matters slightly more
than an ordinary stale pointer because the sentence is the *evidence* for the claim that the
field-initialiser/`Scribe`-default mismatch is unreachable dead code — a reader checking that claim
follows the citation to the wrong lines.

**✅ Fixed 2026-08-09** — corrected to `:1171/1181`, both re-verified as
`CreateNewCustomProfile` and `DuplicateCurrentProfile`. Folded into the Q-02 sweep since it is the
same defect class.

**Related, and deliberately not filed as a finding:** that mismatch itself (`skillShift` −4/6 vs
−3/3, `traitCount` 1/6 vs 2/3, `skillSpread` 0.857321 vs 0.489898) was re-verified as still present
and still unreachable — every construction path calls `ExposeData` immediately, which overwrites
every field. It remains a **carried item** in `HANDOVER.md`, not a new defect. The in-file comment's
claim that the four fields "match the Scribe defaults" is still overstated, which the handover
already records.

---

<a id="q-13"></a>
## Q-13 — The curve draw allocates and sorts already-sorted data every frame

**Severity: Cosmetic.  Confidence: medium-high.**

`ProfileEditorTab.cs:563` allocates `new Vector2[CurveSamples]` on every call to
`DrawQualityDistributionCurve`, and `:579` then runs `Array.Sort(points, (a, b) => a.x.CompareTo(b.x))`
— allocating a comparer delegate as well. The method runs every IMGUI frame the tab is visible.

The sort is provably redundant: `power` is generated as `(i + 0.5f) / CurveSamples` for increasing
`i` (`:570`), and `MapToCenteredX` (`PawnVarianceSettings.cs:1603-1616`) is monotonically
non-decreasing — both branches are `constant + positive slope × input`, meeting continuously at
`compositeScore == baseC`. The points are already in ascending `x`.

Filed only because the immediately adjacent `curveDensityScratch` (`:529`, `:559-561`) is explicitly
cached across frames *because the curve redraws every frame* — so this is the one allocation in a
method already written with per-frame GC pressure in mind. At `CurveSamples = 70` the cost is not
perceptible; no profiling was done.

---

<a id="q-14"></a>
## Q-14 — The passion spend loop discretizes the budget and discards the remainder; no model side does

**Severity: Major.  Confidence: high — mechanism read in code, magnitude simulated, and the
simulation corroborated by an in-game measurement already recorded in `HANDOVER.md`.**

### What

The generator does not spend a continuous budget. `PassionVarianceApplier.cs:81-93`:

```csharp
while (budget >= Constants.MinorPassionCost)
{
    if (budget >= Constants.MajorPassionCost && Rand.Chance(v.passionMajorBias))
    { majorPassions++; budget -= Constants.MajorPassionCost; }
    else
    { minorPassions++; budget -= Constants.MinorPassionCost; }
}
```

The loop buys whole passions at 1.5 / 1.0 pips and **exits with a remainder in `[0, 1)` that is
simply dropped**. A pawn whose budget rolls 4.8 pips receives 4 pips of passion, not 4.8.

**No model side reproduces this.** `DispersionModel.Moments` (`:104`) and `grid_moments`
(`envelope_check.py:288`) both take the budget continuously —

```csharp
float u = Mathf.Min(1f, b * eff / pdiv);
```

— with only the floor-at-1 and the capacity clamp applied. `dispersion_mc.py`, the independent Monte
Carlo, does the same: it is independent in *method*, not in *modelling assumptions*, which is a
limit `HANDOVER.md` already states and which is exactly what lets this through.

### Why it matters — measured

Simulated at **400,000 pawns per profile**, running the spend loop above verbatim against each
preset's real Beta-distributed quality, its passion spread, its floor and its capacity cap. The
simulation reproduces `envelope_check.py`'s published N=1 column to within 0.1pp on every preset
(e.g. `Sovereign` +24.69 vs the published +24.7, `Desperate` −20.76 vs −20.8), which is what makes
the second column credible:

| Profile | pips the model assumes | pips actually delivered | lost | model says | pawns deliver | error |
|---|---|---|---|---|---|---|
| Faithful | 5.001 | 4.552 | 0.449 | +0.0% | +0.0% | — |
| Desperate | 4.036 | 3.576 | 0.460 | −20.76% | −22.06% | +1.30pp |
| Scavenger | 4.306 | 3.854 | 0.452 | −13.27% | −14.10% | +0.83pp |
| Distinct | 4.269 | 3.854 | 0.416 | −8.43% | −8.73% | +0.30pp |
| Wildcard | 5.413 | 4.969 | 0.444 | −2.56% | −2.49% | −0.07pp |
| Specialist | 5.096 | 4.654 | 0.443 | +8.70% | +9.25% | −0.55pp |
| Elite | 5.561 | 5.122 | 0.439 | +20.84% | +22.18% | −1.33pp |
| **Sovereign** | 5.622 | 5.187 | 0.436 | +24.69% | **+26.27%** | **−1.58pp** |

> [!NOTE]
> **Independently re-measured 2026-08-09** with a separately written 400k-pawn simulation and a
> different seed. Every row reproduces: `Sovereign` −1.60pp (vs −1.58 here), `Elite` −1.33pp,
> `Desperate` +1.31pp, and `Faithful` 5.001 assumed vs 4.552 delivered — which is the figure that
> matches the in-game 1000-pawn dump of 4.59. The finding stands as written.

Two things follow, and the second is the one that matters:

1. **Every profile loses ~0.45 pips**, so most of the effect cancels in the ratio to `Faithful` —
   which is why it has survived undetected. The absolute error on `Faithful`'s own baseline is
   −5.95%, and almost all of it is common-mode.
2. **What does not cancel is signed by tier.** The residual pushes every profile *away* from
   `Faithful` in the direction it already sits: the power tiers are further apart in the game than
   the envelope says. `Sovereign` @ N=1 is the tightest N=1 figure in the project at 10.3pp of
   headroom; this reduces it to roughly **8.7pp**. Rule 1 still holds — but the margin the whole
   tuning process is steered by is ~15% smaller than every published figure claims, and the
   published figures are what a retune reads.

The error is **above the in-game gate's 0.5pp tolerance on five of eight presets**, and the gate
cannot see any of it: C# and Python share the omission, so they agree with each other to ~0.000pp.

> [!IMPORTANT]
> **This has already been measured in game, written down, and read as something else.**
> `HANDOVER.md:111-113` records a 1000-pawn dump: *"Measured at 1000 pawns, `Faithful`'s realised
> budget is `4.59` pips mean."* The score assumes `5.001`. The simulation above independently
> predicts `4.552`. **The three agree**, and the ~0.45-pip gap between what the score assumes and
> what pawns receive has been sitting in the handover since the passion-band retune — recorded as
> evidence that `Faithful` reproduces vanilla's budget, which it does, rather than as evidence that
> the *scoring model* assumes a budget no pawn ever gets. That is the finding: not that nobody
> measured it, but that the measurement was filed under the wrong question. (Vanilla's own generator
> spends through the same discretizing loop, so the vanilla-parity claim at `:112-113` stands
> untouched — it is only the model's continuous `5.001` that is wrong.)

### The checklist this argues for

Q-01 argues that the missing artifact is an enumeration of generator branches against model sites.
This entry is what a row of it looks like. The branches found so far that shape the outcome:
the passion floor (mirrored in 4 sites, +1 missing — Q-04), the enable toggles (mirrored in 0 —
Q-01), the spend-loop discretization (mirrored in 0 — this), the surplus-discard against eligible
skills (mirrored as the capacity cap), and the `Clamp(0,20)` skill censoring (mirrored). A future
generator change should have to state which row it adds.

### What was not verified

The **`Faithful`** row is corroborated in game (`HANDOVER.md:111-113`, 4.59 measured vs 4.552
simulated); **the other seven presets are simulation only**, and the simulation shares the
flat-`AssumedVanillaSkillBaseline` assumption every model side makes, so it is not independent of
that. The per-profile error column in particular has not been observed. The figures are the **N=1 mean
composite**; the effect on Best-of-N at N = 5/25/50 was **not** computed and could differ in size,
since Best-of-N is a maximum statistic and the discretization also removes a little dispersion.
Whether the right fix is to model the discretization or to stop discarding the remainder (e.g. spend
the fraction probabilistically) is a Rule 5 decision — the second changes pawns, the first changes
every published number.

---

<a id="q-15"></a>
## Q-15 — A throw in the life-stage postfix locks that pawn out of adult variance permanently

**Severity: Minor.  Confidence: high on the mechanism; the trigger is undemonstrated.**

### What

`GrowthUpPatch.PostfixInner` records the stage **before** doing the work it gates.
`GrowthUpPatch.cs:68-73`:

```csharp
DevelopmentalStage currentStage = ___pawn.DevelopmentalStage;
bool hadBaseline = LastKnownStage.TryGetValue(___pawn.thingIDNumber, out DevelopmentalStage previousStage);
LastKnownStage[___pawn.thingIDNumber] = currentStage; // record unconditionally …

if (currentStage != DevelopmentalStage.Adult) return;
if (!hadBaseline || previousStage == DevelopmentalStage.Adult) return;
```

The work — `settings.ValuesFor(___pawn)` (`:85`) and `GrowUpVariance.Apply(…)` (`:113`) — happens
after. The new wrapper added as P-20's fix (`:50-60`) catches and logs anything that escapes:

```csharp
try { PostfixInner(___pawn); }
catch (Exception ex) { … Log.ErrorOnce(…); }
```

If either call throws, the pawn's entry already reads `Adult`. Every subsequent firing takes
`previousStage == DevelopmentalStage.Adult` and returns at `:73`.

### Why it matters

**There is no recovery path, including a reload.** `LastKnownStage` is session-only and cleared on
`Game.LoadGame` (`:116-119`), which looks like it would give a second chance — it does not. After a
reload the pawn is already `Adult`, so the first observation sets the baseline with
`hadBaseline == false` and returns, and every later firing sees `previousStage == Adult`. The
"genuine transition" the guard requires is observable exactly once per pawn, and it has been spent.
That pawn silently never receives adult growth variance again, in that save, ever.

The code's own comment names the realistic thrower: *"`ValuesFor` is the widest-surface call in the
mod — it walks the faction, race and xenotype dictionaries — so it is the realistic thrower"*
(`:44-45`).

This is filed as Minor, not Major, because it requires an exception that has never been observed,
and because **the guard itself is correct and should stay** — before P-20's fix the same exception
escaped into vanilla's life-stage plumbing during load, which is worse. The defect is the ordering,
not the guard. It is worth recording because it is the shape the project has been bitten by before:
a fix that converts a loud failure into a quiet one, without moving the state write that made the
failure permanent. The fix is small — roll the `LastKnownStage` entry back in the `catch`, or restore
the previous value — but it has to be done in the `catch`, since `PostfixInner` has legitimate
reasons to record the stage on paths that do no work.

### What was not verified

Not reproduced: no throwing `ValuesFor` was constructed, and no third-party race is known to cause
one. The ordering and the unreachability of any retry are both certain from the code. Whether
`GrowUpVariance.Apply` can throw *after* partially modifying a pawn — which would make the lockout
leave a half-applied pawn rather than an untouched one — was not traced.

---

## Rejected during verification

Recorded because knowing what was checked and dismissed is worth as much as the findings.

- **"The rethrow into vanilla is reachable by a normal player."** `HarmonyPatches.cs:69` is
  `if (settings.verboseLogging && Prefs.DevMode) throw;` — it requires *both* the mod's checkbox and
  RimWorld's dev mode, the checkbox is itself drawn only under `Prefs.DevMode`
  (`PawnVarianceSettings.cs:1221`), and the behaviour is disclosed in the tooltip. This is P-17's
  fix working as designed, not a residue of it.
- **"Guard asymmetry survives in the appliers."** Checked directly: `Apply` and `AssignPassions`
  carry identical guards. The specific regression the P-16 fix warns about cannot occur.
- **"The Beta cache can go stale behind the quality slider."** Checked: P-28's fix is present and
  every mutation site calls `MarkDistributionParamsDirty()`.
- **The first-order right-edge CDF bias** — carried permanently by owner decision, present
  identically on both sides. Not re-litigated. Only the *comment* about it is filed, as Q-09.
- **"`Constants.cs:99-100`'s cross-reference is stale."** Raised by the adversarial pass and
  **checked and dismissed**: the note it points at (`PawnVarianceSettings.cs:1451-1465`) is present
  and accurately describes the 24-pip-era `budget * (1f + 0.25f * majorBias)` unit error. The
  cross-reference is intact. Recorded so it is not re-raised.
- **"`PairLoadable` discards a one-sided-null axis without warning."** Also raised adversarially.
  True as stated — `PawnVarianceSettings.cs:586` returns before the warning at `:588` — but the
  comment directly above it (`:583`) says so deliberately: *"null is the normal fresh-save case and
  is not warned about; only a genuine mismatch is."* The discard is the safe outcome either way.
  **Residual worth one line if that code is ever touched:** a *one-sided* null is not the fresh-save
  case and is currently treated as one, so the single genuinely-corrupt shape on that path is the
  one that stays quiet. Too small and too speculative to file.
- **Tree, branch and push state** — see the note under Ground state. Not a defect on this project.
