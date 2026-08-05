# Race Overrides Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a third override axis — per-race profile overrides keyed on `ThingDef.defName` — so Humanoid Alien Races mods (Wolfein, Milira, Milian) can be tuned, alongside the existing faction and xenotype overrides.

**Architecture:** Race overrides mirror the existing faction/xenotype structures exactly: two dictionaries (`raceOverrides`, `racePriorities`), four flattened Scribe staging lists, one UI section. The three-way tie-break replaces the hand-written pairwise branches in `ValuesFor` with a rank table, because three sources would otherwise need eight explicit cases. Race resolution deliberately sits **outside** the `ModsConfig.BiotechActive` gate that currently wraps xenotype resolution — HAR races exist without Biotech.

**Tech Stack:** C# 9 / .NET Framework 4.7.2, RimWorld 1.6 `Assembly-CSharp`, Harmony 2. Build with `dotnet build` from `Source/`. No C# test project exists; logic-level tests use the established Python mirror harness in `zzz-Do-Not-Commit/`.

## Global Constraints

- **The mod is unreleased — there are no existing users and no backward-compatibility obligation.** Do not add migration shims, compatibility branches, or deprecation handling. If a saved config breaks, the fix is to reset it.
- **Keep the field name `factionOverridesTakePrecedence`.** Not for compat — it is simply still accurate and renaming it buys nothing. Its *label* changes; the field name does not.
- **Race overrides ship with zero defaults.** There is no `RestoreDefaultRaceOverrides` and no "Restore Default Race Overrides" button. The installed race list is mod-dependent and unknowable at compile time.
- **Race resolution must not be gated on `ModsConfig.BiotechActive`.** Wolfein Race does not depend on Biotech.
- **Tie-break order is a total order, not pairwise rules:**
  - `factionOverridesTakePrecedence == true` → `Faction > Race > Xenotype`
  - `factionOverridesTakePrecedence == false` → `Race > Xenotype > Faction`
- **Priority level always outranks source.** Source rank is consulted only when two candidates have the *same* `OverridePriority`.
- **The Add Race menu lists only humanlike races that at least one `PawnKindDef` spawns.** This yields exactly `Human`, `Wolfein_Race`, `Milira_Race`, `Milian_Race` on the user's install, and excludes the ~35 mechanoid `ThingDef_AlienRace` entries those same mods ship.
- **Subagents cannot launch RimWorld.** Every in-game verification step is deferred to a single owner-run pass after the last task. Implementers report `BUILD-ONLY` and list the observations they could not perform; that is not a task failure. The two gates subagents *can* run are binding: `dotnet build` must report `0 Error(s), 0 Warning(s)`, and the Python resolver test must exit 0.
- **There is no automated test harness for IMGUI code in this repo.** Pre-existing project condition, not a gap this plan introduces. Do not fabricate unit tests to satisfy a TDD rubric — the resolver test in Task 2 is the one genuine test here.
- Match surrounding code style: 4-space indent, `Widgets`/`Listing_Standard` idioms already in the file, comments only where behavior is non-obvious.
- Scratch/test files go in `zzz-Do-Not-Commit/` (already git-excluded). Never commit them.

## Reference: verified facts this plan rests on

These were confirmed by inspecting the installed mods; do not re-litigate them.

| Fact | Evidence |
|---|---|
| Wolfein and Milira/Milian define **zero** `XenotypeDef`s | grep of both workshop folders |
| They define `ThingDef_AlienRace` defs: `Wolfein_Race`, `Milira_Race`, `Milian_Race` | `About.xml` deps list HAR 2.0 |
| Each of those three is referenced by ≥1 `PawnKindDef` `<race>` | `PawnKinds_*.xml` in both mods |
| Both mods also ship many **mechanoid** alien races (`Wolfein_Mechanoid_*`, `Milian_Mechanoid_*`) that must not appear in the menu | same grep |
| The project builds clean today | `dotnet build` → 0 errors, 0 warnings |
| `build_output.log` in the repo root is **stale** (83 errors) — ignore it | superseded by the clean build |

## File Structure

| File | Change | Responsibility |
|---|---|---|
| `Source/PawnVarianceSettings.cs` | Modify | All four tasks touch this. Fields, persistence, resolution, UI. |
| `Source/ProfileEditorTab.cs` | Modify (Task 5) | Scrub race overrides pointing at a deleted profile. Already a `partial class PawnVarianceSettings`. |
| `zzz-Do-Not-Commit/test_race_resolution.py` | Create (Task 2) | Python mirror of the rank resolver. Not committed. |

`Source/DebugActions.cs` is deliberately **not** touched: it holds two bulk-simulation actions and has no per-pawn override readout to extend, so adding race visibility there would mean inventing a new debug surface outside this feature's scope.

`PawnVarianceSettings.cs` is already ~1100 lines and holds settings state + UI together. That is the established pattern in this codebase; do **not** restructure it as part of this work.

---

### Task 1: Race override state and persistence

Adds the data layer with no behavior wired up yet. Deliverable: race overrides survive a save/load round-trip and a settings import/export.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:41-54` (field declarations)
- Modify: `Source/PawnVarianceSettings.cs:132-148` (`PopulateDefaultOverrides`)
- Modify: `Source/PawnVarianceSettings.cs:327-348` (`ExposeData` save + Scribe calls)
- Modify: `Source/PawnVarianceSettings.cs:369-407` (`ExposeData` PostLoadInit)
- Modify: `Source/PawnVarianceSettings.cs:444-447` (`CopyFrom`)
- Modify: `Source/PawnVarianceSettings.cs:1025-1028` (`ResetToDefaults`)

**Interfaces:**
- Produces: `public Dictionary<string, string> raceOverrides` (key: `ThingDef.defName`, value: profile id) and `public Dictionary<string, OverridePriority> racePriorities`. Tasks 2, 3 and 4 all read and write these two dictionaries by name.

- [ ] **Step 1: Add the dictionary and staging-list fields**

In `Source/PawnVarianceSettings.cs`, replace lines 41-54 with:

```csharp
        public Dictionary<string, string> factionOverrides = new Dictionary<string, string>();
        public Dictionary<string, string> xenotypeOverrides = new Dictionary<string, string>();
        // Keyed on ThingDef.defName (Human, Wolfein_Race, ...). Ships empty on purpose: unlike
        // factions and xenotypes, the installed race list is mod-dependent, so there is nothing
        // sensible to seed.
        public Dictionary<string, string> raceOverrides = new Dictionary<string, string>();
        public Dictionary<string, OverridePriority> factionPriorities = new Dictionary<string, OverridePriority>();
        public Dictionary<string, OverridePriority> xenotypePriorities = new Dictionary<string, OverridePriority>();
        public Dictionary<string, OverridePriority> racePriorities = new Dictionary<string, OverridePriority>();

        private List<string> factionOverrideKeys = new List<string>();
        private List<string> factionOverrideValues = new List<string>();
        private List<string> xenotypeOverrideKeys = new List<string>();
        private List<string> xenotypeOverrideValues = new List<string>();
        private List<string> raceOverrideKeys = new List<string>();
        private List<string> raceOverrideValues = new List<string>();

        private List<string> factionPriorityKeys = new List<string>();
        private List<int> factionPriorityValues = new List<int>();
        private List<string> xenotypePriorityKeys = new List<string>();
        private List<int> xenotypePriorityValues = new List<int>();
        private List<string> racePriorityKeys = new List<string>();
        private List<int> racePriorityValues = new List<int>();
```

- [ ] **Step 2: Null-guard the new dictionaries in `PopulateDefaultOverrides`**

Replace lines 134-137 (the four null guards at the top of `PopulateDefaultOverrides`) with:

```csharp
            if (factionOverrides == null) factionOverrides = new Dictionary<string, string>();
            if (xenotypeOverrides == null) xenotypeOverrides = new Dictionary<string, string>();
            if (raceOverrides == null) raceOverrides = new Dictionary<string, string>();
            if (factionPriorities == null) factionPriorities = new Dictionary<string, OverridePriority>();
            if (xenotypePriorities == null) xenotypePriorities = new Dictionary<string, OverridePriority>();
            if (racePriorities == null) racePriorities = new Dictionary<string, OverridePriority>();
```

Do **not** add a `RestoreDefaultRaceOverrides()` call below. The two existing `RestoreDefault*` calls stay exactly as they are.

- [ ] **Step 3: Flatten the race dictionaries on save**

In `ExposeData`, inside the `if (Scribe.mode == LoadSaveMode.Saving)` block, after the `xenotypePriorityValues` assignment (line 337), add:

```csharp
                raceOverrideKeys = new List<string>(raceOverrides.Keys);
                raceOverrideValues = new List<string>(raceOverrides.Values);
                racePriorityKeys = new List<string>(racePriorities.Keys);
                racePriorityValues = racePriorities.Values.Select(v => (int)v).ToList();
```

- [ ] **Step 4: Add the Scribe calls**

After line 348 (`Scribe_Collections.Look(ref xenotypePriorityValues, ...)`), add:

```csharp
            Scribe_Collections.Look(ref raceOverrideKeys, "raceOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref raceOverrideValues, "raceOverrideValues", LookMode.Value);
            Scribe_Collections.Look(ref racePriorityKeys, "racePriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref racePriorityValues, "racePriorityValues", LookMode.Value);
```

- [ ] **Step 5: Rebuild the race dictionaries on load**

In the `PostLoadInit` block, after the `xenotypePriorities` rebuild loop (ends line 407) and **before** the `PopulateDefaultOverrides()` call on line 409, add:

```csharp
                raceOverrides = new Dictionary<string, string>();
                if (raceOverrideKeys != null && raceOverrideValues != null
                    && raceOverrideKeys.Count == raceOverrideValues.Count)
                {
                    for (int i = 0; i < raceOverrideKeys.Count; i++)
                    {
                        raceOverrides[raceOverrideKeys[i]] = raceOverrideValues[i];
                    }
                }

                racePriorities = new Dictionary<string, OverridePriority>();
                if (racePriorityKeys != null && racePriorityValues != null
                    && racePriorityKeys.Count == racePriorityValues.Count)
                {
                    for (int i = 0; i < racePriorityKeys.Count; i++)
                    {
                        racePriorities[racePriorityKeys[i]] = (OverridePriority)racePriorityValues[i];
                    }
                }
```

Note: a config saved before this feature existed has no `raceOverrideKeys` node. `Scribe_Collections.Look` leaves the list null, the `!= null` guard skips the loop, and `raceOverrides` ends up an empty dictionary — which is the correct default. No migration is needed.

- [ ] **Step 6: Carry race overrides through `CopyFrom`**

Replace lines 444-447 with:

```csharp
            factionOverrides = other.factionOverrides ?? new Dictionary<string, string>();
            xenotypeOverrides = other.xenotypeOverrides ?? new Dictionary<string, string>();
            raceOverrides = other.raceOverrides ?? new Dictionary<string, string>();
            factionPriorities = other.factionPriorities ?? new Dictionary<string, OverridePriority>();
            xenotypePriorities = other.xenotypePriorities ?? new Dictionary<string, OverridePriority>();
            racePriorities = other.racePriorities ?? new Dictionary<string, OverridePriority>();
```

`SettingsTransfer` round-trips through `ExposeData`, so export/import needs no separate change once Steps 3-5 are done.

- [ ] **Step 7: Clear race overrides in `ResetToDefaults`**

Replace lines 1025-1028 with:

```csharp
            factionOverrides.Clear();
            xenotypeOverrides.Clear();
            raceOverrides.Clear();
            factionPriorities.Clear();
            xenotypePriorities.Clear();
            racePriorities.Clear();
```

`PopulateDefaultOverrides(force: true)` on the next line reseeds faction and xenotype only, leaving race empty. That is intended.

- [ ] **Step 8: Build**

Run: `cd Source && dotnet build -v q -nologo`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 9: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "feat: add race override state and persistence

Race overrides are keyed on ThingDef.defName and ship empty -- the
installed race list is mod-dependent, so there is nothing to seed.
Configs saved before this feature load with an empty race map via the
existing null guards, so no migration is needed.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Rank-based three-way resolution

Replaces the pairwise branches in `ValuesFor` with a total-order rank comparison and wires race in. This is the only task with real logic; it gets a test.

**Files:**
- Create: `zzz-Do-Not-Commit/test_race_resolution.py`
- Modify: `Source/PawnVarianceSettings.cs:238-310` (`ValuesFor` and helpers)

**Interfaces:**
- Consumes: `raceOverrides`, `racePriorities` from Task 1.
- Produces: `private string GetRaceDefName(Pawn pawn)` returning `pawn.def.defName` or null. Task 4's debug action calls it.

- [ ] **Step 1: Write the failing test**

Create `zzz-Do-Not-Commit/test_race_resolution.py`. This mirrors the C# resolver the same way the existing `test_priority_resolution.py` does — the C# cannot be unit-tested directly because it needs the RimWorld runtime, so the rule table is validated here and transcribed faithfully.

```python
import sys

LOWEST, LOW, NORMAL, HIGH, HIGHEST = 0, 1, 2, 3, 4

FACTION, RACE, XENO = "faction", "race", "xeno"


def rank_of(source, faction_first):
    # Total order, deliberately. Pairwise rules could produce a cycle;
    # a single ranking cannot. Higher number wins ties.
    if faction_first:
        return {FACTION: 2, RACE: 1, XENO: 0}[source]
    return {RACE: 2, XENO: 1, FACTION: 0}[source]


def resolve(faction_def=None, race_def=None, xeno_def=None,
            faction_overrides=None, race_overrides=None, xenotype_overrides=None,
            faction_priorities=None, race_priorities=None, xenotype_priorities=None,
            faction_first=True, enable_overrides=True, biotech_active=True):
    faction_overrides = faction_overrides or {}
    race_overrides = race_overrides or {}
    xenotype_overrides = xenotype_overrides or {}
    faction_priorities = faction_priorities or {}
    race_priorities = race_priorities or {}
    xenotype_priorities = xenotype_priorities or {}

    if not enable_overrides:
        return None

    candidates = []
    if faction_def and faction_def in faction_overrides:
        candidates.append((faction_overrides[faction_def],
                           faction_priorities.get(faction_def, NORMAL), FACTION))
    if race_def and race_def in race_overrides:
        candidates.append((race_overrides[race_def],
                           race_priorities.get(race_def, NORMAL), RACE))
    # Biotech gate applies to the xenotype source ONLY.
    if biotech_active and xeno_def and xeno_def in xenotype_overrides:
        candidates.append((xenotype_overrides[xeno_def],
                           xenotype_priorities.get(xeno_def, NORMAL), XENO))

    if not candidates:
        return None

    best = max(candidates, key=lambda c: (c[1], rank_of(c[2], faction_first)))
    return best[0]


FAILURES = []


def check(name, actual, expected):
    if actual != expected:
        FAILURES.append("%s: expected %r, got %r" % (name, expected, actual))


RO = {"Wolfein_Race": "wolf"}
XO = {"Hussar": "hussar", "Baseliner": "base"}
FO = {"Empire": "empire"}

# Higher priority beats source rank, in both directions.
check("priority beats rank (xeno wins)",
      resolve(race_def="Wolfein_Race", xeno_def="Hussar",
              race_overrides=RO, xenotype_overrides=XO,
              race_priorities={"Wolfein_Race": NORMAL},
              xenotype_priorities={"Hussar": HIGH}),
      "hussar")
check("priority beats rank (race wins)",
      resolve(race_def="Wolfein_Race", xeno_def="Hussar",
              race_overrides=RO, xenotype_overrides=XO,
              race_priorities={"Wolfein_Race": HIGH},
              xenotype_priorities={"Hussar": NORMAL}),
      "wolf")

# Race beats xenotype on an exact tie, under BOTH toggle states.
check("tie: race beats xeno, toggle on",
      resolve(race_def="Wolfein_Race", xeno_def="Hussar",
              race_overrides=RO, xenotype_overrides=XO, faction_first=True),
      "wolf")
check("tie: race beats xeno, toggle off",
      resolve(race_def="Wolfein_Race", xeno_def="Hussar",
              race_overrides=RO, xenotype_overrides=XO, faction_first=False),
      "wolf")

# The faction toggle governs faction vs BOTH biological sources.
check("tie: faction beats race, toggle on",
      resolve(faction_def="Empire", race_def="Wolfein_Race",
              faction_overrides=FO, race_overrides=RO, faction_first=True),
      "empire")
check("tie: race beats faction, toggle off",
      resolve(faction_def="Empire", race_def="Wolfein_Race",
              faction_overrides=FO, race_overrides=RO, faction_first=False),
      "wolf")

# Three-way tie resolves to the top of the ranking, no cycle.
check("three-way tie, toggle on",
      resolve(faction_def="Empire", race_def="Wolfein_Race", xeno_def="Hussar",
              faction_overrides=FO, race_overrides=RO, xenotype_overrides=XO,
              faction_first=True),
      "empire")
check("three-way tie, toggle off",
      resolve(faction_def="Empire", race_def="Wolfein_Race", xeno_def="Hussar",
              faction_overrides=FO, race_overrides=RO, xenotype_overrides=XO,
              faction_first=False),
      "wolf")

# Race survives with Biotech off; xenotype does not.
check("race works without Biotech",
      resolve(race_def="Wolfein_Race", xeno_def="Hussar",
              race_overrides=RO, xenotype_overrides=XO, biotech_active=False),
      "wolf")
check("xeno suppressed without Biotech",
      resolve(xeno_def="Hussar", xenotype_overrides=XO, biotech_active=False),
      None)

# Shipped defaults: Sanguophage(HIGHEST)/Hussar(HIGH) outrank a Normal race entry.
check("shipped Hussar default outranks race",
      resolve(race_def="Milira_Race", xeno_def="Hussar",
              race_overrides={"Milira_Race": "milira"}, xenotype_overrides=XO,
              xenotype_priorities={"Hussar": HIGH}),
      "hussar")

# A Baseliner override at Normal loses to a race entry at Normal.
check("race beats Baseliner tie",
      resolve(race_def="Wolfein_Race", xeno_def="Baseliner",
              race_overrides=RO, xenotype_overrides=XO),
      "wolf")

# No match at all falls through.
check("no override matches",
      resolve(race_def="Human", race_overrides=RO), None)

# Disabled master switch short-circuits.
check("overrides disabled",
      resolve(race_def="Wolfein_Race", race_overrides=RO, enable_overrides=False),
      None)

if FAILURES:
    print("FAIL (%d)" % len(FAILURES))
    for f in FAILURES:
        print("  " + f)
    sys.exit(1)
print("PASS: all 14 resolution cases")
```

- [ ] **Step 2: Run the test to verify the rule table is self-consistent**

Run: `python zzz-Do-Not-Commit/test_race_resolution.py`
Expected: `PASS: all 14 resolution cases`

If it fails, the rule table is wrong — fix the table before writing any C#. Do not adjust an assertion to match the code.

- [ ] **Step 3: Add the race lookup helper**

In `Source/PawnVarianceSettings.cs`, immediately after `GetXenotypeDefName` (which ends at line 310), add:

```csharp
        private string GetRaceDefName(Pawn pawn)
        {
            // pawn.def is the species ThingDef -- Human, or Wolfein_Race / Milira_Race for HAR
            // races. Deliberately NOT behind the Biotech check: HAR races exist without Biotech.
            return pawn?.def?.defName;
        }
```

- [ ] **Step 4: Add the source rank helper**

Directly below `GetRaceDefName`, add:

```csharp
        // The three override sources, ranked. A total order rather than pairwise rules: pairwise
        // comparisons across three sources can produce a cycle (faction > race > xeno > faction)
        // with no winner, and a single ranking cannot. Higher rank wins an equal-priority tie.
        private enum OverrideSource { Faction, Race, Xenotype }

        private int RankOf(OverrideSource source)
        {
            if (factionOverridesTakePrecedence)
            {
                // Faction > Race > Xenotype
                if (source == OverrideSource.Faction) return 2;
                if (source == OverrideSource.Race) return 1;
                return 0;
            }
            // Race > Xenotype > Faction
            if (source == OverrideSource.Race) return 2;
            if (source == OverrideSource.Xenotype) return 1;
            return 0;
        }
```

- [ ] **Step 5: Replace the resolution body**

Replace lines 242-286 — the whole `if (enableOverrides) { ... }` block in `ValuesFor`, from `if (enableOverrides)` down to and including the closing brace that precedes `Faction fHostile = pawn.Faction;` — with:

```csharp
            if (enableOverrides)
            {
                Faction faction = pawn.Faction;
                if (faction == null && request.HasValue)
                    faction = request.Value.Faction;
                if (faction == null && pawn.kindDef?.defaultFactionDef != null && Find.FactionManager != null)
                    faction = Find.FactionManager.FirstFactionOfDef(pawn.kindDef.defaultFactionDef);

                string bestProfileId = null;
                OverridePriority bestPrio = OverridePriority.Lowest;
                int bestRank = -1;

                void Consider(string profileId, OverridePriority prio, OverrideSource source)
                {
                    int rank = RankOf(source);
                    if (bestProfileId == null || prio > bestPrio || (prio == bestPrio && rank > bestRank))
                    {
                        bestProfileId = profileId;
                        bestPrio = prio;
                        bestRank = rank;
                    }
                }

                if (faction?.def != null
                    && factionOverrides.TryGetValue(faction.def.defName, out var factionProfileId))
                {
                    OverridePriority prio = OverridePriority.Normal;
                    if (factionPriorities.TryGetValue(faction.def.defName, out var fp)) prio = fp;
                    Consider(factionProfileId, prio, OverrideSource.Faction);
                }

                string raceDef = GetRaceDefName(pawn);
                if (raceDef != null && raceOverrides.TryGetValue(raceDef, out var raceProfileId))
                {
                    OverridePriority prio = OverridePriority.Normal;
                    if (racePriorities.TryGetValue(raceDef, out var rp)) prio = rp;
                    Consider(raceProfileId, prio, OverrideSource.Race);
                }

                if (ModsConfig.BiotechActive)
                {
                    string xenoDef = GetXenotypeDefName(pawn, request);
                    if (xenoDef != null && xenotypeOverrides.TryGetValue(xenoDef, out var xenoProfileId))
                    {
                        OverridePriority prio = OverridePriority.Normal;
                        if (xenotypePriorities.TryGetValue(xenoDef, out var xp)) prio = xp;
                        Consider(xenoProfileId, prio, OverrideSource.Xenotype);
                    }
                }

                if (bestProfileId != null) return Resolve(bestProfileId);
            }
```

Two things to be careful about. `bestPrio` starts at `Lowest` rather than a sentinel, so the `bestProfileId == null` clause in `Consider` is what admits the first candidate — an override explicitly set to `Lowest` must still win when it is the only match. And the `ModsConfig.BiotechActive` check now wraps *only* the xenotype block, which is the whole point of the change.

- [ ] **Step 6: Build**

Run: `cd Source && dotnet build -v q -nologo`
Expected: `Build succeeded.` with `0 Error(s)`.

If the local function `Consider` trips a C# version error, note the project is on `LangVersion 9.0` where local functions are fully supported — a failure here means the block was pasted inside the wrong scope.

- [ ] **Step 7: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "feat: resolve overrides by source rank and add the race axis

Three sources cannot be compared pairwise without risking a cycle, so
ValuesFor now ranks candidates on a total order: faction > race > xeno
when the precedence toggle is on, race > xeno > faction when it is off.
Priority still outranks source; rank only breaks exact ties.

The Biotech gate now wraps the xenotype lookup alone. It previously
wrapped the whole biological branch, which would have silently disabled
race overrides for anyone without Biotech -- Wolfein Race does not
depend on it.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Extract the shared override-row renderer

**Pure refactor — no behavior change.** The faction and xenotype sections each carry a near-identical ~55-line row-drawing loop. Adding a third copy for races was rejected by the owner in favour of extracting one renderer. Doing this *before* the race section exists means the new section is written against the helper rather than being a third copy that later needs untangling.

This task must not change what any pixel renders. If you find yourself improving the row layout while you are in there, stop — that is a separate change.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:620-662` (faction row loop + removal)
- Modify: `Source/PawnVarianceSettings.cs:734-776` (xenotype row loop + removal)
- Create: `DrawOverrideRows` in `Source/PawnVarianceSettings.cs`, directly after `OverrideColumnHeaders`

**Interfaces:**
- Consumes: existing `ProfileMenu`, `PriorityMenu`, `LabelFor` helpers. `using System;` is already present at line 1, so `Func<>` needs no new import.
- Produces: `private void DrawOverrideRows(Listing_Standard listing, Dictionary<string, string> overrides, Dictionary<string, OverridePriority> priorities, Func<string, string> defLabelFor)`. Task 4's race section calls it.

- [ ] **Step 1: Add the shared renderer**

Insert directly after `OverrideColumnHeaders` ends (line 607), before `DrawFactionOverridesSection`:

```csharp
        // The row body shared by all three override sections. Geometry is the single source of
        // truth for the 0.35 / 0.28 / 0.20 / 0.14 columns -- OverrideColumnHeaders above mirrors
        // these fractions and must move with them.
        //
        // defLabelFor maps a stored defName to its display label. It is a delegate rather than a
        // generic type parameter because each section looks its key up in a different
        // DefDatabase, and all three fall back to the raw defName when the def is missing so a
        // row whose mod was uninstalled stays visible and removable.
        private void DrawOverrideRows(
            Listing_Standard listing,
            Dictionary<string, string> overrides,
            Dictionary<string, OverridePriority> priorities,
            Func<string, string> defLabelFor)
        {
            string toRemove = null;
            var keys = new List<string>(overrides.Keys);
            foreach (var key in keys)
            {
                var currentProfile = overrides[key];
                OverridePriority currentPrio = OverridePriority.Normal;
                if (priorities.TryGetValue(key, out var p))
                    currentPrio = p;

                string label = defLabelFor(key);

                Rect rowRect = listing.GetRect(30f);
                Rect labelRect = new Rect(rowRect.x, rowRect.y, rowRect.width * 0.35f, rowRect.height);
                Rect buttonRect = new Rect(rowRect.x + rowRect.width * 0.36f, rowRect.y, rowRect.width * 0.28f, rowRect.height);
                Rect prioRect = new Rect(rowRect.x + rowRect.width * 0.65f, rowRect.y, rowRect.width * 0.20f, rowRect.height);
                Rect removeRect = new Rect(rowRect.x + rowRect.width * 0.86f, rowRect.y, rowRect.width * 0.14f, rowRect.height);

                Text.Anchor = TextAnchor.MiddleLeft;
                Widgets.Label(labelRect, label);
                Text.Anchor = TextAnchor.UpperLeft;

                if (Widgets.ButtonText(buttonRect, LabelFor(currentProfile)))
                {
                    string k = key;
                    ProfileMenu(id => overrides[k] = id);
                }
                if (Widgets.ButtonText(prioRect, currentPrio.ToString()))
                {
                    string k = key;
                    PriorityMenu(pr => priorities[k] = pr);
                }
                if (Widgets.ButtonText(removeRect, "Remove"))
                {
                    toRemove = key;
                }
                listing.Gap(4f);
            }
            if (toRemove != null)
            {
                overrides.Remove(toRemove);
                priorities.Remove(toRemove);
            }
        }
```

Note the lambda parameter in the `PriorityMenu` call is `pr`, not `p` — `p` is already bound by the `TryGetValue(key, out var p)` above it in the same scope, which the original per-section code avoided only by luck of scoping.

- [ ] **Step 2: Point the faction section at the renderer**

In `DrawFactionOverridesSection`, replace the entire `else` block body — everything from `OverrideColumnHeaders(listing, "Faction");` (line 619) through the closing `}` of the `if (toRemove != null)` block (line 662) — with:

```csharp
                OverrideColumnHeaders(listing, "Faction");
                DrawOverrideRows(listing, factionOverrides, factionPriorities,
                    key => DefDatabase<FactionDef>.GetNamedSilentFail(key)?.LabelCap.ToString() ?? key);
```

- [ ] **Step 3: Point the xenotype section at the renderer**

In `DrawXenotypeOverridesSection`, replace the equivalent block — `OverrideColumnHeaders(listing, "Xenotype");` (line 733) through the closing `}` of its `if (toRemove != null)` block (line 776) — with:

```csharp
                OverrideColumnHeaders(listing, "Xenotype");
                DrawOverrideRows(listing, xenotypeOverrides, xenotypePriorities,
                    key => DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap.ToString() ?? key);
```

Leave both sections' `Section(...)` header, empty-state `Caption(...)`, `+ Add ...` button and action-strip buttons exactly as they are. Only the row loop moves.

- [ ] **Step 4: Verify the refactor is behavior-preserving**

Run: `cd Source && dotnet build -v q -nologo`
Expected: `Build succeeded.` with `0 Error(s)` and `0 Warning(s)`.

Then confirm by reading the diff that the extracted body is byte-equivalent to what it replaced, modulo the three intended substitutions: `factionOverrides`/`xenotypeOverrides` → `overrides`, `factionPriorities`/`xenotypePriorities` → `priorities`, and the inlined `DefDatabase<T>` lookup → `defLabelFor(key)`.

Run: `git diff --stat`
Expected: `Source/PawnVarianceSettings.cs` shows roughly 60 insertions and 85 deletions — a net reduction. An insertion-heavy diff means the old loops were not actually removed.

This is a `BUILD-ONLY` task: report that the visual confirmation (both existing sections still render identically) is deferred to the owner's in-game pass.

- [ ] **Step 5: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "refactor: extract the shared override-row renderer

The faction and xenotype sections carried a near-identical 55-line row
loop. Races would have made a third copy, so the body is now one
DrawOverrideRows taking the two dictionaries and a label resolver -- a
delegate rather than a generic, because each section resolves its key
against a different DefDatabase.

No behavior change: the row geometry, the profile and priority menus and
the deferred-removal pattern are all byte-equivalent to what they
replaced.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Race Overrides UI section

Adds the settings section, the filtered Add menu, and updates the now-inaccurate labels. Deliverable: the user can add Wolfein and Milira from the menu and see them persist.

**Files:**
- Modify: `Source/PawnVarianceSettings.cs:546-572` (master checkbox, precedence toggle, section calls)
- Modify: `Source/PawnVarianceSettings.cs:599-604` (priority tooltip)
- Create: `DrawRaceOverridesSection` and `SelectableRaces` in `Source/PawnVarianceSettings.cs`, after `DrawXenotypeOverridesSection`

**Interfaces:**
- Consumes: `raceOverrides` / `racePriorities` (Task 1); `DrawOverrideRows` (Task 3); existing `OverrideColumnHeaders`, `Section`, `Caption` helpers.
- Produces: `private void DrawRaceOverridesSection(Listing_Standard listing)`.

- [ ] **Step 1: Update the two checkbox labels**

Replace lines 546-563 with:

```csharp
            listing.CheckboxLabeled(
                "Enable Faction, Race & Xenotype Overrides",
                ref enableOverrides,
                "When enabled, specific faction, race and xenotype profiles take precedence over Hostile and General profiles.");

            listing.Gap(4f);

            bool wasEnabled = GUI.enabled;
            if (!enableOverrides)
            {
                GUI.enabled = false;
                Caption(listing, "Enable the checkbox above to configure per-faction, per-race and per-xenotype profiles.");
            }

            // Field name is deliberately unchanged -- it is Scribed as
            // "factionOverridesTakePrecedence" and renaming it would orphan every saved config.
            listing.CheckboxLabeled(
                "Faction Overrides Take Priority Over Race & Xenotype Overrides",
                ref factionOverridesTakePrecedence,
                "When checked, if a pawn matches a Faction override and also a Race or Xenotype override at the same priority (e.g. an Empire Neanderthal), the Faction override is used. If unchecked, Race and Xenotype overrides take priority.\n\nRace always beats Xenotype at equal priority, regardless of this setting.");
```

- [ ] **Step 2: Call the new section**

Replace lines 567-572 (the two section calls) with:

```csharp
            DrawFactionOverridesSection(listing);

            // Not behind a Biotech check -- HAR races exist without it.
            DrawRaceOverridesSection(listing);

            if (ModsConfig.BiotechActive)
            {
                DrawXenotypeOverridesSection(listing);
            }
```

- [ ] **Step 3: Update the priority tooltip**

Replace the tooltip text at lines 599-604 with:

```csharp
            TooltipHandler.TipRegion(c3,
                "Every override defaults to Normal. Higher priority levels take precedence over "
                + "lower ones.\n\n"
                + "At equal priority the order is Faction, then Race, then Xenotype -- or Race, "
                + "Xenotype, then Faction if the faction-precedence toggle above is off.\n\n"
                + "Factions, races and xenotypes not listed here have no override and fall back to "
                + "the hostile or colony profile.");
```

- [ ] **Step 4: Add the race section**

Insert this method immediately after `DrawXenotypeOverridesSection` ends. Rows come from Task 3's shared renderer. Two deliberate differences from the other two sections: the empty-state caption explains *why* it is empty, and there is one full-width delete button instead of a delete/restore pair, because race overrides have no defaults to restore.

```csharp
        private void DrawRaceOverridesSection(Listing_Standard listing)
        {
            Section(listing, "Race Overrides");

            if (raceOverrides.Count == 0)
            {
                Caption(listing, "No race overrides configured. Race overrides ship empty because the available races depend on which race mods are installed.");
            }
            else
            {
                OverrideColumnHeaders(listing, "Race");
                DrawOverrideRows(listing, raceOverrides, racePriorities,
                    key => DefDatabase<ThingDef>.GetNamedSilentFail(key)?.LabelCap.ToString() ?? key);
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Race Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var raceDef in SelectableRaces())
                {
                    if (!raceOverrides.ContainsKey(raceDef.defName))
                    {
                        var rDef = raceDef;
                        options.Add(new FloatMenuOption(rDef.LabelCap, () =>
                        {
                            raceOverrides[rDef.defName] = VarianceProfiles.DistinctId;
                            racePriorities[rDef.defName] = OverridePriority.Normal;
                        }));
                    }
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No remaining races available", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = oldColor;

            listing.Gap(4f);
            Rect raceActionRow = listing.GetRect(28f);

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(raceActionRow, "Delete All Race Overrides"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to delete all race overrides? This will clear all custom race profile assignments.",
                    () =>
                    {
                        raceOverrides.Clear();
                        racePriorities.Clear();
                    },
                    destructive: true));
            }
            GUI.color = oldColor;
        }

        // Humanlike races that something actually spawns. Two filters, both load-bearing:
        // Humanlike drops the ~35 mechanoid ThingDef_AlienRace entries that Wolfein and Milira
        // ship alongside their playable races, and the PawnKindDef pass drops abstract or
        // unreferenced race defs. On a Wolfein + Milira install this yields exactly Human,
        // Wolfein_Race, Milira_Race and Milian_Race.
        private static IEnumerable<ThingDef> SelectableRaces()
        {
            var seen = new HashSet<ThingDef>();
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefs)
            {
                ThingDef race = kind.race;
                if (race?.race == null) continue;
                if (!race.race.Humanlike) continue;
                seen.Add(race);
            }
            return seen.OrderBy(d => d.LabelCap.ToString());
        }
```

- [ ] **Step 5: Build**

Run: `cd Source && dotnet build -v q -nologo`
Expected: `Build succeeded.` with `0 Error(s)`.

`System.Linq` and `System.Collections.Generic` are already imported in this file (`.Select`/`.ToList` are used in `ExposeData`), so `OrderBy` and `HashSet` need no new usings. If the build disagrees, add them rather than rewriting the method.

- [ ] **Step 6: Verify the menu contents in-game**

This is the acceptance check for the whole feature and cannot be automated — it needs the real def database with the race mods loaded.

1. Launch RimWorld with Humanoid Alien Races 2.0, Wolfein Race, and Milira Race active.
2. Options → Mod Settings → Pawn Variance → Overrides tab.
3. Click **+ Add Race Override**.

Expected: the list contains `Human`, `Wolfein`, `Milira` and `Milian` (labels, not defNames). Expected **absent**: every `*_Mechanoid_*` and `*_FloatUnit_*` entry, and all animals.

If mechanoid races appear, the `Humanlike` filter is wrong. If Milian is missing, check that `Milian_Race` is `Humanlike` in the mod's XML before changing the filter — it may legitimately be classified otherwise.

- [ ] **Step 7: Verify persistence**

Add a Wolfein override set to any profile at High priority. Close the settings window, quit to desktop, relaunch, and reopen the Overrides tab.

Expected: the Wolfein row is still present with the same profile and High priority.

- [ ] **Step 8: Commit**

```bash
git add Source/PawnVarianceSettings.cs
git commit -m "feat: add the Race Overrides settings section

The Add menu lists humanlike races referenced by at least one
PawnKindDef. Both filters matter: Wolfein and Milira each ship a dozen-
plus mechanoid alien races that would otherwise flood the list.

One delete button instead of the delete/restore pair the other sections
have -- race overrides have no defaults to restore.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Stale-reference cleanup

Small but leaves a real bug if skipped. Deliverable: deleting a profile cannot leave a dangling race override.

**Files:**
- Modify: `Source/ProfileEditorTab.cs:193-200`

`ProfileEditorTab.cs` declares `public partial class PawnVarianceSettings`, so `raceOverrides` and `racePriorities` are directly in scope — no qualification needed.

**Interfaces:**
- Consumes: `raceOverrides` / `racePriorities` (Task 1).

- [ ] **Step 1: Scrub race overrides on profile deletion**

In `Source/ProfileEditorTab.cs`, after the `staleXenotypes` block (ends line 200) and before `RefreshResolved();` on line 202, add:

```csharp
                        var staleRaces = new List<string>();
                        foreach (var kv in raceOverrides)
                            if (kv.Value == deletedId) staleRaces.Add(kv.Key);
                        foreach (var k in staleRaces)
                        {
                            raceOverrides.Remove(k);
                            racePriorities.Remove(k);
                        }
```

Without this, deleting a custom profile that a race override points at leaves the override holding a dead id — `LabelFor` would render the raw guid and `Resolve` would fall through to defaults.

- [ ] **Step 2: Build**

Run: `cd Source && dotnet build -v q -nologo`
Expected: `Build succeeded.` with `0 Error(s)`.

- [ ] **Step 3: Verify the scrub**

In-game: create a custom profile, assign it to a race override, then delete that custom profile from the Profile Editor tab.

Expected: the race override row disappears along with it. Expected failure mode if Step 1 was skipped: a row remains showing a guid-like label.

- [ ] **Step 4: Commit**

```bash
git add Source/ProfileEditorTab.cs
git commit -m "fix: scrub race overrides when their profile is deleted

Mirrors the existing faction and xenotype passes. Without it, deleting a
custom profile leaves race overrides pointing at a dead id, which renders
as a raw guid and silently falls through to defaults.

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

## Verification checklist

Run before calling the feature done:

- [ ] `cd Source && dotnet build -v q -nologo` → 0 errors, 0 warnings
- [ ] `python zzz-Do-Not-Commit/test_race_resolution.py` → PASS, 14 cases
- [ ] **+ Add Race Override** lists Human / Wolfein / Milira / Milian and no mechanoids
- [ ] A race override survives a full game restart
- [ ] With Biotech disabled, the Race Overrides section still renders and still applies (the Xenotype section correctly disappears)
- [ ] Deleting a custom profile removes race overrides that pointed at it
- [ ] Settings export → reset → import restores race overrides
- [ ] `git status` shows nothing from `zzz-Do-Not-Commit/` staged

## Known gaps, deliberately out of scope

- **No race+xenotype compound keys.** A "Milira Hussar specifically" rule is not expressible; you get whichever of the two wins on priority. Compound keys mean a combinatorial UI and were rejected during design.
- **`Human` is selectable and matches almost every pawn.** That is intentional — it is the only way to express "vanilla humans specifically, as distinct from alien races" — but a Human override at High priority will shadow most faction overrides. The tooltip does not warn about this.
- **No migration handling.** The mod is unreleased; a config saved before this feature simply loads with an empty race map, which is the desired default anyway.
