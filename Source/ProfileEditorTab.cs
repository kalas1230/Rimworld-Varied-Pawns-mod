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
            if (PawnVarianceMod.Settings.EditingCustom)
            {
                lo = range.min;
                hi = range.max;
            }

            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(ControlGap);
        }

        // Draws a section title, the two enable checkboxes, the reason-for-greying caption, and
        // then SETS GUI.enabled FOR EVERYTHING BELOW IT until the next header. That last part is
        // the point of this method: before it existed, no GUI.enabled write in this file consulted
        // the section enable flags at all, so unticking Traits on a custom profile left the trait
        // sliders fully draggable while doing nothing.
        //
        // outerEnabled is the caller's true GUI.enabled, captured BEFORE any narrowing.
        private static void SectionHeader(
            Listing_Standard listing, string title, bool outerEnabled,
            ref bool enabled, ref bool modWide, string tooltip = null)
        {
            GUI.enabled = outerEnabled;

            listing.Gap(SectionGap);
            listing.GapLine(0f);

            Rect row = listing.GetRect(30f);
            Rect titleRect = row.LeftPart(0.44f);
            Rect modWideRect = new Rect(row.x + row.width * 0.46f, row.y, row.width * 0.26f, row.height);
            Rect boxRect = row.RightPart(0.26f);

            Text.Font = GameFont.Medium;
            Widgets.Label(titleRect, title);
            Text.Font = GameFont.Small;

            // BOTH checkboxes sit outside the grey they control, for two different reasons.
            // The mod-wide box: it is a mod-wide setting, so it must stay clickable while the body
            // is greyed for a preset -- otherwise it is dead on exactly the eight profiles this
            // feature exists to rescue. It is therefore NOT gated on EditingCustom.
            bool modWideVal = modWide;
            Widgets.CheckboxLabeled(modWideRect, "VP_ModWide".Translate(), ref modWideVal);
            modWide = modWideVal;

            // The per-profile box: if it lived inside the grey it controls, unticking it would lock
            // the player out of re-ticking it. Still gated on EditingCustom, since it edits profile
            // data. GUI.enabled AND the write guard, matching every other editable control here.
            bool editingCustom = PawnVarianceMod.Settings.EditingCustom;
            GUI.enabled = outerEnabled && editingCustom;
            bool val = enabled;
            Widgets.CheckboxLabeled(boxRect, "VP_Enable".Translate(), ref val);
            if (editingCustom) enabled = val;

            GUI.enabled = outerEnabled;
            if (!tooltip.NullOrEmpty())
                TooltipHandler.TipRegion(row, tooltip);

            listing.Gap(4f);

            // Why the body below is greyed, in precedence order. Mod-wide wins the message because
            // it wins the behaviour: when it is off, the profile flag genuinely is ignored. Drawn
            // greyed itself, matching the VP_EnableOverridesHint pattern on the Overrides tab.
            if (!modWide || !enabled)
            {
                GUI.enabled = false;
                Caption(listing, !modWide
                    ? "VP_ModWideOffHint".Translate()
                    : "VP_ProfileOffHint".Translate());
            }

            // THE GREYING RULE. EditingCustom is re-applied here because Step 2 removed it from the
            // caller -- drop it and every slider becomes editable on a preset, silently mutating
            // the resolved values a preset hands out.
            GUI.enabled = outerEnabled && editingCustom && modWide && enabled;
        }

        // Rows: 28 (picker) + 4 + 20 (description) + 2 + 28 (quality) + 2 + 20 (best-of-N) + 4
        // + 54 (curve) = 162. Was 140 before the best-of-N row. The body scrolls, so header
        // height costs scrolled space, not visible content.
        private const float HeaderHeight = 162f;
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

            float viewHeight = Math.Max(profileEditorViewHeight, 580f);
            var viewRect = new Rect(0f, 0f, bodyRect.width - 24f, viewHeight);

            Widgets.BeginScrollView(bodyRect, ref profileEditorScrollPos, viewRect);
            var listing = new Listing_Standard();
            listing.Begin(viewRect);

            // GUI.enabled is deliberately NOT narrowed by EditingCustom here any more.
            // DrawGenerationSettings and SectionHeader own it now: SectionHeader has to draw its
            // mod-wide checkbox at the true outer state, since that box is a MOD-WIDE setting and
            // must stay clickable while a preset is being viewed -- which is exactly what a blanket
            // narrow here forbade. Every control below a header is still greyed on a preset;
            // SectionHeader re-applies EditingCustom itself as part of the greying rule.
            bool wasEnabled = GUI.enabled;
            DrawGenerationSettings(listing);
            GUI.enabled = wasEnabled;

            profileEditorViewHeight = listing.CurHeight + 40f;
            listing.End();
            Widgets.EndScrollView();
        }

        private void DrawProfileEditorHeader(Rect rect)
        {
            var v = Editing;
            bool outerEnabled = GUI.enabled;

            // Row 1: profile picker + action strip.
            Rect pickerRow = new Rect(rect.x, rect.y, rect.width, 28f);
            Rect pickerRect = new Rect(pickerRow.x, pickerRow.y, 240f, 28f);
            if (Widgets.ButtonText(pickerRect, LabelFor(EditorProfileId)))
                ProfileMenu(SetEditorProfile);

            var customProfile = GetCustomProfile(EditorProfileId);
            float stripX = pickerRect.xMax + 10f;
            float stripW = pickerRow.xMax - stripX;
            float btnW = (stripW - 4f * 6f) / 5f;

            Rect NextBtn(int i) => new Rect(stripX + i * (btnW + 6f), pickerRow.y, btnW, 28f);

            GUI.color = new Color(0.4f, 0.85f, 0.4f);
            if (Widgets.ButtonText(NextBtn(0), "VP_Btn_New".Translate()))
                CreateNewCustomProfile();
            GUI.color = Color.white;

            if (Widgets.ButtonText(NextBtn(1), "VP_Btn_Duplicate".Translate()))
                DuplicateCurrentProfile();

            GUI.enabled = outerEnabled && customProfile != null;

            if (Widgets.ButtonText(NextBtn(2), "VP_Btn_Rename".Translate()) && customProfile != null)
                Find.WindowStack.Add(new Dialog_RenameProfile(customProfile, () =>
                {
                    RefreshEditor();
                    RefreshResolved();
                }));

            GUI.color = new Color(0.9f, 0.75f, 0.3f);
            if (Widgets.ButtonText(NextBtn(3), "VP_Btn_Reset".Translate()) && customProfile != null)
            {
                // The preset name is substituted rather than written into the sentence: the reset
                // target is VanillaLike below, so naming it from the same object keeps the two
                // from drifting apart, and a translator gets the localised preset name for free.
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "VP_Confirm_ResetProfile".Translate(VarianceProfiles.VanillaLike.label),
                    () =>
                    {
                        customProfile.values = VarianceProfiles.VanillaLike.MakeValues();
                        // The cached editing reference points at the object we just replaced.
                        RefreshEditor();
                        RefreshResolved();
                    },
                    destructive: false));
            }
            GUI.color = Color.white;

            GUI.enabled = outerEnabled && customProfile != null;
            GUI.color = new Color(1f, 0.4f, 0.4f);
            if (Widgets.ButtonText(NextBtn(4), "VP_Btn_Delete".Translate()) && customProfile != null)
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(
                    "VP_Confirm_DeleteProfile".Translate(customProfile.name),
                    () =>
                    {
                        string deletedId = customProfile.id;
                        customProfiles.Remove(customProfile);

                        // The editor moves to whatever is left.
                        SetEditorProfile(customProfiles != null && customProfiles.Count > 0
                            ? customProfiles[0].id
                            : VarianceProfiles.FaithfulId);

                        // The deleted profile may ALSO have been in use as the colony profile, the
                        // hostile profile, or in an override map. Before the editor cursor was split
                        // out, the colony case was handled implicitly because they were one field.
                        // Now it has to be explicit or the settings keep a dangling id.
                        if (activeProfileId == deletedId) activeProfileId = VarianceProfiles.FaithfulId;
                        if (hostileProfileId == deletedId) hostileProfileId = VarianceProfiles.DistinctId;

                        ScrubStaleOverrides(factionOverrides, factionPriorities, deletedId);
                        ScrubStaleOverrides(xenotypeOverrides, xenotypePriorities, deletedId);
                        ScrubStaleOverrides(raceOverrides, racePriorities, deletedId);

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
            var preset = VarianceProfiles.GetPresetById(EditorProfileId);
            string descText = (preset != null && !string.IsNullOrWhiteSpace(preset.description))
                ? preset.description
                : ProfileFingerprint(v);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.65f);
            // All nine shipped descriptions fit whole at this width (longest is 122
            // chars, ~630-730px at Tiny against ~840px). Truncate is a safety net for
            // long localizations and unusually long fingerprints only.
            string truncatedDesc = descText.Truncate(descRow.width);
            // Row 2 has a fixed 20px height that the rest of the header's geometry
            // depends on. Word-wrap must be off so an overlong string clips to one
            // line instead of wrapping to a second and overlapping the rows below.
            bool prevWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(descRow, string.IsNullOrEmpty(truncatedDesc) ? descText : truncatedDesc);
            Text.WordWrap = prevWordWrap;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(descRow, descText);

            // Row 3: quality slider + tier/power readout.
            Rect qualityRow = new Rect(rect.x, descRow.yMax + 2f, rect.width, 28f);
            Rect qLabel = qualityRow.LeftPart(0.32f);
            Rect qSlider = new Rect(qualityRow.x + rect.width * 0.33f, qualityRow.y + 3f, rect.width * 0.30f, 22f);
            Rect qReadout = qualityRow.RightPart(0.34f);

            // Row 3 is fixed-height like Row 2 above, so the label must clip
            // rather than wrap -- the longest (read-only) variant would otherwise
            // wrap onto a second line and overlap the distribution curve below.
            bool prevQualityWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            // Numbers are pre-formatted and passed in as strings: .Translate() substitutes {0}
            // but cannot apply a :F2 format specifier, so the rounding has to happen here.
            Widgets.Label(qLabel, EditingCustom
                ? "VP_AverageQuality".Translate(v.averageQuality.ToString("F2"))
                : "VP_AverageQualityReadOnly".Translate(v.averageQuality.ToString("F2")));
            Text.WordWrap = prevQualityWordWrap;

            // Belt AND braces, matching every other editable control in this file: GUI.enabled
            // greys the slider, the explicit EditingCustom guard is what actually prevents the
            // write. GUI.enabled alone was trusted here and nowhere else; note that GABS's
            // get_ui_layout cannot observe ambient GUI.enabled, so no automated check in this
            // project could catch a regression in the greying half.
            GUI.enabled = outerEnabled && EditingCustom;
            float qualityVal = Widgets.HorizontalSlider(qSlider, v.averageQuality, 0f, 1f);
            if (EditingCustom && qualityVal != v.averageQuality)
            {
                v.averageQuality = qualityVal;
                // MUST invalidate: GetBetaAlphaBeta caches alpha/beta and only re-derives them when
                // this flag is set. The flag is otherwise set in Clone(), ClampAndSwap() and
                // ExposeData() -- none of which run while the editor is open, because
                // MarkDirtyOnWrite fires from WriteSettings() on window CLOSE. Resolve() hands back
                // custom profiles un-cloned (see the aliasing note on Resolve), so without this the
                // curve and the power readout would keep integrating the OLD Beta shape for the
                // rest of the editing session and only catch up after a close/reopen.
                v.MarkDistributionParamsDirty();
            }
            GUI.enabled = outerEnabled;

            // 0.66 against qReadout's RightPart(0.34f): those two must stay disjoint (0.66 + 0.34
            // = 1.00), or the last sliver of the row registers two competing TipRegions and the
            // tooltip flickers between them. Do not raise this to 0.68 -- that summed to 1.02.
            Rect qLabelAndSlider = qualityRow.LeftPart(0.66f);
            // This is the control that gives every range below its meaning, so it is the right
            // place to state the shared rule once. Each range then says which of the two kinds
            // it is, in its own tooltip.
            TooltipHandler.TipRegion(qLabelAndSlider, "VP_QualityTip".Translate().ToString());

            // The readout is output, not input -- always full opacity, even on a
            // read-only preset, so presets stay comparable by cycling the picker.
            // Labelled "Typical" so it reads as one half of a pair with the row below.
            float meanComposite = DispersionModel.TypicalAt(v, v.averageQuality);
            bool prevReadoutWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(qReadout, "VP_TypicalReadout".Translate(
                PawnVarianceSettings.FormatPowerReadout(meanComposite)));
            Text.WordWrap = prevReadoutWordWrap;
            // The second paragraph is the load-bearing half. Without it a player reads Distinct's
            // -10% as "weaker than Faithful" and picks against the profile for the exact reason it
            // exists: its spread is 1.52x Faithful's. This figure is now dispersion-aware (it DOES
            // see skillSpread and passionSpread, via DispersionModel), but it is still a single
            // number describing an average pawn -- it does not show how much pawns differ from each
            // other, so two profiles with the same figure can still play very differently.
            TooltipHandler.TipRegion(qReadout, "VP_TypicalTip".Translate().ToString());

            // Row 3b: the Best-of-N anchor.
            //
            // Row 3 alone actively misleads. Players reroll starts, pick from captures and refuse
            // quest pawns, so the pawn they keep is the best of many -- and on the two variance
            // presets the two figures disagree in SIGN. Wildcard reads -18% typical but +17% at
            // best-of-25: a player picking it for a harder run gets an easier one.
            Rect bestRow = new Rect(rect.x, qualityRow.yMax + 2f, rect.width, 20f);
            // dragActive keys into the single-slot Best-of-N cache (PawnVarianceSettings): while
            // the mouse is held every frame's profile values are potentially different, so the
            // cache would miss every frame anyway -- take the cheap grid rather than pay full
            // resolution for a value that is about to be replaced next frame regardless.
            dragActive = Input.GetMouseButton(0);
            float bestComposite = PawnVarianceSettings.CalculateBestOfNScore(
                v, Constants.BestOfNSampleCount, lowRes: dragActive);
            float bestBaseline = PawnVarianceSettings.FaithfulBestOfNBaseline(
                Constants.BestOfNSampleCount, lowRes: dragActive);

            Text.Font = GameFont.Tiny;
            GUI.color = new Color(1f, 1f, 1f, 0.75f);
            bool prevBestWordWrap = Text.WordWrap;
            Text.WordWrap = false;
            Widgets.Label(bestRow, "VP_BestOfNRow".Translate(
                Constants.BestOfNSampleCount,
                PawnVarianceSettings.FormatPowerPercent(bestComposite, bestBaseline),
                bestComposite.ToString("F2")));
            Text.WordWrap = prevBestWordWrap;
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            TooltipHandler.TipRegion(bestRow,
                "VP_BestOfNTip".Translate(Constants.BestOfNSampleCount).ToString());

            // Row 4: the distribution curve, full width, never greyed.
            Rect curveRect = new Rect(rect.x, bestRow.yMax + 4f, rect.width, CurveHeight);
            DrawQualityDistributionCurve(curveRect, v);
        }

        // One-line summary of a custom profile's values. Never returns empty.
        private static string ProfileFingerprint(VarianceProfileValues v)
        {
            return "VP_Fingerprint".Translate(
                v.traitCountMin.ToString("F0"), v.traitCountMax.ToString("F0"),
                v.passionCountMin.ToString("F1"), v.passionCountMax.ToString("F1"),
                v.skillShiftMin.ToString("F1"), v.skillShiftMax.ToString("F1"),
                v.averageQuality.ToString("F2"));
        }

        private void DrawGenerationSettings(Listing_Standard listing)
        {
            var v = Editing;
            // Captured before any header narrows it, and restored at the end of this method.
            bool outerEnabled = GUI.enabled;

            SectionHeader(listing, "VP_Section_Skills".Translate(), outerEnabled,
                ref v.enableSkillVariance, ref enableSkillVarianceModWide,
                "VP_Section_SkillsTip".Translate());
            Rect noiseRow = listing.GetRect(28f);
            Rect noiseLabelRect = noiseRow.LeftPart(0.42f);
            // Vertically centre the label against the range control.
            noiseLabelRect.y += 4f;
            float sMax = Constants.MaxMagnitude / Mathf.Sqrt(6f);
            Widgets.Label(noiseLabelRect, "VP_SkillSpread".Translate(v.skillSpread.ToString("F2")));
            float sSpreadVal = Widgets.HorizontalSlider(noiseRow.RightPart(0.56f),
                                                        v.skillSpread, 0f, sMax);
            if (EditingCustom) v.skillSpread = sSpreadVal;
            // "within a pawn" vs Passion spread's "between pawns" is the real distinction and is
            // easy to lose: this magnitude is drawn independently per skill around one shared
            // baseline, so it separates a pawn's own skills. Passion spread perturbs a single
            // per-pawn budget, so it separates pawns. Keep both halves of that contrast.
            TooltipHandler.TipRegion(noiseRow, "VP_SkillSpreadTip".Translate().ToString());

            Rect skillDerived = listing.GetRect(18f);
            float skillMag = v.skillSpread * Mathf.Sqrt(6f);
            float qMed = v.MedianQuality();
            float bandAtMedian = Mathf.Lerp(v.skillShiftMin, v.skillShiftMax, qMed);
            float lo = Mathf.Max(0f, Constants.AssumedVanillaSkillBaseline
                                     + bandAtMedian - v.skillSpread);
            float hi = Mathf.Min(Constants.AssumedMaxSkillLevel,
                                 Constants.AssumedVanillaSkillBaseline
                                 + bandAtMedian + v.skillSpread);
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(skillDerived, "VP_SkillSpreadDerived".Translate(
                skillMag.ToString("F1"), lo.ToString("F1"), hi.ToString("F1")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            listing.Gap(ControlGap);
            LabeledFloatRange(listing, "VP_SkillShift".Translate(), SkillShiftRangeId,
                ref v.skillShiftMin, ref v.skillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                "VP_SkillShiftTip".Translate());

            if (ModsConfig.BiotechActive)
                DrawChildSkillShift(listing, v);

            SectionHeader(listing, "VP_Section_Traits".Translate(), outerEnabled,
                ref v.enableTraitVariance, ref enableTraitVarianceModWide,
                "VP_Section_TraitsTip".Translate());
            bool countVal = v.countProtectedTraits;
            listing.CheckboxLabeled(
                "VP_CountProtectedTraits".Translate(),
                ref countVal,
                "VP_CountProtectedTraitsTip".Translate());
            if (EditingCustom) v.countProtectedTraits = countVal;
            listing.Gap(ControlGap);
            // Unlike Skill shift and Passion budget, this one really is a bound --
            // TraitVarianceApplier clamps the rolled target to [min, max] and there is no trait
            // noise knob to push past it. Say so, or a player reasonably assumes the looser
            // semantics they just read two controls above.
            LabeledFloatRange(listing, "VP_TraitCount".Translate(), TraitCountRangeId,
                ref v.traitCountMin, ref v.traitCountMax, 0f, 15f, ToStringStyle.Integer,
                (v.countProtectedTraits
                    ? "VP_TraitCountTipTotal".Translate()
                    : "VP_TraitCountTipRolled".Translate())
                + "\n\n" + "VP_TraitCountTipBody".Translate());

            SectionHeader(listing, "VP_Section_Passions".Translate(), outerEnabled,
                ref v.enablePassionVariance, ref enablePassionVarianceModWide,
                "VP_Section_PassionsTip".Translate());
            Rect passionRow = listing.GetRect(28f);
            Rect leftHalf = passionRow.LeftPart(0.48f);
            Rect rightHalf = passionRow.RightPart(0.48f);

            Rect passionNoiseLabelRect = leftHalf.LeftPart(0.52f);
            // Vertically centre the label against the range control.
            passionNoiseLabelRect.y += 4f;
            Widgets.Label(passionNoiseLabelRect, "VP_PassionSpread".Translate(v.passionSpread.ToString("F2")));
            float pSpreadVal = Widgets.HorizontalSlider(leftHalf.RightPart(0.46f),
                v.passionSpread, 0f, Constants.PassionBudgetSpreadMax);
            if (EditingCustom) v.passionSpread = pSpreadVal;
            TooltipHandler.TipRegion(leftHalf, "VP_PassionSpreadTip".Translate().ToString());

            Rect passionDerived = listing.GetRect(18f);
            float pipExtreme = v.passionSpread * Constants.PassionBudgetClampFactor;
            float qMedP = v.MedianQuality();
            float budgetMid = Mathf.Lerp(v.passionCountMin, v.passionCountMax, qMedP);
            float bLo = Mathf.Max(0f, budgetMid - v.passionSpread);
            float bHi = budgetMid + v.passionSpread;
            Text.Font = GameFont.Tiny;
            GUI.color = Color.gray;
            Widgets.Label(passionDerived, "VP_PassionSpreadDerived".Translate(
                pipExtreme.ToString("F1"), bLo.ToString("F1"), bHi.ToString("F1")));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;

            Rect majorBiasLabelRect = rightHalf.LeftPart(0.52f);
            // Vertically centre the label against the range control.
            majorBiasLabelRect.y += 4f;
            Widgets.Label(majorBiasLabelRect, "VP_MajorBias".Translate(v.passionMajorBias.ToString("F2")));
            float mBiasVal = Widgets.HorizontalSlider(rightHalf.RightPart(0.46f), v.passionMajorBias, 0f, 1f);
            if (EditingCustom) v.passionMajorBias = mBiasVal;
            TooltipHandler.TipRegion(rightHalf, "VP_MajorBiasTip".Translate().ToString());

            listing.Gap(ControlGap);
            // Ceiling is MaxPassionPips (18 = 12 skills x 1.5), not a literal: it is the most the
            // spend loop can ever place, and only at Major bias 1.0. It was 24 until 2026-08-05 --
            // 12 x 2, derived correctly from the era when the caption below read "Major passion =
            // 2". That string was fixed on 2026-08-04 and this bound was not, leaving 6 pips of
            // range that could never buy anything. No preset was affected: the highest any of them
            // reaches is Wildcard at 9.8, so nothing was ever calibrated against the old bound.
            LabeledFloatRange(listing, "VP_PassionBudget".Translate(), PassionCountRangeId,
                ref v.passionCountMin, ref v.passionCountMax, 0f, Constants.MaxPassionPips, ToStringStyle.FloatOne,
                "VP_PassionBudgetTip".Translate(Constants.MaxPassionPips.ToString("F0")));
            Caption(listing, v.passionCountMin > 0f
                ? "VP_PassionCaptionTargets".Translate()
                : "VP_PassionCaptionZero".Translate());

            // The Passions header left GUI.enabled narrowed; nothing else restores it, and leaving
            // it narrowed leaks into whatever the caller draws next.
            GUI.enabled = outerEnabled;
        }

        private void DrawChildSkillShift(Listing_Standard listing, VarianceProfileValues v)
        {
            listing.Gap(ControlGap);
            bool childVal = v.applyChildSkillShift;
            listing.CheckboxLabeled(
                "VP_ChildShiftToggle".Translate(),
                ref childVal,
                "VP_ChildShiftToggleTip".Translate());
            if (EditingCustom) v.applyChildSkillShift = childVal;

            if (v.applyChildSkillShift)
            {
                listing.Gap(ControlGap);
                LabeledFloatRange(listing, "VP_ChildShift".Translate(), ChildSkillShiftRangeId,
                    ref v.childSkillShiftMin, ref v.childSkillShiftMax, -20f, 20f, ToStringStyle.FloatOne,
                    "VP_ChildShiftTip".Translate());
                Caption(listing, v.childSkillShiftMin >= 0f
                    ? "VP_ChildShiftCaptionSafe".Translate()
                    : "VP_ChildShiftCaptionLossy".Translate((-v.childSkillShiftMin).ToString("F0")));
            }
        }

        // CurveSamples was the local `int samples = 70`; curveDensityScratch is reused across
        // frames because the curve redraws every frame and DispersionModel.OutcomeDensity just
        // fills it in place.
        private const int CurveSamples = 70;
        private static float[] curveDensityScratch;
        // Same reasoning as curveDensityScratch, and it was the one allocation in this method that
        // did not follow it: a fresh Vector2[70] per IMGUI frame, plus (until 2026-08-09) an
        // Array.Sort with a freshly allocated comparer delegate beside it. Audit finding Q-13.
        private static Vector2[] curvePointScratch;

        // True while the mouse is held anywhere in the editor -- i.e. a slider may be moving. The
        // single-slot Best-of-N cache now keys on this flag directly (PawnVarianceSettings), so a
        // live drag always misses at drag resolution and the frame right after release misses
        // once more at full resolution before settling back into cache hits. Each missed
        // full-resolution frame pays the full 131k Erf-bearing evaluations, single-threaded in
        // Mono, which is why drag frames ask for the cheap grid instead.
        private static bool dragActive;

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

            // The realised-outcome density (not the raw Beta density mapped through the mean-band
            // composite): this is what actually responds to the two spread sliders, and shows
            // left-censoring (skills piling up at zero) as a pile against the left edge.
            if (curveDensityScratch == null || curveDensityScratch.Length != CurveSamples)
                curveDensityScratch = new float[CurveSamples];
            DispersionModel.OutcomeDensity(v, curveDensityScratch);

            if (curvePointScratch == null || curvePointScratch.Length != CurveSamples)
                curvePointScratch = new Vector2[CurveSamples];
            Vector2[] points = curvePointScratch;
            float maxDensity = 0.001f;

            for (int i = 0; i < CurveSamples; i++)
            {
                // Realised power on [0,1] -- the same axis OutcomeDensity fills into -- mapped
                // through the same centering transform the old composite-score x-coordinate used.
                float power = (i + 0.5f) / CurveSamples;
                float x = MapToCenteredX(power);
                float density = curveDensityScratch[i];

                if (density > maxDensity) maxDensity = density;
                points[i] = new Vector2(x, density);
            }

            // NO SORT. The points are generated in ascending x and cannot be otherwise, so the
            // `Array.Sort(points, (a, b) => a.x.CompareTo(b.x))` that stood here until 2026-08-09
            // sorted already-sorted data every frame and allocated a comparer delegate to do it
            // (audit finding Q-13). The proof, since this is a correctness claim and not just a
            // performance one: `power` is `(i + 0.5f) / CurveSamples` for increasing i, and
            // MapToCenteredX is monotonically non-decreasing -- both of its branches are
            // `constant + positive slope x input`, and they meet continuously at
            // `compositeScore == baseC`, where both evaluate to 0.50. A strictly increasing input
            // through a non-decreasing map is non-decreasing.
            //
            // If MapToCenteredX ever grows a non-monotonic branch, this is the line that breaks --
            // restore the sort rather than reordering the map.

            // Draw Line Segments. maxDensity floors at 0.001f above, so this can never divide by
            // zero even if OutcomeDensity ever returned an all-zero array.
            Color curveColor = new Color(0.35f, 0.85f, 1.00f, 0.95f);
            for (int i = 0; i < CurveSamples - 1; i++)
            {
                float x1 = rect.x + points[i].x * rect.width;
                float y1 = rect.yMax - 3f - (points[i].y / maxDensity) * (rect.height - 6f);
                float x2 = rect.x + points[i + 1].x * rect.width;
                float y2 = rect.yMax - 3f - (points[i + 1].y / maxDensity) * (rect.height - 6f);

                Widgets.DrawLine(new Vector2(x1, y1), new Vector2(x2, y2), curveColor, 2f);
            }

            // Draw Mean Compound Power Line
            float meanRawComposite = DispersionModel.TypicalAt(v, v.averageQuality);
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
