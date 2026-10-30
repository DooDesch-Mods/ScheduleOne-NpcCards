using System;
using System.IO;
using System.Linq;
using MelonLoader;
using MelonLoader.Utils;
using Newtonsoft.Json.Linq;

namespace NpcCards.Host
{
    /// <summary>
    /// Recommends Siesta when the game holds many custom NPCs and Siesta is not installed. The page counts the NPCs of
    /// the next game start itself, because that number moves with every install. Closing the hint lasts until the game
    /// is closed.
    /// </summary>
    internal static class SiestaHint
    {
        internal const string Url = "https://thunderstore.io/c/schedule-i/p/DooDesch/Siesta/";
        private const string MelonName = "Siesta";
        private const string DllName = "Siesta.dll";

        /// <summary>Custom NPCs from which the hint shows.</summary>
        private const int Threshold = 40;

        private static bool _closed;

        internal static JObject State()
        {
            int npcs = -1;
            try { npcs = Personnel.API.All.Count; }
            catch (Exception e) { Core.Log.Warning($"[siesta hint] reading the NPC count from Personnel failed ({e.Message}) - no hint."); }

            bool installed = IsInstalled();
            Core.Log.Msg($"[siesta hint] {npcs} custom NPC(s), threshold {Threshold}, Siesta installed: {installed}, closed: {_closed}.");
            return new JObject
            {
                ["ok"] = true,
                ["offer"] = npcs >= 0 && !installed && !_closed,
                ["npcs"] = npcs,
                ["threshold"] = Threshold,
                ["url"] = Url,
            };
        }

        internal static JObject Close()
        {
            _closed = true;
            Core.Log.Msg("[siesta hint] closed by the player until the game is closed.");
            return new JObject { ["ok"] = true };
        }

        /// <summary>A DLL in Mods counts too: Side Hustle can hold a mod back, and it is installed all the same.</summary>
        private static bool IsInstalled()
        {
            try
            {
                if (MelonBase.RegisteredMelons.Any(m => m.Info.Name == MelonName)) return true;
                return File.Exists(Path.Combine(MelonEnvironment.ModsDirectory, DllName));
            }
            catch (Exception e)
            {
                Core.Log.Warning($"[siesta hint] checking for Siesta failed ({e.Message}) - treated as installed, no hint.");
                return true;
            }
        }
    }
}
