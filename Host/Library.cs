using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Personnel.Cards;
using UnityEngine;

namespace NpcCards.Host
{
    /// <summary>
    /// Installed cards and what changes at the next start. Personnel loads packs once, when the game starts; the cards
    /// installed then are the snapshot, and the difference to the current list is what arrives, leaves or changes after
    /// a restart. Cards that come in through the vault inbox ("Send to game" on the website) are collected for the
    /// Account tab. The import folder is Personnel's drop folder: its PNGs install by themselves at the next start, and
    /// the Import tab installs one now.
    /// </summary>
    internal static class Library
    {
        private sealed class Arrival { public string Code; public string Name; public DateTime At; }

        private static Dictionary<string, string> _loaded;
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>(StringComparer.Ordinal);
        private static readonly List<Arrival> Inbox = new List<Arrival>();
        private static HashSet<string> _known;
        private static readonly Dictionary<string, JObject> FileCache = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);

        internal static string ImportDir => CardsApi.ImportFolder();
        private static string DoneDir => Path.Combine(ImportDir, "Done");

        /// <summary>Takes the snapshot and listens for changes. Personnel loaded its packs in its own init, before ours.</summary>
        internal static void Start()
        {
            var list = Installed();
            _loaded = list.ToDictionary(c => (string)c["code"], c => (string)c["contentHash"] ?? "", StringComparer.Ordinal);
            _known = new HashSet<string>(_loaded.Keys, StringComparer.Ordinal);
            foreach (var c in list) Names[(string)c["code"]] = (string)c["name"];
            if (CardsApi.PendingRestart())
                Core.Log.Warning("[cards] Personnel already reports changed cards at our start; the 'after the next start' count may miss some.");
            CardLibrary.Changed += () => MainThread.Post("cards changed", OnChanged);
            Core.Log.Msg($"[cards] {_loaded.Count} card(s) loaded this game; import folder {ImportDir}.");
        }

        private static List<JObject> Installed()
        {
            try { return JArray.Parse(CardsApi.List()).OfType<JObject>().Where(c => !string.IsNullOrEmpty((string)c["code"])).ToList(); }
            catch (Exception e) { Core.Log.Error($"[cards] reading the installed cards from Personnel failed: {e.Message}"); return new List<JObject>(); }
        }

        private static void OnChanged()
        {
            var list = Installed();
            foreach (var c in list)
            {
                string code = (string)c["code"];
                Names[code] = (string)c["name"];
                if (_known.Add(code) && ((string)c["source"] ?? "").StartsWith("inbox ", StringComparison.Ordinal))
                {
                    Inbox.Insert(0, new Arrival { Code = code, Name = (string)c["name"] ?? code, At = DateTime.Now });
                    Core.Log.Msg($"[cards] '{(string)c["name"]}' ({code}) arrived from the website.");
                }
            }
            Pages.Emit("cards.changed", Summary());
        }

        /// <summary>Called after an install from the page: the game notification, then the page redraws on "cards.changed".</summary>
        internal static void AfterInstall(JObject r)
        {
            if ((bool?)r["ok"] != true || (bool?)r["alreadyInstalled"] == true) return;
            Pages.Notify((string)r["name"] ?? (string)r["code"], Strings.T("Arrives after the next game start."));
        }

        /// <summary>What changes at the next start: <c>{ arriving, leaving, changed }</c> as name lists.</summary>
        internal static JObject Summary()
        {
            var now = Installed();
            var nowCodes = now.ToDictionary(c => (string)c["code"], c => (string)c["contentHash"] ?? "", StringComparer.Ordinal);
            var arriving = new JArray(now.Where(c => !_loaded.ContainsKey((string)c["code"])).Select(c => (string)c["name"]));
            var changed = new JArray(now.Where(c => _loaded.TryGetValue((string)c["code"], out var h) && h != ((string)c["contentHash"] ?? "")).Select(c => (string)c["name"]));
            var leaving = new JArray(_loaded.Keys.Where(k => !nowCodes.ContainsKey(k)).Select(k => Names.TryGetValue(k, out var n) && n != null ? n : k));
            return new JObject { ["arriving"] = arriving, ["leaving"] = leaving, ["changed"] = changed };
        }

        // ---- installed --------------------------------------------------------------------------------------------

        internal static JObject List()
        {
            var cards = new JArray();
            foreach (var c in Installed())
            {
                string code = (string)c["code"];
                cards.Add(new JObject
                {
                    ["code"] = code, ["name"] = (string)c["name"] ?? code, ["role"] = (string)c["role"], ["creator"] = (string)c["creator"],
                    ["importedAt"] = c["importedAt"], ["basedOn"] = (string)c["basedOn"],
                    ["loaded"] = _loaded.ContainsKey(code), ["picture"] = Portrait(code),
                });
            }
            Core.Log.Msg($"[cards] listed {cards.Count} installed card(s).");
            return new JObject { ["ok"] = true, ["cards"] = cards, ["next"] = Summary() };
        }

        // The contact picture Personnel stores with an installed card.
        private static string Portrait(string code)
        {
            string name = "portrait-" + code;
            if (Pages.HasPicture(name)) return "s1://" + name;
            string path = Path.Combine(CardLibrary.PackDir, code, "portrait.png");
            if (!File.Exists(path)) return null;
            try { return Pages.Picture(name, File.ReadAllBytes(path)); }
            catch (Exception e) { Core.Log.Warning($"[cards] reading the picture of {code} failed ({e.Message}); the row shows none."); return null; }
        }

        internal static JObject Remove(string code)
        {
            if (!CardLibrary.IsValidCode(code)) { Core.Log.Warning($"[cards] remove refused: '{code}' is not a card code."); return Calls.Fail("Not a card code."); }
            bool ok = CardsApi.Remove(code);
            Core.Log.Msg($"[cards] remove {code}: {(ok ? "removed" : "no such card")}.");
            return ok ? new JObject { ["ok"] = true, ["next"] = Summary() } : Calls.Fail("This card is not installed.");
        }

        // ---- import folder ----------------------------------------------------------------------------------------

        /// <summary>Card PNGs waiting in the import folder, each read once per file version.</summary>
        internal static JObject ImportFiles()
        {
            var files = new JArray();
            string dir = ImportDir;
            try
            {
                Directory.CreateDirectory(dir);
                foreach (var fi in new DirectoryInfo(dir).GetFiles("*.png").OrderBy(f => f.Name, StringComparer.OrdinalIgnoreCase))
                    files.Add(Describe(fi));
            }
            catch (Exception e)
            {
                Core.Log.Error($"[cards] listing {dir} failed: {e.Message}");
                return Calls.Fail("The import folder could not be read.");
            }
            Core.Log.Msg($"[cards] import folder: {files.Count} file(s) in {dir}.");
            return new JObject { ["ok"] = true, ["folder"] = dir, ["files"] = files };
        }

        private static JObject Describe(FileInfo fi)
        {
            string key = fi.FullName + "|" + fi.LastWriteTimeUtc.Ticks + "|" + fi.Length;
            if (FileCache.TryGetValue(key, out var cached)) return cached;
            var o = new JObject { ["file"] = fi.Name, ["sizeKb"] = fi.Length / 1024 };
            try
            {
                byte[] png = File.ReadAllBytes(fi.FullName);
                var card = JObject.Parse(CardsApi.ReadCard(png));
                if ((bool?)card["ok"] == true)
                {
                    var npc = card["npc"] as JObject;
                    o["card"] = true;
                    o["name"] = (string)npc?["name"] ?? Path.GetFileNameWithoutExtension(fi.Name);
                    o["role"] = CardLibrary.RoleOf(npc);
                    o["creator"] = (string)card["meta"]?["creator"];
                    o["picture"] = Pages.Picture("file-" + Fnv(key), png);
                }
                else
                {
                    o["card"] = false;
                    o["message"] = (string)card["message"];
                    Core.Log.Msg($"[cards] {fi.Name} in the import folder is not a card: {(string)card["error"]} {(string)card["message"]}");
                }
            }
            catch (Exception e)
            {
                o["card"] = false;
                o["message"] = e.Message;
                Core.Log.Warning($"[cards] reading {fi.Name} from the import folder failed: {e.Message}");
            }
            FileCache[key] = o;
            return o;
        }

        /// <summary>Installs one file from the import folder and moves it to Import/Done, as Personnel does at start.</summary>
        internal static JObject ImportFile(string file)
        {
            string full = InFolder(ImportDir, file);
            if (full == null) return Calls.Fail("File not found.");
            var r = JObject.Parse(CardsApi.ImportFile(full));
            if ((bool?)r["ok"] != true)
            {
                Core.Log.Error($"[cards] installing {file} from the import folder failed: {(string)r["error"]} {(string)r["message"]}");
                r["message"] = (string)r["message"] ?? Strings.T("This file is not a card.");
                return r;
            }
            Core.Log.Msg($"[cards] {file} installed as '{(string)r["name"]}' ({(string)r["code"]}){((bool?)r["alreadyInstalled"] == true ? ", already installed" : "")}.");
            MoveToDone(full);
            AfterInstall(r);
            return r;
        }

        private static void MoveToDone(string full)
        {
            try
            {
                Directory.CreateDirectory(DoneDir);
                string target = Path.Combine(DoneDir, Path.GetFileName(full));
                if (File.Exists(target)) target = Path.Combine(DoneDir, Path.GetFileNameWithoutExtension(full) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".png");
                File.Move(full, target);
                Core.Log.Msg($"[cards] moved {Path.GetFileName(full)} to {target}.");
            }
            catch (Exception e)
            {
                Core.Log.Warning($"[cards] moving {full} to {DoneDir} failed ({e.Message}); Personnel installs it again at the next start and finds it already there.");
            }
        }

        internal static JObject OpenFolder()
        {
            string dir = ImportDir;
            try
            {
                Directory.CreateDirectory(dir);
                Application.OpenURL(new Uri(dir).AbsoluteUri);
                Core.Log.Msg($"[cards] opened {dir} in the file browser.");
                return new JObject { ["ok"] = true, ["folder"] = dir };
            }
            catch (Exception e)
            {
                Core.Log.Error($"[cards] opening {dir} failed: {e.Message}");
                return Calls.Fail("The folder could not be opened.");
            }
        }

        /// <summary>Card files that can be uploaded: the import folder and its Done folder, newest first.</summary>
        internal static JObject UploadFiles()
        {
            var files = new JArray();
            foreach (string dir in new[] { ImportDir, DoneDir })
            {
                if (!Directory.Exists(dir)) continue;
                try
                {
                    foreach (var fi in new DirectoryInfo(dir).GetFiles("*.png").OrderByDescending(f => f.LastWriteTimeUtc))
                    {
                        var d = Describe(fi);
                        if ((bool?)d["card"] != true) continue;
                        var o = (JObject)d.DeepClone();
                        o["file"] = dir == DoneDir ? "Done/" + fi.Name : fi.Name;
                        files.Add(o);
                    }
                }
                catch (Exception e) { Core.Log.Error($"[cards] listing {dir} for uploads failed: {e.Message}"); }
            }
            Core.Log.Msg($"[cards] {files.Count} card file(s) can be uploaded.");
            return new JObject { ["ok"] = true, ["files"] = files };
        }

        /// <summary>The full path of an upload candidate ("x.png" or "Done/x.png"), or null after a logged refusal.</summary>
        internal static string UploadPath(string file)
        {
            if (file != null && file.StartsWith("Done/", StringComparison.Ordinal)) return InFolder(DoneDir, file.Substring(5));
            return InFolder(ImportDir, file);
        }

        private static string InFolder(string dir, string file)
        {
            if (string.IsNullOrEmpty(file) || file.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || file.Contains(".."))
            {
                Core.Log.Warning($"[cards] '{file}' is not a file name in {dir}.");
                return null;
            }
            string full = Path.Combine(dir, file);
            if (!File.Exists(full)) { Core.Log.Warning($"[cards] {full} does not exist."); return null; }
            return full;
        }

        // ---- inbox ------------------------------------------------------------------------------------------------

        internal static JArray Arrivals() => new JArray(Inbox.Select(a => new JObject
        {
            ["code"] = a.Code, ["name"] = a.Name, ["at"] = a.At.ToString("HH:mm"), ["picture"] = Portrait(a.Code),
        }));

        private static string Fnv(string s)
        {
            uint h = 2166136261;
            foreach (byte b in Encoding.UTF8.GetBytes(s)) { h ^= b; h *= 16777619; }
            return h.ToString("x8");
        }
    }
}
