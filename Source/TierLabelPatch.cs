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
    // Replaced with hover tooltips instead, per explicit decision after this was surfaced during
    // manual verification. Two complementary patch sites, both cosmetic-only single Postfixes:
    // this one covers the character tab / right-click info card (works for ANY pawn you can
    // select and view — colonists, prisoners, visitors, even a selected hostile), and
    // DrawColonist_Postfix below covers the colonist bar at the top of the screen (colonists and
    // prisoners shown there specifically, with an exact rather than approximated hover rect).
    [HarmonyPatch(typeof(CharacterCardUtility), nameof(CharacterCardUtility.DrawCharacterCard))]
    public static class DrawCharacterCard_Postfix
    {
        public static void Postfix(Rect rect, Pawn pawn)
        {
            if (!TierUtility.IsEligibleForLabel(pawn)) return;

            // Best-effort layout: a strip across the top of the card, roughly where the portrait
            // and name are drawn (DrawCharacterCard doesn't expose its internal sub-layout rects
            // to a Postfix, so this is an approximation, not derived from vanilla's exact
            // coordinates) — adjust the height below if it doesn't line up well visually in-game.
            var tooltipRect = new Rect(rect.x, rect.y, rect.width, 60f);
            TooltipHandler.TipRegion(tooltipRect, TierUtility.TierTooltipTextFor(pawn));
        }
    }

    // Target method unverified — confirm ColonistBarColonistDrawer.DrawColonist's exact signature
    // against decompiled source before relying on it long-term (Global Constraints); found via
    // in-game verification (Task 11) after the character-card-only tooltip turned out to not
    // cover the colonist bar at the top of the screen at all (a separate rendering path).
    [HarmonyPatch(typeof(ColonistBarColonistDrawer), nameof(ColonistBarColonistDrawer.DrawColonist))]
    public static class DrawColonist_Postfix
    {
        public static void Postfix(Rect rect, Pawn colonist)
        {
            if (!TierUtility.IsEligibleForLabel(colonist)) return;

            // Unlike DrawCharacterCard_Postfix's guessed sub-rect, `rect` here IS exactly the
            // portrait's own bounding box — no approximation needed.
            TooltipHandler.TipRegion(rect, TierUtility.TierTooltipTextFor(colonist));
        }
    }
}
