using HarmonyLib;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    public class PawnVarianceMod : Mod
    {
        public static PawnVarianceSettings Settings;

        public PawnVarianceMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<PawnVarianceSettings>();
            var harmony = new Harmony("yourname.pawnvariance");
            harmony.PatchAll();
        }

        public override string SettingsCategory() => "Pawn Variance";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            Settings.MarkDirtyOnWrite();
        }
    }
}
