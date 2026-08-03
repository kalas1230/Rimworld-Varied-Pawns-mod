using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public partial class PawnVarianceSettings
    {
        private void DrawProfileEditorTab(Rect outRect)
        {
            float viewHeight = Math.Max(profileEditorViewHeight, 1000f);
            var viewRect = new Rect(0f, 0f, outRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(outRect, ref profileEditorScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            DrawProfileSelector(listing);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            profileEditorViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
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

            if (v.applyChildSkillShift)
            {
                listing.Gap(ControlGap);
                Caption(listing, $"Skill shift at age 13 growth moment (hard limit per skill):  {v.childSkillShiftMin:F1} to {v.childSkillShiftMax:F1}");
                v.childSkillShiftMin = LabeledSlider(listing, $"Lowest-quality pawn shift:  {v.childSkillShiftMin:F1}", v.childSkillShiftMin, -20f, 20f);
                v.childSkillShiftMax = LabeledSlider(listing, $"Highest-quality pawn shift:  {v.childSkillShiftMax:F1}", v.childSkillShiftMax, -20f, 20f);
                Caption(listing, v.childSkillShiftMin >= 0f
                    ? "The minimum is at or above zero, so growing up can never cost a pawn skill levels."
                    : $"The minimum is below zero, so a low-quality pawn can lose up to {-v.childSkillShiftMin:F0} levels in a skill on their birthday.");
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
