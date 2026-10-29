using System;
using System.Collections.Generic;
using System.IO;
using DooDesch.Localization;
using Newtonsoft.Json.Linq;

namespace NpcCards.Host
{
    /// <summary>
    /// The German table (<c>Assets/app/de.json</c>, English text -> German), registered with L10n so a player
    /// translation file or the shared "DooDesch/Language" setting wins. The page gets every string through "init".
    /// </summary>
    internal static class Strings
    {
        private const string Resource = "NpcCards.Assets.app.de.json";
        private static readonly Dictionary<string, string> German = new Dictionary<string, string>(StringComparer.Ordinal);

        internal static void Register()
        {
            try
            {
                using var s = typeof(Strings).Assembly.GetManifestResourceStream(Resource);
                if (s == null) { Core.Log.Warning($"[l10n] {Resource} is not embedded - the app shows English only."); return; }
                using var r = new StreamReader(s);
                foreach (var prop in JObject.Parse(r.ReadToEnd()).Properties())
                    if (prop.Value.Type == JTokenType.String) German[prop.Name] = (string)prop.Value;
                L10n.Register("de", German);
                Core.Log.Msg($"[l10n] {German.Count} German strings registered; language {L10n.Language}.");
            }
            catch (Exception e)
            {
                Core.Log.Error($"[l10n] reading {Resource} failed ({e.Message}) - the app shows English only.");
            }
        }

        internal static string T(string en) => L10n.T(en);

        internal static string T(string en, params object[] args)
        {
            try { return string.Format(T(en), args); }
            catch (FormatException e)
            {
                Core.Log.Warning($"[l10n] translation of '{en}' has broken placeholders ({e.Message}) - English is shown.");
                return string.Format(en, args);
            }
        }

        /// <summary>Every page string in the current language, for the page's t().</summary>
        internal static JObject ForPage()
        {
            var o = new JObject();
            foreach (string key in German.Keys) o[key] = L10n.T(key);
            return o;
        }
    }
}
