using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Personnel.Cards;
using UnityEngine;

namespace NpcCards.Host
{
    /// <summary>
    /// NPC Vault through Personnel (<see cref="CardsApi"/>). Every call answers at once with <c>{ ok, pending }</c>;
    /// the result arrives as the page event "vault.reply" <c>{ id, ok, body }</c> with the id the page sent. Personnel
    /// runs its callbacks on the main thread. Card pictures load here, straight from the public thumb URL.
    /// </summary>
    internal static class Vault
    {
        private static readonly HttpClient Http = MakeClient();
        private static readonly HashSet<string> Loading = new HashSet<string>(StringComparer.Ordinal);

        internal static string BaseUrl
        {
            get
            {
                try { return (string)JObject.Parse(CardsApi.VaultState())["baseUrl"]; }
                catch (Exception e) { Core.Log.Error($"[vault] reading the vault address from Personnel failed: {e.Message}"); return VaultClient.DefaultBaseUrl; }
            }
        }

        private static HttpClient MakeClient()
        {
            var c = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            c.DefaultRequestHeaders.UserAgent.ParseAdd(Caller);
            return c;
        }

        /// <summary>This mod in a User-Agent: "NPCCards/1.0.0". Vault calls through Personnel send it after Personnel's own.</summary>
        internal static string Caller => "NPCCards/" + DooDesch.ModVersion.Current;

        internal static JObject State()
        {
            var s = JObject.Parse(CardsApi.VaultState());
            s["ok"] = true;
            return s;
        }

        private static JObject Pending() => new JObject { ["ok"] = true, ["pending"] = true };

        private static void Reply(string id, bool ok, JToken body) =>
            Pages.Emit("vault.reply", new JObject { ["id"] = id, ["ok"] = ok, ["body"] = body });

        /// <summary>GET a vault API path (must start with /api/v1/).</summary>
        internal static JObject Get(string id, string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("/api/v1/", StringComparison.Ordinal))
            {
                Core.Log.Warning($"[vault] path '{path}' refused: not under /api/v1/.");
                return Calls.Fail("Something went wrong. See the log.");
            }
            Core.Log.Msg($"[vault] GET {path}");
            using var scope = CardsApi.Caller(Caller);
            CardsApi.VaultGet(path, (ok, body) =>
            {
                if (!ok)
                {
                    Core.Log.Error($"[vault] GET {path} failed: {body}");
                    Reply(id, false, body);
                    return;
                }
                JToken parsed;
                try { parsed = JToken.Parse(body); }
                catch (JsonException e)
                {
                    Core.Log.Error($"[vault] GET {path} answered something that is not JSON ({e.Message}).");
                    Reply(id, false, "The vault answered something unreadable.");
                    return;
                }
                int count = (parsed["cards"] as JArray)?.Count ?? -1;
                Core.Log.Msg(count >= 0 ? $"[vault] GET {path}: {count} card(s), next {(string)parsed["next"] ?? "none"}." : $"[vault] GET {path}: ok.");
                Reply(id, true, parsed);
            });
            return Pending();
        }

        internal static JObject Install(string id, string code)
        {
            if (!CardLibrary.IsValidCode(code)) { Core.Log.Warning($"[vault] install refused: '{code}' is not a card code."); return Calls.Fail("Not a card code."); }
            Core.Log.Msg($"[vault] installing card {code}.");
            using var scope = CardsApi.Caller(Caller);
            CardsApi.VaultInstall(code, json =>
            {
                var r = Parse(json, "install " + code);
                if ((bool?)r["ok"] == true)
                {
                    Core.Log.Msg($"[vault] card {code} installed as '{(string)r["name"]}'{((bool?)r["alreadyInstalled"] == true ? " (already installed)" : "")}.");
                    Library.AfterInstall(r);
                }
                else Core.Log.Error($"[vault] installing card {code} failed: {(string)r["error"]} {(string)r["message"]}");
                Reply(id, true, r);
            });
            return Pending();
        }

        internal static JObject Connect()
        {
            Core.Log.Msg("[vault] connect started.");
            using var scope = CardsApi.Caller(Caller);
            CardsApi.VaultConnect(json =>
            {
                var s = Parse(json, "connect");
                Core.Log.Msg($"[vault] connect: {(string)s["state"]}{((string)s["message"] is string m && m.Length > 0 ? " - " + m : "")}.");
                Pages.Emit("vault.connect", s);
                if ((string)s["state"] == "connected") Pages.Emit("vault.state", State());
            });
            return Pending();
        }

        internal static JObject Disconnect()
        {
            Core.Log.Msg("[vault] disconnect asked.");
            using var scope = CardsApi.Caller(Caller);
            CardsApi.VaultDisconnect(ok =>
            {
                Core.Log.Msg($"[vault] disconnected; token revoked on the vault: {ok}.");
                Pages.Emit("vault.state", State());
            });
            // Personnel forgets the token at once; the page can redraw now.
            return State();
        }

        /// <summary>Publishes a card file from Personnel's import folder (or its Done folder).</summary>
        internal static JObject Upload(string id, string file)
        {
            string full = Library.UploadPath(file);
            if (full == null) return Calls.Fail("File not found.");
            if (!VaultClient.IsConnected) { Core.Log.Warning($"[vault] upload of {file} refused: not connected."); return Calls.Fail("Connect this game to NPC Vault first."); }
            byte[] png;
            try { png = File.ReadAllBytes(full); }
            catch (Exception e) { Core.Log.Error($"[vault] reading {full} for the upload failed: {e.Message}"); return Calls.Fail("The file could not be read."); }
            Core.Log.Msg($"[vault] uploading {Path.GetFileName(full)} ({png.Length / 1024} KB).");
            using var scope = CardsApi.Caller(Caller);
            CardsApi.VaultUpload(png, json =>
            {
                var r = Parse(json, "upload " + file);
                if ((bool?)r["ok"] == true) Core.Log.Msg($"[vault] {file} published as {(string)r["code"]} ({(string)r["url"]}).");
                else if ((bool?)r["duplicate"] == true) Core.Log.Msg($"[vault] {file} is already on the vault as {(string)r["code"]}.");
                else Core.Log.Error($"[vault] upload of {file} failed: {(string)r["message"]}");
                Reply(id, true, r);
            });
            return Pending();
        }

        /// <summary>
        /// Loads a card's small picture (public, no sign-in) and shows it as s1://vault-&lt;code&gt;; the answer is the
        /// event "vault.thumb" { code, url } or { code, url: null } after a logged failure.
        /// </summary>
        internal static JObject Thumb(string code)
        {
            if (!CardLibrary.IsValidCode(code)) { Core.Log.Warning($"[vault] thumb refused: '{code}' is not a card code."); return Calls.Fail("Not a card code."); }
            string name = "vault-" + code;
            if (Pages.HasPicture(name)) return new JObject { ["ok"] = true, ["url"] = "s1://" + name };
            if (!Loading.Add(code)) return Pending();
            string url = BaseUrl.TrimEnd('/') + "/c/" + code + "/thumb.png";
            Task.Run(async () =>
            {
                byte[] png = null;
                string error = null;
                try
                {
                    using var res = await Http.GetAsync(url).ConfigureAwait(false);
                    if (res.IsSuccessStatusCode) png = await res.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                    else error = "HTTP " + (int)res.StatusCode;
                }
                catch (Exception e) { error = e.GetType().Name + ": " + e.GetBaseException().Message; }
                MainThread.Post("thumb " + code, () =>
                {
                    Loading.Remove(code);
                    if (png == null)
                    {
                        Core.Log.Error($"[vault] picture {url} failed ({error}).");
                        Pages.Emit("vault.thumb", new JObject { ["code"] = code, ["url"] = null });
                        return;
                    }
                    Pages.Emit("vault.thumb", new JObject { ["code"] = code, ["url"] = Pages.Picture(name, png) });
                });
            });
            return Pending();
        }

        /// <summary>Opens a page in the player's browser: the vault's own pages and the Personify listing only.</summary>
        internal static JObject OpenUrl(string url)
        {
            string vault = BaseUrl.TrimEnd('/') + "/";
            bool allowed = !string.IsNullOrEmpty(url) && (url.StartsWith(vault, StringComparison.OrdinalIgnoreCase) || url == Calls.PersonifyUrl || url == SiestaHint.Url);
            if (!allowed) { Core.Log.Warning($"[vault] opening '{url}' refused: not a vault page."); return Calls.Fail("Something went wrong. See the log."); }
            Application.OpenURL(url);
            Core.Log.Msg($"[vault] opened {url} in the browser.");
            return new JObject { ["ok"] = true };
        }

        internal static JObject Copy(string text)
        {
            if (string.IsNullOrEmpty(text) || text.Length > 64) { Core.Log.Warning("[vault] copy refused: empty or too long."); return Calls.Fail("Something went wrong. See the log."); }
            GUIUtility.systemCopyBuffer = text;
            Core.Log.Msg($"[vault] '{text}' copied to the clipboard.");
            return new JObject { ["ok"] = true };
        }

        private static JObject Parse(string json, string what)
        {
            if (string.IsNullOrEmpty(json)) { Core.Log.Error($"[vault] {what}: Personnel answered nothing."); return new JObject { ["ok"] = false, ["message"] = "Personnel answered nothing." }; }
            try { return JObject.Parse(json); }
            catch (JsonException e) { Core.Log.Error($"[vault] {what}: Personnel's answer is not JSON ({e.Message})."); return new JObject { ["ok"] = false, ["message"] = "Personnel answered something unreadable." }; }
        }
    }
}
