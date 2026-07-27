using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Verified against RimWorld 1.5/1.6's decompiled Assembly-CSharp.dll (Task 11 in-game
    // verification): neither Pawn.DescriptionDetailed nor Pawn.DescriptionFlavor is called
    // anywhere in vanilla code — a full IL call-graph search of every method on
    // CharacterCardUtility, Dialog_InfoCard, and ITab_Pawn_Character turned up zero callers of
    // either property. This RimWorld version's character card renders no flowing "physical
    // description" text block at all (the left panel only draws trait/ability icon rows via
    // TraitSet.TraitsSorted), so the original design's "append text to the pawn's bio" premise
    // cannot work here — patching either property changes a value nothing ever reads.
    //
    // Replaced with a hover tooltip on the character card instead, per explicit decision after
    // this was surfaced during manual verification: still cosmetic-only, still a single small
    // Postfix, but shows the tier on hover rather than as always-visible inline text.
    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard))]
    public static class DrawCharacterCard_Postfix
    {
        public static void Postfix(Rect rect, Pawn pawn)
        {
            if (!TierUtility.IsEligibleForLabel(pawn)) return;

            string tier = TierUtility.EffectiveTierFor(pawn);
            string color = TierUtility.ColorFor(tier);
            string tierText = color != null ? $"<color={color}>{tier}</color>" : tier;

            // Best-effort layout: a strip across the top of the card, roughly where the portrait
            // and name are drawn (DrawCharacterCard doesn't expose its internal sub-layout rects
            // to a Postfix, so this is an approximation, not derived from vanilla's exact
            // coordinates) — adjust the height below if it doesn't line up well visually in-game.
            var tooltipRect = new Rect(rect.x, rect.y, rect.width, 60f);
            TooltipHandler.TipRegion(tooltipRect, $"Quality tier: {tierText}");
        }
    }
}
