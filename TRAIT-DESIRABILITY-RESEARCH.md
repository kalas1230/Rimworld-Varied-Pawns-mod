# Trait Desirability — Research & Decision Record

Date: 2026-08-03 (rev 3)
Status: **CLOSED — RESOLVED WITHOUT BUILDING A DESIRABILITY ENGINE.**
Scope: Evaluates the "Dynamic Data-Driven Trait Desirability Engine" proposed in `HANDOVER.md` §2.

> # ⛔ DO NOT BUILD THE DESIGN IN §7
>
> **§7 is retained as a record of a design that was investigated and then rejected. It was never
> implemented and should not be.** §8's "blocking items" (in-game post-patch coverage scan, verifying
> `PawnKindDef` cloning) are **moot** — they gated a build that is not happening.
>
> **What actually shipped instead** (2026-08-03, see §10):
> 1. **Trait count removed from `CalculateCompositeScore`.** It is a variance parameter, not a mean
>    one; scoring it rewarded widening spreads, which makes pawns *worse*.
> 2. **Preset trait ranges narrowed toward vanilla's 2–3.** This shrinks the inversion at its source
>    — the inversion's size is proportional to `traitCountMax − traitCountMin`.
>
> Zero new runtime code. No coverage dependency. Nothing that can be silently wrong in a 1000-mod
> pack. **If you are here because you want to make high-quality pawns roll fewer bad traits: that
> problem is already addressed. Read §10, not §7.**

> **Rev 2 changelog.** Rev 1 was reviewed by six independent agents (3 Claude, 3 Gemini via
> agy-bridge). The review found **two genuine bugs in rev 1's data collection** and **one
> mathematically unsound element in rev 1's recommended design**. Both are corrected here.
> Rev 1's headline figure (60.3% invisible) was **wrong**; the corrected figure is **46.7%**.
> Rev 1's recommended rejection-sampling mechanism is **withdrawn** and replaced.
> See §9 for the full review log.

---

## TL;DR

The problem in `HANDOVER.md` §2 is real: quality currently buys a pawn **more trait rolls from a
quality-blind picker**, so high-quality pawns are *more* likely to hit a colony-ruining trait, not
less. Quality is currently anti-correlated with "won't burn the base down."

A general continuous desirability scoring engine remains **not recommended** — 46.7% of modded trait
degrees carry no mechanical XML, and scoring an unreadable trait as 0 invents a fact.

The recommended fix is **narrow, targeted at the marginal draws quality actually bought, and
delivered through vanilla's own exclusion pipeline** — not through rejection sampling, which
provably distorts the distribution it claims to preserve.

---

## 1. The problem being solved

`Source/TraitVarianceApplier.cs:39-54` computes trait count as a lerp from `traitCountMin` to
`traitCountMax` by quality, then hands the delta to `PawnGenerator.GenerateTraitsFor`, which has no
concept of quality.

Consequence: a Sovereign pawn does not merely have the same odds of rolling Pyromaniac as a
Desperate pawn — it has **more independent draws from the same urn**, so its odds are strictly
higher. This is an inversion, not a missing feature.

**Precise statement of the defect:** only the draws *above* `traitCountMin` are implicated. A pawn at
the floor has exactly vanilla odds. This matters for the design in §7.

---

## 2. Ground truth: decompiled structure

Decompiled from `Assembly-CSharp.dll` into `zzz-Do-Not-Commit/decompile/` via
`ilspycmd <dll> -t <Namespace.Type>`.

### 2.1 `TraitDef` (trait-level fields)

| Field | Line |
|---|---|
| `degreeDatas` | 8 |
| `conflictingTraits` | 10 |
| `exclusionTags` | 12 |
| `conflictingPassions` | 14 |
| `forcedPassions` | 16 |
| `requiredWorkTypes` | 18 |
| `requiredWorkTags` | 20 |
| `disabledWorkTypes` | 22 |
| `disabledWorkTags` | 24 |
| `disableHostilityFromAnimalType` | 26 |
| `disableHostilityFromFaction` | 28 |
| `canBeSuppressed` | 30 |
| `commonality` / `commonalityFemale` | 32 / 34 |
| `allowOnHostileSpawn` | 36 |

### 2.2 `TraitDegreeData` (per-degree fields)

`statOffsets`(29), `statFactors`(31), `thinkTree`(33), `randomMentalState`(35),
`randomMentalStateMtbDaysMoodCurve`(37), `forcedMentalState`(39), `forcedMentalStateMtbDays`(41),
`disallowedMentalStates`(43), `disallowedThoughts`(45), `disallowedThoughtsFromIngestion`(47),
`extraThoughtsFromIngestion`(49), `disallowedInspirations`(51), `mentalBreakInspirationGainSet`(53),
`allowedMeditationFocusTypes`(57), `disallowedMeditationFocusTypes`(59),
`mentalBreakInspirationGainChance`(61), `theOnlyAllowedMentalBreaks`(63), `skillGains`(65),
`socialFightChanceFactor`(67), `marketValueFactorOffset`(69), `randomDiseaseMtbDays`(71),
`hungerRateFactor`(73), `painOffset`(75), `painFactor`(77), `mentalStateGiverClass`(79),
`abilities`(81), `ingestibleModifiers`(83), `aptitudes`(85), `enablesNeeds`(87), `disablesNeeds`(89),
`possessions`(93).

### 2.3 `MentalStateDef` (verified — needed for §7's flag)

| Member | Line |
|---|---|
| `stateClass` | 11 |
| `category` (`MentalStateCategory`) | 15 |
| `blockNormalThoughts` | 33 |
| `recoveryMtbDays` | 55 |
| `IsAggro => category == MentalStateCategory.Aggro` | 108 |

**Note the exact names.** A review agent asserted `causesHarm` (does not exist), then
`isAggressive`/`Aggressive` (also wrong). The real member is the property **`IsAggro`**, comparing
against **`MentalStateCategory.Aggro`**. See §9 reliability note.

### 2.4 Errors in the `HANDOVER.md` §2 field list

The handover's §2.1 list was written from memory and is wrong in several places:

- **`disabledWorkTags` is on `TraitDef`, not `TraitDegreeData`.** It therefore cannot vary by degree.
- **Omitted entirely:** `disabledWorkTypes`, `requiredWorkTypes`, `requiredWorkTags`,
  `conflictingPassions`, `forcedPassions`, `disableHostilityFromAnimalType`,
  `disableHostilityFromFaction`, `ingestibleModifiers`, `enablesNeeds`, `disablesNeeds`,
  `randomDiseaseMtbDays`.

### 2.5 Where the tooltip comes from

`Trait.cs:127` — `TipString` appends `currentData.description` first, then lines derived from
`skillGains`, `statOffsets`, `allowedMeditationFocusTypes`, and memes. **Every structured source it
uses is one we already scan.** For a trait with no mechanical fields, the tooltip is prose only.

### 2.6 How the picker filters candidates

`PawnGenerator.GenerateTraitsFor` rejects a candidate when
`request.KindDef.disallowedTraits.NotNullAndContains(newTraitDef)`, among other checks
(`disallowedTraitsWithDegree`, `requiredWorkTags` vs `disabledWorkTags`, `ProhibitedTraits`,
hostile-spawn `allowOnHostileSpawn`, and a `MentalBreakThreshold` gate).

**Critical for §7:** these checks read the **request**, not `pawn.kindDef`. A call passing
`request: null` skips them entirely — which is exactly what `GrowUpVariance.cs:209` does.

---

## 3. Measured data

### 3.1 Vanilla + Anomaly + Biotech

**51 `TraitDef`s, 73 degrees.**

| Signal | Hit rate | Traits |
|---|---|---|
| `randomMentalState` / `forcedMentalState` | **3/73 = 4.1%** | void fascination, pyromaniac, gourmand |
| `disabledWorkTags` / `disabledWorkTypes` | **1/51 = 2.0%** | Pyromaniac only |
| `marketValueFactorOffset` | 7/73 | abrasive −0.15, annoying voice −0.2, creepy breathing −0.1, pyromaniac −0.2, wimp −0.15, chemical fascination −0.15, chemical interest −0.10 |
| custom `mentalStateGiverClass` | **0** | (never used by vanilla) |

Field content of specific traits:

```
Bloodlust        socialFightChanceFactor, allowedMeditationFocusTypes, possessions
Psychopath       allowedMeditationFocusTypes, statFactors, possessions
Wimp             statOffsets, statFactors, marketValueFactorOffset
SlowLearner      statOffsets, statFactors
Nerves    (×4)   statOffsets, statFactors
Industriousness  (×4) statOffsets
```

**Note:** "Depressive" is not a separate `TraitDef` — it is `NaturalMood` degree −2, a stat-offset
trait. Two of the three examples named in `HANDOVER.md` §2 (Wimp, Depressive) are therefore
*deterministic stat traits*, not mental-state traits. **This is load-bearing against §7's flag design
— see §7.3.**

### 3.2 Progression Modpack (1376 workshop mods) — CORRECTED

**142 unique modded `TraitDef`s, 150 degrees, from 325 XML files containing `TraitDef`.**

> **VISIBLE (≥1 mechanical field): 80/150 = 53.3%**
> **INVISIBLE (no mechanical field): 70/150 = 46.7%**

Visibility source: **67 degrees** via degree-level fields, **13 degrees** via `TraitDef`-level fields
only (these were the ones rev 1 miscounted).

Per-mod coverage (mods with ≥4 degrees):

```
 29/ 53 =  54.7%  Vanilla Traits Expanded
 17/ 36 =  47.2%  The Sims Traits
  7/  7 = 100.0%  Vanilla Anomaly Expanded - Insanity
  4/  6 =  66.7%  Altered Carbon 2: ReSleeved
  2/  4 =  50.0%  Cyanobot's Genes
  2/ 10 =  20.0%  Progression: Education
  0/  4 =   0.0%  Dubs Bad Hygiene
  0/  4 =   0.0%  Humanoid Alien Races
```

#### 3.2.1 What rev 1 got wrong

| | rev 1 (buggy) | rev 2 (corrected) |
|---|---|---|
| Unique defs / degrees | 144 / 151 | 142 / 150 |
| Visible | 60/151 = 39.7% | **80/150 = 53.3%** |
| Invisible | 91/151 = **60.3%** | **70/150 = 46.7%** |
| VTE coverage | 39.6% | **54.7%** |
| Altered Carbon 2 | 0.0% | **66.7%** |

Three collection bugs, all found by review:

1. **Only degree-level tags were checked.** `disabledWorkTypes`, `disabledWorkTags`,
   `forcedPassions`, `requiredWorkTags` live on `TraitDef`. A trait declaring those with a bare
   `degreeDatas` was miscounted as fully invisible. **This alone accounts for 13 degrees.**
2. **`modExtensions` was not in the field set.** Humanoid Alien Races and Vanilla Expanded Framework
   express trait mechanics through `<modExtensions><li Class="...">`.
3. **`possessions` was wrongly excluded as cosmetic.** Spawning a pawn with forced equipment is a
   real mechanical effect (`TraitDegreeData.cs:93`).

A fourth issue was corrected defensively: de-duplication on `(modName, defName)` took whichever
version subfolder `os.walk` reached first, which is non-deterministic on NTFS. Rev 2 prefers the
**highest** version folder. The 3.2× raw-to-unique collapse ratio was independently assessed as
sound and expected (mods ship 3–4 version folders plus `Common/`).

#### 3.2.2 Known remaining bias: the scan is pre-patch

**This number is still not the number the mod will see at runtime.** The scan reads raw XML from
disk. RimWorld applies `PatchOperation`s at load, *before* `DefDatabase<TraitDef>` is populated, and
the mod reads post-patch defs. Patch mods in this pack:

```
1657 refs  Inspiration Tweaks
 665 refs  RimTraits - Vanilla Trait Colors
 472 refs  Combat Extended
 310 refs  Vanilla Traits Expanded
 187 refs  Bad Can Be Good (Continued)
 160 refs  Too Many Mods - Compats and Rebalances
 121 refs  Alpha Armoury
  68 refs  Vanilla Anomaly Expanded - Insanity
  45 refs  Better Pyromania
   9 refs  Childrens' Traits Affect Learning
```

Patches that **add** mechanical fields (Combat Extended adds its own stat offsets; Inspiration Tweaks
adds `disallowedInspirations`/`mentalBreakInspirationGainSet`) move traits from invisible to visible.
**Direction of bias is certain: true runtime invisibility is lower than 46.7%.**

Magnitude is unknown and should not be guessed. One review estimated 30–45% but counted *RimTraits –
Vanilla Trait Colors* (665 refs, a **colour** mod) among the mechanical patches, so that estimate is
unreliable.

**ACTION REQUIRED:** the only trustworthy figure comes from an in-game scan against
`DefDatabase<TraitDef>.AllDefsListForReading` after load. This should be a dev-mode debug action and
should run **before** the design in §7 is finalised.

### 3.3 How VTE implements its invisible traits

From `zzz-Do-Not-Commit/decompile/VTE_full/VanillaTraitsExpanded/` — effects live in Harmony patches
and custom workers, not data:

| Mechanism | Files |
|---|---|
| Move speed | `GetTicksPerMove_Patch.cs` |
| Custom mental states | `MentalStateWorker_Kleptomaniac`, `MentalState_Kleptomaniac`, `MentalState_PanicFreezing`, `MentalState_TechnophobeTantrum`, `Patch_TryStartMentalState` |
| Behaviour | `JobGiver_StealingItems`, `JobDriver_StealItems`, `JobGiver_PanicFreezing`, `Hediff_ForcedWork` |
| Mood over time | 13 × `ThoughtWorker_*` — incl. `HaventHarvestedOrgansForLongTime`, `MyRivalsAreAlive`, `HaventExitedColonyForLongTime` |
| Combat | `TakeDamage_Patch`, `Patch_ApplyDamageToPart` |
| Crafting | `GenerateQualityCreatedByPawn_Patch` |
| Diplomacy | `TryAffectGoodwillWith_Patch` |

Sample invisible traits — fields are `['label', 'description']` and nothing else:

```
VTE_Clumsy        "Minor bruises and scrapes plague [PAWN_nameDef] all too often, mostly due to
                   uncoordinated tripping or falling."
VTE_Workaholic    "…has problems taking breaks or resting mid job. [PAWN_pronoun] will continue to
                   work even sacrificing their health or sleep."
VTE_MartialArtist "…learned a specific move in melee combat which allows them to disarm opponents…"
```

Verified in `VTE_SpawnSetup_Patch.cs`: on `GainTrait`, VTE registers pawns into **static mod
singletons** (`TraitsManager.Instance.cowards`, `.snobs`, `.bigBoned`,
`.madSurgeonsWithLastHarvestedTick`) and attaches real hediffs (`VTE_SlowWorkSpeed`,
`SmokeleafAddiction`, `AlcoholAddiction`, `VTE_RestSlowFallFactor`, `VTE_SlowerBleedingRate`).
Relevant to §4's probing rejection.

---

## 4. Ideas evaluated

Rev 1 rejected seven approaches. Review upgraded three to RECONSIDER.

| Idea | Verdict | Reason |
|---|---|---|
| **Continuous desirability ranking** (HANDOVER §2.3) | **REJECTED** | 46.7% of modded degrees have no data. Scoring an unreadable trait 0 invents a fact. Unlike skills — which have a guaranteed 0–20 scalar for every pawn — an unauthored trait has an *absent* value, not a hard-to-compute one. The project already built and retracted this axis (`TraitVarianceApplier.cs:19-22`, `traitNoise` removed). |
| **Frequency as severity** (`randomMentalStateMtbDaysMoodCurve`) | **REJECTED** | Anti-correlated with harm. Gourmand binges more often than Pyromaniac burns — frequency-weighting ranks Gourmand *more* hazardous. Confirmed sound on review. |
| **Parsing `description` prose** | **REJECTED** | `[MustTranslate]` (`TraitDegreeData.cs:22`); prose is genuinely ambiguous ("Workaholic … sacrificing their health"); any lexicon is hardcoding. Rejection stands on fragility even if English text is recoverable via `LanguageDatabase` (claimed on review, unverified). |
| **Runtime empirical probing** | **RECONSIDER (narrowly)** | See §4.1 — rev 1's justification was flawed, but the conclusion survives on different grounds. |
| **`marketValueFactorOffset`** | **RECONSIDER** | Rev 1 rejected it for sparsity (7 vanilla entries). That is a sparse-data fallacy: where an author sets it non-zero, it is a *deliberate, authoritative* statement that the trait degrades the pawn. Usable as **trust-when-present, abstain-otherwise** — never as a general score. Catches Wimp, which §7's mental-state flag misses. |
| **Player tagging** | **RECONSIDER (lazy variant only)** | Rev 1's rejection ("nobody will label 91 traits") stands for an upfront grid. A lazy variant — toggle from the pawn inspect card when a trait is actually encountered, ~10–15 unique traits per session — never asks anyone to label 91. Still low priority. |
| **Severity grading of mental states** | **RECONSIDER (one field only)** | Rev 1 rejected this as requiring unfalsifiable weightings ("Aggro is 3× worse"). But `MentalStateDef.IsAggro` (`MentalStateDef.cs:108`) is a **boolean the game itself defines**, not a weighting we invent. Usable as a binary sub-classifier without any numeric exchange rate. |

### 4.1 Runtime probing — why rev 1's reasoning was wrong but the conclusion holds

Rev 1 rejected probing by citing the crash documented at `TraitVarianceApplier.cs:82-90`. Review
correctly identified this as **conflating two different operations**: that crash came from
`allTraits.Clear()` on an *already-spawned live pawn* bypassing `TraitSet.RemoveTrait`. Probing a
fresh never-spawned scratch pawn is not the same operation, and citing that crash as if it were is
not sound.

Two further corrections from review, one of which was itself wrong:

- Claimed: an unspawned pawn would NPE inside VTE's `SpawnSetup_Patch.AddPawn`. **False** —
  verified in `VTE_SpawnSetup_Patch.cs` that VTE wraps it in its own `try/catch`.
- Confirmed: `AddPawn` registers the pawn into **static mod singletons** and attaches hediffs. So the
  real risk is not a crash but **permanent pollution of other mods' static collections** by a
  throwaway pawn.

**Conclusion — still rejected, on the corrected grounds:** state leakage into third-party static
registries, at startup, in a 1376-mod environment, with no clean deregistration path. If ever
revisited it must be a user-initiated dev action, never automatic.

### 4.2 Static `HediffDef.stages` inspection — proposed on review, does not work

Review proposed reading `HediffDef.stages` (`statOffsets`/`statFactors`/`capMods`) to recover
C#-delivered effects with zero mutation and zero risk. **This does not solve the problem, and no
reviewer caught why.**

The hediff *effects* are readable. The **trait → hediff link is not** — it lives inside VTE's
`GainTrait_Patch`. Nothing statically associates `VTE_SlowWorkSpeed` with `VTE_Slob` short of
name-matching, which is precisely the fragile hardcoding this project rules out. The idea recovers
effect magnitudes for hediffs that cannot be attributed to any trait. **Rejected.**

---

## 5. Objection log

**K1 — Unreadable traits.** **CONFIRMED, quantified at 46.7%** (pre-patch; true runtime figure
lower, unmeasured). Scoring an unreadable trait as 0 is an unevidenced claim; correct behaviour is
abstention.

> **Refinement from review:** abstention is *not* a no-op. It is a **selective filter**. A Sovereign's
> odds of Wimp (scoreable, demoted) fall while its odds of `VTE_Clumsy` (unscoreable, untouched) stay
> at vanilla weight. The player experiences "quality predicts some flaws and not others," which reads
> as broken tailoring rather than principled restraint. Partial coverage reshapes the *composition* of
> a pawn's flaw set even when it never misjudges severity. This is a real second-order cost of any
> partial-coverage design, including §7's.

**K2 — Variance destruction.** **WITHDRAWN.** The claim was that biasing selection reduces entropy
and makes high-quality pawns converge. Rebutted: the mod already does this for skills and passions
and it *creates* variance, because between-profile variance matters more than within-profile
variance. The argument would condemn the mod's own shipped core mechanic, so it proved too much.

**K3 — "Desirable" is undefined for hostiles/traders.** **RESOLVED.** The 5-bucket override system
already handles per-faction and per-xenotype cases.

**K4 — Unfalsifiable in a 1000-mod pack.** **MITIGATED.** Compute coverage once at startup and surface
it in settings, so a bug report carries evidence rather than a vibe.

**K5 — Multiplies into the calibrated envelope.** **PARTIALLY RESOLVED, DISPUTED.** Rev 1's fix was a
strength slider defaulting to 0, preserving the envelope by construction. Review counters that a
default of 0 means **100% of users ship with the defect unfixed**, and calls this an evasion of the
calibration problem rather than a resolution. Both readings are correct; this is a product decision.
The §7.1 split-tranche design substantially weakens the objection by making the intervention
envelope-neutral by construction rather than by defaulting off — see §8.

---

## 6. Do catastrophic traits cluster in readable fields? (conjecture, not established)

Rev 1 argued that catastrophic traits tend to use vanilla mental-state fields while mods write custom
C# only for bounded flavour behaviour — supported by three VTE examples (Kleptomaniac,
TechnophobeTantrum, PanicFreezing) all being bounded, while Pyromaniac, Gourmand, Void Fascination and
all 7 Vanilla Anomaly Expanded traits are XML-visible.

**Review flagged this as n=3 generalisation from a VTE-dominated sample**, and that criticism is
accepted. VTE is 95.3% of the baseline set and is specifically known for heavy Harmony architecture,
so the sample measures VTE's authoring preferences more than the ecosystem's.

Review offered counter-examples (custom arsonist, serial-killer, saboteur traits) but **named no
actual mod or def**, so they are hypotheses, not evidence.

**Status: downgraded from supporting argument to open conjecture.** The proposed mechanism — vanilla
already ships the catastrophic mental states, so mods reaching for catastrophe reuse the existing
field — remains plausible and has a real causal story. It is *not* established.

**Falsification test:** scan the 1376 mods for custom `MentalStateDef`s, custom `JobGiver`/
`MentalStateWorker` classes, and Harmony patches on `Pawn_MindState`/`MentalBreakWorker`, then
classify catastrophic vs bounded across non-VTE mods. Until run, §7 must not lean on this.

---

## 7. Investigated-and-rejected design (NOT BUILT — see §10 for what shipped)

> ⛔ **This entire section describes a design that was rejected.** It is kept because the analysis
> below is genuinely useful if anyone ever revisits hazard filtering — particularly §7.4's discovery
> of a second call site, and §7.3's false-negative table. **Do not implement it.** The shipped
> solution is in §10.

Rev 1's design was **withdrawn in its mechanism** and retained in its philosophy. Three changes were
proposed before the whole approach was dropped.

### 7.1 Filter only the draws quality bought (split-tranche)

Per §1, the inversion comes solely from draws *above* `traitCountMin`. Split the delta:

- **Baseline tranche** — sized to `traitCountMin`, called with the real unmodified request, exactly
  as today. Zero divergence from vanilla at the floor.
- **Bonus tranche** — the remainder. The *only* part subject to hazard avoidance.

This makes the intervention **envelope-neutral by construction** rather than by defaulting off, and
stops applying a correction to pawns whose odds were never inverted. It is a strictly more targeted
version of the same policy at the same cost.

*Fails when* `traitCountMin` is itself high, at which point the baseline is not "safe" either and the
framing degenerates. Acceptable — profiles with a high floor are the ones already accepting risk.

### 7.2 Deliver via `disallowedTraits`, NOT rejection sampling

**Rev 1's reject-and-retry is mathematically unsound and is withdrawn.** Rejection sampling does not
preserve the distribution it claims to. Worked counterexample (from review):

> Four traits of equal commonality, `delta = 2`. `F` = flagged hazard, `C` = unflagged but *conflicts*
> with `F`, `U1`/`U2` = unrelated unflagged.
> Vanilla marginals: P(F)=40%, P(C)=40%, P(U1)=P(U2)=60%.
> Discarding every set containing `F`: P(C)=66.7% (**+67% relative**), P(U1)=P(U2)=66.7% (**+11%
> relative**).
>
> Traits that merely *conflict* with a hazard are disproportionately boosted. "Conditional on no
> hazard" is not "vanilla with hazards removed."

Instead: clone the kindDef for the call and append flagged `TraitDef`s to its `disallowedTraits`
before invoking `GenerateTraitsFor`, with the append gated by a quality-scaled coin flip so it never
reaches certainty. Per §2.6, `disallowedTraits` is a field vanilla's picker already consults **and
renormalises commonality around** — so exclusion is statistically clean, with no wasted `GainTrait`,
no retry-count tuning, and no thrashing in a small candidate pool.

**Two open risks on this mechanism:**
- Cloning `PawnKindDef` per call may have identity-sensitive side effects if vanilla or another mod
  keys off `pawn.kindDef` reference equality. **Must be verified before committing.**
- Per §2.6, the checks read the **request**. `GrowUpVariance.cs:209` passes `request: null`, so this
  delivery does not work there and the growth path needs a separate approach.

### 7.3 Flag sources (expanded — rev 1's single flag had a high false-negative rate)

Rev 1 flagged only `randomMentalState`/`forcedMentalState` plus work disables, and claimed this
"makes exactly one claim … and cannot be wrong." **That claim is withdrawn.** It cannot be wrong about
what the field *says*; it can absolutely be wrong about what ruins colonies.

Confirmed false negatives — all unflagged by rev 1's design:

| Trait | Actual mechanism | Why it matters |
|---|---|---|
| Volatile / Nervous (`Nerves` −1/−2) | `statOffsets` on `MentalBreakThreshold` | Feeds *every* break type, not just its own |
| Depressive (`NaturalMood` −2) | mood `statOffsets` | Permanent, compounding |
| Chemical Fascination / Interest | `Need_Chemical` driven, not `randomMentalState` | Drug spirals |
| Wimp | `statOffsets` (`PainShockThreshold`) + `marketValueFactorOffset` | Instant downing |

Also flagged on review: bundling `disabledWorkTypes`/`disabledWorkTags` into the same binary as mental
states treats "can't clean" as equal to "spontaneous arson" — the same commensurability error the
design claims to escape. And `disabledWorkTags` is `TraitDef`-level, so it over-flags every degree of
a multi-degree trait.

**Revised flag set — separate channels, each individually objective, never summed:**

1. `randomMentalState` / `forcedMentalState` present → **uncontrolled-behaviour** channel.
   Sub-classify by `MentalStateDef.IsAggro` (`MentalStateDef.cs:108`) — a boolean the game defines,
   not a weighting we invent.
2. `marketValueFactorOffset != 0` → **author-declared degradation** channel. Trust when present,
   abstain when absent (§4).
3. `disabledWorkTypes` / `disabledWorkTags` → **capability-loss** channel, kept *separate* from (1),
   and applied at def granularity since that is where the field lives.

Whether `MentalBreakThreshold` stat outliers become a fourth channel is **open** — it reaches
Volatile/Nervous/Depressive but reintroduces the normalization problem that killed the general engine.

### 7.4 Both call sites, via one shared helper

**There are two independent `GenerateTraitsFor` call sites**, and rev 1 addressed only the first:

```
Source/TraitVarianceApplier.cs:72   GenerateTraitsFor(pawn, delta,     request, growthMomentTrait: false)
Source/GrowUpVariance.cs:209        GenerateTraitsFor(pawn, requested, null,    growthMomentTrait: true)
```

The growth path rolls its own quality (`GrowUpVariance.cs:58`) and is **add-only by deliberate
design** (`GrowUpVariance.cs:70-79`: "neither can remove a trait or downgrade a passion"). So a
hazardous trait acquired at the age-13 growth moment is **permanent** — there is no later pass that
could catch it. Leaving it unhandled is not merely inconsistent; it is a permanent-effect gap that
appears exactly when `applyVarianceToChildren` is enabled on a high-quality profile.

Both sites must route through one shared helper so they cannot drift — matching the existing
invariant stated at `GrowUpVariance.cs:10-13` that all three appliers behave identically.

### 7.5 Remaining elements (unchanged from rev 1)

- Selection stays with vanilla's picker; never re-implement it
  (`2026-07-28-additive-trait-model-design.md:188-192`).
- Strength slider. Default value is **open** — see §8.
- Coverage line in settings, computed **lazily on first `DoWindowContents`**, not in a static
  initializer (`DefDatabase` may not be populated at mod-class construction). Mirror the existing
  `cachedFaithfulBaseline` caching pattern.
- Failure semantics must be explicit and logged via `TraitTrace`, never silent.

### 7.6 Integration risks

| # | Risk | Mitigation |
|---|---|---|
| 1 | Intervention degrades trait **count**, not just hazard exposure — the picker already under-fills when candidates run out (`TraitVarianceApplier.cs:72-79`) | Fall back to the last successful draw, never a shorter one. Count distribution feeds the envelope. |
| 2 | Growth-moment path uncovered → permanent hazard (§7.4) | Shared helper; separate delivery for `request: null` |
| 3 | Removal path (`TraitVarianceApplier.cs:100-106`) picks victims uniformly at random | **Owner decision** — scope out explicitly, or bias it. Contradictory philosophies across paths are worse than either alone. |
| 4 | Forced traits (gene/backstory) that are themselves flagged cannot be excluded | Check must inspect only the newly-generated tranche, never `allTraits` |
| 5 | Settings plumbing spans 5 synchronized sites: `VarianceProfileValues` field + `ExposeData` (`VarianceProfile.cs:103-123`), all 9 preset initializers (`VarianceProfile.cs:192-381`), `ClampAndSwap`, the UI draw call, coverage cache | Missing any one fails **silently**, not at build time. **`SettingsTransfer.cs` needs zero changes** — it round-trips via `Scribe_Deep`. |
| 6 | Save compatibility | Fine by construction — `Scribe_Values.Look` defaults omitted fields |
| 7 | Performance — `GenerateTraitsFor` runs for thousands of pawns at world-gen and every raid | Low: bounded, only on the bonus tranche, only at high quality. Keep any bound a small fixed constant. |

---

## 8. Open items requiring the project owner

1. **Slider default.** 0 preserves the envelope by construction but ships the defect unfixed for
   every user. §7.1's split-tranche makes a non-zero default defensible. **Rule 4 applies.**
2. **Growth-moment coverage.** Cover it (permanent-effect gap if not) or scope it out explicitly?
3. **Removal-path bias.** In scope or explicitly excluded? Contradictory philosophies across the two
   paths are worse than either choice alone.
4. **Symmetry.** Should low quality *increase* hazard exposure, mirroring the high-quality reduction,
   or is the effect one-directional?
5. **`MentalBreakThreshold` as a fourth flag channel** — reaches Volatile/Nervous/Depressive, but
   reintroduces normalization.
6. **Handover §2.4 conflict.** §2.4 says "NO UI SETTINGS / TOGGLES." The slider and coverage panel
   contradict it. Owner has indicated the handover is a sketch, not binding — recorded here.

### Blocking before spec

- [ ] **In-game post-patch coverage scan** (§3.2.2). The 46.7% figure is pre-patch and known biased.
- [ ] **Verify `PawnKindDef` cloning is safe** (§7.2).
- [ ] **Falsify or confirm the §6 conjecture**, or write §7 so it does not depend on it.

---

## 9. Multi-agent review log (rev 1 → rev 2)

Rev 1 was reviewed by six agents: three Claude (steelman the full engine; find alternative
mechanisms; integration risk) and three Gemini via agy-bridge (data/methodology; attack the
recommended design; re-examine the rejections).

**Changes forced by review:**

| Finding | Effect on doc |
|---|---|
| Scan omitted `TraitDef`-level fields, `modExtensions`, `possessions` | §3.2 headline 60.3% → **46.7%** |
| Scan de-dup was version-order non-deterministic | Rescan prefers highest version folder |
| Scan is pre-patch, runtime is post-patch | New §3.2.2; in-game scan added as blocking |
| Rejection sampling distorts marginals | §7.2 mechanism **replaced** |
| Binary flag has high false-negative rate | §7.3 flag set **expanded**, "cannot be wrong" withdrawn |
| Second call site at `GrowUpVariance.cs:209` | New §7.4 |
| Only marginal draws are implicated | New §7.1 split-tranche |
| Abstention is a selective filter, not a no-op | §5 K1 refinement |
| §6's n=3 sample is VTE-dominated | §6 downgraded to conjecture |
| Probing rejection conflated two operations | §4.1 rewritten, conclusion retained on new grounds |
| `marketValueFactorOffset` / lazy tagging / `IsAggro` dismissed too fast | §4 upgraded to RECONSIDER |

**Reliability note.** Gemini's architectural reasoning was consistently strong — the distortion proof,
the crash-conflation catch, and the sparse-signal argument were all correct and load-bearing. Its
**specific API claims were wrong roughly half the time**: it asserted `MentalStateDef.causesHarm`
(does not exist, retracted on challenge), then `isAggressive`/`Aggressive` (also wrong — the real
members are `IsAggro` and `MentalStateCategory.Aggro`), and asserted an NPE crash path disproved by
VTE's own `try/catch`. It also proposed `HediffDef.stages` inspection without noticing the trait→hediff
link is unrecoverable (§4.2), and no reviewer caught that.

**Rule: no field, method, or type name from a delegated review enters this document without a
decompile spot-check.** Every API name in rev 2 has been verified against
`zzz-Do-Not-Commit/decompile/`.

---

## 10. WHAT ACTUALLY SHIPPED (2026-08-03)

The engine in §7 was dropped after a scope review. Rev 2 had grown it from "~80–120 lines" to roughly
300 lines plus a verification harness, while the value it bought stayed small and applied only to the
visible subset of traits. A far cheaper fix addresses the same problem completely.

### 10.1 The reframe that made it cheap

The inversion's magnitude is **proportional to the trait-count spread**. A profile at
`traitCountMin = traitCountMax` has no inversion at all; the defect exists only in the draws *above*
the floor. So narrowing preset spreads shrinks the problem at its source, with no runtime code.

Measured per-profile inversion (hazard ≈ 4.1%/draw, vanilla stochastic share) showed it was never
uniform: `Faithful` at 2–3 had a trivial +3.8pp inversion, while `Wildcard` at 0–8 had +28.4pp. The
"quality means better" tiers (`Elite`, `Sovereign`, `Specialist`) were already narrow.

### 10.2 Change 1 — trait count removed from the quality score

`CalculateCompositeScore` weighted trait count at `0.8/3.0` via `traitNorm = count / 8`. Removed;
weights are now `wS = 1.2`, `wP = 1.0` over `2.2`.

**Trait count is a VARIANCE parameter, not a mean one.** Selection is delegated to vanilla's
quality-blind picker, so more traits does not buy *better* traits — it buys more draws from an
unchanged, roughly balanced urn. Scoring it as a mean contributor caused two concrete defects:

- **It rewarded making pawns worse.** Widening a spread raised a profile's score while raising its
  chance of a colony-ender. The metric endorsed gaming itself.
- **It compressed the entire scale.** Counts normalise into a narrow 0.25–0.625 band while
  skill/passion span 0.1–1.0, so the term propped weak profiles up and held strong ones down —
  measured at +8 to +11 points for `Desperate` and −5 to −23 for `Gifted`.

Trait count *does* feed Best-of-N power through variance, but quantifying that requires a per-trait
value model — the exact thing §3–§5 establishes is not recoverable. **Omitting a term we cannot
estimate beats including one we know is wrong in mechanism.**

### 10.3 Change 2 — preset trait ranges narrowed

| Profile | Traits | Hazard exposure | Rationale |
|---|---|---|---|
| `Sovereign` | 2–5 → **2–4** | 18.9% → 15.4% | matches `Elite`; top tier should not carry the most risk |
| `Distinct` | 1–6 → **2–4** | 22.2% → 15.4% | hostile-fallback default |
| `Desperate` | 1–4 → **2–4** | floor raised to vanilla's | also fixed a −45% envelope breach |
| `Wildcard` | 0–8 **unchanged** | 28.5% | deliberate — chaos is the preset's stated purpose |

### 10.4 Consequential retune

Removing the trait term shifted every profile's score, exposing breaches the trait term had been
masking. Skill/passion values were retuned for the three default-reachable profiles that fell
outside ±35%: `Desperate` (−45% at N=1), `Sovereign` (+36.4% at N=1), `Wildcard` (+38.2% at N=50).

Final state — inside ±35% at every batch size, power ordering intact at every N:

```
                N=1     N=5    N=25    N=50
Desperate    -32.9%  -29.9%  -27.2%  -26.3%
Scavenger    -24.3%  -21.9%  -20.0%  -19.4%
Faithful       0.0%    0.0%    0.0%    0.0%
Specialist    +7.9%   +6.0%   +5.2%   +4.9%
Elite        +22.2%  +16.0%  +13.0%  +12.1%
Sovereign    +32.3%  +21.8%  +16.8%  +15.4%
Wildcard     -29.6%   +1.0%  +22.0%  +28.2%   (variance preset, exempt from ordering)
Distinct     -19.2%   -5.4%   +4.3%   +7.5%   (variance preset)
```

`Gifted` was left unpatched — it is the only preset unreachable in the default config. It sits near
+139% because its passion budget hits 12.3 pips against a `/12` normalizer, pinning `passionNorm` at
1.0. That is a passion bug, not a trait one. **Fix before ever making it a default.**

### 10.5 What this does and does not solve

**Solves:** the stated problem. High-quality pawns no longer take a disproportionate number of extra
draws against the hazard pool, because the extra draws largely no longer exist.

**Does not solve:** hazardous traits are still possible at every quality level, at roughly vanilla
rates. That is intended — it preserves story-generator texture and was never the complaint.

**Residual, accepted:** `Wildcard` retains a 28.5% hazard rate at its ceiling, and is the default for
Impid xenotypes. Deliberate.

---

## Appendix — reproducing the scans

- Vanilla defs: `C:/Program Files (x86)/Steam/steamapps/common/RimWorld/Data/*/Defs/TraitDefs/*.xml`
- Workshop mods: `C:/Program Files (x86)/Steam/steamapps/workshop/content/294100/<id>/**/*.xml`
- Mod names: `<id>/About/About.xml` → `<name>`
- **Skip files containing `PatchOperation`** when counting definitions (they are patches, not defs)
- **De-duplicate on `(modName, defName)` keeping the HIGHEST version subfolder** — mods ship `1.3/`
  … `1.6/` plus `Common/`; first-match wins is non-deterministic on NTFS
- **"Visible" = (degree child tags ∩ degree-level mechanical set) OR (TraitDef child tags ∩
  TraitDef-level mechanical set).** Both levels are required — checking only degree level was rev 1's
  primary bug.
  - Degree-level set: the §2.2 field list minus `label`, `description`, `degree`, `commonality`,
    `labelMale`, `labelFemale`, `untranslatedLabel`, `renderNodeProperties`
  - TraitDef-level set: `disabledWorkTypes`, `disabledWorkTags`, `requiredWorkTypes`,
    `requiredWorkTags`, `forcedPassions`, `conflictingPassions`, `disableHostilityFromAnimalType`,
    `disableHostilityFromFaction`, `modExtensions`
- Decompile: `ilspycmd <Assembly-CSharp.dll> -t <Namespace.Type>`
- **The pre-patch caveat in §3.2.2 applies to every number here.** Only an in-game
  `DefDatabase<TraitDef>` scan gives the figure the mod will actually operate on.
