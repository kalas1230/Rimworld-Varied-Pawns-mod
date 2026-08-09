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
| [Q-14](#q-14) | Scoring | The passion spend loop discretizes the budget into whole purchases and discards the remainder; no model side does — **1.60pp** on the enforcing metric | **Major** — ✅ **FIXED 2026-08-09** (models corrected, generator untouched; every published figure moved) |
| [Q-02](#q-02) | Docs | Six `file.cs:NNN` citations in `HANDOVER.md` point at the wrong line; two are off by a consistent 66 | **Major** — ✅ **FIXED 2026-08-09** (a seventh was found and fixed in the same sweep) |
| [Q-03](#q-03) | Scoring | The `Typical` readout divides a dispersion-aware numerator by a mean-band denominator — two different estimators of the same quantity | ~~Minor~~ → **Major** — ✅ **FIXED 2026-08-09** (was `0.00pp`, re-measured at −3.39% once Q-14 landed) |
| [Q-04](#q-04) | Model drift | `CalculateCompositeScore` omits the vanilla passion floor that all four dispersion sites carry | **Minor** — ✅ **FIXED 2026-08-09** |
| [Q-05](#q-05) | Model drift | Six constants that enter the score are outside the golden-file drift check | **Minor** — ✅ **FIXED 2026-08-09** (seven; `PassionBudgetClampFactor` joined them via Q-06) |
| [Q-06](#q-06) | Model drift | The ±4σ truncation window is hardcoded in two mirrors and derived from the constant in the third | **Minor** — ✅ **FIXED 2026-08-09** |
| [Q-07](#q-07) | Docs | The two documents modified in the same uncommitted batch disagree about which file is next for review | **Minor** — ⏸️ **deferred by owner 2026-08-09** (judged not important) |
| [Q-08](#q-08) | Docs | The uncommitted `Constants.cs` diff deletes the only surviving record of the passion-spread rescale figures | **Minor** — ⏸️ **deferred by owner 2026-08-09** (judged not important) |
| [Q-09](#q-09) | Comments | The verify gate's tolerance rationale describes an integrator that no longer exists | **Cosmetic** — ✅ **FIXED 2026-08-09** (ran deeper than filed — see the entry) |
| [Q-10](#q-10) | Packaging | `LoadFolders.xml` still maps `v1.5`, which `About.xml` deliberately dropped | **Cosmetic** — ✅ **FIXED 2026-08-09** |
| [Q-11](#q-11) | Naming | Dead identifiers (`passionNoise`, `skillNoise`, `MinMagnitudeFloor`) survive in comments and docs | **Cosmetic** — ✅ **FIXED 2026-08-09** |
| [Q-12](#q-12) | Comments | `VarianceProfile.cs:31` cites `PawnVarianceSettings.cs:1096/1106`; the real sites are `:1171/:1181` | **Cosmetic** — ✅ **FIXED 2026-08-09** |
| [Q-15](#q-15) | Growth | A throw anywhere in the life-stage postfix locks that pawn out of adult variance permanently, because the stage is recorded before the work | **Minor** — ✅ **FIXED 2026-08-09** |
| [Q-13](#q-13) | UI | The curve draw allocates an array and sorts already-sorted data every frame | **Cosmetic** — ✅ **FIXED 2026-08-09** |
| [Q-17](#q-17) | Model drift | The passion-disabled fallback caps capacity in two different orders across the four mirrors; inert only because current constants never exercise the gap | **Major** — 🔍 **FILED, not fixed 2026-08-09** (owner-directed: record only, out of scope for the branch that found it) |

*(Numbering is by discovery order; the index is grouped by area. `Q-14` and `Q-15` came from the
adversarial pass, which ran last. `Q-17` was found by the 2026-08-09 generator-vs-model
verification branch, after the register below it had already been closed.)*

> [!NOTE]
> **Register closed for defects 2026-08-09.** Every entry was ✅ at closing time except **Q-07** and
> **Q-08**, which the owner deferred as not important. Both are defects *in an uncommitted diff*
> rather than in the code, which is what makes deferring them cheap: they cost nothing until that
> diff is committed. Before working this register again, re-read "What this pass changed about the
> method" at the bottom — the closing sweep found two entries whose stated rationale had been
> invalidated by an *earlier fix in the same register*, and that is now the failure mode to look for
> first.
>
> **Q-17 was added after closing**, by a later branch (2026-08-09 generator-vs-model verification)
> that found it as a byproduct of unrelated work. It is filed, not fixed, by explicit owner
> direction — it is out of scope for the branch that found it.

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
> **Fully superseded 2026-08-09 — every item below is done or deferred.** Kept only because the
> *reasoning* about sequencing is the part worth carrying to the next register. Outcomes against
> what this list predicted:
>
> | Advice | Outcome |
> |---|---|
> | Q-14 next, it moves shipped figures | Done. Every figure moved; tier ordering held. |
> | Q-04/Q-05/Q-06 "as one sweep… none moves a shipped figure" | **Both halves correct.** They were one sweep, and they were the same question — Q-06 turned `PassionBudgetClampFactor` into a scoring constant, which is what put it in Q-05's drift check. `EnvelopeFigures.g.cs`'s `Scores` array came out byte-identical. |
> | Q-03 "not urgent, a trap for a future retune" | **Wrong, and the entry says so itself.** It was sprung within the day by Q-14. |
> | Q-07/Q-08 "before the working tree is committed" | **Expired unactioned** — the tree was committed. See both entries. |
>
> The sequencing lesson that generalises: *fix the entries that move published figures one at a
> time, and batch the ones that move none.* Both halves of that were borne out.

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

> [!NOTE]
> **Both sides of this equation have since moved, and the right-hand name is now wrong.** The
> passion term is `0.251087` and the total `0.250709`, because vanilla's 5-pip budget now runs
> through vanilla's own discretizing spend loop (**Q-14**) — both sides moved together, so the
> invariant itself is untouched and still exact. But it is no longer `FaithfulBaseline()`: that
> function returns the dispersion-aware typical `0.2422` since **Q-03** was fixed. The invariant
> compares against a constants-derived expression, not against that function, so it was never
> affected — only this line's *name* for the quantity was. See Q-03's fix section.

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

**Severity: ~~Minor~~ → Major as of 2026-08-09.  Confidence: high on the structure; the magnitude is
measured, and it is no longer zero.**

> [!CAUTION]
> **Re-measured after the Q-14 fix landed: `Faithful`'s two estimators now differ by −3.39%, where
> they previously agreed to six decimals.** Everything below about the *structure* is unchanged and
> still correct; the "measured at 0.00pp" argument that made this Minor is **void**. The full
> re-measurement, the per-preset table and the mechanism (`E[spent]` is a staircase, and the mean
> band lands just above one of its jumps) are in **Q-14's fix section**. This entry is the one that
> should be worked next.
>
> The prediction in this entry's own text was that the trap would be sprung "by any retune that
> pushes `Faithful`'s band against a clamp". That is not what sprung it — a change to the *shape* of
> the passion function did, with the band untouched. The entry was right that the coupling was
> latent and wrong about what would trigger it.

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

### ✅ Fixed 2026-08-09

**The fix was the one this entry declined to commit to: `FaithfulBaseline()` moved onto
`TypicalAt`.** By the time it was taken the amount was no longer "currently immeasurable" — Q-14
made it `−3.39%` on `Faithful`, and since `FaithfulBaseline` is the denominator of every displayed
percentage *and* the centre line of the distribution curve, the whole readout was skewed by it.

One function changed. The numerators were already dispersion-aware
(`ProfileEditorTab.cs:278` → `TypicalAt`); only the denominator was not.

**The Best-of-N row was already correct** and is untouched: it divides by
`FaithfulBestOfNBaseline(n)`, which is `Faithful`'s own Best-of-N at the same `N`, dispersion-aware
on both sides. That is why the defect was confined to the `Typical` row — and it is worth noting
that the *reason* it was correct is the "same-N baseline" warning already sitting on
`FormatPowerPercent`, i.e. this class of bug had been reasoned about once and the reasoning simply
was not carried to the other readout.

**No shipped figure moves.** `envelope_check.py` reports `EnvelopeFigures.g.cs: unchanged` and Rule
1 / Rule 2 still PASS — the envelope table always used the dispersion-aware estimator for both
numerator and denominator, which is exactly why the tool could not see this defect. It is an
in-game-readout defect only, and no offline gate could ever have caught it.

### There are now two `Faithful` baselines, and conflating them is how this recurs

This fix **splits a number that used to be one number**, which is a trap worth naming:

| | value | what it is | used by |
|---|---|---|---|
| **readout** | `0.2422` | `Faithful`'s dispersion-aware typical | `FaithfulBaseline()`, the `Typical` row, the curve's centre |
| **mean-band** | `0.2507` | what a zero-variance vanilla pawn scores | the both-axes-off invariant (**Q-16**), the `0.2500` argument |

Both are correct and they measure different things: a disabled axis is a zero-variance constant,
while `Faithful` with its axes on has real spread, and the composite is not linear across it. **The
Q-16 invariant still holds and still reads `0.250709`** — it was never comparing against
`FaithfulBaseline()`, it compares against a constants-derived expression, so it was unaffected. But
its *prose* said "which is the Faithful baseline", and that sentence is now false. Corrected in
`HANDOVER.md`, `Constants.cs`, `DebugActions.cs` and `PawnVarianceSettings.cs`; `envelope_check.py`
now prints **both** figures side by side with their labels, so the next reader cannot quote one for
the other.

### What was not verified

Not exercised in game. The readout should now show `Faithful` at `Baseline (0.24)` rather than a
small non-zero percentage, and every other preset's `Typical` percentage moves by roughly +3.4pp
relative to what it displayed before — neither has been observed on screen.

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

### ✅ Fixed 2026-08-09

The floor moved into `PassionNormFor`, which both branches of `CalculateCompositeScore` already
share, so the live branch and the disabled-axis fallback cannot acquire it separately. It is a
**parameter** (`floorToOne`) rather than a read of `v.passionCountMin`, because the fallback branch
has no profile to ask — it scores *vanilla's* budget, and vanilla always floors. Mirrored into
`envelope_check.py`'s `passion_from` the same way. The applier's third clause,
`alreadyCommittedPips <= 0f`, is deliberately **not** mirrored: it is about the grow-up top-up path,
which scores nothing.

Magnitude, since the entry above left it uncomputed. On a custom profile at
`passionCountMin = 0.2, passionCountMax = 0.8`:

| `q` | composite before (no floor) | after (floored) |
|---|---|---|
| 0.00 | 0.034783 | **0.068809** |
| 0.25 | 0.060870 | **0.094896** |

— i.e. the un-floored function scored such a pawn at roughly **half** its real value at the bottom
of the band. `passionCountMin = 0` correctly suppresses the floor, so the `Desperate`-style explicit
request for passionless pawns still works.

**The decisive check is a new one, and it is worth keeping**: at zero spread the dispersion model
must reduce to the mean band, so `TypicalAt` and `CalculateCompositeScore` must agree *exactly* —
and they can only do so if both apply the floor. Measured over 101 qualities on the profile above:
worst deviation **1.4e-16**. Before the fix the same comparison would have read ~0.035. That test
costs nothing and would have caught this on the day the 2026-08-08 floor fix shipped.

**No shipped figure moves** — no preset has `passionCountMin` below 2.2, so the floor cannot engage
on any of them. `envelope_check.py` reports `EnvelopeFigures.g.cs: unchanged` for the `Scores` array
and Rule 1 / Rule 2 still PASS. Build clean, 0 warnings.

### ⚠️ This entry's stated reachability was already out of date when it was filed against this tree

The "why it matters" paragraph above says `CalculateCompositeScore` "is not dead: it computes
`FaithfulBaseline()` … and `MapToCenteredX`". **Neither is true any more, and the change that made
them untrue is Q-03's fix, three entries up in this same register.** `FaithfulBaseline()` now calls
`DispersionModel.TypicalAt`, and `MapToCenteredX` only calls `FaithfulBaseline()`. A grep over
`Source/` on 2026-08-09 finds **no caller of `CalculateCompositeScore` at all** — every other hit is
a comment.

So the C# half of this fix corrects a function the game does not currently call. It was still worth
making, and the function was annotated rather than deleted, for reasons recorded at its definition:
it is the C# mirror of `envelope_check.py`'s `make_composite`, which *is* live (the tool's zero-noise
self-check and the printed mean-band baseline both run through it), and it is the project's only
expression of the mean-band estimator `f(E[X])`. The Python half of this fix is live either way.

**The lesson is about the register, not the code**: an entry's reachability argument has a shelf
life measured in *other entries from the same audit*. Q-03 was fixed on the same day this was filed
and silently invalidated it.

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

### ✅ Fixed 2026-08-09

**Seven, not six.** All six listed constants were added to `GEN_CONSTANTS` and to
`DebugActions.VerifyBestOfN`'s stale-table block, plus `PassionBudgetClampFactor`: Q-06's fix made
both quadratures derive their truncation window from it, so it stopped being a Monte-Carlo-only
input and became a scoring constant on every side. Filing it here rather than as a new entry because
it is the same defect with the same cause.

`GEN_CONSTANTS` also gained a rule, since "which constants belong in the drift check?" is exactly
the question that produced this gap: *if `required` reads a constant to compute a score or a moment,
it belongs in `GEN_CONSTANTS`.* Mechanical, and it would have caught all seven. The reading that let
them out was "the check covers the weights".

`EnvelopeFigures.g.cs` was regenerated. **The `Scores` array is byte-identical** — the file gained
exactly seven `Gen*` constants and nothing else, which is the proof that this fix is diagnostic-only
and moves no figure.

**What this does not fix.** The check compares the golden file's constants against the live
`Constants.cs`; it cannot tell you what a drifting constant *did* to the figures. It names the cause
so an unexplained mismatch downstream is attributable, which is what the entry asked for.

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

### ✅ Fixed 2026-08-09

Both quadratures now derive the window instead of asserting it in a comment.
`DispersionModel.EnsureNodes` reads `zmax = Constants.PassionBudgetClampFactor` and builds
`dz = 2·zmax / GaussNodes` from it; `envelope_check.py`'s `_gauss_nodes` takes `C` and reads the same
constant. All three sites — both quadratures and `dispersion_mc.py` — now move together.

Verified two ways: the shipped figures are **unchanged** (`EnvelopeFigures.g.cs` `Scores` identical,
Rule 1 / Rule 2 PASS, self-check `4.10e-04` before and after), which is what "numerically identical
today" predicts; and forcing the constant to `3.0` moves the node range to `±2.954` instead of
`±3.939`, i.e. the derivation genuinely tracks the constant rather than coincidentally matching it.

`PassionBudgetClampFactor` was also added to the golden-file drift check as part of Q-05 — this fix
is what turned it into a scoring input on all three sides, so it now needs the same protection as
the other scoring constants.

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

> [!NOTE]
> **⏸️ Deferred by the owner, 2026-08-09**, as not important — not fixed, not withdrawn.
>
> **Re-verified, and the framing has aged out.** The disagreement itself is still live:
> `AUDIT-2026-08-06-problem-register.md:187` still says `GrowUpVariance.cs` is **NEXT UP** while
> `HANDOVER.md:1566` says `Source/DebugActions.cs` is. What is no longer true is "in the same
> *uncommitted* batch" — both files were committed in the interim, so this is now a committed
> contradiction between two documents rather than something a pre-commit check would catch. The
> advice to "resolve it before the working tree is committed" has therefore already expired
> unactioned; the fix is now a one-line edit to the older register whenever it is next opened.

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

> [!CAUTION]
> **⏸️ Deferred by the owner, 2026-08-09** as not important — but re-verification found this entry
> describes a door that **has since closed**, so record what actually happened rather than the
> warning.
>
> This entry was filed as *"the uncommitted diff will delete the figure"*. That diff has been
> committed. A grep for `1.19` on 2026-08-09 returns **nothing in the working tree and nothing in
> `HEAD`**, and `HANDOVER.md` never carried it. So the passion-axis rescale figure
> `passionNoise 0.25 went sigma 1.19 -> 1.00 (-16%)` is **already gone from the repo** — it is not
> at risk, it is lost, and recovering it means reading a pre-2026-08-06 commit or re-deriving it
> from `PassionBudgetSpreadMin`'s old value.
>
> That is not fatal: it is a historical tuning figure, the skill-axis equivalent survives in
> `HANDOVER.md`, and the constant it describes is current and documented. Recorded because the
> entry's own point was that the handover tells readers to consult a table, and for the passion axis
> there is still no table to consult.
>
> Adjacent, for the avoidance of doubt: **Q-11** rewrote the surrounding comment (it contained the
> dead `passionNoise` identifier). It did not delete this figure — the figure was already absent
> from that comment — and it did not re-add it.

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

### ✅ Fixed 2026-08-09 — and the comment was wrong in a second way the entry did not reach

Answering "what *is* the source of raw disagreement now?" turned up more than a stale sentence.
**The two sides no longer differ in resolution at all**, so the tolerance's entire premise is gone:

| | mod | tool |
|---|---|---|
| q-nodes | `DispersionModel.QNodes` = 256 | `QGRID` = 256 |
| x-nodes | `XNodes` = 512 | `XGRID` = 512 |
| triangular / Gauss nodes | 65 / 65 | `TGRID` / `GGRID` = 65 / 65 |

The reference `Scores` are integrated on **the same grid the mod uses**. `GRID = 20000` and
`Constants.BestOfNIntegrationNodes = 1024` — the two numbers the gate printed in its own header as
"reference 20000 nodes, live 1024 nodes" — belong to the retired analytic scheme. `beta_grid`'s
`run +=` survives, but only on the tool's zero-noise self-check path, not on the path this gate
compares. So the expected raw gap is **float-precision-scale** (float32 vs float64, plus
`MathUtil.NormalCdf`'s ~1.5e-7 Erf), not the ~0.9% the comment claimed.

Three consequences, and the third is the one to argue with:

1. The comment now states all of the above, including which claims it replaces.
2. The header line reports `dispersion grid 256q x 512x on both sides` instead of two node counts
   neither integrator uses. **`Constants.BestOfNIntegrationNodes` thereby lost its last reader** and
   is now dead; it is annotated as such at its definition, keeping the 0.35pp/0.17pp measurement,
   rather than deleted.
3. **The 3% tolerance was left at 3%.** It is now ~4 orders of magnitude looser than the
   disagreement it is sized for, which is a real argument for tightening it — but this gate *has
   never been run against a running build since the dispersion model landed*, so the tiny expected
   gap is predicted, not measured. Tightening a threshold onto a prediction is how a gate starts
   failing for no reason. The comment says explicitly: tighten it once the gate has been run and
   the observed deviations are in hand. Until then the 0.5pp **display** tolerance is what carries
   the weight, and it is unaffected.

**Still not verified in game** — which is precisely why (3) went the way it did.

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

### ✅ Fixed 2026-08-09 — and swept beyond the one file

Owner instruction was to remove **any** support for or mention of 1.5, so the fix was not confined
to the entry:

| Site | Change |
|---|---|
| `About/LoadFolders.xml` | the `<v1.5>` mapping deleted; `<v1.6>` is the only entry |
| `Source/GrowthUpPatch.cs:10` | *"Verified against RimWorld 1.5/1.6's decompiled Assembly-CSharp.dll"* → **1.6**. This one mattered more than the XML: it claimed a verification against an assembly nobody in this repo has ever compiled against |

Checked and found clean: `PawnVarianceMod.csproj` names no RimWorld version (it references a single
install), and `HANDOVER.md` makes no version claim at all.

**Deliberately kept:** the *rationale* comments in `About.xml` and the new one in `LoadFolders.xml`,
both of which name 1.5 in order to say it was dropped and why. They are the guard against someone
re-adding it, i.e. the opposite of a support claim. Say so if you want them stripped too.

**Not touched:** dated documents under `docs/superpowers/` (the 2026-07-27 spec and plans) that
record 1.5 as a target *at the time they were written*, and `AUDIT-2026-08-06-problem-register.md`'s
P-07 entry. Editing those would falsify the record rather than remove a claim — the mod's live
targeting is what "support" means here, and it is now 1.6 everywhere.

Every remaining `1.5` in the tree is the **Major passion cost in pips**, an unrelated quantity that
happens to share the digits.

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

### ✅ Fixed 2026-08-09

All three, each in the way its context called for rather than by blanket search-and-replace:

- **`Constants.cs`, `PassionBudgetSpreadMin`** — rewritten to name `passionSpread` and its
  `PassionNoiseScalar` accessor, and to attribute the clamp window to `PassionBudgetClampFactor`
  rather than to "vanilla's hardcoded 4" (true of vanilla, no longer true of us — see Q-06). The
  old name is kept in one trailing sentence *as* the old name.
- **`HANDOVER.md`, `MinMagnitudeFloor`** — now reads `MagnitudeLerpLow (named MinMagnitudeFloor
  until P-11 renamed it — the old name greps to nothing)`. The history the entry called "arguably
  intentional" is preserved; what changed is that the greppable name is the one in front.
- **`HANDOVER.md`, the `skillNoise` rescale table** — the entry's actual complaint was that the
  table reports pre-rename 0–1 scalar values without saying so, unlike the comparable passage
  further down. A note now says so, and the column header reads `skillNoise (pre-rename scalar)`.
  The figures are deliberately left unconverted: they record a retune that happened in those units.

Left alone as genuine history: the four places `HANDOVER.md` describes the rename itself
(*"`skillNoise`/`passionNoise` → `skillSpread`/`passionSpread`"*) or the one-line accessor change in
the two appliers. Those sentences are *about* the old names.

**Found in the same sweep, same class, not previously filed:** `Constants.cs`'s
`VanillaPassionBudget` comment worked its own arithmetic to `5 × 0.9391 / 18 = 0.2609` — the
pre-Q-14 continuous figure. Since Q-14 that branch spends through the loop and delivers 4.8125 pips,
giving `0.2511`. Corrected, with the old value named so it is recognisable if it turns up elsewhere.

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

### ✅ Fixed 2026-08-09

The sort is gone and the array joins `curveDensityScratch` as a cached `curvePointScratch`, so the
method now allocates nothing per frame.

The monotonicity argument is written into the code rather than left in this register, because
removing a sort is a **correctness** claim and the next reader has to be able to check it without
finding this file: `power` is `(i + 0.5f) / CurveSamples` for increasing `i`, and `MapToCenteredX`
is monotonically non-decreasing — both branches are `constant + positive slope × input` and they
meet continuously at `compositeScore == baseC`, where both evaluate to `0.50`. A strictly increasing
input through a non-decreasing map is non-decreasing. The comment also names the condition under
which the removal stops being safe (a non-monotonic branch added to `MapToCenteredX`) and says to
restore the sort rather than reorder the map.

**Not profiled, and still not worth profiling** — the justification is that the allocation was
inconsistent with the method it sits in, not that it was measurably slow.

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

### ✅ Fixed 2026-08-09 — the models were corrected, the generator was not touched

**Owner decision (Rule 5): model the discretization.** The competing option — making the generator
stop discarding the remainder — was rejected on the ground the register itself supplies: vanilla's
generator spends through the same discretizing loop, so our generator is *faithful* and it is the
model that invented a continuous spend. "Fixing" the generator would have bought a tidier readout by
breaking the vanilla-parity property `Faithful`'s whole band is built on.

**No pawn changes. `PassionVarianceApplier` is byte-identical.**

New `Source/PassionSpend.cs` returns the **exact** distribution of delivered pips, not an estimate.
Every branch in the loop compares the remaining budget against `MinorPassionCost` or
`MajorPassionCost`, so the outcome distribution depends on the budget only through which pair of
consecutive thresholds it falls between — between two breakpoints it is literally constant, so a
table indexed by breakpoint *is* the answer rather than a discretisation of it. The table is also
tiny: the loop exits holding less than one Minor, so delivered pips lie in
`(budget − MinorPassionCost, budget]` and at most three totals carry mass (two at the shipped
costs). That is what lets every caller apply the capacity limit and `Clamp01` **per outcome**
instead of to a mean, which keeps the *second* moment exact — the discretization removes a little
dispersion as well as shifting the mean, and Best-of-N is a maximum statistic that reads dispersion
directly.

| Site | Change |
|---|---|
| `Source/PassionSpend.cs` | new — the exact spend-loop distribution, shared by both C# consumers |
| `DispersionModel.Moments` | spends each Gauss node's budget through the loop; capacity and `Clamp01` per outcome, so `p2` stays exact |
| `CalculateCompositeScore` | new `PassionNormFor` helper, shared with the disabled-axis fallback so the two branches cannot drift |
| `envelope_check.py` | `make_spend`, used by `make_composite` and `grid_moments`; `PassionBudgetClampFactor` added to `required` |
| `dispersion_mc.py` | runs the real loop, sampled — deliberately *not* the exact table, so it stays an independent method |
| `DebugActions.cs` | invariant 1 routed through the loop; new invariant 1b (below) |

**Order of operations: spend, then cap.** The loop runs *before* the capacity limit because that is
the generator's order — it spends the whole budget into whole passions and only discovers how many
eligible skills exist at assignment, discarding the surplus there. This only matters in the tail
where capacity binds, but it is a decision the fix was forced to make and it is recorded here rather
than left implicit.

**The vanilla fallback discretizes too**, and it had to. Vanilla's generator runs the same loop, and
`Faithful`'s band at `q = 0.50` *is* `VanillaPassionBudget` pips at `VanillaMajorBias` — so if the
live branch discretized and the fallback did not, the two would sit on different scales and **Q-16's
both-axes-off invariant would break**. Both sides moved together and it still holds exactly:
`(0.8 × 0.25 + 1.5 × 0.251087) / 2.3 = 0.250709`, verified at `1.1e-16`.

### What moved

| | before | after |
|---|---|---|
| `FaithfulBaseline` | `0.257089` | **`0.250709`** |
| `Sovereign` @ N=1 | +24.7% (10.3pp headroom) | **+26.3% (8.7pp)** |
| `Wildcard` @ N=50 | +25.9% (9.1pp) | **+26.5% (8.5pp)** — still the tightest in the mod |
| `Desperate` @ N=1 | −20.8% | **−22.1%** |
| tool self-check | `2.13e-04` | `4.10e-04` |

Every figure moved, every preset moved the same way, and **the tier ordering is unchanged**. Rule 1
and Rule 2 PASS at N = 1/5/25/50. `EnvelopeFigures.g.cs` regenerated; every pasted table in
`HANDOVER.md` repasted from the tool rather than hand-edited. Build clean, 0 warnings.

The predicted `Sovereign` headroom in the entry above — *"reduces it to roughly 8.7pp"*, derived
from a 400k-pawn simulation before any code was written — came out at **8.7pp**.

### How it was verified

- **`dispersion_mc.py` reproduces all 32 cells to ≤ 0.0004.** This is the check that matters: the
  exact table claims to be the distribution of a loop it never runs, and the Monte Carlo is the only
  place that loop is actually executed. Its own self-check averages 20,000 real spend loops per
  point and lands `1.47e-04` against a `1e-3` tolerance.
- **Table vs. loop, directly**: 300k sampled loops at 13 budgets × 3 biases reproduce the table's
  mean *and* standard deviation to Monte-Carlo error, including across a breakpoint
  (`b = 5.49` → `4.7312`, `b = 5.50` → `5.4230`).
- **Monotonicity re-proved.** `HANDOVER.md`'s Best-of-N derivation requires `composite` monotonic in
  `q`; the spend loop makes the passion axis a *step* function, so this was no longer inherited.
  Checked exhaustively: `E[spent]` non-decreasing in budget over 101 biases × 6,801 budgets, and
  `composite` non-decreasing in `q` over 20,001 points × 8 presets. Both hold.

### The self-check that had to be restated, and why it is not a weakened test

`envelope_check.py`'s `with_noise=False` self-check failed at `4.06e-03` on the first run. That was
correct behaviour, not a tolerance problem: **the spend loop flips a coin per passion, so it is a
dispersion source that survives zeroing both spread fields.** "Zero noise" had stopped meaning "zero
variance", and the check was comparing a Normal approximation of a two-point distribution against an
exact mean. The mode now suppresses the loop's *variance* while still routing its *mean* through the
loop, so the analytic side averages the same outcomes and the check keeps its teeth. Same reasoning
in `dispersion_mc.py`, whose self-check was exact to `1e-12` and now averages 20,000 draws against a
Monte-Carlo tolerance — a genuine loss of strength, recorded rather than hidden.

### ⚠️ This fix promoted Q-03 from `0.00pp` to a live defect — see that entry

`Q-03` was filed Minor **because it measured `0.000pp`**: `TypicalAt` (dispersion-aware, the
readout's numerator) and `CalculateCompositeScore` (mean-band, the shared denominator) agreed to six
decimals on `Faithful`. They no longer do. Re-measured after this fix:

| Profile | `TypicalAt` | mean-band composite | gap |
|---|---|---|---|
| **Faithful** | 0.242215 | 0.250709 | **−3.39%** ← this one is the denominator |
| Desperate | 0.189226 | 0.195159 | −3.04% |
| Elite | 0.295470 | 0.303061 | −2.50% |
| Wildcard | 0.233955 | 0.228976 | +2.17% |

The cause is specific and was not obvious: `E[spent(b)]` is a staircase, and the mean band lands at
`b = 5.0`, which sits **just above a jump** (`4.8125` delivered), while the average of the staircase
over the budget Gaussian is `≈ 4.55`. `f(E[X])` and `E[f(X)]` were nearly equal on a smooth function
and are not on a step function.

**The shipped envelope table is unaffected** — it uses the dispersion-aware estimator for both
numerator and denominator, consistently. What is affected is the **in-game Row 3 readout**, whose
denominator is `FaithfulBaseline` (mean-band) while its numerator is `TypicalAt`. That is now skewed
by ~3.4%. Q-03's own entry says moving `FaithfulBaseline` onto `TypicalAt` is a Rule 5 / Rule 6
action; it was **not** taken in the same edit, because it is a second scoring decision and landing it
here would make this regeneration unattributable — the exact mistake the "Where to start" note warns
about. **It was taken immediately afterwards, as its own change: Q-03 is now ✅ fixed**, and because
it moves no envelope figure the split was what let each change be attributed to its own cause.

### Still not verified in game

Everything above is offline. The **C#/Python cross-check has not been run against a running build** —
that is `Verify Best-of-N against envelope_check.py`, and it is what proves the C# mirror of
`PassionSpend` matches the Python one. Until it is run, the C# side is argued, not measured. The new
invariant 1b (Faithful at zero spread, live branch = fallback branch, exact) has likewise only been
predicted through the Python mirror.

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

### ✅ Fixed 2026-08-09 — rolled back in the `catch`, and bounded at one retry

The fix is where the entry said it had to be. `Postfix` snapshots the pawn's `LastKnownStage` entry
*before* calling `PostfixInner` — that is the value the `catch` needs, and by the time the `catch`
runs `PostfixInner` has already overwritten it — and on a throw restores it (or removes the key if
there was none). The next life-stage firing then sees a genuine `NotAdult → Adult` transition again
and retries. `PostfixInner` is untouched, so its legitimate reasons to record the stage on no-op
paths are preserved.

**The retry is bounded at one, and that is the part the entry did not specify.** An unbounded
rollback is not a safe reading of "undo the write": `GrowUpVariance.Apply` mutates the pawn, so a
thrower that fires partway through would be re-run on every `AgeTickInterval`, additively
re-shifting the same pawn's skills — which is **exactly the save corruption the `LastKnownStage`
dictionary was introduced to end** (see the class comment on the old `HashSet<int>` guard). A
session-only `StageRollbackSpent` set records that a pawn has had its retry; a second throw leaves
the `Adult` baseline standing and accepts the lockout. That buys back the transient cases — a
dictionary mid-rebuild, a race with another mod's faction edit — without reopening the repeating
one. Cleared alongside `LastKnownStage` in `ClearForNewGame`, for the same `thingIDNumber`-collision
reason.

The `Log.ErrorOnce` message now states which of the two happened (`baseline rolled back, so the next
life-stage firing will retry once` / `this pawn already had its one retry`), so the log distinguishes
a recovered pawn from a skipped one.

**Not verified in game, and not reproducible on demand** — the trigger was undemonstrated when
filed and still is. What changed is the consequence of the trigger, not the odds of it. The
untraced question above (can `Apply` throw after partially modifying a pawn?) is *why* the retry is
bounded rather than unbounded: the fix is built to be correct whichever way that answer goes.

---

<a id="q-17"></a>
## Q-17 — The passion-disabled fallback caps capacity in two different orders across the four mirrors, and only current constants hide it

**Severity: Major.  Confidence: high. Filed, not fixed — pre-existing and out of scope for the
branch that found it (2026-08-09 generator-vs-model verification work).**

### What

There are four mirrors of "how does a disabled passion axis score", and they apply the capacity
cap in two different orders:

- `docs/tools/envelope_check.py`'s `grid_moments` else-branch (`:501-507`) and
  `Source/DispersionModel.cs`'s `Moments` else-branch (`:174-190`) compute
  `min(1, pips * eff / pdiv)` with **no capacity cap applied to `pips` at all** — the raw
  vanilla-budget outcome pips go straight into the efficiency/pdiv ratio and then into a `Clamp01`.
- `docs/tools/envelope_check.py`'s `make_composite` → `passion_from` (`:309-331`, the disabled
  branch calls it at `:361`) and `Source/PawnVarianceSettings.cs`'s `PassionNormFor` (`:1475-1499`,
  called from the disabled branch of `CalculateCompositeScore` at `:1573-1575`) apply
  `min(pips, capacity)` **first**, then divide by `pdiv` and clamp.

So two of the four mirrors cap pips at `capacity` before scaling; the other two never cap pips at
all and rely on the `Clamp01` at the very end to catch anything that overshoots.

### Why it matters

It is inert today, and only by luck of the current constants. `Constants.VanillaPassionBudget = 5.0`
sits far below `capacity` at vanilla's own Major bias (**15.00 pips**, measured: 12 skills at a
price of `Minor + (Major - Minor) * 0.5 = 1.25`), so vanilla's outcome pips never
approach `capacity` in the first place — the missing cap in the first pair of mirrors is a cap that
never had anything to clamp. The four mirrors agree with each other today because the input never
exercises the divergence, not because the formulas match.

The condition that makes it live: raise `VanillaPassionBudget` past `capacity` (or lower `capacity`
below the current budget, e.g. by retuning `MajorPassionCost`/`MinorPassionCost`). At that point the
`Moments`/`grid_moments` pair keeps scoring the uncapped pips (bounded only by the late `Clamp01`,
which saturates at 1.0 rather than at the pip figure `PassionNormFor`/`passion_from` would produce),
while the `PassionNormFor`/`passion_from` pair caps first and produces a strictly lower figure for
any outcome whose pips exceed capacity. The two pairs would silently diverge — a real defect with no
gate watching it.

This is the same shape as Q-04 and Q-14: the generator-agreement story ("all four mirrors agree") is
true only because none of them is actually being exercised on the branch that would expose the
difference, which is exactly the failure mode this whole audit series exists to catch.

**Task 2's `check_mean_band_consistency` DOES catch this the moment it becomes live — measured, see
below.** Its `_probe-both-off` probe compares `grid_moments` (uncapped-first) against
`make_composite` (capped-first) at zero spread, which is exactly the two branches that disagree on
capping order. At the CURRENT constants the gap is `0.00e+00` because the cap has nothing to clamp,
so the probe reads as passing — but the gate is re-evaluated on every run, so a retune past the
threshold fails it immediately and by a wide margin. **This entry originally claimed the opposite
("does NOT catch this either"); that claim was wrong and is corrected here.** It was reasoning from
"both sides are exercised equally at today's constants" to "the gate is structurally blind", which
does not follow: the gate is not blind, the input is merely below the threshold.

That materially lowers the risk. The remaining exposure is narrow but real: `Moments` and
`grid_moments` are *mutually* consistent, so the in-game `Verify Best-of-N` 32/32 gate — which
compares those two — stays green through the divergence. Only the offline
`check_mean_band_consistency` sees it.

### Verified — the divergence was forced numerically

Measured 2026-08-09 with a read-only probe that raised `VanillaPassionBudget` **in memory only**
(`zzz-Do-Not-Commit/q17_probe.py`; nothing on disk was changed). `grid_moments` vs `make_composite`
on a both-axes-off Faithful at `q = 0.50`:

| `VanillaPassionBudget` | `grid_moments` | `make_composite` | gap |
|---|---|---|---|
| 5.00 (shipped) | 0.250709 | 0.250709 | `0.00e+00` |
| 14.00 (just under capacity) | 0.556521 | 0.556521 | `0.00e+00` |
| 20.00 (past capacity 15.00) | 0.739130 | 0.597353 | **`1.42e-01`** |

So the mechanism is confirmed, the trigger is exactly the capacity threshold as predicted, and the
magnitude is large — 0.142 on a `[0,1]` axis, roughly 14pp. Against
`check_mean_band_consistency`'s `1e-9` threshold that is eight orders of magnitude over the line,
so the failure would be unmissable rather than marginal.

### Not fixed — filed only

Out of scope for the branch that found it: the owner directed this be recorded, not corrected, so
that a future retune of `VanillaPassionBudget`, `MajorPassionCost` or `MinorPassionCost` is not the
first time anyone learns the four mirrors were never actually equivalent formulas. None of
`Source/PawnVarianceSettings.cs`, `Source/DispersionModel.cs`, or the two functions in
`docs/tools/envelope_check.py` named above were changed by this entry.

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

---

## What this pass changed about the method

Added 2026-08-09 after the closing sweep (Q-04, Q-05, Q-06, Q-09, Q-10, Q-11, Q-13, Q-15). Three
things came out of it that are about *how the register is worked*, not about any one entry.

**1. A finding's rationale can be invalidated by another finding in the same register, silently.**
It happened twice here:

- **Q-04** justified its severity by "`CalculateCompositeScore` is not dead: it computes
  `FaithfulBaseline()` and `MapToCenteredX`". **Q-03's fix removed that**, hours later, by moving
  `FaithfulBaseline` onto `TypicalAt`. The function now has no caller at all.
- **Q-09** described a stale comment. The comment was staler than described: the two integrators no
  longer differ in *resolution* either, which retired `Constants.BestOfNIntegrationNodes` entirely.

Both were caught only because every claim was re-verified against the tree before being acted on —
which is the rule this register already states, applied to its own entries rather than only to the
code. **Before fixing an entry, re-check its "why it matters" paragraph, not just its "what".**

**2. "Fix it where the other sites already agree" is worth preferring to "fix it here".** Q-04's
floor went into `PassionNormFor`, which both branches share, rather than into the two call sites;
Q-06's window is derived in both quadratures rather than corrected in one. In both cases the shape
of the fix removes the possibility of the same drift recurring, which a correct-value-in-two-places
fix does not.

**3. Three fixes produced a test that did not exist before, and those are the durable part.**

| Fix | The check it left behind |
|---|---|
| Q-04 | At zero spread, `TypicalAt` must equal `CalculateCompositeScore` **exactly** — measured `1.4e-16`. Only true if both apply the floor; would have caught this on the day the 2026-08-08 floor fix shipped. |
| Q-05 | Seven more constants whose drift is now *named* by the in-game gate rather than surfacing as an unexplained mismatch. |
| Q-06 | Forcing `PassionBudgetClampFactor` to 3.0 moves both quadratures' node ranges — proof the value is derived, not coincidentally equal. |

**What none of this pass did: run in game.** Every claim above is offline. `envelope_check.py`
PASSes Rule 1 and Rule 2 at N = 1/5/25/50, `EnvelopeFigures.g.cs`'s `Scores` array is byte-identical
(the file gained seven `Gen*` constants and nothing else), and the build is clean at 0 warnings —
but the C#/Python cross-check `Verify Best-of-N against envelope_check.py` **has still never been
run against a running build since the dispersion model landed**, which is why Q-09's 3% raw
tolerance was left alone rather than tightened onto a prediction. That run is the outstanding
verification for this register as a whole, not for any single entry in it.
