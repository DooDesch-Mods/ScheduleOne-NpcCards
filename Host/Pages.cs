using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sideload.Api;

namespace NpcCards.Host
{
    /// <summary>
    /// The two places the page lives: the phone app (always registered) and the main-menu surface (while the gamemode
    /// is open). Events and pictures go to both, so an answer reaches whichever page asked. Pictures are kept, so a
    /// surface mounted later gets the ones the phone already has.
    /// </summary>
    internal static class Pages
    {
        internal static AppHandle Phone;
        private static SurfaceHandle _menu;
        private static readonly Dictionary<string, byte[]> Pictures = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        internal static void AttachMenu(SurfaceHandle surface)
        {
            _menu = surface;
            foreach (var kv in Pictures) surface.Image(kv.Key, kv.Value);
            Core.Log.Msg($"[pages] menu page attached, {Pictures.Count} picture(s) handed over.");
        }

        internal static void DetachMenu() => _menu = null;

        internal static bool HasPicture(string name) => Pictures.ContainsKey(name);

        internal static void Emit(string name, JToken payload)
        {
            string text = payload?.ToString(Formatting.None) ?? "";
            if (Phone == null && _menu == null) { Core.Log.Warning($"[pages] event '{name}' dropped: no page is registered."); return; }
            Phone?.Emit(name, text);
            _menu?.Emit(name, text);
        }

        /// <summary>An event for the menu page only (its keys are not the phone's).</summary>
        internal static void EmitMenu(string name, JToken payload) => _menu?.Emit(name, payload?.ToString(Formatting.None) ?? "");

        /// <summary>Shows <paramref name="png"/> to the pages as <c>s1://name</c>.</summary>
        internal static string Picture(string name, byte[] png)
        {
            Pictures[name] = png;
            Phone?.Image(name, png);
            _menu?.Image(name, png);
            return "s1://" + name;
        }

        /// <summary>A game notification from the phone app (shown inside a save).</summary>
        internal static void Notify(string title, string text)
        {
            if (Phone == null) { Core.Log.Warning($"[pages] notification '{title}: {text}' dropped: the phone app is not registered."); return; }
            Phone.Notify(title, text, 6f);
            Core.Log.Msg($"[pages] notification: {title}: {text}");
        }
    }
}
