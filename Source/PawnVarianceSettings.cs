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
                if (factionOverrideKeys != null && factionOverrideValues != null
                    && factionOverrideKeys.Count == factionOverrideValues.Count)
                {
                    for (int i = 0; i < factionOverrideKeys.Count; i++)
                    {
                        factionOverrides[factionOverrideKeys[i]] = factionOverrideValues[i];
                    }
                }

                xenotypeOverrides = new Dictionary<string, string>();
                if (xenotypeOverrideKeys != null && xenotypeOverrideValues != null
                    && xenotypeOverrideKeys.Count == xenotypeOverrideValues.Count)
                {
                    for (int i = 0; i < xenotypeOverrideKeys.Count; i++)
                    {
                        xenotypeOverrides[xenotypeOverrideKeys[i]] = xenotypeOverrideValues[i];
                    }
                }

                factionPriorities = new Dictionary<string, OverridePriority>();
                if (factionPriorityKeys != null && factionPriorityValues != null
                    && factionPriorityKeys.Count == factionPriorityValues.Count)
                {
                    for (int i = 0; i < factionPriorityKeys.Count; i++)
                    {
                        factionPriorities[factionPriorityKeys[i]] = (OverridePriority)factionPriorityValues[i];
                    }
                }

                xenotypePriorities = new Dictionary<string, OverridePriority>();
                if (xenotypePriorityKeys != null && xenotypePriorityValues != null
                    && xenotypePriorityKeys.Count == xenotypePriorityValues.Count)
                {
                    for (int i = 0; i < xenotypePriorityKeys.Count; i++)
                    {
                        xenotypePriorities[xenotypePriorityKeys[i]] = (OverridePriority)xenotypePriorityValues[i];
                    }
                }

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

            // Not behind a Biotech check -- HAR races exist without it.
            DrawRaceOverridesSection(listing);

            if (ModsConfig.BiotechActive)
            {
                DrawXenotypeOverridesSection(listing);
            }

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
                    key => LabelOf(DefDatabase<FactionDef>.GetNamedSilentFail(key)) ?? key);
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Faction Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var factionDef in DefDatabase<FactionDef>.AllDefs)
                {
                    if (!factionOverrides.ContainsKey(factionDef.defName))
                    {
                        var fDef = factionDef;
                        options.Add(new FloatMenuOption(LabelOf(fDef), () =>
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
                    key => LabelOf(DefDatabase<XenotypeDef>.GetNamedSilentFail(key)) ?? key);
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Xenotype Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var xenoDef in DefDatabase<XenotypeDef>.AllDefs)
                {
                    if (!xenotypeOverrides.ContainsKey(xenoDef.defName))
                    {
                        var xDef = xenoDef;
                        options.Add(new FloatMenuOption(LabelOf(xDef), () =>
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

            var selectableList = SelectableRaces().ToList();
            var duplicateLabels = selectableList.GroupBy(r => LabelOf(r))
                                                .Where(g => g.Count() > 1)
                                                .Select(g => g.Key)
                                                .ToHashSet();

            if (raceOverrides.Count == 0)
            {
                Caption(listing, "No race overrides configured. Race overrides ship empty because the available races depend on which race mods are installed.");
            }
            else
            {
                OverrideColumnHeaders(listing, "Race");
                DrawOverrideRows(listing, raceOverrides, racePriorities,
                    key =>
                    {
                        ThingDef d = DefDatabase<ThingDef>.GetNamedSilentFail(key);
                        if (d == null) return key;
                        string labelStr = LabelOf(d);
                        return duplicateLabels.Contains(labelStr) ? $"{labelStr} ({d.defName})" : labelStr;
                    });
            }

            Color oldColor = GUI.color;
            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (listing.ButtonText("+ Add Race Override"))
            {
                var options = new List<FloatMenuOption>();
                foreach (var raceDef in selectableList)
                {
                    if (!raceOverrides.ContainsKey(raceDef.defName))
                    {
                        var rDef = raceDef;
                        string labelStr = LabelOf(rDef);
                        string displayLabel = duplicateLabels.Contains(labelStr)
                            ? $"{labelStr} ({rDef.defName})"
                            : labelStr;

                        options.Add(new FloatMenuOption(displayLabel, () =>
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

        private void CreateNewCustomProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
            string newName = "Custom " + (customProfiles.Count + 1);
            var profile = new CustomProfile(newId, newName, VarianceProfiles.VanillaLike.MakeValues());
            customProfiles.Add(profile);
            // Selects it in the editor only. The colony keeps whatever profile it was using.
            SetEditorProfile(newId);
        }

        private void DuplicateCurrentProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
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

        private static float CalculateCompositeScore(float q, VarianceProfileValues v)
        {
            float skillNorm = 0.25f;
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
            float passionNorm = Constants.VanillaPassionBudget
                * PassionPipEfficiency(Constants.VanillaMajorBias) / Constants.MaxPassionPips;
            if (v.enablePassionVariance)
            {
                // Three terms, and they are three genuinely different things. Do not collapse them.
                //   budget      — pips the profile targets at this quality. The spend loop charges
                //                 Majors MajorPassionCost and Minors MinorPassionCost out of it.
                //   capacity    — the most pips the pawn's skills can physically absorb.
                //   efficiency  — what a pip is WORTH at this Major bias. See PassionPipEfficiency.
                //
                // This line used to read `budget * (1f + 0.25f * v.passionMajorBias)`, a 24-pip-era
                // leftover. When it was written the denominator was 12 (the SKILL COUNT), so the
                // budget was being read as a COUNT OF PASSIONS and the 1.25 was the quality premium
                // of an all-Major set over an all-Minor set of the same size — coherent in count
                // units. The denominator was later corrected to 18 pips; that numerator was not,
                // and a count-unit premium inflated a pip-unit quantity by up to 25% until
                // 2026-08-06. It also ran backwards at the top end: it made a LOW Major bias
                // saturate LATE (18 pips at bias 0) when a low bias is exactly the case that
                // saturates EARLY, since 12 Minors fill all 12 skills for 12 pips.
                //
                // The instinct behind it was sound and is now expressed properly by `efficiency`:
                // a Major really is worth more than its 1.5-pip price. What was wrong was the
                // units (a count premium on a pip quantity), the anchor (it scaled above the
                // ceiling instead of discounting below it) and the magnitude (1.25 from nowhere,
                // against 1.18 derived from the game's own XP rates).
                //
                // Capacity is what actually caps the axis: each skill holds at most one passion,
                // and a passion costs on average Minor + (Major - Minor) * majorBias, so a profile
                // can place one per skill and no more. Budget above that is rolled and then
                // discarded by the applier (open decision 2 — deliberately not clamped at roll
                // time), so the score must not keep counting it. Without this cap a custom profile
                // at budget 18 and Major bias 0 would score a saturated 1.0 while only 12 pips are
                // spendable — 12 Minors fill all 12 skills.
                //
                // The skill count is DERIVED, not a constant: MaxPassionPips is 12 skills x a
                // Major, so dividing it back out gives 12 exactly and cannot drift out of step
                // with the ceiling. See the note on Constants.MaxPassionPips before replacing this
                // with a named 12.
                float skillCount = Constants.MaxPassionPips / Constants.MajorPassionCost;
                float budget = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
                float capacity = skillCount
                    * (Constants.MinorPassionCost
                       + (Constants.MajorPassionCost - Constants.MinorPassionCost) * v.passionMajorBias);
                float efficiency = PassionPipEfficiency(v.passionMajorBias);
                passionNorm = Mathf.Clamp01(
                    Mathf.Min(budget, capacity) * efficiency / Constants.MaxPassionPips);
            }

            // These two weights and Constants.MaxPassionPips jointly set the skill/passion exchange
            // rate — see the derivation on Constants.CompositeSkillWeight. Retuning one alone moves
            // the rate without looking like it does.
            float wS = v.enableSkillVariance ? Constants.CompositeSkillWeight : 0f;
            float wP = v.enablePassionVariance ? Constants.CompositePassionWeight : 0f;
            float totalW = wS + wP;

            if (totalW <= 0f) return q;
            return Mathf.Clamp01((wS * skillNorm + wP * passionNorm) / totalW);
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
        private static bool cachedBestOfN_skillOn, cachedBestOfN_passionOn;
        private static int cachedBestOfN_n = -1;

        public static float CalculateBestOfNScore(VarianceProfileValues v, int n)
        {
            if (v == null || n < 1) return 0f;

            if (cachedBestOfN_n == n
                && cachedBestOfN_avgQ == v.averageQuality
                && cachedBestOfN_shiftMin == v.skillShiftMin
                && cachedBestOfN_shiftMax == v.skillShiftMax
                && cachedBestOfN_passionMin == v.passionCountMin
                && cachedBestOfN_passionMax == v.passionCountMax
                && cachedBestOfN_majorBias == v.passionMajorBias
                && cachedBestOfN_skillOn == v.enableSkillVariance
                && cachedBestOfN_passionOn == v.enablePassionVariance)
            {
                return cachedBestOfNResult;
            }

            float result = CalculateBestOfNScoreCore(v, n);

            cachedBestOfN_n = n;
            cachedBestOfN_avgQ = v.averageQuality;
            cachedBestOfN_shiftMin = v.skillShiftMin;
            cachedBestOfN_shiftMax = v.skillShiftMax;
            cachedBestOfN_passionMin = v.passionCountMin;
            cachedBestOfN_passionMax = v.passionCountMax;
            cachedBestOfN_majorBias = v.passionMajorBias;
            cachedBestOfN_skillOn = v.enableSkillVariance;
            cachedBestOfN_passionOn = v.enablePassionVariance;
            cachedBestOfNResult = result;

            return result;
        }

        private static float CalculateBestOfNScoreCore(VarianceProfileValues v, int n)
        {
            return DispersionModel.BestOfN(v, n);
        }

        private static float cachedFaithfulBaseline = -1f;

        private static float FaithfulBaseline()
        {
            if (cachedFaithfulBaseline < 0f)
                cachedFaithfulBaseline = CalculateCompositeScore(0.50f, VarianceProfiles.VanillaLike.MakeValues());
            return cachedFaithfulBaseline;
        }

        // Faithful's own Best-of-N score, for comparing a Best-of-N figure against Best-of-N (see
        // FormatPowerPercent). Cached separately from CalculateBestOfNScore's cache -- see the
        // comment on that cache for why sharing a slot would thrash -- and keyed on n as well as
        // the value, so a future change to Constants.BestOfNSampleCount cannot serve a stale
        // baseline computed for a different n.
        private static float cachedFaithfulBestOfN = -1f;
        private static int cachedFaithfulBestOfN_n = -1;

        public static float FaithfulBestOfNBaseline(int n)
        {
            if (cachedFaithfulBestOfN_n != n || cachedFaithfulBestOfN < 0f)
            {
                cachedFaithfulBestOfN = CalculateBestOfNScoreCore(VarianceProfiles.VanillaLike.MakeValues(), n);
                cachedFaithfulBestOfN_n = n;
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
