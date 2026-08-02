using System;
using System.IO;
using UnityEngine;
using Verse;

namespace PawnVarianceMod
{
    // Clipboard export/import of a whole tuning: custom profiles, faction/xenotype override maps
    // with their priorities, and the General toggles.
    //
    // Both directions go through PawnVarianceSettings.ExposeData rather than a hand-rolled format,
    // so the payload can never drift from what the mod actually persists. Import in particular
    // reuses the game's own settings-load mechanism (Scribe_Deep on a ModSettings object), because
    // the dictionaries are stored flattened into parallel lists and are only rebuilt inside
    // ExposeData's PostLoadInit branch — a branch that Scribe only reaches for objects registered
    // by Scribe_Deep. Calling ExposeData directly would load the lists and never rebuild the maps.
    public static class SettingsTransfer
    {
        // Bumped only when the payload shape changes incompatibly. Nothing branches on it yet; it
        // exists so that a payload written today can be recognised by a future version that needs
        // to migrate it. Scribe_Values already defaults any field a payload omits.
        public const int ConfigVersion = 1;

        private const string RootNode = "PawnVarianceConfig";
        private const string SettingsNode = "ModSettings";

        private static string TransferFilePath =>
            Path.Combine(Path.GetTempPath(), "PawnVarianceMod_Transfer.xml");

        // Returns the XML payload, or null if it could not be produced.
        public static string Export(PawnVarianceSettings settings)
        {
            if (settings == null) return null;

            string path = TransferFilePath;
            try
            {
                // Saving is the safe direction — it cannot corrupt the live settings — but it runs
                // through the same global Scribe singleton, so it gets the same finally block.
                Scribe.saver.InitSaving(path, RootNode);
                int version = ConfigVersion;
                Scribe_Values.Look(ref version, "configVersion", 0);
                Scribe_Deep.Look(ref settings, SettingsNode);
                Scribe.saver.FinalizeSaving();

                return File.ReadAllText(path);
            }
            catch (Exception ex)
            {
                Log.Error("[PawnVarianceMod] Settings export failed: " + ex);
                return null;
            }
            finally
            {
                // Scribe is a global singleton shared with the player's save file. If anything above
                // threw part-way, Scribe.mode is still Saving and the next autosave would write into
                // a half-open document. ForceStop is what makes this feature safe to ship.
                Scribe.ForceStop();
                TryDelete(path);
            }
        }

        // Replaces every setting from the payload. Returns false and leaves the live settings
        // completely untouched if anything goes wrong: the payload is loaded into a throwaway
        // settings object first, and the live one is only written once that has fully succeeded.
        public static bool Import(PawnVarianceSettings settings, string payload, out string error)
        {
            error = null;
            if (settings == null)
            {
                error = "Settings unavailable.";
                return false;
            }

            payload = payload?.Trim();
            if (string.IsNullOrEmpty(payload))
            {
                error = "Clipboard is empty.";
                return false;
            }

            // Parse the payload ourselves before Scribe ever sees it. This is not belt-and-braces:
            // ScribeLoader.InitLoading catches its own XML parse failure, writes a red
            // Log.Error("Exception while init loading file: ...") and *then* rethrows. Our catch
            // gets the exception but cannot unwrite that error — the same shape of problem as
            // Faction.OfPlayer logging inside the getter. Verified in game: pasting garbage
            // produced a red error and blocked the GABS bridge until acknowledged. Validating here
            // means InitLoading only ever receives XML that has already parsed once.
            System.Xml.XmlDocument doc;
            try
            {
                doc = new System.Xml.XmlDocument();
                doc.LoadXml(payload);
            }
            catch (Exception ex)
            {
                Log.Warning("[PawnVarianceMod] Rejected a settings import that was not valid XML: " + ex.Message);
                error = "Clipboard does not contain a Varied Pawns settings export.";
                return false;
            }

            if (doc.DocumentElement == null || doc.DocumentElement.Name != RootNode)
            {
                error = "Clipboard does not contain a Varied Pawns settings export.";
                return false;
            }

            string path = TransferFilePath;
            PawnVarianceSettings loaded = null;
            try
            {
                File.WriteAllText(path, payload);

                Scribe.loader.InitLoading(path);
                int version = 0;
                Scribe_Values.Look(ref version, "configVersion", 0);
                Scribe_Deep.Look(ref loaded, SettingsNode);
                // Runs cross-ref resolution and the PostLoadInit pass that rebuilds the override
                // dictionaries from the flattened key/value lists and clamps every profile.
                Scribe.loader.FinalizeLoading();
            }
            catch (Exception ex)
            {
                Log.Warning("[PawnVarianceMod] Settings import failed: " + ex);
                error = "Could not read the pasted settings. See the log for details.";
                return false;
            }
            finally
            {
                Scribe.ForceStop();
                TryDelete(path);
            }

            if (loaded == null)
            {
                error = "The pasted settings contained no readable configuration.";
                return false;
            }

            settings.CopyFrom(loaded);
            return true;
        }

        public static void CopyToClipboard(string text)
        {
            GUIUtility.systemCopyBuffer = text;
        }

        public static string ReadClipboard()
        {
            return GUIUtility.systemCopyBuffer;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception ex)
            {
                Log.Warning("[PawnVarianceMod] Could not clean up the settings transfer file: " + ex.Message);
            }
        }
    }
}
