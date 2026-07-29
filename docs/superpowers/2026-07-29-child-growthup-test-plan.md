# Child / Biotech growth-up test plan

Date: 2026-07-29
Covers: HANDOVER.md "NOT tested" item 1 (a–f), plus items 2 and 4 while you're already in there.

Everything below is run with **Verbose logging (dev mode)** ON (Options → Mod settings → Pawn
Variance Mod → General). That switch now drives the trait trace as well as the passion one.

---

## Before you start: two things that will otherwise waste the session

### 1. The grow-up patch needs a baseline firing before it can detect anything

`DevelopmentalStage_Postfix` only acts when **it has itself observed** the pawn as NotAdult on an
earlier firing and now sees Adult (`GrowthUpPatch.cs:48-54`). Verified in the decompile of
`Pawn_AgeTracker`: `PostResolveLifeStageChange` is called from `AgeTickInterval` only when the
`lifeStageChange` flag is set, and `ExposeData` resets `cachedLifeStageIndex = -1` on
`PostLoadInit` — so the next `RecalculateLifeStageIndex` sees a changed index and sets that flag.
That gives **exactly one resync firing per pawn after every load**, which is what records the
baseline.

**Therefore: after loading the save, let the game run unpaused for a few seconds before aging
anyone up.** If you load and immediately age the child in the same paused moment, no baseline was
ever recorded, the patch correctly declines to act, and you will see nothing — which looks
identical to a bug. This is the single most likely way to get a false negative today.

### 2. Child→Adult is the **13th** birthday, not the 18th

`Pawn_AgeTracker.AdultMinAge` is the minAge of the first life stage whose `developmentalStage` is
Adult. From the Core defs that is `HumanlikeTeenager` at **13**. Stages are: Baby 0–2, Child 3–8,
PreTeenager 9–12 (still `DevelopmentalStage.Child`), Teenager 13–17 (**Adult**), Adult 18+.

So the transition you are testing is **12 → 13**.

### Aging a pawn

Dev mode on → open the Debug Actions menu and search `age`; use the pawn-targeted age action on
your test child. Set the age to 13 in one step from 12.

---

## A prediction to confirm or refute first (cheapest, highest information)

**`TraitAgeCap` may be structurally dead in normal Biotech play.** Two paths reach it, and on
current reading neither can make it bind:

- **Generation** — `GeneratePawn_Postfix` early-returns for any non-Adult pawn when Biotech is
  active (`HarmonyPatches.cs:27`), so `TraitVarianceApplier.Apply` never runs on a child at all.
- **Grow-up** — the pawn is Adult by definition, i.e. age ≥ 13. `TraitAgeCap.MaxRolledTraitsFor`
  returns `int.MaxValue` once `age >= GrowthMomentAges.Max()`, and that max **is** 13
  (`{ 7, 10, 13 }`, confirmed in the decompile). So the cap is already released at the exact
  moment grow-up fires.

The trace answers this directly: every grow-up trace should read `age cap none (fully grown)`.
If it does, item (c) below is not "untested" so much as "unreachable", and the honest fix is to
document that rather than to keep trying to observe a cap that cannot bind. If instead you ever
see a numeric cap, that's the interesting case and worth capturing verbatim.

Do this one first because it costs one grow-up and it determines whether (c) is worth pursuing.

---

## The tests

### (a) Genuine Child→Adult transition fires exactly once, at the real moment

**Setup.** A colony with a 12-year-old. Load, unpause a few seconds (see above), then age to 13.

**Expect.** Exactly one `Trait assignment (grow-up)` block and one passion trace for that pawn,
with `stage Adult` and `age 13` in the header.

**The regression this is really guarding.** Save, quit to menu, reload, unpause, and let it run.
Expect **zero** further grow-up traces for that now-adult pawn. This is the save-corruption bug
the `LastKnownStage` dictionary replaced a `HashSet` to fix — the old guard couldn't tell the
post-load resync from a real transition and re-shifted every adult's skills on every reload. Do
two or three reloads; skills and traits must be byte-identical each time.

**Also worth one reload:** the dictionary is deliberately session-only and cleared on
`Game.LoadGame` / `Game.InitNewGame`. After a reload the pawn's first firing re-establishes a
baseline of Adult, so the `previousStage == Adult` branch is what declines. Both guards should be
doing work here.

### (b) `alreadyCommittedPips` tops up rather than rerolls

**Setup.** Before aging up, **write down the child's existing passions** (skill + Minor/Major).
Growth moments at 7 and 10 will have granted some.

**Expect.** In the passion trace, `committed pips N` where N = (Minors × 1) + (Majors × 2) over
the list you wrote down. Every passion you noted is still present afterward and **not
downgraded** — Major must not become Minor.

**Why it should hold, so you know what a real failure looks like.** The grow-up path calls
`AssignPassions`, not `Apply`, and only `Apply` resets passions to `None`
(`PassionVarianceApplier.cs:24-27`). The level-ordered walk then only considers records where
`passion == Passion.None`. So a lost or downgraded pre-existing passion is a genuine bug, not a
tuning artifact.

**The interesting edge:** a child already above budget. `committed pips` exceeds the rolled
budget, so `budget` goes negative, the `while (budget >= 1f)` loop never runs, and the pawn gets
zero new passions while keeping all existing ones. Confirm that rather than assuming it — a
crash or a wipe here would be the bad outcome. Note the budget floor is deliberately skipped
whenever `alreadyCommittedPips > 0`, so nothing forces a passion on this path.

### (c) `TraitAgeCap` — only if the prediction above was refuted

If grow-up traces show a real numeric cap, verify the mapping: 0 below age 7, 1 at 7–9, 2 at
10–12, 3 from 13. Otherwise record that the cap can't bind and move on.

### (d) `applyVarianceOnGrowUp` off actually suppresses the path

Turn the checkbox off, repeat (a). Expect **no** grow-up trait or passion trace, and the pawn's
skills/traits/passions unchanged across the birthday. Cheap; it's the very first line of the
postfix (`GrowthUpPatch.cs:44`), so this is confirming the wiring, not the logic.

### (e) Children generate untouched

**Setup.** Trigger a raid or event that generates child pawns (a refugee-family quest is the
easiest reliable source), or generate some in dev mode.

**Expect.** **No** trait or passion trace at all for any non-Adult pawn — that's the
`HarmonyPatches.cs:27` early return. Their traits and skills should look vanilla.

**Note the asymmetry this creates**, and decide whether you're happy with it: a child generated
today gets zero variance, then receives a full adult-sized variance pass the moment they turn 13.
That's the intended design as written, but it means a colony that raises children sees the mod's
effect arrive in a jump rather than gradually. Flagging it as a design question, not a bug.

### (f) The growth-moment letter is unaffected

At ages 7 and 10 (i.e. *before* the transition test), the `ChoiceLetter_GrowthMoment` should offer
its normal passion/trait choices. The mod does not patch that path at all, so this is a
sanity check that nothing it does to traits upstream breaks the letter's options. Confirm the
choices appear and applying one works.

---

## While you're in there (HANDOVER items 2 and 4)

### `countProtectedTraits` = ON

Only the default off-path has ever been played. Turn it on and generate a raid containing
xenotype pawns (Yttakin are ideal — they carry forced traits).

**Expect** in the trace: `countProtectedTraits ON (target is total traits)`, and
`protected P, rolled R -> desired total max(P,R)` rather than `P + R`. The floor behaviour is the
thing to confirm: a pawn with 3 protected traits and a rolled target of 1 must end at **3**, not 1
— protected traits are never removed to hit a lower target. The trace's
`all remaining traits are protected` line fires exactly in that case.

### `applyToHostilePawns` = off

Turn it off, trigger a raid. Expect **zero** traces for raider pawns and a full trace for any
neutral/friendly pawn generated at the same time.

---

## What to send back

The `Player.log` for the session is enough — the traces are self-contained and greppable by
`[PawnVarianceMod] Trait assignment` / `Passion assignment`. Copy it to `temp/` as with previous
sessions (that folder is gitignored).

If something looks wrong, the trace line that looked wrong is far more useful than a description
of the symptom — that's the whole reason this diagnostic exists.
