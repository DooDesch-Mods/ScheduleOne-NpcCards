using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sideload.Api;

namespace NpcCards.Host
{
    /// <summary>
    /// Every <c>s1.call</c> the page makes, shared by the phone app and the menu surface. Arguments are a JSON object
    /// (or nothing); answers are JSON with <c>ok</c>, and <c>msg</c> when it failed. See <c>Assets/app/app.js</c>.
    /// </summary>
    internal static class Calls
    {
        internal const string AppId = "npccards";
        internal const string Bundle = "NpcCards.Assets.app";
        internal const string PersonifyUrl = "https://thunderstore.io/c/schedule-i/p/DooDesch/Personify/";

        private static readonly Dictionary<string, Func<JObject, string, JObject>> Handlers = new Dictionary<string, Func<JObject, string, JObject>>(StringComparer.Ordinal)
        {
            ["init"] = (a, where) => Init(where),
            ["vault.state"] = (a, _) => Vault.State(),
            ["vault.get"] = (a, _) => Vault.Get((string)a["id"], (string)a["path"]),
            ["vault.install"] = (a, _) => Vault.Install((string)a["id"], (string)a["code"]),
            ["vault.thumb"] = (a, _) => Vault.Thumb((string)a["code"]),
            ["vault.connect"] = (a, _) => Vault.Connect(),
            ["vault.disconnect"] = (a, _) => Vault.Disconnect(),
            ["vault.upload"] = (a, _) => Vault.Upload((string)a["id"], (string)a["file"]),
            ["cards.installed"] = (a, _) => Library.List(),
            ["cards.remove"] = (a, _) => Library.Remove((string)a["code"]),
            ["cards.importFiles"] = (a, _) => Library.ImportFiles(),
            ["cards.importFile"] = (a, _) => Library.ImportFile((string)a["file"]),
            ["cards.openFolder"] = (a, _) => Library.OpenFolder(),
            ["cards.inbox"] = (a, _) => new JObject { ["ok"] = true, ["cards"] = Library.Arrivals() },
            ["cards.uploadFiles"] = (a, _) => Library.UploadFiles(),
            ["open.url"] = (a, _) => Vault.OpenUrl((string)a["url"]),
            ["copy"] = (a, _) => Vault.Copy((string)a["text"]),
            ["siesta.hint"] = (a, _) => SiestaHint.State(),
            ["siesta.close"] = (a, _) => SiestaHint.Close(),
            ["menu.close"] = (a, where) => Menu.MenuSurface.RequestClose(where),
        };

        /// <summary>The calls that never reach Personnel, so they still answer without it.</summary>
        private static readonly HashSet<string> WithoutPersonnel = new HashSet<string>(StringComparer.Ordinal) { "init", "menu.close" };

        internal static void RegisterPhone()
        {
            try
            {
                var app = Apps.Register(AppId, Bundle, "NPC Cards", "NPC Cards");
                app.Orientation("landscape", "portrait");
                foreach (var kv in Handlers) { var h = kv; app.OnCall(h.Key, arg => Run(h.Key, arg, "phone", h.Value)); }
                Pages.Phone = app;
                Core.Log.Msg(Apps.Available ? "[phone] app registered with Sideload." : "[phone] Sideload is not loaded yet - the app registration is queued.");
            }
            catch (Exception e)
            {
                Core.Log.Error($"[phone] registering the phone app failed: {e}");
            }
        }

        internal static void RegisterMenu(SurfaceHandle surface)
        {
            foreach (var kv in Handlers) { var h = kv; surface.OnCall(h.Key, arg => Run(h.Key, arg, "menu", h.Value)); }
        }

        private static string Run(string name, string arg, string where, Func<JObject, string, JObject> handler)
        {
            try
            {
                if (!PersonnelCheck.Loaded && !WithoutPersonnel.Contains(name))
                {
                    Core.Log.Warning($"[{where}] {name} refused: Personnel is not loaded.");
                    return Fail(PersonnelCheck.Notice).ToString(Formatting.None);
                }
                JObject a = string.IsNullOrWhiteSpace(arg) ? new JObject() : JObject.Parse(arg);
                return (handler(a, where) ?? Fail("Something went wrong. See the log.")).ToString(Formatting.None);
            }
            catch (Exception e)
            {
                Core.Log.Error($"[{where}] {name}({arg}) failed: {e}");
                return Fail("Something went wrong. See the log.").ToString(Formatting.None);
            }
        }

        internal static JObject Fail(string en) => new JObject { ["ok"] = false, ["msg"] = Strings.T(en) };

        private static JObject Init(string where) => PersonnelCheck.Loaded ? FullInit(where) : new JObject
        {
            ["ok"] = true,
            ["where"] = where,
            ["version"] = DooDesch.ModVersion.Current,
            ["strings"] = Strings.ForPage(),
            ["personnelMissing"] = true,
        };

        // Not inlined, because this names types that resolve into Personnel (see Core).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static JObject FullInit(string where) => new JObject
        {
            ["ok"] = true,
            ["where"] = where,
            ["version"] = DooDesch.ModVersion.Current,
            ["strings"] = Strings.ForPage(),
            ["vault"] = Vault.State(),
            ["personifyUrl"] = PersonifyUrl,
            ["importFolder"] = Library.ImportDir,
        };
    }
}
