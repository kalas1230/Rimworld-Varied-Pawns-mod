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

    public class PawnVarianceSettings : ModSettings
    {
        // Housekeeping preferences: deliberately outside the profile system, so switching profiles
        // never silently re-enables logging or changes whether raiders get variance.
        public bool applyToHostilePawns = true;
        public bool applyVarianceToChildren = true;
        public bool verboseLogging = false;

        public List<CustomProfile> customProfiles = new List<CustomProfile>();
        public string activeProfileId = VarianceProfiles.FaithfulId;
        public string hostileProfileId = VarianceProfiles.DistinctId;

        public bool enableOverrides = true;
        public bool factionOverridesTakePrecedence = true;
        public bool hasInitializedDefaultOverrides = false;
        public Dictionary<string, string> factionOverrides = new Dictionary<string, string>();
        public Dictionary<string, string> xenotypeOverrides = new Dictionary<string, string>();
        public Dictionary<string, OverridePriority> factionPriorities = new Dictionary<string, OverridePriority>();
        public Dictionary<string, OverridePriority> xenotypePriorities = new Dictionary<string, OverridePriority>();

        private List<string> factionOverrideKeys = new List<string>();
        private List<string> factionOverrideValues = new List<string>();
        private List<string> xenotypeOverrideKeys = new List<string>();
        private List<string> xenotypeOverrideValues = new List<string>();

        private List<string> factionPriorityKeys = new List<string>();
        private List<int> factionPriorityValues = new List<int>();
        private List<string> xenotypePriorityKeys = new List<string>();
        private List<int> xenotypePriorityValues = new List<int>();

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

        public bool EditingCustom => GetCustomProfile(activeProfileId) != null;

        public PawnVarianceSettings()
        {
            PopulateDefaultOverrides();
            RefreshResolved();
        }

        public void PopulateDefaultOverrides(bool force = false)
        {
            if (factionOverrides == null) factionOverrides = new Dictionary<string, string>();
            if (xenotypeOverrides == null) xenotypeOverrides = new Dictionary<string, string>();
            if (factionPriorities == null) factionPriorities = new Dictionary<string, OverridePriority>();
            if (xenotypePriorities == null) xenotypePriorities = new Dictionary<string, OverridePriority>();

            if (hasInitializedDefaultOverrides && !force)
            {
                EnsureDefaultPriorities();
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

        private void EnsureDefaultPriorities()
        {
            if (factionOverrides.ContainsKey("Empire") && !factionPriorities.ContainsKey("Empire")) factionPriorities["Empire"] = OverridePriority.Highest;
            if (factionOverrides.ContainsKey("Ancients") && !factionPriorities.ContainsKey("Ancients")) factionPriorities["Ancients"] = OverridePriority.High;
            if (factionOverrides.ContainsKey("AncientsHostile") && !factionPriorities.ContainsKey("AncientsHostile")) factionPriorities["AncientsHostile"] = OverridePriority.High;

            if (xenotypeOverrides.ContainsKey("Sanguophage") && !xenotypePriorities.ContainsKey("Sanguophage")) xenotypePriorities["Sanguophage"] = OverridePriority.Highest;
            if (xenotypeOverrides.ContainsKey("Highmate") && !xenotypePriorities.ContainsKey("Highmate")) xenotypePriorities["Highmate"] = OverridePriority.High;
            if (xenotypeOverrides.ContainsKey("Genie") && !xenotypePriorities.ContainsKey("Genie")) xenotypePriorities["Genie"] = OverridePriority.High;
            if (xenotypeOverrides.ContainsKey("Hussar") && !xenotypePriorities.ContainsKey("Hussar")) xenotypePriorities["Hussar"] = OverridePriority.High;
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

                string factionProfileId = null;
                OverridePriority factionPrio = OverridePriority.Normal;
                bool hasFactionOverride = false;
                if (faction?.def != null && factionOverrides.TryGetValue(faction.def.defName, out factionProfileId))
                {
                    hasFactionOverride = true;
                    if (factionPriorities.TryGetValue(faction.def.defName, out var p))
                        factionPrio = p;
                }

                string xenoProfileId = null;
                OverridePriority xenoPrio = OverridePriority.Normal;
                bool hasXenoOverride = false;
                if (ModsConfig.BiotechActive)
                {
                    string xenoDef = GetXenotypeDefName(pawn, request);
                    if (xenoDef != null && xenotypeOverrides.TryGetValue(xenoDef, out xenoProfileId))
                    {
                        hasXenoOverride = true;
                        if (xenotypePriorities.TryGetValue(xenoDef, out var p))
                            xenoPrio = p;
                    }
                }

                if (hasFactionOverride && hasXenoOverride)
                {
                    if (factionPrio > xenoPrio) return Resolve(factionProfileId);
                    if (xenoPrio > factionPrio) return Resolve(xenoProfileId);

                    // Equal priority tie-break
                    if (factionOverridesTakePrecedence) return Resolve(factionProfileId);
                    return Resolve(xenoProfileId);
                }

                if (hasFactionOverride) return Resolve(factionProfileId);
                if (hasXenoOverride) return Resolve(xenoProfileId);
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

        public override void ExposeData()
        {
            base.ExposeData();

            Scribe_Collections.Look(ref customProfiles, "customProfiles", LookMode.Deep);
            Scribe_Values.Look(ref activeProfileId, "activeProfileId", VarianceProfiles.FaithfulId);
            Scribe_Values.Look(ref hostileProfileId, "hostileProfileId", VarianceProfiles.DistinctId);
            Scribe_Values.Look(ref applyToHostilePawns, "applyToHostilePawns", true);
            Scribe_Values.Look(ref applyVarianceToChildren, "applyVarianceToChildren", true);
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
            }

            Scribe_Collections.Look(ref factionOverrideKeys, "factionOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref factionOverrideValues, "factionOverrideValues", LookMode.Value);
            Scribe_Collections.Look(ref xenotypeOverrideKeys, "xenotypeOverrideKeys", LookMode.Value);
            Scribe_Collections.Look(ref xenotypeOverrideValues, "xenotypeOverrideValues", LookMode.Value);

            Scribe_Collections.Look(ref factionPriorityKeys, "factionPriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref factionPriorityValues, "factionPriorityValues", LookMode.Value);
            Scribe_Collections.Look(ref xenotypePriorityKeys, "xenotypePriorityKeys", LookMode.Value);
            Scribe_Collections.Look(ref xenotypePriorityValues, "xenotypePriorityValues", LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customProfiles == null || customProfiles.Count == 0)
                {
                    customProfiles = new List<CustomProfile>
                    {
                        new CustomProfile("custom_1", "Custom 1", VarianceProfiles.VanillaLike.MakeValues())
                    };
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
            const float viewHeight = 600f;
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

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawProfileEditorTab(Rect outRect)
        {
            const float viewHeight = 1600f;
            var viewRect = new Rect(0f, 0f, outRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref profileEditorScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            DrawProfileSelector(listing);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawOverridesTab(Rect outRect)
        {
            const float viewHeight = 1400f;
            var viewRect = new Rect(0f, 0f, outRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref overridesScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            Caption(listing, "Priority Levels: Every override defaults to Normal. Overrides set to High / Highest take precedence over lower tiers. Ties at the same priority level are broken by the Faction vs Xenotype Precedence toggle above. Unlisted factions/xenotypes have no custom profile override.");
            listing.Gap(4f);

            listing.CheckboxLabeled(
                "Enable Faction & Xenotype Overrides",
                ref enableOverrides,
                "When enabled, specific faction and xenotype profiles take precedence over Hostile and General profiles.");

            listing.Gap(4f);

            bool wasEnabled = GUI.enabled;
            if (!enableOverrides)
            {
                GUI.enabled = false;
                Caption(listing, "Enable the checkbox above to configure per-faction and per-xenotype profiles.");
            }

            listing.CheckboxLabeled(
                "Faction Overrides Take Priority Over Xenotype Overrides",
                ref factionOverridesTakePrecedence,
                "When checked, if a pawn matches both a Faction override and a Xenotype override (e.g. an Empire Neanderthal), the Faction override is used. If unchecked, the Xenotype override takes priority.");

            listing.Gap(SectionGap);

            DrawFactionOverridesSection(listing);

            if (ModsConfig.BiotechActive)
            {
                DrawXenotypeOverridesSection(listing);
            }

            GUI.enabled = wasEnabled;

            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawFactionOverridesSection(Listing_Standard listing)
        {
            Section(listing, "Faction Overrides");
            Caption(listing, "Assign custom profiles to specific factions. Faction overrides take precedence over Hostile and General settings.");

            if (factionOverrides.Count == 0)
            {
                Caption(listing, "No faction overrides configured.");
            }
            else
            {
                string toRemove = null;
                var keys = new List<string>(factionOverrides.Keys);
                foreach (var key in keys)
                {
                    var currentProfile = factionOverrides[key];
                    OverridePriority currentPrio = OverridePriority.Normal;
                    if (factionPriorities.TryGetValue(key, out var p))
                        currentPrio = p;

                    FactionDef def = DefDatabase<FactionDef>.GetNamedSilentFail(key);
                    string label = def != null ? def.LabelCap.ToString() : key;

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
                        ProfileMenu(id => factionOverrides[k] = id);
                    }
                    if (Widgets.ButtonText(prioRect, currentPrio.ToString()))
                    {
                        string k = key;
                        PriorityMenu(p => {
                            if (p == OverridePriority.Normal) factionPriorities.Remove(k);
                            else factionPriorities[k] = p;
                        });
                    }
                    if (Widgets.ButtonText(removeRect, "Remove"))
                    {
                        toRemove = key;
                    }
                    listing.Gap(4f);
                }
                if (toRemove != null)
                {
                    factionOverrides.Remove(toRemove);
                    factionPriorities.Remove(toRemove);
                }
            }

            listing.Gap(ControlGap);
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
                        }));
                    }
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No remaining factions available", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(4f);
            Rect factionActionRow = listing.GetRect(28f);
            float halfWF = (factionActionRow.width - 8f) / 2f;
            Rect delFactionRect = new Rect(factionActionRow.x, factionActionRow.y, halfWF, factionActionRow.height);
            Rect restoreFactionRect = new Rect(factionActionRow.x + halfWF + 8f, factionActionRow.y, halfWF, factionActionRow.height);

            if (Widgets.ButtonText(delFactionRect, "Delete All Faction Overrides"))
            {
                factionOverrides.Clear();
                factionPriorities.Clear();
            }
            if (Widgets.ButtonText(restoreFactionRect, "Restore Default Faction Overrides"))
            {
                RestoreDefaultFactionOverrides();
            }
        }

        private void DrawXenotypeOverridesSection(Listing_Standard listing)
        {
            Section(listing, "Xenotype Overrides");
            Caption(listing, "Assign custom profiles to specific xenotypes. Xenotype overrides take precedence over Faction, Hostile, and General settings.");

            if (xenotypeOverrides.Count == 0)
            {
                Caption(listing, "No xenotype overrides configured.");
            }
            else
            {
                string toRemove = null;
                var keys = new List<string>(xenotypeOverrides.Keys);
                foreach (var key in keys)
                {
                    var currentProfile = xenotypeOverrides[key];
                    OverridePriority currentPrio = OverridePriority.Normal;
                    if (xenotypePriorities.TryGetValue(key, out var p))
                        currentPrio = p;

                    XenotypeDef def = DefDatabase<XenotypeDef>.GetNamedSilentFail(key);
                    string label = def != null ? def.LabelCap.ToString() : key;

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
                        ProfileMenu(id => xenotypeOverrides[k] = id);
                    }
                    if (Widgets.ButtonText(prioRect, currentPrio.ToString()))
                    {
                        string k = key;
                        PriorityMenu(p => {
                            if (p == OverridePriority.Normal) xenotypePriorities.Remove(k);
                            else xenotypePriorities[k] = p;
                        });
                    }
                    if (Widgets.ButtonText(removeRect, "Remove"))
                    {
                        toRemove = key;
                    }
                    listing.Gap(4f);
                }
                if (toRemove != null)
                {
                    xenotypeOverrides.Remove(toRemove);
                    xenotypePriorities.Remove(toRemove);
                }
            }

            listing.Gap(ControlGap);
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
                        }));
                    }
                }
                if (options.Count == 0)
                {
                    options.Add(new FloatMenuOption("No remaining xenotypes available", null));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            listing.Gap(4f);
            Rect xenoActionRow = listing.GetRect(28f);
            float halfWX = (xenoActionRow.width - 8f) / 2f;
            Rect delXenoRect = new Rect(xenoActionRow.x, xenoActionRow.y, halfWX, xenoActionRow.height);
            Rect restoreXenoRect = new Rect(xenoActionRow.x + halfWX + 8f, xenoActionRow.y, halfWX, xenoActionRow.height);

            if (Widgets.ButtonText(delXenoRect, "Delete All Xenotype Overrides"))
            {
                xenotypeOverrides.Clear();
                xenotypePriorities.Clear();
            }
            if (Widgets.ButtonText(restoreXenoRect, "Restore Default Xenotype Overrides"))
            {
                RestoreDefaultXenotypeOverrides();
            }
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

        private void DrawProfileSelector(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Profile");
            Text.Font = GameFont.Small;

            if (listing.ButtonText(LabelFor(activeProfileId)))
                ProfileMenu(id => { activeProfileId = id; RefreshResolved(); });

            var preset = VarianceProfiles.GetPresetById(activeProfileId);
            string desc = preset != null ? preset.description : VarianceProfiles.CustomDescription;
            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            listing.Label(desc);
            GUI.color = Color.white;

            var customProfile = GetCustomProfile(activeProfileId);
            if (customProfile != null)
            {
                DrawNameField(listing, customProfile);

                if (listing.ButtonText("+ New Custom Profile"))
                {
                    CreateNewCustomProfile();
                }

                if (listing.ButtonText("Duplicate Profile"))
                {
                    DuplicateCurrentProfile();
                }

                if (listing.ButtonText("Reset to Faithful"))
                {
                    customProfile.values = VarianceProfiles.VanillaLike.MakeValues();
                    RefreshResolved();
                }

                if (customProfiles.Count > 1)
                {
                    if (listing.ButtonText("Delete this profile"))
                    {
                        customProfiles.Remove(customProfile);
                        activeProfileId = customProfiles[0].id;
                        RefreshResolved();
                    }
                }
            }
            else
            {
                if (listing.ButtonText("+ New Custom Profile"))
                {
                    CreateNewCustomProfile();
                }

                if (listing.ButtonText("Duplicate Profile"))
                {
                    DuplicateCurrentProfile();
                }
            }
        }

        private void CreateNewCustomProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
            string newName = "Custom " + (customProfiles.Count + 1);
            var profile = new CustomProfile(newId, newName, VarianceProfiles.VanillaLike.MakeValues());
            customProfiles.Add(profile);
            activeProfileId = newId;
            RefreshResolved();
        }

        private void DuplicateCurrentProfile()
        {
            string newId = "custom_" + DateTime.Now.Ticks;
            string newName = LabelFor(activeProfileId) + " Copy";
            var profile = new CustomProfile(newId, newName, Resolve(activeProfileId).Clone());
            customProfiles.Add(profile);
            activeProfileId = newId;
            RefreshResolved();
        }

        private void DrawNameField(Listing_Standard listing, CustomProfile profile)
        {
            if (profile == null) return;
            Rect row = listing.GetRect(28f);
            Rect labelRect = row.LeftPart(0.34f);
            Rect fieldRect = row.RightPart(0.64f);

            Widgets.Label(labelRect, "Profile name");
            profile.name = Widgets.TextField(fieldRect, profile.name ?? string.Empty);
            listing.Gap(ControlGap);
        }

        private void DrawGenerationSettings(Listing_Standard listing)
        {
            var v = Active;

            Section(listing, "Overall quality");
            Caption(listing, "Drives every roll below. Higher quality shifts a pawn toward the top of each range you set.");
            v.averageQuality = LabeledSlider(listing, $"Average pawn quality:  {v.averageQuality:F2}", v.averageQuality, 0f, 1f);
            float meanComposite = CalculateCompositeScore(v.averageQuality, v);
            Caption(listing, $"An average pawn currently reads as: {TierForQuality(meanComposite)} (Overall Power: {meanComposite:F2})");
            DrawQualityDistributionCurve(listing, v);

            Section(listing, "Skills");
            listing.CheckboxLabeled("Enable skill variance", ref v.enableSkillVariance);
            listing.Gap(ControlGap);
            v.skillNoise = LabeledSlider(listing, $"Skill noise (spread between a pawn's own skills):  {v.skillNoise:F2}", v.skillNoise, 0f, 1f);
            Caption(listing, $"Skill shift range (applied on top of vanilla roll):  {v.skillShiftMin:F1} to {v.skillShiftMax:F1}");
            v.skillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {v.skillShiftMin:F1}", v.skillShiftMin, -20f, 20f);
            v.skillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {v.skillShiftMax:F1}", v.skillShiftMax, -20f, 20f);

            if (ModsConfig.BiotechActive)
                DrawChildSkillShift(listing, v);

            Section(listing, "Traits");
            listing.CheckboxLabeled("Enable trait variance", ref v.enableTraitVariance);
            listing.CheckboxLabeled(
                "Count xenotype/forced traits toward the trait count",
                ref v.countProtectedTraits,
                "When off, the range below counts only traits this mod rolls, and traits forced by a xenotype, gene, backstory or scenario are added on top. When on, the range counts every trait the pawn has. Forced traits are never removed either way.");
            listing.Gap(ControlGap);
            Caption(listing, v.countProtectedTraits
                ? $"Total traits on the pawn:  {v.traitCountMin:F0} to {v.traitCountMax:F0}"
                : $"Traits this mod rolls, forced traits added on top:  {v.traitCountMin:F0} to {v.traitCountMax:F0}");
            v.traitCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {v.traitCountMin:F0}", v.traitCountMin, 0f, 15f);
            v.traitCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {v.traitCountMax:F0}", v.traitCountMax, 0f, 15f);

            Section(listing, "Passions");
            listing.CheckboxLabeled("Enable passion variance", ref v.enablePassionVariance);
            listing.Gap(ControlGap);
            v.passionNoise = LabeledSlider(listing, $"Passion noise (how much the total budget varies):  {v.passionNoise:F2}", v.passionNoise, 0f, 1f);
            v.passionMajorBias = LabeledSlider(listing, $"Major passion bias:  {v.passionMajorBias:F2}", v.passionMajorBias, 0f, 1f);
            Caption(listing, "How often the budget is spent on a Major passion instead of a Minor one. Majors always go to the pawn's best skills first.");
            listing.Gap(ControlGap);
            Caption(listing, $"Total passion budget (Minor = 1, Major = 2):  {v.passionCountMin:F0} to {v.passionCountMax:F0}");
            v.passionCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {v.passionCountMin:F0}", v.passionCountMin, 0f, 24f);
            v.passionCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {v.passionCountMax:F0}", v.passionCountMax, 0f, 24f);
            Caption(listing, v.passionCountMin > 0f
                ? "Rolls vary around these target values, but every pawn receives at least one passion."
                : "Minimum is 0, so pawns with no passions are possible.");
        }

        private void DrawChildSkillShift(Listing_Standard listing, VarianceProfileValues v)
        {
            listing.Gap(ControlGap);
            listing.CheckboxLabeled(
                "Also shift skills when a child grows up",
                ref v.applyChildSkillShift,
                "Not recommended. Diverges from vanilla growth mechanics.\n\n"
                + "Vanilla never re-rolls skill levels at age 13. Enabling this shifts skills at that growth moment, so colonists can gain or lose skill levels on their birthday.\n\n"
                + "Traits and passions at 13 are unaffected by this toggle. Requires \"Apply variance to children growing up\" in General settings.");

            bool outerEnabled = GUI.enabled;
            GUI.enabled = outerEnabled && v.applyChildSkillShift;
            listing.Gap(ControlGap);
            Caption(listing, $"Skill shift at age 13 growth moment (hard limit per skill):  {v.childSkillShiftMin:F1} to {v.childSkillShiftMax:F1}");
            v.childSkillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {v.childSkillShiftMin:F1}", v.childSkillShiftMin, -20f, 20f);
            v.childSkillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {v.childSkillShiftMax:F1}", v.childSkillShiftMax, -20f, 20f);
            Caption(listing, v.childSkillShiftMin >= 0f
                ? "The minimum is at or above zero, so growing up can never cost a pawn skill levels."
                : $"The minimum is below zero, so a low-quality pawn can lose up to {-v.childSkillShiftMin:F0} levels in a skill on their birthday.");
            GUI.enabled = outerEnabled;
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
                if (listing.ButtonText(LabelFor(hostileProfileId)))
                    ProfileMenu(id => { hostileProfileId = id; RefreshResolved(); });
                Caption(listing, "Colonists are selected by the player, but raiders arrive directly. Using a separate hostile profile balances raider difficulty independently from your colony.");
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

            listing.Gap(SectionGap);
            if (listing.ButtonText("Reset All Settings"))
                ResetToDefaults();
        }

        private void ResetToDefaults()
        {
            customProfiles = new List<CustomProfile>
            {
                new CustomProfile("custom_1", "Custom 1", VarianceProfiles.VanillaLike.MakeValues())
            };
            activeProfileId = VarianceProfiles.FaithfulId;
            hostileProfileId = VarianceProfiles.DistinctId;
            applyToHostilePawns = true;
            applyVarianceToChildren = true;
            verboseLogging = false;
            enableOverrides = true;
            hasInitializedDefaultOverrides = false;
            factionOverrides.Clear();
            xenotypeOverrides.Clear();
            factionPriorities.Clear();
            xenotypePriorities.Clear();
            PopulateDefaultOverrides(force: true);
            RefreshResolved();
        }

        private static string TierForQuality(float quality)
        {
            if (quality < 0.2f) return "Incompetent";
            if (quality < 0.5f) return "Standard";
            if (quality < 0.8f) return "Specialist";
            return "Prodigy";
        }

        private static float CalculateCompositeScore(float q, VarianceProfileValues v)
        {
            float skillNorm = 0.25f;
            if (v.enableSkillVariance)
            {
                float shift = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, q);
                float avgSkill = Mathf.Clamp(Constants.AssumedVanillaSkillBaseline + shift, 0f, 20f);
                skillNorm = Mathf.Clamp01(avgSkill / 20f);
            }

            float traitNorm = 0.25f;
            if (v.enableTraitVariance)
            {
                float count = Mathf.Lerp(v.traitCountMin, v.traitCountMax, q);
                traitNorm = Mathf.Clamp01(count / 8f);
            }

            float passionNorm = 0.25f;
            if (v.enablePassionVariance)
            {
                float budget = Mathf.Lerp(v.passionCountMin, v.passionCountMax, q);
                float pips = budget * (1f + 0.25f * v.passionMajorBias);
                passionNorm = Mathf.Clamp01(pips / 12f);
            }

            float wS = v.enableSkillVariance ? 1.2f : 0f;
            float wT = v.enableTraitVariance ? 0.8f : 0f;
            float wP = v.enablePassionVariance ? 1.0f : 0f;
            float totalW = wS + wT + wP;

            if (totalW <= 0f) return q;
            return Mathf.Clamp01((wS * skillNorm + wT * traitNorm + wP * passionNorm) / totalW);
        }

        private static float cachedFaithfulBaseline = -1f;

        private static float MapToCenteredX(float compositeScore)
        {
            if (cachedFaithfulBaseline < 0f)
            {
                cachedFaithfulBaseline = CalculateCompositeScore(0.50f, VarianceProfiles.VanillaLike.MakeValues());
            }
            float baseC = cachedFaithfulBaseline;
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

        private static void DrawQualityDistributionCurve(Listing_Standard listing, VarianceProfileValues v)
        {
            listing.Gap(4f);
            Rect rect = listing.GetRect(54f);

            // Dark container background
            Widgets.DrawBoxSolid(rect, new Color(0.08f, 0.09f, 0.11f, 0.85f));
            Widgets.DrawBox(rect, 1);

            // Tier Background Bands (centered at 0.50 for Faithful)
            DrawTierBand(rect, 0.00f, 0.25f, new Color(0.70f, 0.20f, 0.20f, 0.12f)); // Sub-Standard
            DrawTierBand(rect, 0.25f, 0.50f, new Color(0.50f, 0.50f, 0.50f, 0.08f)); // Standard / Below Avg
            DrawTierBand(rect, 0.50f, 0.75f, new Color(0.20f, 0.50f, 0.70f, 0.12f)); // Above Avg
            DrawTierBand(rect, 0.75f, 1.00f, new Color(0.85f, 0.70f, 0.20f, 0.15f)); // Prodigy / Exceptional

            // Vertical Tier Dividers
            DrawVerticalTierMarker(rect, 0.25f);
            DrawVerticalTierMarker(rect, 0.50f); // Center line (Faithful)
            DrawVerticalTierMarker(rect, 0.75f);

            // Sample Beta Distribution and map through Composite Quality Function & Centered Scaling
            v.GetBetaAlphaBeta(out float alpha, out float beta);
            int samples = 70;
            Vector2[] points = new Vector2[samples];
            float maxDensity = 0.001f;

            for (int i = 0; i < samples; i++)
            {
                float q = Mathf.Clamp(0.005f + (i / (float)(samples - 1)) * 0.99f, 0.001f, 0.999f);
                float density = Mathf.Exp((alpha - 1f) * Mathf.Log(q) + (beta - 1f) * Mathf.Log(1f - q));
                float rawComposite = CalculateCompositeScore(q, v);
                float composite = MapToCenteredX(rawComposite);

                if (density > maxDensity) maxDensity = density;
                points[i] = new Vector2(composite, density);
            }

            // Sort points by composite score x-coordinate for smooth rendering
            Array.Sort(points, (a, b) => a.x.CompareTo(b.x));

            // Draw Line Segments
            Color curveColor = new Color(0.35f, 0.85f, 1.00f, 0.95f);
            for (int i = 0; i < samples - 1; i++)
            {
                float x1 = rect.x + points[i].x * rect.width;
                float y1 = rect.yMax - 3f - (points[i].y / maxDensity) * (rect.height - 6f);
                float x2 = rect.x + points[i + 1].x * rect.width;
                float y2 = rect.yMax - 3f - (points[i + 1].y / maxDensity) * (rect.height - 6f);

                Widgets.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), curveColor, 2f);
            }

            // Draw Mean Compound Power Line
            float meanRawComposite = CalculateCompositeScore(v.averageQuality, v);
            float meanComposite = MapToCenteredX(meanRawComposite);
            float meanX = rect.x + meanComposite * rect.width;
            Widgets.DrawLine(new Vector2(meanX, rect.y), new Vector2(meanX, rect.yMax), Color.yellow, 1.5f);

            listing.Gap(ControlGap);
        }

        private static void DrawTierBand(Rect rect, float startFrac, float endFrac, Color color)
        {
            float xMin = rect.x + startFrac * rect.width;
            float width = (endFrac - startFrac) * rect.width;
            Rect bandRect = new Rect(xMin, rect.y, width, rect.height);
            Widgets.DrawBoxSolid(bandRect, color);
        }

        private static void DrawVerticalTierMarker(Rect rect, float frac)
        {
            float x = rect.x + frac * rect.width;
            Widgets.DrawLine(new Vector2(x, rect.y), new Vector2(x, rect.yMax), new Color(1f, 1f, 1f, 0.15f), 1f);
        }
    }
}
