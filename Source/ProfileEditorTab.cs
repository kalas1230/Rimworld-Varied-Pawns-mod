using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public partial class PawnVarianceSettings
    {
        // Stable, unique control ids. Widgets.FloatRange uses the caller-supplied id
        // against its own static drag-tracking state, NOT GUIUtility.GetControlID
        // auto-indexing -- so conditionally rendering the child-shift block cannot
        // shift these or hijack an in-progress drag. Never reuse or auto-generate them.
        private const int SkillShiftRangeId      = 1001;
        private const int ChildSkillShiftRangeId = 1002;
        private const int TraitCountRangeId      = 1003;
        private const int PassionCountRangeId    = 1004;

        private const float RangeRowHeight = 32f;
        private const float RangeLabelFrac = 0.42f;

        private static void LabeledFloatRange(
            Listing_Standard listing, string label, int id,
            ref float lo, ref float hi, float min, float max,
            ToStringStyle style, string tooltip = null)
        {
            Rect row = listing.GetRect(RangeRowHeight);
            Rect labelRect = row.LeftPart(RangeLabelFrac);
            Rect rangeRect = row.RightPart(1f - RangeLabelFrac - 0.02f);

            // Vertically centre the label against the range control.
            labelRect.y += 4f;
            Widgets.Label(labelRect, label);

            // Marshal into a local: an r-value struct cannot be passed by ref.
            var range = new FloatRange(lo, hi);
            Widgets.FloatRange(rangeRect, id, ref range, min, max, null, style);
            lo = range.min;
            hi = range.max;

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(ControlGap);
        }

        private static void SectionHeader(
            Listing_Standard listing, string title, ref bool enabled, string tooltip = null)
        {
            listing.Gap(SectionGap);
            listing.GapLine(0f);

            Rect row = listing.GetRect(30f);
            Rect titleRect = row.LeftPart(0.70f);
            Rect boxRect = row.RightPart(0.28f);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            Widgets.CheckboxLabeled(boxRect, "Enable", ref enabled);

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(4f);
        }

        private const float HeaderHeight = 140f;
        private const float HeaderGutter = 8f;
        private const float CurveHeight = 54f;

        private void DrawProfileEditorTab(Rect outRect)
        {
            Rect headerRect = new Rect(outRect.x, outRect.y, outRect.width, HeaderHeight);
            DrawProfileEditorHeader(headerRect);

            Rect bodyRect = new Rect(
                outRect.x,
                headerRect.yMax + HeaderGutter,
                outRect.width,
                outRect.height - HeaderHeight - HeaderGutter);

            float viewHeight = Math.Max(profileEditorViewHeight, bodyRect.height);
            var viewRect = new Rect(0f, 0f, bodyRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(bodyRect, ref profileEditorScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            bool wasEnabled = GUI.enabled;
            GUI.enabled = wasEnabled && EditingCustom;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            profileEditorViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawProfileEditorHeader(Rect rect)
        {
            var v = Active;
            bool outerEnabled = GUI.enabled;

            // Row 1: profile picker + action strip.
            Rect pickerRow = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect pickerRect = new Rect(pickerRow.x, pickerRow.y, 240f, 28f);
            if (Widgets.ButtonText(pickerRect, LabelFor(activeProfileId)))
                ProfileMenu(id => { activeProfileId = id; RefreshResolved(); });

            var customProfile = GetCustomProfile(activeProfileId);
            float stripX = pickerRect.xMax + 10f;
            float stripW = pickerRow.xMax - stripX;
            float btnW = (stripW - 4f * 6f) / 5f;

            Rect NextBtn(int i) => new Rect(stripX + i * (btnW + 6f), pickerRow.y, btnW, 28f);

            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (Widgets.ButtonText(NextBtn(0), "+ New"))
                CreateNewCustomProfile();
            GUI.color = Color.white;

            if (Widgets.ButtonText(NextBtn(1), "Duplicate"))
                DuplicateCurrentProfile();

            GUI.enabled = outerEnabled && customProfile != null;

            if (Widgets.ButtonText(NextBtn(2), "Rename") && customProfile != null)
                Find.WindowStack.Add(new Dialog_RenameProfile(customProfile, RefreshResolved));

            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (Widgets.ButtonText(NextBtn(3), "Reset") && customProfile != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "Reset this profile to Faithful? All of its current values will be replaced.",
                    () =>
                    {
                        customProfile.values = VarianceProfiles.VanillaLike.MakeValues();
                        RefreshResolved();
                    },
                    destructive: false));
            }
            GUI.color = Color.white;

            GUI.enabled = outerEnabled && customProfile != null && customProfiles.Count > 1;
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(NextBtn(4), "Delete") && customProfile != null && customProfiles.Count > 1)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    $"Delete the profile \"{customProfile.name}\"? This cannot be undone.",
                    () =>
                    {
                        customProfiles.Remove(customProfile);
                        if (customProfiles.Count > 0)
                            activeProfileId = customProfiles[0].id;
                        RefreshResolved();
                    },
                    destructive: true));
            }
            GUI.color = Color.white;
            GUI.enabled = outerEnabled;

            // Row 2: authored prose for presets, generated fingerprint for customs.
            // Constant height, and never empty in either state, so the header cannot
            // shift when switching profiles.
            Rect descRow = new Rect(rect.x, pickerRow.yMax + 4f, rect.width, 20f);
            var preset = VarianceProfiles.GetPresetById(activeProfileId);
            string descText = (preset != null && !string.IsNullOrEmpty(preset.description))
                ? preset.description
                : ProfileFingerprint(v);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            // All nine shipped descriptions fit whole at this width (longest is 122
            // chars, ~630-730px at Tiny against ~840px). Truncate is a safety net for
            // long localizations and unusually long fingerprints only.
            string truncatedDesc = descText.Truncate(descRow.width);
            Widgets.Label(descRow, string.IsNullOrEmpty(truncatedDesc) ? descText : truncatedDesc);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(descRow, descText);

            // Row 3: quality slider + tier/power readout.
            Rect qualityRow = new Rect(rect.x, descRow.yMax + 2f, rect.width, 28f);
            Rect qLabel = qualityRow.LeftPart(0.26f);
            Rect qSlider = new Rect(qualityRow.x + rect.width * 0.27f, qualityRow.y + 3f, rect.width * 0.40f, 22f);
            Rect qReadout = qualityRow.RightPart(0.30f);

            Widgets.Label(qLabel, EditingCustom
                ? $"Average pawn quality:  {v.averageQuality:F2}"
                : $"Average pawn quality:  {v.averageQuality:F2}  (read-only)");

            GUI.enabled = outerEnabled && EditingCustom;
            v.averageQuality = Widgets.HorizontalSlider(qSlider, v.averageQuality, 0f, 1f);
            GUI.enabled = outerEnabled;

            Rect qLabelAndSlider = qualityRow.LeftPart(0.67f);
            TooltipHandler.TipRegion(qLabelAndSlider,
                "Drives every roll below. Higher quality shifts a pawn toward the top of each range you set.");

            // The readout is output, not input -- always full opacity, even on a
            // read-only preset, so presets stay comparable by cycling the picker.
            float meanComposite = CalculateCompositeScore(v.averageQuality, v);
            Widgets.Label(qReadout, $"→  {TierForQuality(meanComposite)} ({meanComposite:F2})");
            TooltipHandler.TipRegion(qReadout,
                "Overall Power: the composite score every profile is calibrated against. "
                + "Faithful's baseline is 0.31.");

            // Row 4: the distribution curve, full width, never greyed.
            Rect curveRect = new Rect(rect.x, qualityRow.yMax + 4f, rect.width, CurveHeight);
            DrawQualityDistributionCurve(curveRect, v);
        }

        // One-line summary of a custom profile's values. Never returns empty.
        private static string ProfileFingerprint(VarianceProfileValues v)
        {
            return string.Format(
                "Traits {0:F0}–{1:F0}  ·  Passions {2:F1}–{3:F1}  ·  Skill shift {4:F1} to {5:F1}  ·  Quality {6:F2}",
                v.traitCountMin, v.traitCountMax,
                v.passionCountMin, v.passionCountMax,
                v.skillShiftMin, v.skillShiftMax,
                v.averageQuality);
        }

        private void DrawGenerationSettings(Listing_Standard listing)
        {
            var v = Active;

            SectionHeader(listing, "Skills", ref v.enableSkillVariance,
                "When off, this profile leaves vanilla skill levels untouched.");
            Rect noiseRow = listing.GetRect(28f);
            Rect noiseLabelRect = noiseRow.LeftPart(0.42f);
            // Vertically centre the label against the range control.
            noiseLabelRect.y += 4f;
            Widgets.Label(noiseLabelRect, $"Skill noise:  {v.skillNoise:F2}");
            v.skillNoise = Widgets.HorizontalSlider(noiseRow.RightPart(0.56f), v.skillNoise, 0f, 1f);
            TooltipHandler.TipRegion(noiseRow, "How widely a single pawn's own skills spread apart from each other.");
            listing.Gap(ControlGap);
            LabeledFloatRange(listing, "Skill shift", SkillShiftRangeId,
                ref v.skillShiftMin, ref v.skillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                "Applied on top of the vanilla roll. The low handle is the shift for the lowest-quality pawn, the high handle for the highest-quality pawn.");

            if (ModsConfig.BiotechActive)
                DrawChildSkillShift(listing, v);

            SectionHeader(listing, "Traits", ref v.enableTraitVariance,
                "When off, this profile leaves vanilla trait generation untouched.");
            listing.CheckboxLabeled(
                "Count xenotype/forced traits toward the trait count",
                ref v.countProtectedTraits,
                "When off, the range below counts only traits this mod rolls, and traits forced by a xenotype, gene, backstory or scenario are added on top. When on, the range counts every trait the pawn has. Forced traits are never removed either way.");
            listing.Gap(ControlGap);
            LabeledFloatRange(listing, "Trait count", TraitCountRangeId,
                ref v.traitCountMin, ref v.traitCountMax, 0f, 15f, ToStringStyle.Integer,
                v.countProtectedTraits
                    ? "Total traits on the pawn, including xenotype and forced traits."
                    : "Traits this mod rolls. Xenotype, gene, backstory and scenario traits are added on top.");

            SectionHeader(listing, "Passions", ref v.enablePassionVariance,
                "When off, this profile leaves vanilla passion assignment untouched.");
            Rect passionRow = listing.GetRect(28f);
            Rect leftHalf = passionRow.LeftPart(0.48f);
            Rect rightHalf = passionRow.RightPart(0.48f);

            Rect passionNoiseLabelRect = leftHalf.LeftPart(0.52f);
            // Vertically centre the label against the range control.
            passionNoiseLabelRect.y += 4f;
            Widgets.Label(passionNoiseLabelRect, $"Passion noise:  {v.passionNoise:F2}");
            v.passionNoise = Widgets.HorizontalSlider(leftHalf.RightPart(0.46f), v.passionNoise, 0f, 1f);
            TooltipHandler.TipRegion(leftHalf, "How much the total passion budget varies between pawns.");

            Rect majorBiasLabelRect = rightHalf.LeftPart(0.52f);
            // Vertically centre the label against the range control.
            majorBiasLabelRect.y += 4f;
            Widgets.Label(majorBiasLabelRect, $"Major bias:  {v.passionMajorBias:F2}");
            v.passionMajorBias = Widgets.HorizontalSlider(rightHalf.RightPart(0.46f), v.passionMajorBias, 0f, 1f);
            TooltipHandler.TipRegion(rightHalf, "How often the budget is spent on a Major passion instead of a Minor one. Majors always go to the pawn's best skills first.");

            listing.Gap(ControlGap);
            LabeledFloatRange(listing, "Passion budget", PassionCountRangeId,
                ref v.passionCountMin, ref v.passionCountMax, 0f, 24f, ToStringStyle.FloatOne,
                "Minor passion = 1, Major passion = 2. Presets use fractional budgets, so this reads to one decimal.");
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
                LabeledFloatRange(listing, "Child shift at 13", ChildSkillShiftRangeId,
                    ref v.childSkillShiftMin, ref v.childSkillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                    "Hard limit per skill at the age-13 growth moment.");
                Caption(listing, v.childSkillShiftMin >= 0f
                    ? "The minimum is at or above zero, so growing up can never cost a pawn skill levels."
                    : $"The minimum is below zero, so a low-quality pawn can lose up to {-v.childSkillShiftMin:F0} levels in a skill on their birthday.");
            }
        }

        private static void DrawQualityDistributionCurve(Rect rect, VarianceProfileValues v)
        {
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
