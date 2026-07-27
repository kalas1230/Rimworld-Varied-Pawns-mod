using HarmonyLib;
using Verse;

namespace PawnVarianceMod
{
    // Verified against RimWorld 1.5/1.6's decompiled Assembly-CSharp.dll: Pawn.GetDescription()
    // does not exist; the real bio-tab text is Pawn.DescriptionDetailed, a virtual property
    // getter overridden directly on Pawn (not inherited unmodified from Thing). Confirmed
    // distinct from the inspect-string method (Thing.GetInspectString(), which exists
    // separately), satisfying the Global Constraints requirement.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DescriptionDetailed), MethodType.Getter)]
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
