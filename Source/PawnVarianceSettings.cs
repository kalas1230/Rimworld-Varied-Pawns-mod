using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public enum OverridePriority
    {
        Lowest = 0,
        Low = 1,
        Normal = 2,
        High = 3,
        Highest = 4
    }

    public partial class PawnVarianceSettings : ModSettings
    {
        // Housekeeping preferences: deliberately outside the profile system, so switching profiles
        // never silently re-enables logging or changes whether raiders get variance.
        public bool applyToHostilePawns = true;
        public bool applyVarianceToChildren = false;
        public bool verboseLogging = false;

        public List<CustomProfile> customProfiles = new List<CustomProfile>();
        public string activeProfileId = VarianceProfiles.FaithfulId;
        public string hostileProfileId = VarianceProfiles.DistinctId;

        // Which profile the Profile Editor tab is LOOKING AT. Deliberately separate from
        // activeProfileId and deliberately NOT Scribed: this is a view cursor, not a setting.
        // Sharing one field meant that cycling the editor's picker to compare presets silently
        // reassigned the colony's active profile out from under the player.
        private string editorProfileId;
        private VarianceProfileValues editingValues;

        public bool enableOverrides = true;
        public bool factionOverridesTakePrecedence = true;
        public bool hasInitializedDefaultOverrides = false;
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

        // Resolved values the appliers actually read. Two live sets now, not one, which is why the
        // Beta cache moved onto VarianceProfileValues — a shared cache would hand one profile's
        // quality shape to the other's rolls.
        public VarianceProfileValues Active { get; private set; }
        public VarianceProfileValues Hostile { get; private set; }

        private Vector2 scrollPosition = Vector2.zero;

        private enum SettingsTab { General, ProfileEditor, Overrides }
        private SettingsTab currentTab = SettingsTab.General;
        private Vector2 generalScrollPos = Vector2.zero;
        private Vector2 profileEditorScrollPos = Vector2.zero;
        private Vector2 overridesScrollPos = Vector2.zero;

        private float generalViewHeight = 800f;
        private float profileEditorViewHeight = 2000f;
        private float overridesViewHeight = 1600f;

        public string EditorProfileId
        {
            get
            {
                // Opens on whatever the colony is using, then diverges freely.
                if (string.IsNullOrEmpty(editorProfileId))
                {
                    editorProfileId = activeProfileId;
                    editingValues = null;
                }
                // Self-heals if the id was left dangling by a reset/import that wiped
                // customProfiles out from under it (see ResetToDefaults/CopyFrom).
                else if (VarianceProfiles.GetPresetById(editorProfileId) == null
                    && GetCustomProfile(editorProfileId) == null)
                {
                    editorProfileId = activeProfileId;
                    editingValues = null;
                }
                return editorProfileId;
            }
        }

        // Resolved values the Profile Editor edits. Cached rather than resolved per frame:
        // Resolve() hands back a fresh MakeValues() for presets, so a per-frame call would
        // allocate every frame and discard the Beta cache on VarianceProfileValues each time.
        public VarianceProfileValues Editing
        {
            get
            {
                // Touch the cursor first: its getter is what revalidates a dangling id and drops
                // the cache. Skipping it when editingValues is non-null would hand back the stale
                // object the revalidation exists to prevent.
                _ = EditorProfileId;
                if (editingValues == null) RefreshEditor();
                return editingValues;
            }
        }

        public bool EditingCustom => GetCustomProfile(EditorProfileId) != null;

        public void SetEditorProfile(string id)
        {
            editorProfileId = id;
            RefreshEditor();
        }

        public void RefreshEditor()
        {
            editingValues = Resolve(EditorProfileId);
            editingValues.profileLabel = LabelFor(EditorProfileId);
        }

        public PawnVarianceSettings()
        {
            PopulateDefaultOverrides();
            RefreshResolved();
        }

        public void PopulateDefaultOverrides(bool force = false)
        {
            if (factionOverrides == null) factionOverrides = new Dictionary<string, string>();
            if (xenotypeOverrides == null) xenotypeOverrides = new Dictionary<string, string>();
            if (raceOverrides == null) raceOverrides = new Dictionary<string, string>();
            if (factionPriorities == null) factionPriorities = new Dictionary<string, OverridePriority>();
            if (xenotypePriorities == null) xenotypePriorities = new Dictionary<string, OverridePriority>();
            if (racePriorities == null) racePriorities = new Dictionary<string, OverridePriority>();

            if (hasInitializedDefaultOverrides && !force)
            {
                return;
            }

            RestoreDefaultFactionOverrides();
            RestoreDefaultXenotypeOverrides();

            hasInitializedDefaultOverrides = true;
        }

        public void RestoreDefaultFactionOverrides()
        {
            if (factionOverrides == null) factionOverrides = new Dictionary<string, string>();
            if (factionPriorities == null) factionPriorities = new Dictionary<string, OverridePriority>();

            factionOverrides.Clear();
            factionPriorities.Clear();

            SetFactionDefault("Empire", VarianceProfiles.EliteId, OverridePriority.Highest);
            SetFactionDefault("Ancients", VarianceProfiles.SovereignId, OverridePriority.High);
            SetFactionDefault("AncientsHostile", VarianceProfiles.SovereignId, OverridePriority.High);
            SetFactionDefault("Pirate", VarianceProfiles.ScavengerId, OverridePriority.Normal);
            SetFactionDefault("PirateSavage", VarianceProfiles.ScavengerId, OverridePriority.Normal);
            SetFactionDefault("OutlanderCivil", VarianceProfiles.FaithfulId, OverridePriority.Low);
            SetFactionDefault("OutlanderRough", VarianceProfiles.FaithfulId, OverridePriority.Low);
            SetFactionDefault("TribeCivil", VarianceProfiles.DesperateId, OverridePriority.Low);
            SetFactionDefault("TribeRough", VarianceProfiles.DesperateId, OverridePriority.Low);
            SetFactionDefault("TribeSavage", VarianceProfiles.DesperateId, OverridePriority.Low);
        }

        public void RestoreDefaultXenotypeOverrides()
        {
            if (xenotypeOverrides == null) xenotypeOverrides = new Dictionary<string, string>();
            if (xenotypePriorities == null) xenotypePriorities = new Dictionary<string, OverridePriority>();

            xenotypeOverrides.Clear();
            xenotypePriorities.Clear();

            SetXenotypeDefault("Sanguophage", VarianceProfiles.SovereignId, OverridePriority.Highest);
            SetXenotypeDefault("Highmate", VarianceProfiles.EliteId, OverridePriority.High);
            SetXenotypeDefault("Genie", VarianceProfiles.SpecialistId, OverridePriority.High);
            SetXenotypeDefault("Hussar", VarianceProfiles.SpecialistId, OverridePriority.High);
            SetXenotypeDefault("Waster", VarianceProfiles.ScavengerId, OverridePriority.Normal);
            SetXenotypeDefault("Pigskin", VarianceProfiles.ScavengerId, OverridePriority.Normal);
            SetXenotypeDefault("Dirtmole", VarianceProfiles.SpecialistId, OverridePriority.Normal);
            SetXenotypeDefault("Neanderthal", VarianceProfiles.DistinctId, OverridePriority.Normal);
            SetXenotypeDefault("Yttakin", VarianceProfiles.DistinctId, OverridePriority.Normal);
            SetXenotypeDefault("Impid", VarianceProfiles.WildcardId, OverridePriority.Normal);
        }

        private void SetFactionDefault(string factionDef, string profileId, OverridePriority priority)
        {
            if (!factionOverrides.ContainsKey(factionDef)) factionOverrides[factionDef] = profileId;
            if (!factionPriorities.ContainsKey(factionDef)) factionPriorities[factionDef] = priority;
        }

        private void SetXenotypeDefault(string xenoDef, string profileId, OverridePriority priority)
        {
            if (!xenotypeOverrides.ContainsKey(xenoDef)) xenotypeOverrides[xenoDef] = profileId;
            if (!xenotypePriorities.ContainsKey(xenoDef)) xenotypePriorities[xenoDef] = priority;
        }

        public CustomProfile GetCustomProfile(string id)
        {
            if (string.IsNullOrEmpty(id) || customProfiles == null) return null;
            return customProfiles.Find(p => p.id == id);
        }

        public VarianceProfileValues Resolve(string id)
        {
            VarianceProfileValues vals = null;
            var preset = VarianceProfiles.GetPresetById(id);
            if (preset != null) vals = preset.MakeValues();
            else
            {
                var custom = GetCustomProfile(id);
                // Deliberately a live reference, not a clone: settings are applied live, with no
                // apply/cancel step, so an edit to a custom profile must reach the values pawn
                // generation reads. Presets are cloned above for the opposite reason — they are
                // static templates that must stay pristine. Do not "fix" this asymmetry.
                if (custom != null) vals = custom.values;
                // An id matching nothing at all. This used to fall through to customProfiles[0],
                // which generated pawns from an arbitrary unrelated profile with no error and no
                // visible symptom (the UI keeps showing the requested profile's name, because
                // LabelFor is asked about the REQUESTED id), and — since that was a live reference,
                // not a clone — the label write below then stamped the dead id onto that innocent
                // profile. A dangling id is a defect, not a preference: fall back to the pristine
                // default and say so. Known route in: an imported payload naming a custom_ id that
                // the payload does not itself carry (SettingsTransfer.CopyFrom does not validate).
                else
                {
                    Log.WarningOnce($"[PawnVarianceMod] Profile id '{id}' resolves to nothing; falling back to {VarianceProfiles.VanillaLike.label}. A settings import may reference a profile it did not include.", ("PawnVarianceMod.DanglingProfileId." + id).GetHashCode());
                    vals = VarianceProfiles.VanillaLike.MakeValues();
                }
            }

            if (vals != null) vals.profileLabel = LabelFor(id);
            return vals;
        }

        public string LabelFor(string id)
        {
            var preset = VarianceProfiles.GetPresetById(id);
            if (preset != null) return preset.label;

            var custom = GetCustomProfile(id);
            if (custom != null) return custom.name;

            return id ?? "?";
        }

        // The ONE answer to "what faction is this pawn?" — every caller must use it, and the reason
        // is not tidiness. pawn.Faction is observably null at GenerateNewPawnInternal postfix time
        // for some pawns (which is why the request fallback below exists at all — it would be dead
        // code otherwise). The hostile-pawn toggle used to test bare pawn.Faction while the override
        // lookup used the full chain, so a pawn whose faction was only knowable from the request
        // slipped past the "this mod never touches them" guard and then had that same hostile
        // faction's override applied to it — the exact inverse of the setting. Weakest test guarding
        // the strongest promise. Resolve identically everywhere or that gap comes back.
        public Faction EffectiveFactionOf(Pawn pawn, PawnGenerationRequest? request)
        {
            if (pawn == null) return null;

            Faction faction = pawn.Faction;
            if (faction == null && request.HasValue)
                faction = request.Value.Faction;
            if (faction == null && pawn.kindDef?.defaultFactionDef != null && Find.FactionManager != null)
                faction = Find.FactionManager.FirstFactionOfDef(pawn.kindDef.defaultFactionDef);
            return faction;
        }

        // Null-safe on BOTH sides. OfPlayerSilentFail returns null rather than logging when there is
        // no player faction yet — which is precisely why the project adopted it (to kill world-gen
        // log spam), so these call sites demonstrably run in that window. Three of the four original
        // sites passed that null straight into HostileTo; only one guarded it. One place now.
        public static bool IsHostileToPlayer(Faction faction)
        {
            Faction player = Faction.OfPlayerSilentFail;
            return faction != null && player != null && faction.HostileTo(player);
        }

        // True when the hostile toggle is off and this pawn is one it promises not to touch.
        public bool IsExcludedAsHostile(Pawn pawn, PawnGenerationRequest? request)
            => !applyToHostilePawns && IsHostileToPlayer(EffectiveFactionOf(pawn, request));

        public VarianceProfileValues ValuesFor(Pawn pawn) => ValuesFor(pawn, null);

        public VarianceProfileValues ValuesFor(Pawn pawn, PawnGenerationRequest? request)
        {
            if (pawn == null) return Active;

            if (enableOverrides)
            {
                Faction faction = EffectiveFactionOf(pawn, request);

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

            // Same resolution as the override branch above. This used to stop at request.Faction,
            // omitting the kindDef.defaultFactionDef step, so the two branches could disagree about
            // the same pawn: one could see a hostile faction the other could not.
            if (applyToHostilePawns && IsHostileToPlayer(EffectiveFactionOf(pawn, request)))
            {
                return Hostile;
            }

            return Active;
        }

        private string GetXenotypeDefName(Pawn pawn, PawnGenerationRequest? request = null)
        {
            if (pawn == null) return null;
            if (request.HasValue && request.Value.ForcedXenotype != null)
                return request.Value.ForcedXenotype.defName;
            if (pawn.genes?.Xenotype != null)
                return pawn.genes.Xenotype.defName;
            if (pawn.kindDef?.xenotypeSet != null && pawn.kindDef.xenotypeSet.Count > 0)
                return pawn.kindDef.xenotypeSet[0]?.xenotype?.defName;
            return null;
        }

        private string GetRaceDefName(Pawn pawn)
        {
            // pawn.def is the species ThingDef -- Human, or Wolfein_Race / Milira_Race for HAR
            // races. Deliberately NOT behind the Biotech check: HAR races exist without Biotech.
            return pawn?.def?.defName;
        }

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

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref customProfiles, "customProfiles", LookMode.Deep);
            Scribe_Values.Look(ref activeProfileId, "activeProfileId", VarianceProfiles.FaithfulId);
            Scribe_Values.Look(ref hostileProfileId, "hostileProfileId", VarianceProfiles.DistinctId);
            Scribe_Values.Look(ref applyToHostilePawns, "applyToHostilePawns", true);
            Scribe_Values.Look(ref applyVarianceToChildren, "applyVarianceToChildren", false);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);

            Scribe_Values.Look(ref enableOverrides, "enableOverrides", true);
            Scribe_Values.Look(ref factionOverridesTakePrecedence, "factionOverridesTakePrecedence", true);
            Scribe_Values.Look(ref hasInitializedDefaultOverrides, "hasInitializedDefaultOverrides", false);

            if (Scribe.mode == LoadSaveMode.Saving)
            {
                factionOverrideKeys = new List<string>(factionOverrides.Keys);
                factionOverrideValues = new List<string>(factionOverrides.Values);
                xenotypeOverrideKeys = new List<string>(xenotypeOverrides.Keys);
                xenotypeOverrideValues = new List<string>(xenotypeOverrides.Values);

                factionPriorityKeys = new List<string>(factionPriorities.Keys);
                factionPriorityValues = factionPriorities.Values.Select(v => (int)v).ToList();
                xenotypePriorityKeys = new List<string>(xenotypePriorities.Keys);
                xenotypePriorityValues = xenotypePriorities.Values.Select(v => (int)v).ToList();
                raceOverrideKeys = new List<string>(raceOverrides.Keys);
                raceOverrideValues = new List<string>(raceOverrides.Values);
                racePriorityKeys = new List<string>(racePriorities.Keys);
                racePriorityValues = racePriorities.Values.Select(v => (int)v).ToList();
            }

            Scribe_Collections.Look(ref factionOverrideKeys, "factionOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref factionOverrideValues, "factionOverrideValues", LookMode.Value);
            Scribe_Collections.Look(ref xenotypeOverrideKeys, "xenotypeOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref xenotypeOverrideValues, "xenotypeOverrideValues", LookMode.Value);

            Scribe_Collections.Look(ref factionPriorityKeys, "factionPriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref factionPriorityValues, "factionPriorityValues", LookMode.Value);
            Scribe_Collections.Look(ref xenotypePriorityKeys, "xenotypePriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref xenotypePriorityValues, "xenotypePriorityValues", LookMode.Value);
            Scribe_Collections.Look(ref raceOverrideKeys, "raceOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref raceOverrideValues, "raceOverrideValues", LookMode.Value);
            Scribe_Collections.Look(ref racePriorityKeys, "racePriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref racePriorityValues, "racePriorityValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customProfiles == null)
                {
                    customProfiles = new List<CustomProfile>();
                }

                foreach (var profile in customProfiles)
                {
                    profile.values?.ClampAndSwap();
                    if (string.IsNullOrWhiteSpace(profile.name))
                        profile.name = "Custom Profile";
                }

                if (string.IsNullOrEmpty(activeProfileId))
                    activeProfileId = VarianceProfiles.FaithfulId;
                if (string.IsNullOrEmpty(hostileProfileId))
                    hostileProfileId = VarianceProfiles.DistinctId;

                factionOverrides = new Dictionary<string, string>();
                if (PairLoadable("Faction overrides", factionOverrideKeys, factionOverrideValues))
                {
                    for (int i = 0; i < factionOverrideKeys.Count; i++)
                    {
                        factionOverrides[factionOverrideKeys[i]] = factionOverrideValues[i];
                    }
                }

                xenotypeOverrides = new Dictionary<string, string>();
                if (PairLoadable("Xenotype overrides", xenotypeOverrideKeys, xenotypeOverrideValues))
                {
                    for (int i = 0; i < xenotypeOverrideKeys.Count; i++)
                    {
                        xenotypeOverrides[xenotypeOverrideKeys[i]] = xenotypeOverrideValues[i];
                    }
                }

                factionPriorities = new Dictionary<string, OverridePriority>();
                if (PairLoadable("Faction priorities", factionPriorityKeys, factionPriorityValues))
                {
                    for (int i = 0; i < factionPriorityKeys.Count; i++)
                    {
                        factionPriorities[factionPriorityKeys[i]] = (OverridePriority)factionPriorityValues[i];
                    }
                }

                xenotypePriorities = new Dictionary<string, OverridePriority>();
                if (PairLoadable("Xenotype priorities", xenotypePriorityKeys, xenotypePriorityValues))
                {
                    for (int i = 0; i < xenotypePriorityKeys.Count; i++)
                    {
                        xenotypePriorities[xenotypePriorityKeys[i]] = (OverridePriority)xenotypePriorityValues[i];
                    }
                }

                raceOverrides = new Dictionary<string, string>();
                if (PairLoadable("Race overrides", raceOverrideKeys, raceOverrideValues))
                {
                    for (int i = 0; i < raceOverrideKeys.Count; i++)
                    {
                        raceOverrides[raceOverrideKeys[i]] = raceOverrideValues[i];
                    }
                }

                racePriorities = new Dictionary<string, OverridePriority>();
                if (PairLoadable("Race priorities", racePriorityKeys, racePriorityValues))
                {
                    for (int i = 0; i < racePriorityKeys.Count; i++)
                    {
                        racePriorities[racePriorityKeys[i]] = (OverridePriority)racePriorityValues[i];
                    }
                }

                PopulateDefaultOverrides();
                RefreshResolved();
            }
        }

        private void RefreshResolved()
        {
            Active = Resolve(activeProfileId);
            Active.profileLabel = LabelFor(activeProfileId);
            Hostile = Resolve(hostileProfileId);
            Hostile.profileLabel = LabelFor(hostileProfileId);
        }

        // Adopts every setting from a settings object loaded elsewhere (see SettingsTransfer).
        // Only the public state is copied: the private flattened staging lists are rebuilt from the
        // dictionaries by ExposeData's Saving branch, so copying them would only risk carrying
        // stale keys across.
        public void CopyFrom(PawnVarianceSettings other)
        {
            if (other == null) return;

            applyToHostilePawns = other.applyToHostilePawns;
            applyVarianceToChildren = other.applyVarianceToChildren;
            verboseLogging = other.verboseLogging;

            customProfiles = other.customProfiles ?? new List<CustomProfile>();
            activeProfileId = other.activeProfileId;
            hostileProfileId = other.hostileProfileId;

            enableOverrides = other.enableOverrides;
            factionOverridesTakePrecedence = other.factionOverridesTakePrecedence;
            // Travels with the payload on purpose: a config whose overrides were deliberately
            // emptied carries `true` and must stay empty rather than being repopulated.
            hasInitializedDefaultOverrides = other.hasInitializedDefaultOverrides;

            factionOverrides = other.factionOverrides ?? new Dictionary<string, string>();
            xenotypeOverrides = other.xenotypeOverrides ?? new Dictionary<string, string>();
            raceOverrides = other.raceOverrides ?? new Dictionary<string, string>();
            factionPriorities = other.factionPriorities ?? new Dictionary<string, OverridePriority>();
            xenotypePriorities = other.xenotypePriorities ?? new Dictionary<string, OverridePriority>();
            racePriorities = other.racePriorities ?? new Dictionary<string, OverridePriority>();

            if (string.IsNullOrEmpty(activeProfileId)) activeProfileId = VarianceProfiles.FaithfulId;
            if (string.IsNullOrEmpty(hostileProfileId)) hostileProfileId = VarianceProfiles.DistinctId;

            // customProfiles was just replaced wholesale; drop the editor's cached cursor and
            // values so the next access re-resolves against the new state instead of pointing
            // at a profile that no longer exists.
            editorProfileId = null;
            editingValues = null;

            MarkDirtyOnWrite();
        }

        // Whether a flattened key/value pair from the save is safe to rebuild a dictionary from.
        //
        // The count guard has to exist -- mismatched lists would pair the wrong key with the wrong
        // value, which is worse than dropping the axis. What was missing is the WARNING. A silent
        // drop presents to the player as "I never configured these", not "these were discarded",
        // and it is not recoverable: PopulateDefaultOverrides runs afterwards but re-seeds only the
        // default set, and early-returns entirely once hasInitializedDefaultOverrides is true,
        // which it will be for any existing save.
        //
        // null is the normal fresh-save case and is not warned about; only a genuine mismatch is.
        private static bool PairLoadable<T>(string axis, List<string> keys, List<T> values)
        {
            if (keys == null || values == null) return false;
            if (keys.Count == values.Count) return true;
            Log.Warning($"[PawnVarianceMod] {axis}: the saved settings hold {keys.Count} keys "
                        + $"against {values.Count} values. This axis has been DISCARDED rather "
                        + "than risk pairing the wrong key with the wrong value -- the overrides "
                        + "on it will need to be set up again.");
            return false;
        }

        public void MarkDirtyOnWrite()
        {
            if (customProfiles != null)
            {
                foreach (var profile in customProfiles)
                {
                    profile.values?.ClampAndSwap();
                    if (string.IsNullOrWhiteSpace(profile.name))
                        profile.name = "Custom Profile";
                }
            }
            RefreshResolved();
        }

        // Vertical rhythm constants
        private const float SectionGap = 14f;
        private const float ControlGap = 10f;
        private const float SliderLabelGap = 2f;

        public void DoWindowContents(Rect inRect)
        {
            var tabs = new List<TabRecord>
            {
                new TabRecord("General", () => currentTab = SettingsTab.General, currentTab == SettingsTab.General),
                new TabRecord("Profile Editor", () => currentTab = SettingsTab.ProfileEditor, currentTab == SettingsTab.ProfileEditor),
                new TabRecord("Overrides", () => currentTab = SettingsTab.Overrides, currentTab == SettingsTab.Overrides)
            };

            Rect tabRect = new Rect(inRect.x, inRect.y + 40f, inRect.width, inRect.height - 40f);
            TabDrawer.DrawTabs(tabRect, tabs);

            Rect contentRect = tabRect.ContractedBy(10f);

            switch (currentTab)
            {
                case SettingsTab.General:
                    DrawGeneralTab(contentRect);
                    break;
                case SettingsTab.ProfileEditor:
                    DrawProfileEditorTab(contentRect);
                    break;
                case SettingsTab.Overrides:
                    DrawOverridesTab(contentRect);
                    break;
            }
        }

        private void DrawGeneralTab(Rect outRect)
        {
            float viewHeight = Math.Max(generalViewHeight, 600f);
            var viewRect = new Rect(0f, 0f, outRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref generalScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            Text.Font = GameFont.Medium;
            listing.Label("Active Colony Profile");
            Text.Font = GameFont.Small;
            Caption(listing, "Default profile applied to player colonists and neutral pawns:");

            if (listing.ButtonText(LabelFor(activeProfileId)))
                ProfileMenu(id => { activeProfileId = id; RefreshResolved(); });

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            listing.Label(VarianceProfiles.DescriptionFor(activeProfileId));
            GUI.color = Color.white;

            // Any override beats this setting — that is the design, but without saying so the
            // General tab names a profile that may never apply to a single colonist. Observed
            // 2026-08-06: a Human race override at Normal silently supersedes it, because the
            // player faction has no override and the race one is then the only match.
            Caption(listing, "Overrides on a pawn's faction, race or xenotype take precedence over this.");

            listing.Gap(SectionGap);

            DrawGlobalSettings(listing);

            generalViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawOverridesTab(Rect outRect)
        {
            float viewHeight = Math.Max(overridesViewHeight, 1000f);
            var viewRect = new Rect(0f, 0f, outRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref overridesScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

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

            listing.Gap(SectionGap);

            DrawFactionOverridesSection(listing);

            if (ModsConfig.BiotechActive)
            {
                DrawXenotypeOverridesSection(listing);
            }

            // Last on purpose: race overrides exist mainly for race mods, so they should not sit
            // above xenotypes in the list. Not behind a Biotech check -- HAR races exist without it.
            DrawRaceOverridesSection(listing);

            GUI.enabled = wasEnabled;

            overridesViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }

        // Column captions for the two override lists. Geometry mirrors the row rects below
        // (0.35 / 0.28 / 0.20 / 0.14) -- if those move, move these with them.
        private static void OverrideColumnHeaders(Listing_Standard listing, string firstColumn)
        {
            Rect row = listing.GetRect(18f);
            Rect c1 = new Rect(row.x, row.y, row.width * 0.35f, row.height);
            Rect c2 = new Rect(row.x + row.width * 0.36f, row.y, row.width * 0.28f, row.height);
            Rect c3 = new Rect(row.x + row.width * 0.65f, row.y, row.width * 0.20f, row.height);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            Widgets.Label(c1, firstColumn);
            Widgets.Label(c2, "Profile");
            Widgets.Label(c3, "Priority");
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            // The fourth column is the Remove button and needs no caption.
            TooltipHandler.TipRegion(c3,
                "Every override defaults to Normal. Higher priority levels take precedence over "
                + "lower ones.\n\n"
                + "At equal priority the order is Faction, then Race, then Xenotype -- or Race, "
                + "Xenotype, then Faction if the faction-precedence toggle above is off.\n\n"
                + "Factions, races and xenotypes not listed here have no override and fall back to "
                + "the hostile or colony profile.");

            listing.Gap(2f);
        }

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

        private void DrawFactionOverridesSection(Listing_Standard listing)
        {
            Section(listing, "Faction Overrides");

            if (factionOverrides.Count == 0)
            {
                Caption(listing, "No faction overrides configured.");
            }
            else
            {
                OverrideColumnHeaders(listing, "Faction");
                DrawOverrideRows(listing, factionOverrides, factionPriorities,
                    key => FactionMenuDefs.LabelForKey(key));
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Faction Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var factionDef in FactionMenuDefs.Defs)
                {
                    if (!factionOverrides.ContainsKey(factionDef.defName))
                    {
                        var fDef = factionDef;
                        options.Add(new FloatMenuOption(FactionMenuDefs.LabelFor(fDef), () =>
                        {
                            factionOverrides[fDef.defName] = VarianceProfiles.DistinctId;
                            factionPriorities[fDef.defName] = OverridePriority.Normal;
                        }));
                    }
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No remaining factions available", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = oldColor;

            listing.Gap(4f);
            Rect factionActionRow = listing.GetRect(28f);
            float halfWF = (factionActionRow.width - 8f) / 2f;
            Rect delFactionRect = new Rect(factionActionRow.x, factionActionRow.y, halfWF, factionActionRow.height);
            Rect restoreFactionRect = new Rect(factionActionRow.x + halfWF + 8f, factionActionRow.y, halfWF, factionActionRow.height);

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(delFactionRect, "Delete All Faction Overrides"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to delete all faction overrides? This will clear all custom faction profile assignments.",
                    () =>
                    {
                        factionOverrides.Clear();
                        factionPriorities.Clear();
                    },
                    destructive: true));
            }

            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (Widgets.ButtonText(restoreFactionRect, "Restore Default Faction Overrides"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to restore default faction overrides? This will reset all faction profile overrides to their default assignments.",
                    () =>
                    {
                        RestoreDefaultFactionOverrides();
                    },
                    destructive: false));
            }
            GUI.color = oldColor;
        }

        private void DrawXenotypeOverridesSection(Listing_Standard listing)
        {
            Section(listing, "Xenotype Overrides");

            if (xenotypeOverrides.Count == 0)
            {
                Caption(listing, "No xenotype overrides configured.");
            }
            else
            {
                OverrideColumnHeaders(listing, "Xenotype");
                DrawOverrideRows(listing, xenotypeOverrides, xenotypePriorities,
                    key => XenotypeMenuDefs.LabelForKey(key));
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Xenotype Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var xenoDef in XenotypeMenuDefs.Defs)
                {
                    if (!xenotypeOverrides.ContainsKey(xenoDef.defName))
                    {
                        var xDef = xenoDef;
                        options.Add(new FloatMenuOption(XenotypeMenuDefs.LabelFor(xDef), () =>
                        {
                            xenotypeOverrides[xDef.defName] = VarianceProfiles.DistinctId;
                            xenotypePriorities[xDef.defName] = OverridePriority.Normal;
                        }));
                    }
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No remaining xenotypes available", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }
            GUI.color = oldColor;

            listing.Gap(4f);
            Rect xenoActionRow = listing.GetRect(28f);
            float halfWX = (xenoActionRow.width - 8f) / 2f;
            Rect delXenoRect = new Rect(xenoActionRow.x, xenoActionRow.y, halfWX, xenoActionRow.height);
            Rect restoreXenoRect = new Rect(xenoActionRow.x + halfWX + 8f, xenoActionRow.y, halfWX, xenoActionRow.height);

            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(delXenoRect, "Delete All Xenotype Overrides"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to delete all xenotype overrides? This will clear all custom xenotype profile assignments.",
                    () =>
                    {
                        xenotypeOverrides.Clear();
                        xenotypePriorities.Clear();
                    },
                    destructive: true));
            }

            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (Widgets.ButtonText(restoreXenoRect, "Restore Default Xenotype Overrides"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Are you sure you want to restore default xenotype overrides? This will reset all xenotype profile overrides to their default assignments.",
                    () =>
                    {
                        RestoreDefaultXenotypeOverrides();
                    },
                    destructive: false));
            }
            GUI.color = oldColor;
        }

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
                    key => RaceMenuDefs.LabelForKey(key));
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Race Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var raceDef in RaceMenuDefs.Defs)
                {
                    if (!raceOverrides.ContainsKey(raceDef.defName))
                    {
                        var rDef = raceDef;
                        options.Add(new FloatMenuOption(RaceMenuDefs.LabelFor(rDef), () =>
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

        // Def.LabelCap throws on a def whose <label> is missing or empty, and nothing stops a
        // third-party mod shipping one. All three override sections render defs they did not
        // author, so all three route their labels through here and fall back to the defName.
        internal static string LabelOf(Def d)
        {
            if (d == null) return null;
            return string.IsNullOrEmpty(d.label) ? d.defName : d.LabelCap.ToString();
        }

        // The def list behind one Add-override menu: sorted by label, with labels that collide
        // between mods disambiguated by defName.
        //
        // WHY SORTED. The faction and xenotype menus used to walk DefDatabase.AllDefs directly,
        // which is mod load order. That is fine against vanilla's ~31 concrete FactionDefs and
        // unusable against the ~500 a large workshop library carries (measured on a 1376-mod
        // install: 534 FactionDef tags, 35 of them Abstract="True"). Note the volume itself was
        // never the problem -- Verse.FloatMenu lays options into columns and scrolls once they
        // pass MaxScreenHeightPercent (0.9), so it does not clip or overflow. Finding one entry
        // in an unordered wall of 500 is the problem.
        //
        // WHY DISAMBIGUATED. Mods reuse vanilla-style faction names freely -- 202 of those 534
        // workshop defs inherit from OutlanderFactionBase alone -- so two menu rows reading
        // "Outlander union" is a realistic outcome, and picking the wrong one is invisible until
        // pawns generate. The race menu already disambiguated this way (CreepJoiner labels itself
        // "Human"); factions and xenotypes now match it, and the OVERRIDE ROWS use the same labels
        // as the menu so a row can be traced back to the entry that created it.
        //
        // WHY CACHED. These are rebuilt on every frame the settings window draws, and building one
        // sorts every def on the axis. DefDatabase does not change after load, so once per session
        // is enough and correct.
        private sealed class AddMenuDefs<T> where T : Def
        {
            internal readonly List<T> Defs;
            private readonly HashSet<string> ambiguous;

            internal AddMenuDefs(IEnumerable<T> source)
            {
                Defs = source.OrderBy(d => LabelOf(d)).ToList();
                ambiguous = Defs.GroupBy(d => LabelOf(d))
                                .Where(g => g.Count() > 1)
                                .Select(g => g.Key)
                                .ToHashSet();
            }

            // The label to show for a def, qualified by defName only when it would otherwise be
            // ambiguous. Unqualified is the common case and stays clean.
            internal string LabelFor(T d)
            {
                string label = LabelOf(d);
                return ambiguous.Contains(label) ? $"{label} ({d.defName})" : label;
            }

            // The same label, resolved from the stored override key. Falls back to the raw key for
            // an override whose def is no longer installed -- that row must still render and still
            // be removable.
            internal string LabelForKey(string defName)
            {
                T d = DefDatabase<T>.GetNamedSilentFail(defName);
                return d == null ? defName : LabelFor(d);
            }
        }

        private static AddMenuDefs<FactionDef> factionMenuDefs;
        private static AddMenuDefs<XenotypeDef> xenotypeMenuDefs;
        private static AddMenuDefs<ThingDef> raceMenuDefs;

        private static AddMenuDefs<FactionDef> FactionMenuDefs =>
            factionMenuDefs ?? (factionMenuDefs = new AddMenuDefs<FactionDef>(DefDatabase<FactionDef>.AllDefs));

        private static AddMenuDefs<XenotypeDef> XenotypeMenuDefs =>
            xenotypeMenuDefs ?? (xenotypeMenuDefs = new AddMenuDefs<XenotypeDef>(DefDatabase<XenotypeDef>.AllDefs));

        // Races come from SelectableRaces()'s PawnKindDef traversal, not from a DefDatabase sweep,
        // so this caches strictly more work than the other two: that traversal used to run once per
        // frame the Overrides tab was open, over every PawnKindDef installed.
        private static AddMenuDefs<ThingDef> RaceMenuDefs =>
            raceMenuDefs ?? (raceMenuDefs = new AddMenuDefs<ThingDef>(SelectableRaces()));

        // Drops every override pointing at a deleted profile, from BOTH of the axis's dictionaries.
        // Each override axis is two parallel maps keyed the same way; removing a key from one and
        // not the other leaves a priority with no override, which is the failure mode this exists
        // to make impossible. A fourth axis gets the scrub by calling this, not by remembering to
        // write nine more lines.
        //
        // internal, not private: the Delete button's lambda is unreachable from a debug action, so
        // scrub behaviour could only ever be tested against a copy of it. It can now be called.
        internal static void ScrubStaleOverrides(
            Dictionary<string, string> overrides,
            Dictionary<string, OverridePriority> priorities,
            string deletedId)
        {
            if (overrides == null) return;

            var stale = new List<string>();
            foreach (var kv in overrides)
                if (kv.Value == deletedId) stale.Add(kv.Key);

            foreach (var key in stale)
            {
                overrides.Remove(key);
                priorities?.Remove(key);
            }
        }

        // Humanlike races that something actually spawns. Two filters, both load-bearing:
        // Humanlike drops the ~35 mechanoid ThingDef_AlienRace entries that Wolfein and Milira
        // ship alongside their playable races, and the PawnKindDef pass drops abstract or
        // unreferenced race defs. Measured 2026-08-06 on a Wolfein + Milira + Anomaly install:
        // Human, CreepJoiner, Milira_Race, Wolfein_Race. Milian_Race is NOT in the list — its only
        // def is the abstract Milian_Base with zero concrete children, so no PawnKindDef spawns it
        // and the traversal drops it. CreepJoiner also labels itself "Human", which is why the two
        // call sites above disambiguate duplicate labels with the defName.
        // internal, not private: the "Dump Add-menu race list" debug action calls this directly so
        // the harness checks the list the menu actually builds rather than a copy of the filter.
        internal static IEnumerable<ThingDef> SelectableRaces()
        {
            var seen = new HashSet<ThingDef>();
            foreach (var kind in DefDatabase<PawnKindDef>.AllDefs)
            {
                ThingDef race = kind.race;
                if (race?.race == null) continue;
                if (!race.race.Humanlike) continue;
                seen.Add(race);
            }
            return seen.OrderBy(d => LabelOf(d));
        }

        private static void Section(Listing_Standard listing, string title)
        {
            listing.Gap(SectionGap);
            listing.GapLine(SectionGap);
            Text.Font = GameFont.Medium;
            listing.Label(title);
            Text.Font = GameFont.Small;
            listing.Gap(4f);
        }

        private static void Caption(Listing_Standard listing, string text)
        {
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            Text.Font = GameFont.Tiny;
            listing.Label(text);
            Text.Font = GameFont.Small;
            GUI.color = Color.white;
        }

        private static float LabeledSlider(Listing_Standard listing, string label, float value, float min, float max)
        {
            listing.Label(label);
            listing.Gap(SliderLabelGap);
            float result = listing.Slider(value, min, max);
            listing.Gap(ControlGap);
            return result;
        }

        private void PriorityMenu(Action<OverridePriority> onPick)
        {
            var options = new List<FloatMenuOption>();
            foreach (OverridePriority p in Enum.GetValues(typeof(OverridePriority)))
            {
                var captured = p;
                options.Add(new FloatMenuOption(captured.ToString(), () => onPick(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void ProfileMenu(Action<string> onPick)
        {
            var options = new List<FloatMenuOption>();
            if (customProfiles != null)
            {
                foreach (var custom in customProfiles)
                {
                    var captured = custom.id;
                    options.Add(new FloatMenuOption(custom.name, () => onPick(captured)));
                }
            }
            foreach (var preset in VarianceProfiles.Presets)
            {
                var captured = preset.stringId;
                options.Add(new FloatMenuOption(preset.label, () => onPick(captured)));
            }
            Find.WindowStack.Add(new FloatMenu(options));
        }

        // Ticks alone is not a uniqueness guarantee: DateTime.Now's nominal unit is 100ns but its
        // real resolution on Windows is commonly 1-15ms, so two creations inside one timer tick
        // collide. GetCustomProfile resolves by Find(p => p.id == id) -- FIRST match wins -- so the
        // loser stays visible and selectable in the menu while activeProfileId silently resolves to
        // the other one. Improbable from human clicking; the defect was the absent check, not the
        // odds. Suffixes on collision rather than looping on the clock, which could spin.
        private string NewCustomProfileId()
        {
            string baseId = "custom_" + DateTime.Now.Ticks;
            if (customProfiles == null) return baseId;
            string candidate = baseId;
            for (int suffix = 2; customProfiles.Any(p => p.id == candidate); suffix++)
                candidate = baseId + "_" + suffix;
            return candidate;
        }

        private void CreateNewCustomProfile()
        {
            string newId = NewCustomProfileId();
            string newName = "Custom " + (customProfiles.Count + 1);
            var profile = new CustomProfile(newId, newName, VarianceProfiles.VanillaLike.MakeValues());
            customProfiles.Add(profile);
            // Selects it in the editor only. The colony keeps whatever profile it was using.
            SetEditorProfile(newId);
        }

        private void DuplicateCurrentProfile()
        {
            string newId = NewCustomProfileId();
            string newName = LabelFor(EditorProfileId) + " Copy";
            var profile = new CustomProfile(newId, newName, Resolve(EditorProfileId).Clone());
            customProfiles.Add(profile);
            SetEditorProfile(newId);
        }

        private void DrawGlobalSettings(Listing_Standard listing)
        {
            Section(listing, "General");
            Caption(listing, "These apply to every profile and are not changed by switching profiles.");

            listing.CheckboxLabeled(
                "Apply to hostile-faction pawns",
                ref applyToHostilePawns,
                "When off, raiders and other hostile pawns are generated exactly as in vanilla and this mod never touches them. When on, they are generated from the profile you pick below.");

            if (applyToHostilePawns)
            {
                listing.Gap(ControlGap);
                Caption(listing, "Profile used for raiders and other hostiles:");
                Rect hostileRow = listing.GetRect(30f);
                if (Widgets.ButtonText(hostileRow, LabelFor(hostileProfileId)))
                    ProfileMenu(id => { hostileProfileId = id; RefreshResolved(); });
                TooltipHandler.TipRegion(hostileRow,
                    "Colonists are selected by the player, but raiders arrive directly. Using a "
                    + "separate hostile profile balances raider difficulty independently from your colony.");
                listing.Gap(ControlGap);
            }

            if (ModsConfig.BiotechActive)
                listing.CheckboxLabeled(
                    "Apply variance to children growing up",
                    ref applyVarianceToChildren,
                    "Applies trait and passion variance when a child turns 13. The mod waits for growth choices to resolve, then tops up traits and passions to match profile targets. Existing traits and passions are never removed.");
            // Gated on Prefs.DevMode, the same way the row above is gated on ModsConfig.BiotechActive.
            // The label said "(dev mode)" while the control was drawn for everyone, so the setting a
            // player reaches for BECAUSE something is going wrong was the one that turns a logged,
            // survivable error into a thrown one -- during world generation, a raid, or a starting
            // scenario. The rethrow itself is deliberate and disclosed, so it is kept; it is now
            // reachable only by someone who has dev mode on. HarmonyPatches re-checks Prefs.DevMode
            // at the throw site so an already-ticked setting cannot fire for a normal player.
            if (Prefs.DevMode)
                listing.CheckboxLabeled(
                    "Verbose logging (dev mode)",
                    ref verboseLogging,
                    "Rethrows exceptions instead of swallowing them, and logs a per-pawn breakdown of how traits and passions were assigned. Leave off for normal play.");

            DrawShareSettingsSection(listing);

            listing.Gap(SectionGap);
            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (listing.ButtonText("Reset All Settings"))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset all settings to defaults? All custom profiles, overrides, and options will be restored to defaults.",
                    () => ResetToDefaults(),
                    destructive: false));
            }
            GUI.color = Color.white;
        }

        private void DrawShareSettingsSection(Listing_Standard listing)
        {
            Section(listing, "Share Settings");

            Rect row = listing.GetRect(30f);
            float halfW = (row.width - 8f) / 2f;
            Rect exportRect = new Rect(row.x, row.y, halfW, row.height);
            Rect importRect = new Rect(row.x + halfW + 8f, row.y, halfW, row.height);

            TooltipHandler.TipRegion(exportRect,
                "Copies your whole configuration to the clipboard as text: every custom profile, "
                + "both override lists with their priorities, and the options above. Paste it "
                + "anywhere to share it, or import someone else's.");

            if (Widgets.ButtonText(exportRect, "Export to Clipboard"))
            {
                string payload = SettingsTransfer.Export(this);
                if (payload != null)
                {
                    SettingsTransfer.CopyToClipboard(payload);
                    Messages.Message("Varied Pawns settings copied to the clipboard.",
                        MessageTypeDefOf.TaskCompletion, false);
                }
                else
                {
                    Messages.Message("Could not export settings. See the log for details.",
                        MessageTypeDefOf.RejectInput, false);
                }
            }

            if (Widgets.ButtonText(importRect, "Import from Clipboard"))
            {
                // Replaces everything, so it asks first. There is no merge mode by design.
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Importing replaces ALL of your Varied Pawns settings: every custom profile, both override lists, and the general options.\n\nThis cannot be undone. Continue?",
                    ImportFromClipboard,
                    destructive: true));
            }
        }

        private void ImportFromClipboard()
        {
            string error;
            if (SettingsTransfer.Import(this, SettingsTransfer.ReadClipboard(), out error))
            {
                Write();
                Messages.Message("Varied Pawns settings imported.", MessageTypeDefOf.TaskCompletion, false);
            }
            else
            {
                Messages.Message(error, MessageTypeDefOf.RejectInput, false);
            }
        }

        private void ResetToDefaults()
        {
            customProfiles = new List<CustomProfile>();
            activeProfileId = VarianceProfiles.FaithfulId;
            hostileProfileId = VarianceProfiles.DistinctId;
            applyToHostilePawns = true;
            applyVarianceToChildren = false;
            verboseLogging = false;
            enableOverrides = true;
            hasInitializedDefaultOverrides = false;
            factionOverrides.Clear();
            xenotypeOverrides.Clear();
            raceOverrides.Clear();
            factionPriorities.Clear();
            xenotypePriorities.Clear();
            racePriorities.Clear();
            PopulateDefaultOverrides(force: true);
            RefreshResolved();

            // customProfiles was just replaced wholesale; drop the editor's cached cursor and
            // values so the next access re-resolves against the new state instead of pointing
            // at a profile that no longer exists.
            editorProfileId = null;
            editingValues = null;
        }

        public static string FormatPowerReadout(float meanComposite)
        {
            float baseC = FaithfulBaseline();
            if (baseC <= 0f) return $"Power: {meanComposite:F2}";

            float diffPct = ((meanComposite - baseC) / baseC) * 100f;
            if (Mathf.Abs(diffPct) < 0.5f)
            {
                return $"Baseline ({meanComposite:F2})";
            }

            string sign = diffPct > 0f ? "+" : "";
            return $"{sign}{diffPct:F0}% vs Faithful ({meanComposite:F2})";
        }

        // Just the signed percentage. FormatPowerReadout returns a whole sentence, which would
        // print "vs Faithful" twice when two anchors sit on screen together.
        //
        // The caller must supply a baseline measured at the SAME N as `composite`. A Best-of-N
        // score compared against the N=1 mean silently compares two different quantities -- the
        // batch size changes the shape of the distribution being summarised, not just its value.
        // (Best-of-25 Sovereign vs the N=1 Faithful baseline reads +59%; vs Faithful's own
        // Best-of-25 score it is the true +19%.) Passing the wrong-N baseline compiles fine and
        // looks plausible, which is exactly how this bug shipped once already.
        public static string FormatPowerPercent(float composite, float baseline)
        {
            if (baseline <= 0f) return composite.ToString("F2");

            float diffPct = ((composite - baseline) / baseline) * 100f;
            if (Mathf.Abs(diffPct) < 0.5f) return "baseline";

            return $"{(diffPct > 0f ? "+" : "")}{diffPct:F0}%";
        }

        // How much a passion pip is WORTH at a given Major bias, relative to a pip spent on an
        // all-Major roll. Range 0.848 (all Minor) .. 1.000 (all Major). Added 2026-08-06.
        //
        // ── WHY THIS EXISTS ──────────────────────────────────────────────────────────────────
        // The passion budget is denominated in pips, and the score used to be "count the pips".
        // But pips and value do not line up, because vanilla prices a Major at 1.5 pips while a
        // Major is worth more than 1.5 Minors:
        //
        //     LearnRateFactor:  None 0.35x   Minor 1.00x   Major 1.50x
        //     gain over None:               Minor +0.65    Major +1.15
        //     so a Major is worth 1.15 / 0.65 = 1.769 Minors, and costs 1.5 pips.
        //
        // Majors are underpriced by the pip currency. Two profiles can spend an identical budget
        // and the Major-heavy one is genuinely stronger, by up to ~18% at the extremes. A score
        // that counts pips alone cannot see that, which also made `passionMajorBias` a slider that
        // visibly changes pawns and never moves the readout.
        //
        // ── THE DERIVATION ───────────────────────────────────────────────────────────────────
        // At bias b, one passion costs   price(b) = Minor + (Major - Minor) * b        pips
        //           and is worth         gain(b)  = minorGain + (majorGain - minorGain) * b
        // so value per pip is gain(b) / price(b), and this returns it normalised against b = 1.
        //
        // ── WHY NORMALISED AT b = 1, NOT AT VANILLA'S b = 0.5 ────────────────────────────────
        // Anchoring at 1.0 keeps MaxPassionPips meaning what it says: 18 pips of all-Major is
        // exactly a saturated axis, score 1.0. Anchoring at vanilla's 0.5 would make Major-heavy
        // profiles multiply ABOVE 1.0 and clamp — the axis would saturate before 18 pips and the
        // ceiling would stop being the ceiling. That is the same class of mistake as the factor
        // this replaced. The cost of anchoring high is that every profile below b = 1 scores
        // slightly lower than it did, which is a scale shift, not a ranking change.
        //
        // ── WHAT THIS MODEL DOES *NOT* CAPTURE — read before trusting it too far ─────────────
        // It values a passion by its XP-rate increment over having no passion, and nothing else.
        // It therefore assumes all twelve skills are equally worth training, ignores that Majors
        // land on the pawn's BEST skills first (concentration is worth something on its own),
        // ignores diminishing returns once a skill nears 20, and has no time axis at all — the
        // same limitation already documented for the exchange rate R, whose ~2.0 is a
        // colony-lifetime average. It does NOT double-count R's discount for passions landing on
        // skills the colony never assigns: R prices a pip in skill-levels, this re-weights pips by
        // grade. Two different axes.
        //
        // This is a display-only score (see "CalculateCompositeScore is display-only" in
        // HANDOVER). 1.769 is defensible and derived from mechanics that actually run; it is not
        // the only defensible number. Two alternatives were considered and rejected on
        // 2026-08-06: vanilla's own `Pawn_SkillTracker.MajorPassionWeight = 2` (a valuation
        // vanilla declares and never calls), and the 1.25 that used to sit here (not derived from
        // anything). Changing this moves every published figure — see the CAUTION in HANDOVER.
        internal static float PassionPipEfficiency(float majorBias)
        {
            float minorGain = Constants.PassionLearnRateMinor - Constants.PassionLearnRateNone;
            float majorGain = Constants.PassionLearnRateMajor - Constants.PassionLearnRateNone;

            float pricePerPassion = Constants.MinorPassionCost
                + (Constants.MajorPassionCost - Constants.MinorPassionCost) * majorBias;
            float gainPerPassion = minorGain + (majorGain - minorGain) * majorBias;

            // Value per pip at this bias, over value per pip when every passion is a Major.
            return (gainPerPassion / pricePerPassion) / (majorGain / Constants.MajorPassionCost);
        }

        // A passion budget in pips -> the normalised passion axis. Mirror of `passion_from` in
        // docs/tools/envelope_check.py's make_composite; DispersionModel.Moments does the same
        // thing node by node. IF YOU CHANGE ONE, CHANGE ALL THREE.
        //
        // Five terms, and they are five genuinely different things. Do not collapse them.
        //   budget      — pips the profile targets at this quality.
        //   floor       — vanilla's guarantee that an adult gets at least one passion. See below.
        //   spend loop  — how many of those pips a pawn ACTUALLY receives. Whole passions only,
        //                 remainder discarded. See PassionSpend for why this is not the same as
        //                 the budget and why the difference does not cancel (finding Q-14).
        //   capacity    — the most pips the pawn's skills can physically absorb.
        //   efficiency  — what a pip is WORTH at this Major bias. See PassionPipEfficiency.
        //
        // The loop runs BEFORE the capacity limit, which is the generator's order: it spends the
        // whole budget into whole passions and only discovers how many eligible skills exist at
        // assignment, discarding the surplus there.
        //
        // Capacity is what actually caps the axis: each skill holds at most one passion, and a
        // passion costs on average Minor + (Major - Minor) * majorBias, so a profile can place one
        // per skill and no more. Budget above that is rolled and then discarded by the applier
        // (open decision 2 — deliberately not clamped at roll time), so the score must not keep
        // counting it. Without this cap a custom profile at budget 18 and Major bias 0 would score
        // a saturated 1.0 while only 12 pips are spendable — 12 Minors fill all 12 skills.
        //
        // The skill count is DERIVED, not a constant: MaxPassionPips is 12 skills x a Major, so
        // dividing it back out gives 12 exactly and cannot drift out of step with the ceiling. See
        // the note on Constants.MaxPassionPips before replacing this with a named 12.
        //
        // This whole expression once read `budget * (1f + 0.25f * v.passionMajorBias)`, a 24-pip-era
        // leftover. When it was written the denominator was 12 (the SKILL COUNT), so the budget was
        // being read as a COUNT OF PASSIONS and the 1.25 was the quality premium of an all-Major set
        // over an all-Minor set of the same size — coherent in count units. The denominator was
        // later corrected to 18 pips; that numerator was not, and a count-unit premium inflated a
        // pip-unit quantity by up to 25% until 2026-08-06. It also ran backwards at the top end: it
        // made a LOW Major bias saturate LATE (18 pips at bias 0) when a low bias is exactly the
        // case that saturates EARLY, since 12 Minors fill all 12 skills for 12 pips. The instinct
        // behind it was sound and is now expressed properly by `efficiency`: a Major really is
        // worth more than its 1.5-pip price. What was wrong was the units, the anchor (it scaled
        // above the ceiling instead of discounting below it) and the magnitude (1.25 from nowhere,
        // against 1.18 derived from the game's own XP rates).
        //
        // `floorToOne` is vanilla's at-least-one-passion guarantee, and it is a PARAMETER rather
        // than a read of v.passionCountMin because the disabled-axis fallback below has no profile
        // to ask -- vanilla's own budget is what it scores, and vanilla always floors. Until
        // 2026-08-09 this function had no floor at all: the generator applied it
        // (PassionVarianceApplier.cs:76), DispersionModel.Moments applied it, and both Python
        // mirrors applied it, while THIS function -- which computes MapToCenteredX's marker
        // position -- did not. That is finding Q-04, and it is the 2026-08-08 floor fix surviving
        // in the one site that fix's enumeration ("the model sides") did not count as a model side.
        // Keep this condition identical to the applier's, minus its alreadyCommittedPips clause:
        // that clause is about the grow-up top-up path, which scores nothing.
        //
        // Split declaration: this function only carries the three markers it actually
        // implements. `enable-toggles` and `skill-clamp` live on CalculateCompositeScore below
        // instead -- this function takes no enable* flags and does no skill clamping, so a
        // marker for either here would be a declaration this code does not back up. See the
        // matching split note above CalculateCompositeScore for where those two live and why.
        //
        // MIRRORS: passion-floor
        // MIRRORS: passion-spend-loop
        // MIRRORS: passion-capacity
        private static float PassionNormFor(float budget, float majorBias, bool floorToOne)
        {
            if (budget < 1f && floorToOne) budget = 1f;
            if (budget < 0f) budget = 0f;

            float skillCount = Constants.MaxPassionPips / Constants.MajorPassionCost;
            float capacity = skillCount
                * (Constants.MinorPassionCost
                   + (Constants.MajorPassionCost - Constants.MinorPassionCost) * majorBias);
            float efficiency = PassionPipEfficiency(majorBias);

            // Averaged over the spend loop's own coin flips, with capacity and Clamp01 applied per
            // OUTCOME rather than to the mean. This function only needs the first moment, but
            // DispersionModel.Moments needs the second from the same outcomes, so both are written
            // the same way — a mean-then-clamp shortcut here would silently stop mirroring it.
            var outcomes = PassionSpend.Outcomes(budget, majorBias);
            float acc = 0f;
            for (int i = 0; i < outcomes.Length; i++)
            {
                float pips = Mathf.Min(outcomes[i].Pips, capacity);
                acc += outcomes[i].Weight
                       * Mathf.Clamp01(pips * efficiency / Constants.MaxPassionPips);
            }
            return acc;
        }

        // ⚠️ CURRENTLY HAS NO CALLER. Verified 2026-08-09 by grep over Source/: every other hit on
        // this name is a comment. It is kept, and kept correct, for two reasons rather than out of
        // sentiment:
        //
        //   1. It is the C# mirror of envelope_check.py's make_composite, which is very much live
        //      -- it drives the tool's zero-noise self-check and the printed mean-band baseline.
        //      A drifting mirror is how this project's last three defects were built, so the rule
        //      is that the mirror stays faithful whether or not the game reads it today.
        //   2. It is the MEAN-BAND estimator, f(E[X]). The dispersion-aware E[f(X)] lives in
        //      DispersionModel. Both are legitimate and they answer different questions (see the
        //      two-baselines note on FaithfulBaseline); deleting this one would leave the project
        //      with no expression of the mean band at all.
        //
        // It lost its last caller when Q-03 moved FaithfulBaseline() onto DispersionModel.TypicalAt
        // -- so the audit register's Q-04 rationale ("it computes FaithfulBaseline and
        // MapToCenteredX") describes the tree as it was two fixes earlier. DO NOT read the absence
        // of a caller as licence to let it drift: if a future readout wants a mean-band figure it
        // will call this, and it must be right when that happens.
        //
        // Split declaration: this function carries `enable-toggles` and `skill-clamp` because it
        // is where the enable* flags are actually read (v.enableSkillVariance /
        // v.enablePassionVariance below) and where the resulting skill norm is clamped. The other
        // three markers -- passion-floor, passion-spend-loop, passion-capacity -- live above
        // PassionNormFor instead, because that is the function that implements them. Splitting
        // the five markers across the two functions that actually carry them is what keeps each
        // MIRRORS comment an honest claim about the code it sits on, rather than a blanket
        // declaration attached to whichever function happened to be nearby.
        //
        // MIRRORS: enable-toggles
        // MIRRORS: skill-clamp
        private static float CalculateCompositeScore(float q, VarianceProfileValues v)
        {
            // Skill variance off => the pawn keeps vanilla's levels, i.e. AssumedVanillaSkillBaseline
            // out of AssumedMaxSkillLevel. Derived, not the literal 0.25f this used to be: the same
            // fallback is mirrored in DispersionModel.Moments, and two hardcoded copies of 5/20 is
            // how the constants drift apart. Unlike the passion fallback below, this one really is
            // the skill axis's own baseline rather than a value borrowed from elsewhere.
            float skillNorm = Constants.AssumedVanillaSkillBaseline / Constants.AssumedMaxSkillLevel;
            if (v.enableSkillVariance)
            {
                float shift = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, q);
                float avgSkill = Mathf.Clamp(Constants.AssumedVanillaSkillBaseline + shift, 0f, Constants.AssumedMaxSkillLevel);
                skillNorm = Mathf.Clamp01(avgSkill / Constants.AssumedMaxSkillLevel);
            }

            // Trait count is deliberately NOT scored. It is a VARIANCE parameter, not a mean one:
            // selection is delegated to vanilla's quality-blind picker, so more traits does not buy
            // better traits, it buys more draws from an unchanged (roughly balanced) urn — including
            // the ~4% that can trigger uncontrolled behaviour. Scoring it as `count / 8` treated a
            // variance knob as a mean contributor, which (a) rewarded widening a spread even though
            // that makes pawns strictly worse to play with, and (b) compressed the whole scale,
            // because counts normalise into a narrow 0.25-0.625 band while skill/passion span
            // 0.1-1.0 — propping weak profiles up and holding strong ones down. Trait count does
            // feed Best-of-N power through variance, but quantifying that needs a per-trait value
            // model, which is not recoverable from def data (see TRAIT-DESIRABILITY-RESEARCH.md).
            // Omitting a term we cannot estimate beats including one we know is wrong.

            // Passion variance off => the pawn keeps vanilla's assignment, whose budget averages
            // VanillaPassionBudget pips at vanilla's own 50/50 coin flip. Not 0.25: that was the
            // skill axis's baseline (5/20) copied across, which is right there and only
            // coincidentally near-right here. Scored through the same efficiency term as every
            // other profile, or this branch would silently sit on a different scale.
            // Runs through the spend loop as well, because vanilla's own generator discretizes
            // exactly the way ours does. Scoring this branch continuously while the live branch
            // below discretizes would put the two on different scales and break the both-axes-off
            // invariant Q-16 turns on -- Faithful's band at q = 0.50 is VanillaPassionBudget pips
            // at VanillaMajorBias, so the two paths must agree term for term.
            // floorToOne: true. Vanilla's budget is `5 + clamp(Gaussian, -4, 4)`, which bottoms out
            // at 1 -- the floor is where that guarantee comes from, so the branch that scores
            // vanilla has to carry it. It changes nothing at VanillaPassionBudget = 5; it is here
            // so the two branches stay term-for-term identical, which is what Q-16's invariant
            // depends on.
            float passionNorm = PassionNormFor(Constants.VanillaPassionBudget,
                                               Constants.VanillaMajorBias,
                                               floorToOne: true);
            if (v.enablePassionVariance)
            {
                // Pips the profile targets at this quality. Everything that turns pips into a
                // normalised axis — the floor, the spend loop, capacity, efficiency — is in
                // PassionNormFor, which the disabled-axis fallback above shares so the two cannot
                // drift apart.
                float budget = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
                passionNorm = PassionNormFor(budget, v.passionMajorBias,
                                             floorToOne: v.passionCountMin > 0f);
            }

            // These two weights and Constants.MaxPassionPips jointly set the skill/passion exchange
            // rate — see the derivation on Constants.CompositeSkillWeight. Retuning one alone moves
            // the rate without looking like it does.
            //
            // THE WEIGHTS ARE UNCONDITIONAL, and the two fallbacks above are why. Until 2026-08-09
            // these read `v.enableSkillVariance ? Constants.CompositeSkillWeight : 0f` and the same
            // for passion, which multiplied each disabled axis's fallback by zero — so the whole
            // derivation above (VanillaPassionBudget x efficiency, and the argument for why it is
            // 0.2609 and "not 0.25") computed a value that could never reach the result. Both
            // constants had no other consumer in Source/, i.e. they were dead.
            //
            // Dropping the axis is also wrong on its own terms. A disabled axis does not mean the
            // pawn has no skills or no passions; it means the pawn keeps VANILLA's, which is
            // exactly what the fallbacks measure. Zeroing the weight instead rescales the composite
            // so the surviving axis is 100% of it, putting that profile on a different scale from
            // every other profile while still being divided by the same Faithful baseline.
            //
            // The check that settles it: with BOTH axes off every pawn is untouched vanilla, so the
            // score must be exactly the Faithful baseline. Keeping the weights gives
            //     (0.8 x 0.25 + 1.5 x 0.251087) / 2.3 = 0.250709
            // which is the mean-band composite of the vanilla-like profile. The old code returned
            // `q` there — the raw quality roll, on no meaningful scale at all.
            //
            // NOTE this is NOT FaithfulBaseline(). It was, until 2026-08-09; that function now
            // returns the dispersion-aware typical (0.2422) because the readout it feeds is
            // dispersion-aware and was dividing two different estimators (finding Q-03). Both
            // numbers are correct and they measure different things — a disabled axis is a
            // zero-variance constant, Faithful with its axes on has real spread. Do not "fix" one
            // to match the other.
            //
            // The passion term is 0.251087 rather than the 0.260870 it was until 2026-08-09
            // because vanilla's 5-pip budget now runs through vanilla's own discretizing spend
            // loop like every other budget (4.8125 pips delivered, not 5.0 — see PassionSpend and
            // finding Q-14). BOTH sides of this equality moved together, which is exactly why it
            // still holds: a fix that discretized the live branch and not the fallback would put
            // the two on different scales, and this is the check that would have caught it.
            //
            // No shipped figure moves: all eight presets set both flags true, so this is reachable
            // only from a custom profile. DispersionModel.Moments mirrors this; keep them in step.
            const float wS = Constants.CompositeSkillWeight;
            const float wP = Constants.CompositePassionWeight;

            return Mathf.Clamp01((wS * skillNorm + wP * passionNorm) / (wS + wP));
        }

        // Expected composite score of the best of n pawns: E[composite(max(q1..qn))].
        //
        // This is the figure that describes actual play. The player CHOOSES which pawns to keep --
        // rerolling start scenarios, picking from raid captures, accepting or refusing quest pawns
        // -- so the pawn that ends up in the colony is the maximum of n rolls, not a typical roll.
        // A mean-based figure systematically understates any high-dispersion profile, which is
        // exactly why the project's own envelope maths is Best-of-N.
        //
        // Mirror of expected_best_of_n() in docs/tools/envelope_check.py. If you change one, change
        // both, and re-run the cross-check -- the UI and HANDOVER's table must not disagree.
        // Density of the max is n * F(q)^(n-1) * f(q).
        //
        // Single-entry cache keyed on every input that affects the integration. The header calls
        // this once per frame for the edited profile AND once (via FaithfulBestOfNBaseline) for
        // Faithful -- if both went through the same cache slot they would evict each other every
        // frame and the cache would never hit. FaithfulBestOfNBaseline therefore has its own,
        // separate cache and calls the uncached core directly instead of sharing this slot.
        private static float cachedBestOfNResult;
        private static float cachedBestOfN_avgQ, cachedBestOfN_shiftMin, cachedBestOfN_shiftMax;
        private static float cachedBestOfN_passionMin, cachedBestOfN_passionMax, cachedBestOfN_majorBias;
        private static float cachedBestOfN_skillNoiseScalar, cachedBestOfN_passionNoiseScalar;
        private static bool cachedBestOfN_skillOn, cachedBestOfN_passionOn;
        // Keyed on lowRes too: a live slider drag changes every other key field every frame
        // anyway (miss rate ~0%), but the flag still has to be part of the key so the FIRST
        // full-resolution frame after the mouse is released misses naturally instead of
        // reusing the low-res result computed one frame earlier at the same profile values.
        private static bool cachedBestOfN_lowRes;
        private static int cachedBestOfN_n = -1;

        public static float CalculateBestOfNScore(VarianceProfileValues v, int n, bool lowRes = false)
        {
            if (v == null || n < 1) return 0f;

            if (cachedBestOfN_n == n
                && cachedBestOfN_avgQ == v.averageQuality
                && cachedBestOfN_shiftMin == v.skillShiftMin
                && cachedBestOfN_shiftMax == v.skillShiftMax
                && cachedBestOfN_passionMin == v.passionCountMin
                && cachedBestOfN_passionMax == v.passionCountMax
                && cachedBestOfN_majorBias == v.passionMajorBias
                && cachedBestOfN_skillNoiseScalar == v.SkillNoiseScalar
                && cachedBestOfN_passionNoiseScalar == v.PassionNoiseScalar
                && cachedBestOfN_skillOn == v.enableSkillVariance
                && cachedBestOfN_passionOn == v.enablePassionVariance
                && cachedBestOfN_lowRes == lowRes)
            {
                return cachedBestOfNResult;
            }

            float result = CalculateBestOfNScoreCore(v, n, lowRes);

            cachedBestOfN_n = n;
            cachedBestOfN_avgQ = v.averageQuality;
            cachedBestOfN_shiftMin = v.skillShiftMin;
            cachedBestOfN_shiftMax = v.skillShiftMax;
            cachedBestOfN_passionMin = v.passionCountMin;
            cachedBestOfN_passionMax = v.passionCountMax;
            cachedBestOfN_majorBias = v.passionMajorBias;
            cachedBestOfN_skillNoiseScalar = v.SkillNoiseScalar;
            cachedBestOfN_passionNoiseScalar = v.PassionNoiseScalar;
            cachedBestOfN_skillOn = v.enableSkillVariance;
            cachedBestOfN_passionOn = v.enablePassionVariance;
            cachedBestOfN_lowRes = lowRes;
            cachedBestOfNResult = result;

            return result;
        }

        private static float CalculateBestOfNScoreCore(VarianceProfileValues v, int n, bool lowRes = false)
        {
            return DispersionModel.BestOfN(v, n, lowRes);
        }

        private static float cachedFaithfulBaseline = -1f;

        // The denominator of the "Typical" readout and the centre line of the distribution curve.
        //
        // DISPERSION-AWARE, and it must be. Until 2026-08-09 this read
        // `CalculateCompositeScore(0.50f, ...)` -- the MEAN BAND, with no noise integrated -- while
        // every numerator it divides is `DispersionModel.TypicalAt`, which integrates the noise
        // through the same clamps the generator applies. Those are two different estimators of the
        // same quantity, `f(E[X])` against `E[f(X)]` (audit finding Q-03), and they agree only
        // where `f` is linear across the range the noise actually reaches.
        //
        // That WAS true to six decimals, which is why the mismatch was filed as Minor on a measured
        // 0.00pp and left alone. It stopped being true the moment the passion axis started spending
        // budgets through the generator's discretizing loop (Q-14): E[spent] is a staircase, the
        // mean band lands at 5.0 pips which sits just ABOVE one of its jumps (4.8125 delivered),
        // and the average of the staircase over the budget Gaussian is ~4.55. Faithful's two
        // estimators went from agreeing to six decimals to differing by -3.39%, on the number that
        // is the denominator of EVERY displayed percentage and the centre of the curve.
        //
        // Use TypicalAt, not CalculateCompositeScore, if this is ever touched again: the readout is
        // dispersion-aware by design (see HANDOVER "Dispersion-aware scoring"), so the reference it
        // is measured against has to be the same estimator. The Best-of-N row already did this
        // correctly via FaithfulBestOfNBaseline, which is why only this one was wrong.
        //
        // Evaluated at 0.50 rather than at VanillaLike.averageQuality only because those are the
        // same number; if Faithful's averageQuality ever moves, this should follow it.
        private static float FaithfulBaseline()
        {
            if (cachedFaithfulBaseline < 0f)
                cachedFaithfulBaseline = DispersionModel.TypicalAt(
                    VarianceProfiles.VanillaLike.MakeValues(), 0.50f);
            return cachedFaithfulBaseline;
        }

        // Faithful's own Best-of-N score, for comparing a Best-of-N figure against Best-of-N (see
        // FormatPowerPercent). Cached separately from CalculateBestOfNScore's cache -- see the
        // comment on that cache for why sharing a slot would thrash -- and keyed on n as well as
        // the value, so a future change to Constants.BestOfNSampleCount cannot serve a stale
        // baseline computed for a different n.
        private static float cachedFaithfulBestOfN = -1f;
        private static int cachedFaithfulBestOfN_n = -1;
        private static bool cachedFaithfulBestOfN_lowRes;

        public static float FaithfulBestOfNBaseline(int n, bool lowRes = false)
        {
            if (cachedFaithfulBestOfN_n != n || cachedFaithfulBestOfN < 0f
                || cachedFaithfulBestOfN_lowRes != lowRes)
            {
                cachedFaithfulBestOfN = CalculateBestOfNScoreCore(VarianceProfiles.VanillaLike.MakeValues(), n, lowRes);
                cachedFaithfulBestOfN_n = n;
                cachedFaithfulBestOfN_lowRes = lowRes;
            }
            return cachedFaithfulBestOfN;
        }

        private static float MapToCenteredX(float compositeScore)
        {
            float baseC = FaithfulBaseline();
            if (baseC <= 0f || baseC >= 1f) return compositeScore;

            if (compositeScore <= baseC)
            {
                return 0.50f * (compositeScore / baseC);
            }
            else
            {
                return 0.50f + 0.50f * ((compositeScore - baseC) / (1.0f - baseC));
            }
        }

    }
}
