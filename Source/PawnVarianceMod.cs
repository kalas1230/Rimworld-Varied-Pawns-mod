using System;
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
            var harmony = new Harmony("kalas.pawnvariance");

            // Patched individually (not PatchAll) rather than as one bundle: several patch
            // targets in this mod are explicitly unverified against the target RimWorld version
            // (see each patch class's own comment). If PatchAll() were used, one wrong target
            // would throw during this constructor and silently prevent EVERY patch from applying
            // — including the core GeneratePawn postfix, which has nothing wrong with it. Patching
            // each class separately means a broken growth-up or bio-label target only disables
            // that one feature.
            PatchIndividually(harmony, typeof(GeneratePawn_Postfix));
            PatchIndividually(harmony, typeof(DevelopmentalStage_Postfix));
            PatchIndividually(harmony, typeof(Game_LoadGame_Postfix));
            PatchIndividually(harmony, typeof(Game_InitNewGame_Postfix));
            PatchIndividually(harmony, typeof(GrowthMomentMakeChoices_Postfix));
        }

        private static void PatchIndividually(Harmony harmony, Type patchClass)
        {
            try
            {
                harmony.CreateClassProcessor(patchClass).Patch();
            }
            catch (Exception ex)
            {
                Log.Error($"[PawnVarianceMod] Failed to apply Harmony patch {patchClass.Name}: {ex}");
            }
        }

        public override string SettingsCategory() => "Varied Pawns";

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
