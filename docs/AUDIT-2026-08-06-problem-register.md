# Problem Register — Varied Pawns audit, 2026-08-06

**Purpose.** A standing register of *defects and risks found*. Each entry states what is wrong, why
it is wrong, when it bites, and what was actually verified versus inferred.

> [!NOTE]
> **Update 2026-08-06, after an independent cite-check of this register.** Every code citation in
> Part B was re-opened and confirmed correct at the stated line. Two corrections to this document
> itself, and five findings acted on:
>
> - **Corrected:** the ground-state table below said `origin/main` was **3 commits behind** local
>   `main`, "all documentation". `git ls-remote` shows it is **10 behind**, and three of those are
>   code (`0f96eba`, `a5f68fd`, `42a8b71`). P-01 was understated, not overstated.
> - **Corrected:** P-16 said "all three appliers". The unguarded dereferences number ~15 sites,
>   including `GrowUpVariance.cs` :119–:217 and `PassionVarianceApplier.cs` :89/:101/:111.
> - **FIXED in code:** P-14, P-15, P-16, P-26, and P-27 (which fell out of P-26's fix). Build clean,
>   `envelope_check.py` still `PASS` / `unchanged`. Per-entry resolutions are inline below.
> - **FIXED in docs:** P-01's two false `HANDOVER.md` status lines. The tree itself is deliberately
>   left uncommitted at the owner's instruction.
> - **Deferred by the owner:** everything retune-facing — P-02, P-03, P-05, P-06, P-08, P-09.

**Ground state this audit was taken against**

| | |
|---|---|
| Branch | `main` |
| `HEAD` | `d477898` (`docs: downgrade the modded-skill-count item in the passion audit`) |
| `origin/main` | `9526093` — **3 commits behind local `main`** |
| Working tree | **NOT clean** — 10 files modified, 587 insertions / 59 deletions |

**How to read severity**

| Level | Meaning |
|---|---|
| **Critical** | Wrong pawns, lost data, or a crash on a normal path. |
| **Major** | Wrong numbers a player or a future agent will act on; or a live process risk. |
| **Minor** | Real but bounded — bad hygiene, latent trap, cosmetic-to-player. |
| **Cosmetic** | Wording, naming, tidiness. |

**Confidence** is stated per finding, along with what could *not* be checked. Every line citation
in this file was opened and read before being written down — §1.10 of `HANDOVER.md` records two
occasions where an unchecked citation sent someone editing the wrong code.

---

## Index

| # | Area | Finding | Severity |
|---|---|---|---|
| [P-01](#p-01) | Process | The entire STEP 1 passion-axis rework is uncommitted, unreviewed and unpushed, while the handover says the tree is clean | **Major** — ✅ docs fixed |
| [P-02](#p-02) | Docs | `HANDOVER.md` states the `Faithful` baseline is "exactly 0.2500" and that it "no longer depends on the weights at all" — both are false since STEP 1 | **Major** |
| [P-03](#p-03) | Docs | The exchange rate `R = 1.94` and Rule 7 were not updated for the pip-efficiency term; `R` is now bias-dependent | **Major** |
| [P-04](#p-04) | Docs | Two different rules are both numbered **6**, and the document cites rules by number | **Minor** |
| [P-05](#p-05) | Docs | The canonical model statement still reads `passionNorm = pips/18`, omitting both STEP 1 terms | **Minor** |
| [P-06](#p-06) | Docs | Headroom is quoted as `6.5pp` in the mandatory rule and `6.6pp` in the tool output it points at | **Cosmetic** |
| [P-07](#p-07) | Packaging | `About.xml` claims RimWorld **1.5** support that nothing in this repo builds or tests | **Minor** |
| [P-08](#p-08) | Docs vs code | The passion-variance-OFF fallback is documented as `0.2778` and implemented as `0.2609` | **Minor** |
| [P-09](#p-09) | Model drift | `Constants.QualityClampEpsilon` is applied in C# but absent from `envelope_check.py` and from the drift check | **Minor** |
| [P-10](#p-10) | Scoring | `CalculateBestOfNScoreCore` has a silent fallback that returns the exact quantity Defect A was deleted for | **Minor** |
| [P-11](#p-11) | Naming | `MinMagnitudeFloor` is not a floor and is no longer a minimum; the name invites the mistake its own comment warns about | **Cosmetic** |
| [P-12](#p-12) | Model drift | Python clamps the composite on one side, C# on both | **Cosmetic** |
| [P-13](#p-13) | Comments | `Constants.cs:142` says the integration nodes were measured "across all seven presets"; there are eight | **Cosmetic** |
| [P-14](#p-14) | Settings | `Resolve()` answers a dangling id with an **unrelated profile's live values**, and corrupts that profile's label on the way out | **Major** — ✅ FIXED |
| [P-15](#p-15) | Passions | `gene.passionPreAdd` is snapshotted **after** the bump, so a removed passion gene leaves its passion behind permanently | **Major** — ✅ FIXED |
| [P-16](#p-16) | Robustness | All three appliers dereference `pawn.skills` / `pawn.story` unguarded, while guarding `pawn.genes` everywhere | **Major** — ✅ FIXED |
| [P-17](#p-17) | Robustness | "Verbose logging **(dev mode)**" is not gated on dev mode, and one of its two effects rethrows inside the pawn-generation postfix | **Minor** |
| [P-18](#p-18) | Dead code | `TraitAgeCap`'s age 7–9 and 10–12 branches cannot be reached under vanilla growth ages | **Minor** |
| [P-19](#p-19) | Settings | `Resolve()` clones presets but aliases custom profiles, so `Active`, `Hostile` and `Editing` can be one object | **Minor** |
| [P-20](#p-20) | Robustness | The life-stage postfix has no exception guard, unlike both sibling postfixes | **Minor** |
| [P-21](#p-21) | Persistence | A parallel-list count mismatch discards an entire override axis **silently** | **Minor** |
| [P-22](#p-22) | Settings | Custom profile ids come from `DateTime.Now.Ticks` with no uniqueness check | **Minor** |
| [P-23](#p-23) | Traits | Gene `forcedTraits` overwrite `kindDef` `forcedTraits` degree without any precedence rule | **Minor** |
| [P-24](#p-24) | UI | The quality slider writes unconditionally, breaking the file's own double-guard pattern | **Minor** |
| [P-25](#p-25) | Memory | `LastKnownStage` never drops entries for dead pawns | **Cosmetic** |
| [P-26](#p-26) | Overrides | "This mod never touches them" is enforced by a weaker faction test than the one that decides the override — hostile pawns can be varied with the toggle off | **Major** — ✅ FIXED |
| [P-27](#p-27) | Robustness | Three of four `HostileTo(Faction.OfPlayerSilentFail)` call sites omit the null check the fourth has — during world gen, which is exactly when it is null | **Minor** — ✅ FIXED |

*(Entries below are added as the audit proceeds; the index is kept in sync.)*

### What was checked and found clean

Recorded so this audit is not silently re-run. Verified during this pass:

- **`envelope_check.py` currently PASSES** Rule 1 and Rule 2 at N = 1, 5, 25, 50, and reports
  `Source/EnvelopeFigures.g.cs: unchanged` — so the working tree's `Constants.cs`,
  `VarianceProfile.cs` and `EnvelopeFigures.g.cs` are mutually consistent *right now*. There is no
  stale-generation defect in the present tree.
- **The Python and C# composites are a faithful term-for-term mirror**: skill-norm clamp chain,
  `budget` / `capacity` / `efficiency` composition and ordering, `PassionPipEfficiency`'s formula
  and its bias-1.0 normalisation anchor, and `alpha = m·K, beta = (1−m)·K` with no α/β swap.
- **Both integrators renormalise identically.** Python divides by `F[-1]`; C#
  (`PawnVarianceSettings.cs:1498-1515`) divides by `total`, the same discrete integral. The
  shared first-order right-edge CDF bias is structurally identical on both sides — nothing new
  beyond the deliberately-carried inaccuracy already recorded in `HANDOVER.md:624`.
- **The in-game drift check covers `GEN_CONSTANTS` 1:1.** `DebugActions.cs:181-211`'s
  `CheckConstant` calls match all 11 entries of `envelope_check.py:184-187` exactly.
- **The uncommitted diff is exactly the documented STEP 1 / 1b change** — premium removed,
  capacity + efficiency added, and the five new constants added to *both* the Python `required`
  list and the drift check in the same edit. No half-applied change.
- **Trait jitter matches its documentation.** `TraitVarianceApplier.cs:228` is
  `((float)Rand.Value - 0.5f) * Constants.SmallRandomJitter` with `SmallRandomJitter = 0.5f`,
  i.e. ±0.25 — exactly what `HANDOVER.md:1311` claims.
- **`zzz-Do-Not-Commit/test_race_resolution.py` exists**, contrary to a first reading; the
  reference at `HANDOVER.md:1849` is good.
- **`SettingsTransfer.cs` is clean** on its own terms. Both directions route through
  `ExposeData` so the payload cannot drift from what the mod persists; the pre-parse via
  `XmlDocument.LoadXml` (`:89-100`) genuinely does prevent `ScribeLoader`'s unsuppressable red
  error; `Scribe.ForceStop()` is in a `finally` on both paths, which is what makes sharing the
  global Scribe singleton with the player's save safe; and `Import` loads into a throwaway object
  and only calls `CopyFrom` after full success (`:134-141`). Its one real weakness — `CopyFrom` not
  validating profile ids — is not a `SettingsTransfer` bug; it is P-14, and it lands in `Resolve`.
  `configVersion` is written and read but never branched on, which `:19-22` states outright.

### Where to start

The register is ordered by discovery, not by priority. If these are worked rather than filed:

1. ~~**P-01** first~~ — ✅ done: the false status lines are corrected; the tree stays uncommitted by the owner's instruction. Originally: it is not a code fix — commit the STEP 1 working tree, or correct the two
   handover lines that say it is already committed. Everything else is easier to reason about once
   the tree state and the document agree.
2. ~~**P-14** and **P-26**~~ — ✅ both fixed. They were the two that produced *wrong pawns silently*,
   the category this project has been bitten by three times.
3. **P-02, P-03, P-05, P-08** are one job, not four: a single sweep of everything the STEP 1b
   efficiency term invalidated. Doing this **before STEP 2** matters — the retune reads exactly
   these paragraphs, and three of the four are load-bearing arguments rather than stray figures.
   **Deferred by the owner** along with the rest of the retune work; still the right sequencing when
   STEP 2 is picked up.
4. ~~**P-15, P-16, P-27**~~ — ✅ all three fixed without needing the decompile. Each turned out to be
   correct-against-stated-intent (P-15) or correct-either-way (P-16, P-27), so the unread vanilla
   methods stopped gating the fix.

**What is left after this pass:** the retune-facing docs sweep (item 3, plus P-06 and P-09), and the
un-actioned Minors/Cosmetics — P-07, P-10, P-11, P-12, P-13, P-18, P-19, P-20, P-21, P-22, P-23,
P-24, P-25. Note **P-20** (unguarded life-stage postfix) is the largest of those and now sits next to
freshly-changed code in `GrowthUpPatch.cs`.

> **⚠️ Nothing below has been exercised in-game.** The four code fixes compile clean and
> `envelope_check.py` still reports `PASS` / `unchanged`, but no GABS session has run against them.
> P-15 in particular changes behaviour that only shows up on gene *removal*, which no verification
> pass in this project has ever driven.

---

<a id="p-01"></a>
## P-01 — The STEP 1 passion-axis rework is uncommitted, unreviewed, and unpushed

**Severity: Major.  Confidence: high.**

### What

`HANDOVER.md` opens by asserting the work is safely landed:

- line 5: *"`origin/main` and `main` are both at `9526093`; the working tree is clean."*
- line 22: *"**Nothing is uncommitted any more.** Everything the 2026-08-06 sessions produced is in."*

Neither holds. Observed:

```
$ git status --short
 M HANDOVER.md
 M Source/Constants.cs
 M Source/DebugActions.cs
 M Source/EnvelopeFigures.g.cs
 M Source/GrowUpVariance.cs
 M Source/HarmonyPatches.cs
 M Source/PassionVarianceApplier.cs
 M Source/PawnVarianceSettings.cs
 M Source/VarianceProfile.cs
 M docs/tools/envelope_check.py

$ git diff --stat
 10 files changed, 587 insertions(+), 59 deletions(-)
```

`origin/main` is at `9526093`; local `main` is three commits ahead (`a67ee16`, `ec9c519`,
`d477898` — all documentation). So *nothing* of STEP 1's code has left the machine, and the three
commits that *have* been made describe work that is not in any commit.

### Why it matters

This is not tidiness. The uncommitted set is precisely the change the document calls the highest-risk
class of edit in the project:

- `PawnVarianceSettings.cs` — `CalculateCompositeScore`, `PassionPipEfficiency`, the capacity cap.
- `envelope_check.py` — the mirror of that same maths.
- `EnvelopeFigures.g.cs` — the generated golden reference the in-game gate diffs against.
- `Constants.cs` — new scoring constants.

`HANDOVER.md:292` states plainly: *"Retuning is exactly where this project has shipped its worst
defects."* Three concrete consequences of the state above:

1. **No review has run against it.** §1.10 established that per-task review misses cross-task
   numerical defects and that the whole-branch review is what caught both Best-of-N bugs. There is
   no branch to review — the change has no commit boundary.
2. **The one gate the document explicitly leaves open cannot be closed by anyone else.**
   `HANDOVER.md:145` — *"❗ NOT verified: run the in-game `Verify Best-of-N` action"* — requires
   building this working tree. Nobody but this machine can.
3. **A single `git checkout .` destroys it.** 587 insertions of hand-derived numerical work with no
   backup exist only in the filesystem.

### What was not verified

Whether the owner is deliberately holding the change back pending the in-game gate. That is a
plausible and defensible reason to leave it uncommitted — but if so, the two handover lines quoted
above are the thing to fix, because they tell the next reader the opposite.

---

<a id="p-02"></a>
## P-02 — `HANDOVER.md` still claims the `Faithful` baseline is exactly `0.2500` and weight-independent

**Severity: Major.  Confidence: high.**

### What

`HANDOVER.md:1499-1502`, in the **MANDATORY ARCHITECTURAL RULES** section:

> **Measured `Faithful` baseline is exactly `0.2500`** at `q = 0.50`. This is not a coincidence and
> not a tuned value: `skillNorm = 5/20 = 0.25` and `passionNorm = 4.5/18 = 0.25`, so both axes agree
> and **the baseline no longer depends on the weights at all**. Retuning `wS`/`wP` now moves the
> profiles around a fixed reference instead of moving the reference itself.

Three statements later in the same document contradict it:

- `HANDOVER.md:1553` (pasted tool output): `Faithful baseline @ q=0.50: 0.2237`
- `HANDOVER.md:1540` : *"`Faithful`'s two axes were close (0.250 vs 0.250 then, **0.250 vs 0.222**
  after the 2026-08-06 passion-axis fix)"*
- `HANDOVER.md:278` (STEP 2 brief): *"the `Faithful` baseline is now `0.2237`, not `0.2500`."*

### Why it matters

The false paragraph is not a stray figure — it is a **load-bearing argument**, and it is the more
authoritative-looking of the two locations because it sits inside the rules section rather than in a
dated progress log.

The argument it makes is: *because both axes read 0.25, the baseline is invariant under `wS`/`wP`,
so you may retune the weights without moving the reference.* That was true when
`passionNorm = 4.5/18 = 0.2500`. After STEP 1, `passionNorm ≈ 0.222` while `skillNorm` is still
`0.250`, so the axes **disagree** and the baseline is once again a weighted blend of two different
numbers — i.e. **changing `wS` or `wP` now moves `Faithful` itself**, which moves every percentage
in the table simultaneously, because every percentage is measured *against* `Faithful`.

STEP 2 (*"Retune every preset"*) is the next scheduled task and is exactly the task that reads this
paragraph. An agent that trusts it will change a weight expecting the reference to hold still.

### What was not verified

The precise current value of `Faithful`'s `passionNorm` was taken from the document's own two
statements rather than recomputed; the contradiction is established from the text alone regardless
of which figure is right.

---

<a id="p-03"></a>
## P-03 — `R = 1.94` and Rule 7 were not updated for the pip-efficiency term

**Severity: Major.  Confidence: high.**

### What

`HANDOVER.md:1508` and Rule 7 (`HANDOVER.md:1741`) both define the skill↔passion exchange rate as a
single constant built from exactly three numbers:

> **`R = (20 / MaxPassionPips) · (wP / wS) = (20/18) · (1.4/0.8) = 1.94` skill levels per passion pip.**
> All three numbers live in `Constants.cs`

> 7. **THE EXCHANGE RATE `R` DEPENDS ON THE NORMALIZER, NOT JUST THE WEIGHTS**:
>    `R = (AssumedMaxSkillLevel / MaxPassionPips) · (wP / wS)`. … Recompute `R` before and after
>    touching **any of the three**.

STEP 1b (`HANDOVER.md:84-115`) added a **fourth** factor to the passion axis:
`passionNorm` is now multiplied by `PassionPipEfficiency(majorBias)`, tabulated in the document as

| Major bias | 0.00 | 0.35 | 0.50 | 0.65 | 0.80 | 1.00 |
|---|---|---|---|---|---|---|
| pip efficiency | 0.848 | 0.916 | 0.939 | 0.960 | 0.978 | **1.000** |

Neither the `R` section nor Rule 7 mentions it.

### Why it matters

`R` answers "how many skill levels is one passion pip worth?" — it is derived by equating the two
axes' contributions to the composite. With the efficiency term in place, that derivation now yields

```
wS · (Δlevels / 20)  =  wP · (Δpips / 18) · eff(bias)
⇒  Δlevels/Δpips  =  (20/18)·(wP/wS)·eff(bias)  =  1.94 · eff(bias)
```

So **`R` is no longer a scalar — it is a function of the profile's `passionMajorBias`**, running
from `1.94 × 0.848 = 1.65` at bias 0 to `1.94` at bias 1.0. The quoted `1.94` is now the
*all-Major special case*, not the general rate. For the shipped presets the true rate is roughly
`1.82` (`Faithful`, bias 0.5) and `1.86` (`Sovereign`, bias 0.70).

A second consequence: above the new **capacity cap** (`skills × (Minor + (Major−Minor)·bias)`) the
marginal value of a pip is **zero**, so `R` is not merely bias-dependent but piecewise — it falls off
a cliff. `HANDOVER.md:63` notes the cap binds no shipped preset today, but it is reachable on custom
profiles, which is precisely where a live `R` would be used.

Rule 7's failure mode is the specific one it was written to prevent. It instructs the reader to
recompute `R` after touching "any of the three" — a reader who touches
`Constants.PassionLearnRateMajor` has touched none of the three, has moved `R` anyway, and has been
told by a mandatory rule that they are safe. (The separate recalculate-trigger list at
`HANDOVER.md:1631` *does* list the learn-rate constants, so the two lists now disagree with each
other as well.)

**The same omission is in the code.** `Constants.cs:121-131`, the comment that *defines* the
weights, carries the identical pre-STEP-1 derivation and the identical incomplete warning:

```csharp
// Composite-score axis weights. The exchange rate they encode is
//     R = (AssumedMaxSkillLevel / MaxPassionPips) * (CompositePassionWeight / CompositeSkillWeight)
//       = (20/18) * (1.4/0.8) = 1.94 skill levels per passion pip.
…
// NOTE: R depends on MaxPassionPips as much as on these weights. Changing either without
// the other silently moves the exchange rate — recompute R before touching them.
```

This matters more than the handover copy: `Constants.cs` was otherwise swept thoroughly during
STEP 1 (see P-02, where the same file *does* correct the stale `0.2500` claim in detail). The `R`
block is the one part of it that was not, which makes it look current when it is not.

### What was not verified

`1.82` / `1.86` are arithmetic on the document's own efficiency table and preset biases, not
independently recomputed from `Constants.cs`. The structural point — that `R` gained a fourth input
and neither statement of it was updated — does not depend on those two figures.

---

<a id="p-04"></a>
## P-04 — Two different mandatory rules are both numbered 6

**Severity: Minor.  Confidence: high.**

### What

`HANDOVER.md:1736-1742` lists the "Remaining rules" as **3, 4, 5, 6, 7, 6**:

- line 1740 — `6. **RECALCULATE THE ENVELOPE AFTER ANY SCORING-CONSTANT CHANGE**`
- line 1742 — `6. **PROTECTION OF REVIEWED CODE (STRICT PERMISSION REQUIRED)**`

### Why it matters

The document cites rules by number, in places where the two candidates mean very different things:

- `HANDOVER.md:1548` — *"Pasted, not hand-edited (Rule 6)"* → means the recalculate rule.
- `HANDOVER.md:1718` — *"Changing one is a Rule 4 consultation item"* → but consultation is **Rule 5**
  (`MANDATORY CONSULTATION`, line 1739); Rule 4 is the children/growth-moment default. A second,
  independent misnumbering.
- `HANDOVER.md:1363` — *"Rule 5 item, not yet decided"* → consultation, correct.

So one rule number is ambiguous and one cross-reference is simply wrong. The rules are the part of
the document that carries "do not do this without asking", which is the worst place for a reader to
have to guess which item is meant.

### What was not verified

Whether Rules 1 and 2 (stated separately at `HANDOVER.md:1275-1278`) were ever intended to be part
of the same numbered list — they are, judging by the "Remaining rules" heading starting at 3.

---

<a id="p-05"></a>
## P-05 — The canonical statement of the composite still reads `passionNorm = pips/18`

**Severity: Minor.  Confidence: high.**

### What

`HANDOVER.md:1295-1296`, under *"How the percentages are derived"* — the section a newcomer reads
first to learn the model:

> 3. Composite is `(0.8·skillNorm + 1.4·passionNorm) / 2.2` (`CalculateCompositeScore`), where
>    `skillNorm = (5 + skillShift)/20` and **`passionNorm = pips/18`**.

STEP 1 added two things to that expression and neither appears here: the **capacity cap** on pips
and the **pip-efficiency scaling**. Both are described only in §1.11, ~1200 lines earlier and under
a "next up" progress heading rather than in the model reference.

### Why it matters

Lower severity than P-02 only because it understates rather than contradicts: a reader who
implements from this line gets the pre-STEP-1 model and will not know why their numbers differ from
`EnvelopeFigures.g.cs`. It is the same failure mode as the stale `24`-pip comment recorded in
§1.10 — a description that has quietly become an argument for the wrong code.

---

<a id="p-06"></a>
## P-06 — Headroom quoted as `6.5pp` in the rule and `6.6pp` in the output it cites

**Severity: Cosmetic.  Confidence: high.**

`HANDOVER.md:1740` (Rule 6) and `HANDOVER.md:1647` both say *"the tightest preset currently has only
**6.5pp** of headroom (`Sovereign` @ N=1)"*. The pasted tool output at `HANDOVER.md:1572` and the
repeat at `:1612` say `Sovereign @ N=1: +28.4% (6.6pp of headroom)`. §1.11 line 57 explains the
history — the figure went `6.5 → 9.0 → 6.6` across STEP 1 and 1b — so `6.5` is a pre-STEP-1 leftover
in two places that were not swept.

Harmless to the maths; recorded because the whole point of Rule 6 is that these figures go stale
silently, and it has gone stale inside its own text.

---

<a id="p-07"></a>
## P-07 — `About.xml` advertises RimWorld 1.5 support that nothing here builds or tests

**Severity: Minor.  Confidence: medium.**

### What

`About/About.xml`:

```xml
<supportedVersions>
  <li>1.5</li>
  <li>1.6</li>
</supportedVersions>
```

`About/LoadFolders.xml` maps **both** versions to the same root `/`, so both load the single
`Assemblies/PawnVarianceMod.dll`. That DLL is built by `Source/PawnVarianceMod.csproj` against
exactly one reference set:

```xml
<RimWorldDir Condition="'$(RimWorldDir)' == ''">C:\Program Files (x86)\Steam\steamapps\common\RimWorld</RimWorldDir>
…
<Reference Include="Assembly-CSharp">
  <HintPath>$(RimWorldDir)\RimWorldWin64_Data\Managed\Assembly-CSharp.dll</HintPath>
```

— the owner's live install, which the handover's decompilation work (§1.11 addendum) confirms is
**1.6**. There is no 1.5 configuration, no second output folder, and every verification pass on
record (GABS sessions §1.8 and §1.9) ran on 1.6.

### Why it matters

A single assembly compiled against 1.6 will load under 1.5 only for as long as every API it touches
is binary-identical between the two. RimWorld routinely changes `PawnGenerationRequest`'s shape and
`Def` internals across minor versions, and this mod is unusually exposed there: it patches pawn
generation, reads `PawnGenerationRequest`, and touches `GrowthUtility` and life-stage APIs. A
mismatch surfaces as a `MissingMethodException` **at pawn generation time**, i.e. as a broken save
rather than a refusal to load.

### What was not verified

Whether any API actually differs — that needs a 1.5 `Assembly-CSharp.dll` to compile against, which
is not present on this machine as far as this audit checked. **This is a stated risk, not a
confirmed break.** The concrete, checkable defect is narrower and independent of the API question:
*support is advertised for a version that no build, no test and no in-game session in this
project's history has ever exercised.*

---

<a id="p-08"></a>
## P-08 — The passion-variance-OFF fallback is documented as `0.2778` and implemented as `0.2609`

**Severity: Minor.  Confidence: high.**

### What

`HANDOVER.md:208-210` records what the fallback was changed to during STEP 1:

> **`passionNorm` fallback when passion variance is OFF** was a flat `0.25` … Vanilla's passion
> budget averages 5 pips, so it is now `Constants.VanillaPassionBudget / MaxPassionPips` =
> **0.2778**.

The code does something different. `PawnVarianceSettings.cs:1358-1359`:

```csharp
float passionNorm = Constants.VanillaPassionBudget
    * PassionPipEfficiency(Constants.VanillaMajorBias) / Constants.MaxPassionPips;
```

and `Constants.cs:58` states the resulting value outright:

```
// 5 pips x PassionPipEfficiency(VanillaMajorBias = 0.5) / 18 = 5 x 0.9391 / 18 = 0.2609.
```

`5 / 18 = 0.2778` is the value *before* the efficiency term; `0.2609` is the value after.

### Why it matters

The code is right and the handover is stale — this is STEP 1b landing after §1.11's STEP 1 write-up
and the write-up not being re-swept. Two reasons to record it anyway:

1. It is the **same failure mode as P-02, P-03 and P-05**: a figure derived under a two-term model
   left standing after a third term was added. That is now four instances from one change, which
   says the STEP 1b sweep was systematically incomplete rather than unlucky.
2. The handover explicitly justifies the change with *"No preset disables the axis, so no pasted
   figure moved."* That justification is still sound, but the number attached to it is not, and this
   is the value a future agent would use to hand-check a profile with `enablePassionVariance = false`.

---

<a id="p-09"></a>
## P-09 — `QualityClampEpsilon` is applied in C# but absent from `envelope_check.py`

> **✅ RESOLVED 2026-08-07, ahead of the retune.** `beta_grid` now takes an `eps` argument and
> applies the same `[eps, 1-eps]` clamp as `GetBetaAlphaBeta`; `QualityClampEpsilon` is added to the
> tool's `required` list and to `GEN_CONSTANTS`, so it is drift-checked in-game like the other
> scoring constants (`DebugActions.cs`). The constants regex was widened to accept scientific
> notation, because the constant is written `1e-3f` and the old `[\d.]+` pattern made the tool exit
> with *"Constants.cs is missing: QualityClampEpsilon"*. The literal-not-expression constraint
> documented in `HANDOVER.md` is unchanged. **No envelope figure moved** — all eight presets sit in
> `0.32 … 0.55`, so the clamp is a numerical no-op today; `EnvelopeFigures.g.cs` regenerated solely
> to carry the new `Gen` constant. Fixed before the retune specifically because, as noted below, the
> divergence is only reachable by editing a preset to an extreme value — a retune-shaped action.

**Severity: Minor.  Confidence: high.**

### What

The two implementations disagree about the domain of `averageQuality`.

**C# clamps.** `VarianceProfile.cs:77`, inside `GetBetaAlphaBeta`, which
`CalculateBestOfNScoreCore` calls at `PawnVarianceSettings.cs:1493`:

```csharp
float m = Mathf.Clamp(averageQuality, Constants.QualityClampEpsilon, 1f - Constants.QualityClampEpsilon);
```

**Python does not.** `envelope_check.py:260-262`:

```python
def beta_grid(m, K):
    a, b = m * K, (1.0 - m) * K
    lb = math.lgamma(a + b) - math.lgamma(a) - math.lgamma(b)
```

`m` is used raw. `QualityClampEpsilon` is not in the tool's `required` constants list
(`envelope_check.py:83-90`), so the tool never even reads it — and it is likewise absent from the
`GEN_CONSTANTS` snapshot the in-game verify action diffs (`DebugActions.cs:181-211`), so **no
existing check can notice this divergence**.

### Why it matters, precisely

Two distinct consequences, and it is worth keeping them apart because they have different reach:

1. **A numerical disagreement for any preset with `averageQuality < 0.001` or `> 0.999`.** C# would
   score it at the clamped value, Python at the true value, and `EnvelopeFigures.g.cs` is generated
   from Python while the in-game gate compares it against C#. The gate would fail with **no bug in
   either implementation** — the worst kind of failure, because the previous two gate failures
   (§1.8) were real and this one would look identical.
2. **The Python tool has no protection at all at the endpoints.** At `m = 0`, `a = 0` and
   `math.lgamma(0.0)` is `inf`, so `lb` is `-inf`, every density is `0`, `total` is `0`, and the
   renormalisation `v / total` raises `ZeroDivisionError`. The tool crashes rather than reporting.

**Reachability, checked rather than assumed:** the Profile Editor's quality slider is declared
`Widgets.HorizontalSlider(qSlider, v.averageQuality, 0f, 1f)` (`ProfileEditorTab.cs:239`) and
`VarianceProfileValues.ClampAndSwap` applies only `Mathf.Clamp01` (`VarianceProfile.cs:96`), never
the epsilon — so **`0.0` and `1.0` are reachable by dragging a custom profile's slider to the end**.
That path is safe in-game (the epsilon clamp catches it) and never reaches Python, since the tool
parses only the eight hardcoded presets out of `VarianceProfile.cs`. So the live risk is confined to
someone *editing a preset* to an extreme value, which is a retune-shaped action — i.e. STEP 2.

All eight shipped presets sit in `0.32 … 0.55` (`VarianceProfile.cs:226-398`), so the divergence is
worth **exactly zero** today.

---

<a id="p-10"></a>
## P-10 — `CalculateBestOfNScoreCore` silently falls back to the quantity Defect A was deleted for

**Severity: Minor.  Confidence: high.**

### What

`PawnVarianceSettings.cs:1507-1508`:

```csharp
if (total <= 0f || float.IsNaN(total) || float.IsInfinity(total))
    return CalculateCompositeScore(v.averageQuality, v);
```

Eight lines above it, the comment that opens the same method explains at length why that expression
is wrong (`PawnVarianceSettings.cs:1479-1488`):

> No `n == 1` shortcut. Returning `composite(averageQuality)` here would be assuming
> `E[composite(q)] == composite(E[q])`, which holds only while composite is LINEAR in q. … by
> Jensen the shortcut UNDERSTATES the true expectation: it returned `0.197666` against the
> reference's `0.204709`, moving `Wildcard`'s displayed "Typical" figure from −18% to −21%.

So the exact expression removed as **Defect A** (`HANDOVER.md:540`) is still present as the
degenerate-input path, returning a wrong-but-plausible number with no log line and no visible
marker.

### Why it matters

Not that the fallback is wrong to exist — a guard against `Log(0)`/overflow poisoning the whole
readout is reasonable, and returning *something* beats returning `NaN` into a UI string. Two things
are wrong with it as written:

1. **It is silent.** If it ever fires, the Profile Editor shows a figure that is understated by up
   to ~3.5pp on a convex profile, and nothing anywhere says so. The in-game verify action would
   report a gate failure against `EnvelopeFigures.g.cs` and the reader would go hunting the
   integrator, not the guard.
2. **It contradicts its own file eight lines up** without acknowledging it. The next agent to read
   `CalculateBestOfNScoreCore` top-to-bottom meets a paragraph saying "this expression is a defect,
   here is the measured cost" and then the expression, unannotated. Given that this project has
   twice re-introduced a removed idea by reading a stale justification (the 24-pip era, twice), an
   un-annotated resurrection of a named defect is a trap worth naming.

### What was not verified

Whether the guard is reachable at all. With `K = 8` fixed and `m` clamped to `[0.001, 0.999]`,
`alpha ∈ [0.008, 7.992]` and the largest exponent at the extreme node is around `+7.6`, nowhere near
`float` overflow — so it looks unreachable in practice. It was not proved unreachable, and if it is,
that is an argument for making it an explicit error rather than for leaving it silent.

---

<a id="p-11"></a>
## P-11 — `MinMagnitudeFloor` is neither a floor nor, any longer, a minimum

**Severity: Cosmetic.  Confidence: high.**

`Constants.cs:16` is `public const float MinMagnitudeFloor = 0f;`, and the comment immediately above
it (`Constants.cs:10-15`) exists entirely to tell the reader the name is misleading:

> NOTE this is a Lerp LOW ENDPOINT, not a floor applied after the fact — dropping it rescales
> magnitude at EVERY noise setting, not only at 0.

That warning is accurate and well argued, and it is doing a job the identifier should be doing. The
name asserts two things that are now false: that the value is a *floor* (it is a `Lerp` endpoint)
and that it is a *minimum magnitude* (it is zero, so there is no minimum). `PassionBudgetSpreadMin`
has the same shape of problem and is at least honestly named "Min".

Recorded as cosmetic because nothing computes wrongly. It is included because the misreading it
invites — "a floor, so raising it only affects the low end" — is precisely the misreading that made
the `0.5f → 0f` change a `−25%` dispersion move on `Faithful` rather than the no-op it was expected
to be.

---

<a id="p-12"></a>
## P-12 — Python clamps the composite on one side, C# on both

**Severity: Cosmetic.  Confidence: high.**

- `envelope_check.py:156` — `return min(1.0, (wS * skill_norm + wP * passion_norm) / (wS + wP))`
- `PawnVarianceSettings.cs:1414` — `return Mathf.Clamp01((wS * skillNorm + wP * passionNorm) / totalW);`

`min(1.0, …)` bounds only the top; `Clamp01` bounds both. Since `skill_norm` and `passion_norm` are
each already clamped into `[0, 1]` and both weights are non-negative — either
`CompositeSkillWeight` / `CompositePassionWeight`, both positive, or `0` when an axis is disabled —
the weighted mean cannot go negative on either side. **Zero numerical difference under any reachable
input.**

Recorded only because these two lines are the pair the whole mirror contract rests on, and "they
differ but it cannot matter" is a claim worth having written down with its reasoning rather than
rediscovered as a suspected bug on the next comparison pass.

---

<a id="p-13"></a>
## P-13 — `Constants.cs:142` cites "all seven presets"; there are eight

**Severity: Cosmetic.  Confidence: high.**

```csharp
// Midpoint-rule nodes for the Best-of-N integral. Measured against the 20000-node
// reference in docs/tools/envelope_check.py across all seven presets: 512 nodes lands
// 0.35pp off, which can flip a whole-percent readout; 1024 lands 0.17pp. Do not lower it.
public const int BestOfNIntegrationNodes = 1024;
```

Eight presets ship — `Faithful`, `Distinct`, `Wildcard`, `Desperate`, `Elite`, `Sovereign`,
`Specialist`, `Scavenger` (`VarianceProfile.cs:226-398`), and the envelope table at
`HANDOVER.md:1556-1563` lists all eight. The likely history is that the measurement predates a
preset change — `Constants.cs:97` records that `Gifted` was *removed* on 2026-08-04, which would
have taken the count the other way.

The consequence is small but not zero: the `0.35pp` and `0.17pp` error figures quoted here are the
evidence for `1024`, and they were measured over a preset set that no longer matches the shipped
one. The comment's instruction ("Do not lower it") is almost certainly still right — `Wildcard`, the
kinked profile that drives the worst-case error, is still present.

---

# Part B — Code findings

Everything below came from three parallel read-only audits and was then **re-verified line by line
against the source by hand** before being written down. Several candidates were discarded at that
stage and are listed under "Rejected during cite-check" at the end — knowing what was checked and
dismissed is worth as much as the findings themselves, and `HANDOVER.md:342` records two occasions
where an unchecked citation sent someone editing the wrong code.

---

<a id="p-14"></a>
## P-14 — `Resolve()` answers a dangling id with an unrelated profile's live values, and corrupts its label

> **✅ RESOLVED 2026-08-06.** The `customProfiles[0]` branch is deleted; a dangling id now falls to
> `VarianceProfiles.VanillaLike.MakeValues()` (a clone, so the trailing label write is harmless) and
> emits a `Log.WarningOnce` naming the id. The custom-profile aliasing is deliberately left alone —
> P-19 argues it is intended, and clone-on-resolve would break live editing. **T5-M1 should be
> reclassified as resolved-in-effect:** an unvalidated imported id can still arrive, but it can no
> longer silently generate pawns from someone else's profile.

**Severity: Major.  Confidence: high.**

### What

`PawnVarianceSettings.cs:219-234`, verbatim:

```csharp
public VarianceProfileValues Resolve(string id)
{
    VarianceProfileValues vals = null;
    var preset = VarianceProfiles.GetPresetById(id);
    if (preset != null) vals = preset.MakeValues();
    else
    {
        var custom = GetCustomProfile(id);
        if (custom != null) vals = custom.values;
        else if (customProfiles != null && customProfiles.Count > 0) vals = customProfiles[0].values;
        else vals = VarianceProfiles.VanillaLike.MakeValues();
    }

    if (vals != null) vals.profileLabel = LabelFor(id);
    return vals;
}
```

Two problems on the `customProfiles[0]` line, and they compound.

**1. The fallback is arbitrary, not safe.** An id matching nothing resolves to *whichever custom
profile happens to be first in the list* — that profile's quality, skill band and passion budget are
then used to generate pawns. The `else` branch two lines down shows what a safe fallback looks like
(`VarianceProfiles.VanillaLike`), and it is reached only when the custom list is **empty**. The mod
is careful when it has nothing to offer and careless when it has something.

**2. It then writes the wrong label into that profile.** `vals` is a **live reference** here, not a
clone (that is P-19), so the trailing `vals.profileLabel = LabelFor(id)` mutates
`customProfiles[0].values.profileLabel` — and `LabelFor` returns `id ?? "?"` for an unresolvable id
(`PawnVarianceSettings.cs:244`). A valid custom profile ends up labelled with a dead id string.

Contrast the preset branch: `preset.MakeValues()` is a clone (`VarianceProfile.cs:202`, documented
*"Always a copy… a preset must stay pristine"*), so the same label write is harmless there. **The
protection exists and does not extend to the branch that needs it most.**

### When it triggers

`Resolve` is called from `RefreshResolved` (`:487-493`) on `activeProfileId` and `hostileProfileId`,
and from `RefreshEditor` (`:129-133`). It needs one of those — or an override value — to name
nothing. The known route is clipboard import: `SettingsTransfer.Import` → `CopyFrom`, which
`HANDOVER.md:324` already records as *not validating imported profile ids* (tracked as **T5-M1**,
rated Minor). A payload referencing a `custom_…` id that is not itself in the payload lands here.

### Why this reclassifies the tracked T5-M1

T5-M1 is filed as "ids are not validated on import", which reads as input hygiene. The actual
consequence is that **pawns are generated from a profile the player never chose, with no error, no
log line and no visible symptom** — the General tab still shows the requested profile's name,
because `LabelFor` is asked about the *requested* id, not the one that was used. The player sees
`Sovereign` and gets whatever `customProfiles[0]` happens to be. That is not an import nicety; it is
a silent wrong-profile path, and the severity should follow.

### What was not verified

Whether any shipped UI route can produce a dangling id without an import. The Delete button and
`ResetToDefaults` both scrub correctly, so an externally-supplied payload appears to be required.

---

<a id="p-15"></a>
## P-15 — `gene.passionPreAdd` is snapshotted after the bump, so gene removal cannot restore the passion

> **✅ RESOLVED 2026-08-06.** `gene.passionPreAdd = record.passion;` moved above the `if (record.passion
> == Passion.None)` block, matching vanilla `Pawn_GeneTracker.AddGene`'s ordering. The restore branch
> now records `None`; the walk-already-assigned branch is unchanged. The unread-method caveat below
> still stands for the *size* of the in-game symptom, but the fix is correct against the code's own
> stated intent either way, so it does not gate on a decompile.

**Severity: Major.  Confidence: high on the code; medium on the downstream symptom.**

### What

`PassionVarianceApplier.cs:260-266`:

```csharp
if (record.passion == Passion.None)
{
    Passion bumped = gene.def.passionMod.NewPassionFor(record);
    trace?.AppendLine($"  GENE BUMP: {gene.def.defName} restored {record.def.defName} to {bumped} (walk never reached it)");
    record.passion = bumped;
}
gene.passionPreAdd = record.passion;
```

The last line runs **after** `record.passion` has been overwritten with `bumped`. The comment
justifying the whole block (`PassionVarianceApplier.cs:235-238`) states the opposite intent in these
words:

> Also refresh `Gene.passionPreAdd`, since `AddGene`'s original snapshot **(whatever the passion was
> before its bump)** is now stale after our reroll — leaving it stale would make a later gene removal
> (`Gene.NewPassionForOnRemoval`) restore the wrong pre-bump value.

`passionPreAdd` means *what this skill's passion was before this gene touched it*.

### The two branches behave differently, and only one is wrong

| Branch | `record.passion` on entry | Correct `passionPreAdd` | What is stored | Verdict |
|---|---|---|---|---|
| Walk left the skill at `None`; the gene restores it | `None` | **`None`** | `bumped` (Minor or Major) | ❌ **wrong** |
| Walk already assigned a passion; the gene does not fire | e.g. `Minor` | `Minor` | `Minor` | ✅ correct |

The defect is confined to exactly the case the block exists to handle — the restore path.

### Consequence

On removal, vanilla's `Gene.NewPassionForOnRemoval` reads `passionPreAdd` to decide what to put
back. Told the pre-add value was `Minor`, it restores `Minor` — the passion the gene itself granted.
**The gene's bonus survives its own removal, permanently.** Vanilla's `Pawn_GeneTracker.AddGene`
avoids this by writing `passionPreAdd` *before* applying `NewPassionFor`.

### When

Biotech; a pawn carrying an `AddOneLevel` passion-mod gene (the shipped `AptitudeRemarkable_*`
family) whose bumped skill was not reached by the passion walk; and later losing that gene —
xenogerm implantation replacing the germline, gene-removal surgery, or a xenotype change.

### What was not verified

`Gene.NewPassionForOnRemoval`'s body was not decompiled during this audit, so the restoration
behaviour is inferred from the field name, from vanilla's `AddGene` ordering, and from this file's
own comment describing the mechanism. **The ordering defect relative to the code's stated intent is
certain regardless**; only the size of the in-game symptom depends on that unread method. The
project already has `ilspycmd` installed (`HANDOVER.md:152`), so confirming it is cheap.

---

<a id="p-16"></a>
## P-16 — All three appliers dereference `pawn.skills` and `pawn.story` unguarded

> **⚠️ The first attempt at this fix introduced a worse bug, caught in review.** `Apply` guarded on
> `pawn.skills` only, then wiped every passion to `None` before delegating to `AssignPassions`,
> whose guard also required `pawn.story` — so an admitted-then-rejected pawn was left permanently
> passionless with nothing logged. A narrower guard at a mutating entry point is strictly worse than
> no guard, which at least threw where the postfix could catch it. **Any future guard added to a
> method that mutates before delegating must match the delegate's guard exactly.**
>
> **✅ RESOLVED 2026-08-06.** Six guards replace ~15 unguarded dereferences (the original count in
> this entry was low — `GrowUpVariance.cs` alone has ten). Gated at both patch entry points
> (`HarmonyPatches.cs`, `GrowUpVariance.Apply`) and defensively at each public applier entry:
> `SkillVarianceApplier.Shift` (one guard covers both wrappers), `PassionVarianceApplier.Apply` and
> `.AssignPassions`, `TraitVarianceApplier.Apply`. The HAR-reachability question is now moot — the
> guard is correct whether or not such a race ships.

**Severity: Major.  Confidence: high on the asymmetry; medium on reachability.**

### What

| File | Line | Expression |
|---|---|---|
| `SkillVarianceApplier.cs` | 54 | `foreach (SkillRecord record in pawn.skills.skills)` |
| `PassionVarianceApplier.cs` | 32 | `foreach (SkillRecord record in pawn.skills.skills)` |
| `TraitVarianceApplier.cs` | 26 | `List<Trait> current = pawn.story.traits.allTraits;` |
| `GrowUpVariance.cs` | 103 | `float existingPips = pawn.skills.skills.Sum(` |

The only upstream gate is `pawn.RaceProps.Humanlike` (`HarmonyPatches.cs:24`, `GrowthUpPatch.cs:44`).

### Why the asymmetry is the evidence

This is not a blanket "add null checks" complaint. The same files guard the *other* optional tracker
obsessively — `pawn.genes` is null-checked at **every** use:

```
PassionVarianceApplier.cs:90    if (ModsConfig.BiotechActive && pawn.genes != null)
PassionVarianceApplier.cs:239   if (ModsConfig.BiotechActive && pawn.genes != null)
TraitVarianceApplier.cs:140     if (ModsConfig.BiotechActive && pawn.genes != null)
TraitVarianceApplier.cs:191     if (!ModsConfig.BiotechActive || pawn.genes == null) return null;
```

and `DebugActions.cs:636` reaches for this very data defensively —
`pawn.story?.traits?.allTraits?.Count ?? 0` — while the applier that *writes* it does not. The
codebase already knows these trackers are optional; the knowledge did not reach the hot path.

`RaceProps.Humanlike` is an intelligence check. It does not promise a populated `skills` or `story`
tracker, and Humanoid Alien Races — which this mod explicitly supports, and against whose races the
2026-08-06 sessions were run (`HANDOVER.md:364`) — lets a race def turn those components off.

### How it composes with P-17

An NRE here lands in `GeneratePawn_Postfix`'s `catch` (`HarmonyPatches.cs:55`) and is logged once —
**unless `verboseLogging` is on, in which case it is rethrown into RimWorld's pawn generation**
(P-17). Together: install a HAR race with skills disabled, tick the setting whose label promises
more logging, and pawn generation throws.

### What was not verified

That a currently-published HAR race actually ships `Humanlike == true` with a null `skills` or
`story` tracker. This is a documented HAR capability, not a def observed during this audit — **the
reachability is inferred; the missing guard is not.** Settling it is cheap: the owner has Wolfein
and Milira installed, and a debug action walking `DefDatabase<ThingDef>` for humanlike races missing
those components would answer it in one run.

---

<a id="p-17"></a>
## P-17 — "Verbose logging (dev mode)" is not gated on dev mode, and rethrows inside pawn generation

**Severity: Minor.  Confidence: high.**

### What

`HarmonyPatches.cs:55-59`, the exception guard on the pawn-generation postfix:

```csharp
catch (Exception ex)
{
    if (settings.verboseLogging) throw;
    Log.ErrorOnce($"[PawnVarianceMod] Exception applying variance to {pawn.LabelShort}: {ex}", …);
}
```

**This is deliberate and disclosed** — the checkbox's own tooltip says so
(`PawnVarianceSettings.cs:1139-1142`):

> "Rethrows exceptions instead of swallowing them, and logs a per-pawn breakdown of how traits and
> passions were assigned. Leave off for normal play."

So this entry is **not** a claim that the behaviour is unintended. Three narrower problems survive
that reading:

1. **The "(dev mode)" in the label is not enforced.** The checkbox is drawn unconditionally, with no
   `Prefs.DevMode` gate — compare the line immediately above it, which *is* conditional
   (`if (ModsConfig.BiotechActive)`, `PawnVarianceSettings.cs:1134`). Any player can tick it in the
   ordinary Mod Settings window. The label describes an intended audience; nothing restricts it to
   that audience.
2. **One checkbox, two unrelated effects.** "More log detail" and "convert a contained error into a
   pawn-generation-breaking exception" have no reason to share a control. A player who wants the
   per-pawn trace — the reason anyone ticks it — cannot get it without the rethrow.
3. **The sibling postfix reached the opposite conclusion.** `GrowthMomentMakeChoices_Postfix`
   (`GrowthUpPatch.cs:174-177`) catches and logs unconditionally, consulting `verboseLogging` only
   to decide how chatty to be (`:161`). Its comment explains why (`:157-158`): *"this postfix runs
   inside vanilla's UI dialog-close path, so an escaping exception would break the dialog."* The
   same reasoning applies at least as strongly to `GenerateNewPawnInternal`, whose own class comment
   (`HarmonyPatches.cs:8-15`) is entirely about not disturbing pawn generation.

### Consequence

The failure mode is diagnostic-inverted: the setting a user reaches for **because** something is
going wrong is the setting that turns a logged, survivable error into a thrown one — during world
generation, a raid, or a starting scenario.

---

<a id="p-18"></a>
## P-18 — `TraitAgeCap`'s age 7–9 and 10–12 branches cannot be reached under vanilla growth ages

**Severity: Minor.  Confidence: high for the vanilla case.**

### What

`TraitAgeCap.MaxRolledTraitsFor` (`TraitAgeCap.cs:16-30`) computes a per-age cap; its comment states
the intent as *"0 rolled traits below age 7, 1 at 7-9, 2 at 10-12 and 3 from 13 on"*:

```csharp
if (age >= momentAges.Max()) return int.MaxValue;
return momentAges.Count(a => a <= age);
```

It has exactly two callers, and **neither can present it with a pawn under 13** in a vanilla config:

| Caller | Gate above it | Age on entry |
|---|---|---|
| `TraitVarianceApplier.cs:46` (generation) | `HarmonyPatches.cs:38` — `if (… AgeBiologicalYears < Constants.VanillaAdultPassionAge) return;` | **≥ 13** |
| `GrowUpVariance.cs:175` (growth moment) | `GrowthUpPatch.cs:50` — `if (currentStage != DevelopmentalStage.Adult) return;` | **≥ 13** |

With `GrowthUtility.GrowthMomentAges = { 7, 10, 13 }`, `momentAges.Max()` is 13, so line 27 returns
`int.MaxValue` on every call and the `Count(a => a <= age)` line never executes. The scenario the
comment guards against — *"Without this cap the mod would hand a five-year-old a full adult trait
load"* — is already prevented two layers up.

### Why record it rather than delete the class

It is **not** unconditionally dead, and that is the point:

- Its own comment says thresholds are *"read from `GrowthUtility` at runtime rather than hardcoded,
  since Biotech content or another mod may change them."* A mod adding a growth moment at 16 makes
  `Max()` 16 and the cap binds immediately at 13–15.
- A HAR race reaching `DevelopmentalStage.Adult` before 13 would reach it too.

So it is live code with an unreachable-in-vanilla core. Two consequences: nothing in the shipped
configuration exercises the counting branch, so a bug in it would not surface; and a reader taking
the comment at face value believes the mod actively caps child traits when in vanilla it never gets
the chance.

### What was not verified

Whether any mod on the owner's setup alters `GrowthMomentAges`, and whether Milira or Wolfein
declare life stages that reach `Adult` early.

---

<a id="p-19"></a>
## P-19 — `Resolve()` clones presets but aliases custom profiles

**Severity: Minor.  Confidence: high on the fact; the intent is genuinely ambiguous.**

### What

Same method as P-14. Line 223 clones; line 227 does not:

```csharp
if (preset != null) vals = preset.MakeValues();   // clone — "a preset must stay pristine"
…
if (custom != null) vals = custom.values;          // live reference
```

`RefreshResolved` (`:489-492`) assigns `Active` and `Hostile` from `Resolve`; `RefreshEditor`
(`:131`) assigns `editingValues` from it. For a **custom** profile id all of these are the same
object as `customProfiles[n].values`. Set a custom profile as Active and open it in the editor, and
`Active` and `Editing` are one instance — every slider frame writes directly into the values pawn
generation reads.

### Why Minor rather than Major

Very likely intended. The mod has no apply/cancel step; settings are live, and edits to a custom
profile *should* take effect. Presets are cloned because they are static templates that must not be
mutated — a different requirement. On that reading the asymmetry is correct.

What is not defensible is that it is **undocumented**. `MakeValues`'s doc comment
(`VarianceProfile.cs:202`) explains the cloning rule for presets and says nothing about the custom
path, so a reader generalises the wrong invariant. Concrete residue:

- `RefreshResolved` writes `Active.profileLabel` (`:490`) and `RefreshEditor` writes
  `editingValues.profileLabel` (`:132`) — both mutate stored profile state as a side effect of an
  operation named "refresh", which reads as read-only.
- It is the mechanism that turns P-14 from "wrong values returned" into "another profile corrupted".

`DuplicateCurrentProfile` (`:1106`) gets this right — `Resolve(EditorProfileId).Clone()` — which
shows the aliasing is understood at one call site and not at the others.

---

<a id="p-20"></a>
## P-20 — The life-stage postfix has no exception guard, unlike both of its siblings

**Severity: Minor.  Confidence: high on the gap; medium on how likely a throw is.**

### What

Three Harmony postfixes; two are guarded, one is not.

| Patch | Guard |
|---|---|
| `GeneratePawn_Postfix.Postfix` (`HarmonyPatches.cs:46-59`) | `try`/`catch` around all applier calls |
| `GrowthMomentMakeChoices_Postfix.Postfix` (`GrowthUpPatch.cs:159-177`) | `try`/`catch`, with the reason spelled out at `:157-158` |
| **`DevelopmentalStage_Postfix.Postfix` (`GrowthUpPatch.cs:40-89`)** | **none** |

Unprotected work in that third method, ahead of the internally-guarded `GrowUpVariance.Apply` at
`:88`:

```csharp
VarianceProfileValues v = settings.ValuesFor(___pawn);                // :60
…
else if (GrowUpPendingComponent.HasUnresolvedGrowthLetter(___pawn))   // :80
{
    pending.Register(___pawn);                                        // :82
```

### Why it matters more than the line count suggests

This is a postfix on `Pawn_AgeTracker.PostResolveLifeStageChange`, and the class's own comment
(`GrowthUpPatch.cs:26-31`) establishes how hot that path is: `AgeTickInterval` is its only caller,
and **it re-fires once on the first tick after any save load, for potentially every pawn on the
map**. So the unguarded code runs against every humanlike pawn in the save, on every load.

`ValuesFor` is the widest-surface call in the mod — it walks faction, race and xenotype dictionaries
keyed by `defName` and resolves a profile id (and see P-14 for what it can resolve to). An exception
there escapes into vanilla's life-stage plumbing during load. The neighbouring class documented
precisely this hazard for the dialog path and guarded it; this one did not get the same treatment.

### What was not verified

Whether `ValuesFor`, `HasUnresolvedGrowthLetter` or `Register` can actually throw on reachable
input. This is a defence-in-depth gap and an inconsistency with two siblings, not a demonstrated
crash.

---

<a id="p-21"></a>
## P-21 — A parallel-list count mismatch discards an entire override axis, silently

**Severity: Minor.  Confidence: high.**

### What

`PawnVarianceSettings.cs:422-430`, repeated verbatim for all six flattened pairs (overrides and
priorities × faction/race/xenotype) at `:432`, `:442`, `:452`, `:462`, `:472`:

```csharp
factionOverrides = new Dictionary<string, string>();
if (factionOverrideKeys != null && factionOverrideValues != null
    && factionOverrideKeys.Count == factionOverrideValues.Count)
{
    for (int i = 0; i < factionOverrideKeys.Count; i++)
    {
        factionOverrides[factionOverrideKeys[i]] = factionOverrideValues[i];
    }
}
```

The count guard is right to exist — mismatched lists would otherwise throw or pair the wrong key
with the wrong value. The problem is the **`else` that does not exist**: on mismatch the dictionary
is left empty and nothing is logged, at any level.

### Consequence

Every override the player configured on that axis is gone after a load, and the mod reports success.
Pawns then generate from the Active/Hostile profile as if no override had been set, and the
Overrides tab shows an empty list — which reads as "I never set these up", not "these were
discarded".

Recovery is partial at best: `PopulateDefaultOverrides()` runs afterwards (`:482`) but re-seeds only
the *default* set, and it early-returns entirely when `hasInitializedDefaultOverrides` is already
true (`:150`) — which it will be for any existing save.

A single `Log.Warning` on the else-path would turn silent data loss into a diagnosable event. That
is the shape of the fix; it is not applied here.

### What was not verified

How a count mismatch arises in practice. A hand-edited or truncated clipboard payload is the obvious
route; whether `Scribe_Collections` can itself produce one on a partially-written save was not
investigated.

---

<a id="p-22"></a>
## P-22 — Custom profile ids come from `DateTime.Now.Ticks` with no uniqueness check

**Severity: Minor.  Confidence: high on the missing check; low on collision probability.**

`PawnVarianceSettings.cs:1094` and `:1104`, in `CreateNewCustomProfile` and
`DuplicateCurrentProfile`:

```csharp
string newId = "custom_" + DateTime.Now.Ticks;
```

Neither checks the result against existing ids. `GetCustomProfile` (`:213-217`) resolves by
`customProfiles.Find(p => p.id == id)` — **first match wins** — so two profiles sharing an id make
the second permanently unreachable by id. It stays visible and selectable in the profile menu, which
iterates the list directly, so a player can pick it and have `activeProfileId` resolve to the *other*
one.

`DateTime.Now`'s nominal unit is 100 ns but its real resolution on Windows is coarser — commonly
1–15 ms — so the guarantee the type implies is weaker than it looks. Two creations inside one timer
tick is still improbable from human clicking; **the finding is the absent check, not a prediction
that it will fire.**

Note this also breaks the id convention the handover documents (`HANDOVER.md:1762`: *"string IDs
(`custom_1`, `custom_2`)"*), which no longer describes what is generated.

---

<a id="p-23"></a>
## P-23 — Gene `forcedTraits` overwrite `kindDef` `forcedTraits` degree with no precedence rule

**Severity: Minor.  Confidence: medium.**

`TraitVarianceApplier.CaptureForcedTraits` (`:132-147`) merges two forced-trait sources into one
dictionary by unconditional assignment:

```csharp
if (pawn.kindDef?.forcedTraits != null)
    foreach (var t in pawn.kindDef.forcedTraits)
        forced[t.def] = t.degree ?? FirstValidDegree(t.def);

if (ModsConfig.BiotechActive && pawn.genes != null)
    foreach (var gene in pawn.genes.GenesListForReading)
        if (gene.def.forcedTraits != null)
            foreach (var t in gene.def.forcedTraits)
                forced[t.def] = t.degree;      // overwrites the kindDef entry
```

If both sources force the same `TraitDef` at different degrees, the gene silently wins because it is
written second. No comment acknowledges a precedence decision, so the ordering reads as incidental
rather than chosen — and the file is otherwise unusually careful here (the lines just above,
`:126-131`, argue in detail about a nullable-degree fallback).

The consumer encodes the opposite rule. `TraitProtection.Build` (`:37-41`) attributes each protected
trait to a source **first-writer-wins** (`!defs.ContainsKey`) while taking the **degree** from this
dictionary, where the *last* writer won. On a collision a trait can be labelled kindDef-forced while
carrying the gene's degree.

Narrow — it needs a `PawnKindDef` and an active `GeneDef` forcing the same trait at different
degrees, a mod-content combination rather than anything vanilla ships. Recorded because the two
halves encode opposite precedence rules and neither states which is intended.

---

<a id="p-24"></a>
## P-24 — The quality slider writes unconditionally, breaking the file's own double-guard pattern

**Severity: Minor.  Confidence: medium.**

`ProfileEditorTab.cs:238-240`:

```csharp
GUI.enabled = outerEnabled && EditingCustom;
v.averageQuality = Widgets.HorizontalSlider(qSlider, v.averageQuality, 0f, 1f);
GUI.enabled = outerEnabled;
```

Every other editable control in the file belts *and* braces — `GUI.enabled` **plus** an explicit
`if (EditingCustom)` before the write. Compare `ProfileEditorTab.cs:438-445`, on the same object:

```csharp
bool childVal = v.applyChildSkillShift;
…
if (EditingCustom) v.applyChildSkillShift = childVal;
```

The quality slider trusts `GUI.enabled` alone. That the rest of the file does not is itself the
evidence that the author judged `GUI.enabled` insufficient.

Impact is bounded: for a read-only preset `v` is `editingValues`, which came from
`preset.MakeValues()` — a clone — so a stray write cannot reach the preset. It would show a wrong
on-screen number until the editor re-resolves. Worth noting alongside: `HANDOVER.md:521` records
that GABS's `get_ui_layout` **cannot observe ambient `GUI.enabled`**, so no automated check in this
project can detect a regression in this area.

### What was not verified

Whether RimWorld's `Widgets.HorizontalSlider` can return a changed value while
`GUI.enabled == false`. That needs a runtime test. The inconsistency with the rest of the file is
certain; the exploitability is not.

---

<a id="p-25"></a>
## P-25 — `LastKnownStage` never drops entries for dead pawns

**Severity: Cosmetic.  Confidence: high on the leak; high that it does not matter.**

`GrowthUpPatch.cs:38` declares the dictionary and `:48` writes to it unconditionally:

```csharp
private static readonly Dictionary<int, DevelopmentalStage> LastKnownStage = new Dictionary<int, DevelopmentalStage>();
…
LastKnownStage[___pawn.thingIDNumber] = currentStage;
```

The only removal path is `ClearForNewGame()` (`:97-100`), wired to `Game.LoadGame` and
`Game.InitNewGame`. Nothing removes an entry when a pawn dies or is destroyed, so a long single
session with heavy humanlike churn accumulates one `int → enum` pair per pawn ever observed.

Recorded for completeness only. Entries are a few bytes, the gate is humanlike-only, and the design
note at `:91-96` shows the session-only lifetime is deliberate. **No action implied.**

---

<a id="p-26"></a>
## P-26 — The "never touches them" promise uses a weaker faction test than the override lookup

> **✅ RESOLVED 2026-08-06.** Faction resolution is now a single method,
> `PawnVarianceSettings.EffectiveFactionOf(pawn, request)` (`pawn.Faction` → `request.Faction` →
> `kindDef.defaultFactionDef`), with `IsHostileToPlayer` and `IsExcludedAsHostile` on top. All four
> enforcement sites route through it: `HarmonyPatches.cs`, `GrowUpVariance.cs`, `GrowthUpPatch.cs`,
> and `ValuesFor`'s Hostile-profile branch — which also closes the `:258` vs `:306` asymmetry noted
> at the end of this entry. The toggle's test is now identical to the override lookup's, so the
> unmeasured-frequency question no longer gates anything: the two can no longer disagree.

**Severity: Major.  Confidence: high on the inconsistency; medium on frequency.**

### What the player is promised

`PawnVarianceSettings.cs:1116-1119`, the checkbox and its tooltip:

> **"Apply to hostile-faction pawns"** — *"When off, raiders and other hostile pawns are generated
> exactly as in vanilla and **this mod never touches them**. When on, they are generated from the
> profile you pick below."*

That is an absolute claim, and it is enforced in one place: `HarmonyPatches.cs:25`.

### Three different answers to "what faction is this pawn?"

The mod resolves a pawn's faction in three places, with three different amounts of effort:

| Site | Purpose | Resolution chain |
|---|---|---|
| `HarmonyPatches.cs:25` | **enforces the toggle** | `pawn.Faction` — **only** |
| `PawnVarianceSettings.cs:255-259` | picks the faction *override* | `pawn.Faction` → `request.Faction` → `kindDef.defaultFactionDef` via `FactionManager` |
| `PawnVarianceSettings.cs:306-307` | picks the *Hostile profile* | `pawn.Faction` → `request.Faction` |

The weakest test is the one guarding the strongest promise.

### Why the fallbacks are evidence, not speculation

`ValuesFor` does not fall back to `request.Faction` for decoration. That line exists because
`pawn.Faction` is observably `null` at postfix time for some pawns — otherwise the fallback would be
unreachable code, and the third fallback through `FactionManager` (`:258-259`) would be even more so.
So the codebase already asserts, in its own structure, that **`pawn.Faction` alone is not a reliable
answer at the moment `GeneratePawn_Postfix` runs**.

### Consequence

For any pawn generated with `pawn.Faction == null` and `request.Faction` set to a hostile faction,
with `applyToHostilePawns` **off**:

1. `HarmonyPatches.cs:25` does not fire — the null check short-circuits the hostility test.
2. Execution continues into the appliers, so the mod **does** touch the pawn.
3. `ValuesFor` then recovers the faction via `request.Faction` (`:257`) and cheerfully applies that
   hostile faction's override (`:276-282`) — `Empire → Elite@Highest`, `Pirate → Scavenger`, and so
   on from the shipped defaults.

The net result is the exact inverse of the setting: a hostile pawn, with hostile variance disabled,
generated from a hostile-faction override. The user-facing symptom is raiders that are not vanilla
despite the toggle, which is very hard to attribute — a player checking the setting sees it off.

### What was not verified

**How often `pawn.Faction` is actually null when this postfix runs.** `GenerateNewPawnInternal` does
assign the faction during generation, so this may be confined to specific paths (quest pawns,
scenario pawns, world-pawn creation, faction-less generation that is assigned later). Establishing
the frequency needs a runtime probe, and the mod already has the harness for it: a counter in the
postfix logging `pawn.Faction == null && request.Faction != null` over a
`Roll pawns and dump distribution` batch would answer it in one run. **Until that is measured, treat
this as a confirmed inconsistency with an unmeasured blast radius, not a confirmed regression.**

The `kindDef.defaultFactionDef` asymmetry between `:258-259` and `:306-307` is a second, smaller
instance of the same problem: a pawn can be faction-overridden through a route the Hostile-profile
branch cannot see, so the two branches can disagree about the same pawn.

---

<a id="p-27"></a>
## P-27 — Three of four `HostileTo(Faction.OfPlayerSilentFail)` calls omit the null check the fourth has

> **✅ RESOLVED 2026-08-06** as a side effect of P-26. All four sites now call
> `IsHostileToPlayer(Faction)`, which is null-safe on both the faction and the player side. The
> question of what `Faction.HostileTo(null)` actually does is now unreachable rather than unanswered
> — worth noting if anyone later wonders why the decompile was never done.

**Severity: Minor.  Confidence: high on the asymmetry; medium on the consequence.**

### What

Four call sites test hostility against the player faction. Only one guards against the player
faction not existing:

```csharp
// PawnVarianceSettings.cs:309  — GUARDED
if (applyToHostilePawns && fHostile != null && Faction.OfPlayerSilentFail != null
    && fHostile.HostileTo(Faction.OfPlayerSilentFail))

// HarmonyPatches.cs:25  — NOT guarded
if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayerSilentFail)) return;

// GrowUpVariance.cs:41  — NOT guarded
if (!settings.applyToHostilePawns && pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayerSilentFail))

// GrowthUpPatch.cs:55  — NOT guarded
if (!settings.applyToHostilePawns && ___pawn.Faction != null && ___pawn.Faction.HostileTo(Faction.OfPlayerSilentFail)) return;
```

### Why the null case is not hypothetical

`OfPlayerSilentFail` differs from `OfPlayer` in exactly one respect: it returns `null` instead of
logging an error when there is no player faction. The mod adopted it deliberately — `HANDOVER.md:1781`
records it as *"Clean Non-Spam Faction Handling: replaced `Faction.OfPlayer` with
`Faction.OfPlayerSilentFail` across call sites to eliminate world-gen log errors."*

So the project already established that **these call sites run when the player faction does not yet
exist** — that is the whole reason for the switch. The switch removed the log spam and left three of
the four sites passing the resulting `null` straight into `HostileTo`.

### What was not verified

**What `Faction.HostileTo(null)` actually does.** It was not decompiled during this audit. The
plausible outcomes range from "returns false harmlessly" to "logs an error per pawn" to an NRE inside
`RelationWith`. The guarded site at `:309` suggests whoever wrote it did not want to find out — and
that is the finding: **one author decided the null needed handling and three sibling call sites of
the same expression do not handle it.** One of the two positions is wrong, and which one is a
ten-minute check with the `ilspycmd` setup already in place (`HANDOVER.md:152`).

Note the severity is capped by scope: even in the worst case this is a world-generation-time error
path, not a wrong-pawn path.

---

## Rejected during cite-check

Candidates raised during the audit and dropped after verification, recorded so the same ground is
not re-covered.

| Candidate | Why it was dropped |
|---|---|
| *"`DevelopmentalStage_Postfix` and `LastKnownStage` live in `HarmonyPatches.cs`"* | Wrong file. `HarmonyPatches.cs` is **62 lines** and contains only `GeneratePawn_Postfix`; both are in `GrowthUpPatch.cs`. The underlying findings survived and are filed as P-20 and P-25 with corrected citations. |
| *"The age-13 early return contradicts the `applyVarianceToChildren` setting"* | It does not. That setting is labelled **"Apply variance to children growing up"** and its tooltip scopes it to *"when a child turns 13"* (`PawnVarianceSettings.cs:1136-1138`); it gates the growth-moment path (`GrowthUpPatch.cs:43`, `GrowUpVariance.cs:35`) and was never meant to gate generation. What survives is the narrower P-18. |
| *"The `verbose logging` rethrow is an unnoticed bug"* | It is documented in the checkbox's own tooltip. Downgraded to P-17, which flags only the ungated "(dev mode)" label, the bundling of two effects, and the disagreement with the sibling postfix. |
| *"`Constants.cs` contains malformed `\` comment markers"* | A tool rendering artifact. `cat -A` confirms the bytes are `//` throughout. |
| *"`zzz-Do-Not-Commit/test_race_resolution.py` is a dangling reference"* | The file exists; `HANDOVER.md:1849` is correct. |
| *"`EnvelopeFigures.g.cs` may be stale against the current constants"* | Running `python docs/tools/envelope_check.py` reports `Source/EnvelopeFigures.g.cs: unchanged` and `PASS`. The generated table is current. |
