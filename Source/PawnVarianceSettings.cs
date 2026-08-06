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
                if (custom != null) vals = custom.values;
                else if (customProfiles != null && customProfiles.Count > 0) vals = customProfiles[0].values;
                else vals = VarianceProfiles.VanillaLike.MakeValues();
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

        public VarianceProfileValues ValuesFor(Pawn pawn) => ValuesFor(pawn, null);

        public VarianceProfileValues ValuesFor(Pawn pawn, PawnGenerationRequest? request)
        {
            if (pawn == null) return Active;

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

            Faction fHostile = pawn.Faction;
            if (fHostile == null && request.HasValue) fHostile = request.Value.Faction;

            if (applyToHostilePawns && fHostile != null && Faction.OfPlayerSilentFail != null
                && fHostile.HostileTo(Faction.OfPlayerSilentFail))
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
                    key => DefDatabase<FactionDef>.GetNamedSilentFail(key)?.LabelCap.ToString() ?? key);
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
                        options.Add(new FloatMenuOption(fDef.LabelCap, () =>
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
                    key => DefDatabase<XenotypeDef>.GetNamedSilentFail(key)?.LabelCap.ToString() ?? key);
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
                        options.Add(new FloatMenuOption(xDef.LabelCap, () =>
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
            var duplicateLabels = selectableList.GroupBy(r => r.LabelCap.ToString())
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
                        string labelStr = d.LabelCap.ToString();
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
                        string labelStr = rDef.LabelCap.ToString();
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
            return seen.OrderBy(d => d.LabelCap.ToString());
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

            float passionNorm = 0.25f;
            if (v.enablePassionVariance)
            {
                float budget = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
                float pips = budget * (1f + 0.25f * v.passionMajorBias);
                passionNorm = Mathf.Clamp01(pips / Constants.MaxPassionPips);
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

        // Scratch buffer for the Best-of-N grid. Static and reused: the settings window redraws
        // every frame while open, and a fresh 1024-float array per frame is pure GC churn.
        private static float[] betaDensityScratch;

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
            // No n == 1 shortcut. Returning composite(averageQuality) here would be assuming
            // E[composite(q)] == composite(E[q]), which holds only while composite is LINEAR in q.
            // It is linear for every preset whose skill band keeps AssumedVanillaSkillBaseline +
            // shift above zero -- but Wildcard's skillShiftMin of -8.7 drives it negative, and the
            // Mathf.Clamp in CalculateCompositeScore puts a kink at q = 0.2868. Past that kink the
            // function is convex, so by Jensen the shortcut UNDERSTATES the true expectation: it
            // returned 0.197666 against the reference's 0.204709, moving Wildcard's displayed
            // "Typical" figure from -18% to -21%. Seven of eight presets are linear and matched to
            // six decimals, which is exactly why this survived review. The integral below is
            // correct at n == 1 too -- Pow(cdf, 0) is 1, so it reduces to E[composite(q)].
            int nodes = Constants.BestOfNIntegrationNodes;
            if (betaDensityScratch == null || betaDensityScratch.Length != nodes)
                betaDensityScratch = new float[nodes];

            v.GetBetaAlphaBeta(out float alpha, out float beta);
            float dq = 1f / nodes;

            // Unnormalised Beta density on a midpoint grid. The normalising constant is divided
            // out below rather than computed via lgamma, which keeps this allocation-free.
            float total = 0f;
            for (int i = 0; i < nodes; i++)
            {
                float q = (i + 0.5f) * dq;
                float d = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                betaDensityScratch[i] = d;
                total += d * dq;
            }

            if (total <= 0f || float.IsNaN(total) || float.IsInfinity(total))
                return CalculateCompositeScore(v.averageQuality, v);

            float acc = 0f;
            float cdf = 0f;
            for (int i = 0; i < nodes; i++)
            {
                float q = (i + 0.5f) * dq;
                float density = betaDensityScratch[i] / total;
                cdf += density * dq;   // running CDF, inclusive of the current cell
                acc += CalculateCompositeScore(q, v) * n * Mathf.Pow(cdf, n - 1) * density * dq;
            }

            return acc;
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
