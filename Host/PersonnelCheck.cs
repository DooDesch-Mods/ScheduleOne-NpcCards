using System;

namespace NpcCards.Host
{
    /// <summary>
    /// Finds out whether Personnel can be reached before anything names its types. A Personnel.dll that sits in a
    /// subfolder of Mods, or none at all, otherwise ends in a FileNotFoundException when such a method is compiled.
    /// </summary>
    internal static class PersonnelCheck
    {
        internal const string Notice = "Personnel is not loaded.";
        private const string ProbeType = "Personnel.Cards.CardsApi, Personnel";

        internal static bool Loaded { get; private set; }

        internal static bool Detect()
        {
            try
            {
                Loaded = Type.GetType(ProbeType, throwOnError: false) != null;
            }
            catch (Exception e)
            {
                Core.Log.Error($"[personnel] looking for {ProbeType} failed: {e.Message}");
                Loaded = false;
            }
            if (Loaded) Core.Log.Msg("[personnel] found.");
            else Core.Log.Error("[personnel] not loaded: the app only shows a notice. Personnel.dll has to sit directly in Mods.");
            return Loaded;
        }
    }
}
