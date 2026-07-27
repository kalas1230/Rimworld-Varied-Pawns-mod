using HarmonyLib;
using Verse;

namespace PawnVarianceMod
{
    // Target method unverified — confirm the bio-description generator against decompiled
    // source, and confirm it's distinct from the inspect-string method (Global Constraints).
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetDescription))]
    public static class GetDescription_Postfix
    {
        public static void Postfix(Pawn __instance, ref string __result)
        {
            if (!TierUtility.IsEligibleForLabel(__instance)) return;

            string tier = TierUtility.EffectiveTierFor(__instance);
            string color = TierUtility.ColorFor(tier);

            string labelText = color != null
                ? $"<color={color}>{tier}</color>"
                : tier;

            __result += $"\n\n{labelText}";
        }
    }
}
