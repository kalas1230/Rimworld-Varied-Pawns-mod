using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public class PawnVarianceSettings : ModSettings
    {
        // Effective generation values. These are a mirror of the active profile — a preset's recipe
        // when a preset is selected, the player's own numbers when Custom is. Every applier reads
        // them directly, so nothing downstream has to know profiles exist.
        public float averageQuality = 0.5f;
        public float skillNoise = 0.35f;
        public float passionNoise = 0.35f;
        public float passionMajorBias = 0.8f;
        public float skillShiftMin = -4f;
        public float skillShiftMax = 6f;
        public float traitCountMin = 1f;
        public float traitCountMax = 6f;
        public float passionCountMin = 1f;
        public float passionCountMax = 7f;
        public bool enableSkillVariance = true;
        public bool enableTraitVariance = true;
        public bool enablePassionVariance = true;

        // When false (default), the trait count sliders mean "traits this mod rolls" and protected
        // traits (gene/xenotype, kindDef-, backstory-, request- and scenario-forced) sit on top as a
        // floor. When true, the sliders mean "total traits on the pawn" and protected traits are
        // counted against that total. Either way protected traits are never removed, so a pawn can
        // still exceed the target when its protected set alone is larger.
        public bool countProtectedTraits = false;

        // Housekeeping preferences: deliberately outside the profile system, so switching profiles
        // never silently re-enables logging or changes whether raiders get variance.
        public bool applyToHostilePawns = true;
        public bool applyVarianceOnGrowUp = true;
        public bool verboseLogging = false;
        public bool showQualityTier = true;

        public VarianceProfileId activeProfile = VarianceProfileId.Custom;

        // The player's own tuning, kept intact while a preset is selected so switching to a preset
        // and back is lossless.
        private VarianceProfileValues customValues = VarianceProfiles.NewCustomDefaults();

        private bool betaCacheDirty = true;
        private Vector2 scrollPosition = Vector2.zero;
        private float cachedAlpha, cachedBeta;

        private bool EditingCustom => activeProfile == VarianceProfileId.Custom;

        public PawnVarianceSettings()
        {
            // A fresh install never reaches PostLoadInit, so the effective fields would otherwise
            // keep their declared initializers while Custom holds the Faithful recipe. Sync now so
            // "Custom starts as Faithful" is true from the very first launch.
            ApplyActiveProfile();
        }

        public override void ExposeData()
        {
            base.ExposeData();

            // Custom's values keep the pre-profile node names, so a settings file written by an
            // older build of the mod loads into Custom — which is also the profile it will be on,
            // since a missing activeProfile node defaults to Custom.
            customValues.ExposeData();

            Scribe_Values.Look(ref activeProfile, "activeProfile", VarianceProfileId.Custom);
            Scribe_Values.Look(ref applyToHostilePawns, "applyToHostilePawns", true);
            Scribe_Values.Look(ref applyVarianceOnGrowUp, "applyVarianceOnGrowUp", true);
            Scribe_Values.Look(ref verboseLogging, "verboseLogging", false);
            Scribe_Values.Look(ref showQualityTier, "showQualityTier", true);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (customValues == null) customValues = VarianceProfiles.NewCustomDefaults();
                customValues.ClampAndSwap();
                ApplyActiveProfile();
            }
        }

        // Copies the active profile's values into the effective fields the appliers read.
        private void ApplyActiveProfile()
        {
            var preset = VarianceProfiles.GetPreset(activeProfile);
            AdoptValues(preset != null ? preset.MakeValues() : customValues);
        }

        private void AdoptValues(VarianceProfileValues v)
        {
            averageQuality = v.averageQuality;
            skillNoise = v.skillNoise;
            passionNoise = v.passionNoise;
            passionMajorBias = v.passionMajorBias;
            skillShiftMin = v.skillShiftMin;
            skillShiftMax = v.skillShiftMax;
            traitCountMin = v.traitCountMin;
            traitCountMax = v.traitCountMax;
            passionCountMin = v.passionCountMin;
            passionCountMax = v.passionCountMax;
            enableSkillVariance = v.enableSkillVariance;
            enableTraitVariance = v.enableTraitVariance;
            enablePassionVariance = v.enablePassionVariance;
            countProtectedTraits = v.countProtectedTraits;
            betaCacheDirty = true;
        }

        private VarianceProfileValues CaptureEffectiveValues() => new VarianceProfileValues
        {
            averageQuality = averageQuality,
            skillNoise = skillNoise,
            passionNoise = passionNoise,
            passionMajorBias = passionMajorBias,
            skillShiftMin = skillShiftMin,
            skillShiftMax = skillShiftMax,
            traitCountMin = traitCountMin,
            traitCountMax = traitCountMax,
            passionCountMin = passionCountMin,
            passionCountMax = passionCountMax,
            enableSkillVariance = enableSkillVariance,
            enableTraitVariance = enableTraitVariance,
            enablePassionVariance = enablePassionVariance,
            countProtectedTraits = countProtectedTraits,
        };

        private void SelectProfile(VarianceProfileId id)
        {
            if (EditingCustom) customValues = CaptureEffectiveValues();
            activeProfile = id;
            ApplyActiveProfile();
        }

        // "Copy to Custom": takes whatever preset is on screen and makes it the player's editable
        // starting point, rather than forcing them to re-type a preset's numbers by hand.
        private void ForkPresetIntoCustom()
        {
            customValues = CaptureEffectiveValues();
            activeProfile = VarianceProfileId.Custom;
            betaCacheDirty = true;
        }

        // Edge Case 10: Write() from the settings window marks the Beta cache dirty; trait score
        // bounds are untouched here (they rebuild only on mod-list change, in TraitDesirabilityCache)
        public void MarkDirtyOnWrite()
        {
            if (EditingCustom)
            {
                customValues = CaptureEffectiveValues();
                customValues.ClampAndSwap();
            }
            ApplyActiveProfile();
        }

        public void GetBetaAlphaBeta(out float alpha, out float beta)
        {
            if (betaCacheDirty)
            {
                float m = Mathf.Clamp(averageQuality, Constants.QualityClampEpsilon, 1f - Constants.QualityClampEpsilon);
                cachedAlpha = m * Constants.BetaConcentrationK;
                cachedBeta = (1f - m) * Constants.BetaConcentrationK;
                betaCacheDirty = false;
            }
            alpha = cachedAlpha;
            beta = cachedBeta;
        }

        // Vertical rhythm. Sliders sit flush against their own label by design (they belong
        // together), so all the visible separation between one control and the next comes from
        // these — without them a column of sliders reads as one undifferentiated block.
        private const float SectionGap = 14f;   // before a section's rule
        private const float ControlGap = 10f;   // between two controls inside a section
        private const float SliderLabelGap = 2f;

        public void DoWindowContents(Rect inRect)
        {
            const float viewHeight = 1500f; // generous fixed estimate; content is scrollable so overshoot is harmless
            var outRect = inRect;
            var viewRect = new Rect(0f, 0f, inRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref scrollPosition, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            DrawProfileSelector(listing);

            // Presets are recipes, not starting points: their sliders render read-only so a stray
            // drag can't leave the pawn generation silently off-preset while the dropdown still
            // claims otherwise. Editing goes through Custom.
            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            DrawGlobalSettings(listing);

            listing.End();
            Widgets.EndScrollView();
        }

        // A titled section break: blank space, a rule, then the heading. Used between every
        // top-level group so skills/traits/passions can't visually bleed into each other.
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

        // Label directly above its own slider, with the gap after rather than between, so the pair
        // reads as one control instead of the label floating between two sliders.
        private static float LabeledSlider(Listing_Standard listing, string label, float value, float min, float max)
        {
            listing.Label(label);
            listing.Gap(SliderLabelGap);
            float result = listing.Slider(value, min, max);
            listing.Gap(ControlGap);
            return result;
        }

        private void DrawProfileSelector(Listing_Standard listing)
        {
            Text.Font = GameFont.Medium;
            listing.Label("Profile");
            Text.Font = GameFont.Small;

            if (listing.ButtonText(VarianceProfiles.LabelFor(activeProfile)))
            {
                var options = new List<FloatMenuOption>
                {
                    new FloatMenuOption(VarianceProfiles.CustomLabel, () => SelectProfile(VarianceProfileId.Custom))
                };
                foreach (var preset in VarianceProfiles.Presets)
                {
                    var captured = preset;
                    options.Add(new FloatMenuOption(captured.label, () => SelectProfile(captured.id)));
                }
                Find.WindowStack.Add(new FloatMenu(options));
            }

            GUI.color = new Color(1f, 1f, 1f, 0.7f);
            listing.Label(VarianceProfiles.DescriptionFor(activeProfile));
            GUI.color = Color.white;

            if (EditingCustom)
            {
                if (listing.ButtonText("Reset Custom to Faithful"))
                {
                    customValues = VarianceProfiles.NewCustomDefaults();
                    ApplyActiveProfile();
                }
            }
            else if (listing.ButtonText("Copy this profile to Custom"))
            {
                ForkPresetIntoCustom();
            }
        }

        private void DrawGenerationSettings(Listing_Standard listing)
        {
            Section(listing, "Overall quality");
            Caption(listing, "Drives every roll below. Higher quality shifts a pawn toward the top of each range you set.");
            averageQuality = LabeledSlider(listing, $"Average pawn quality:  {averageQuality:F2}", averageQuality, 0f, 1f);
            Caption(listing, $"An average pawn currently reads as: {TierUtility.TierForQuality(averageQuality)}");

            Section(listing, "Skills");
            listing.CheckboxLabeled("Enable skill variance", ref enableSkillVariance);
            listing.Gap(ControlGap);
            skillNoise = LabeledSlider(listing, $"Skill noise (spread between a pawn's own skills):  {skillNoise:F2}", skillNoise, 0f, 1f);
            Caption(listing, $"Skill shift range — applied on top of the level vanilla rolled:  {skillShiftMin:F1} to {skillShiftMax:F1}");
            skillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {skillShiftMin:F1}", skillShiftMin, -20f, 20f);
            skillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {skillShiftMax:F1}", skillShiftMax, -20f, 20f);

            Section(listing, "Traits");
            listing.CheckboxLabeled("Enable trait variance", ref enableTraitVariance);
            listing.CheckboxLabeled(
                "Count xenotype/forced traits toward the trait count",
                ref countProtectedTraits,
                "When off, the range below counts only traits this mod rolls, and traits forced by a xenotype, gene, backstory or scenario are added on top. When on, the range counts every trait the pawn has. Forced traits are never removed either way.");
            listing.Gap(ControlGap);
            Caption(listing, countProtectedTraits
                ? $"Total traits on the pawn:  {traitCountMin:F0} to {traitCountMax:F0}"
                : $"Traits this mod rolls, forced traits added on top:  {traitCountMin:F0} to {traitCountMax:F0}");
            traitCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {traitCountMin:F0}", traitCountMin, 0f, 15f);
            traitCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {traitCountMax:F0}", traitCountMax, 0f, 15f);

            Section(listing, "Passions");
            listing.CheckboxLabeled("Enable passion variance", ref enablePassionVariance);
            listing.Gap(ControlGap);
            passionNoise = LabeledSlider(listing, $"Passion noise (how much the total budget varies):  {passionNoise:F2}", passionNoise, 0f, 1f);
            passionMajorBias = LabeledSlider(listing, $"Major passion bias:  {passionMajorBias:F2}", passionMajorBias, 0f, 1f);
            Caption(listing, "How often the budget is spent on a Major passion instead of a Minor one. Majors always go to the pawn's best skills first.");
            listing.Gap(ControlGap);
            Caption(listing, $"Total passion strength — Minor passion = 1, Major passion = 2:  {passionCountMin:F0} to {passionCountMax:F0}");
            passionCountMin = LabeledSlider(listing, $"Lowest-quality pawn:  {passionCountMin:F0}", passionCountMin, 0f, 24f);
            passionCountMax = LabeledSlider(listing, $"Highest-quality pawn:  {passionCountMax:F0}", passionCountMax, 0f, 24f);
            Caption(listing, passionCountMin > 0f
                ? "These are averages, and the roll varies around them — but every pawn is guaranteed at least one passion, as in vanilla."
                : "Minimum is 0, so pawns with no passion at all are possible.");
        }

        private void DrawGlobalSettings(Listing_Standard listing)
        {
            Section(listing, "General");
            Caption(listing, "These apply to every profile and are not changed by switching profiles.");
            listing.CheckboxLabeled("Show quality tier hover tooltip", ref showQualityTier);
            listing.CheckboxLabeled("Apply to hostile-faction pawns", ref applyToHostilePawns);
            if (ModsConfig.BiotechActive)
                listing.CheckboxLabeled("Apply variance on grow-up (Biotech)", ref applyVarianceOnGrowUp);
            listing.CheckboxLabeled(
                "Verbose logging (dev mode)",
                ref verboseLogging,
                "Rethrows exceptions instead of swallowing them, and logs a per-pawn breakdown of how passions were assigned. Leave off for normal play.");

            listing.Gap(SectionGap);
            if (listing.ButtonText("Reset All Settings"))
                ResetToDefaults();
        }

        private void ResetToDefaults()
        {
            customValues = VarianceProfiles.NewCustomDefaults();
            activeProfile = VarianceProfileId.Custom;
            applyToHostilePawns = true;
            applyVarianceOnGrowUp = true;
            verboseLogging = false;
            showQualityTier = true;
            ApplyActiveProfile();
        }
    }
}
