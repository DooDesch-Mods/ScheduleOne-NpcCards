using System;
using Newtonsoft.Json.Linq;
using Sideload.Api;
using UnityEngine;

namespace NpcCards.Menu
{
    /// <summary>
    /// The app page as a full-screen surface over the main menu. A surface has no back gesture, so Escape and
    /// right-click are sent to the page as "nav.back"; the page steps back one level or asks to close.
    /// </summary>
    internal static class MenuSurface
    {
        private const string SurfaceId = "npccards-menu";

        private static GameObject _root;
        private static Action _returnToHub;
        private static bool _closeRequested;

        internal static bool IsOpen => _root != null;

        internal static void Open(Action returnToHub)
        {
            _returnToHub = returnToHub;
            if (!Surfaces.Available)
            {
                Core.Log.Error("[menu] Sideload 1.36 or newer is needed for the menu page - it was not found.");
                returnToHub?.Invoke();
                return;
            }
            if (_root != null) Close("opened again");

            _root = new GameObject("NpcCards_Menu");
            UnityEngine.Object.DontDestroyOnLoad(_root);
            var canvas = _root.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 30000;
            _root.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // The page is laid out for a 720 CSS pixel short side and scales with the screen.
            var surface = Surfaces.Mount(_root.GetComponent<RectTransform>(), SurfaceId, Host.Calls.Bundle, 720f);
            if (!Surfaces.IsMounted(SurfaceId))
            {
                Core.Log.Error("[menu] the page could not be mounted - see the Sideload log lines above.");
                Close("mount failed");
                returnToHub?.Invoke();
                return;
            }
            Host.Calls.RegisterMenu(surface);
            Host.Pages.AttachMenu(surface);
            Core.Log.Msg("[menu] page mounted over the main menu.");
        }

        internal static void Close(string why)
        {
            if (_root == null) return;
            Host.Pages.DetachMenu();
            try { Surfaces.Unmount(SurfaceId); }
            catch (Exception e) { Core.Log.Warning($"[menu] unmounting the page failed: {e.Message}"); }
            UnityEngine.Object.Destroy(_root);
            _root = null;
            Core.Log.Msg($"[menu] page closed ({why}).");
        }

        /// <summary>The page's "menu.close": leaves on the next frame, so the call returns before the page goes.</summary>
        internal static JObject RequestClose(string where)
        {
            if (where != "menu" || _root == null) { Core.Log.Warning($"[menu] close asked from the {where} page while the menu page is {(IsOpen ? "open" : "closed")} - ignored."); return Host.Calls.Fail("Something went wrong. See the log."); }
            _closeRequested = true;
            return new JObject { ["ok"] = true };
        }

        internal static void Tick()
        {
            if (_root == null) return;
            if (_closeRequested)
            {
                _closeRequested = false;
                var back = _returnToHub;
                if (back != null) back();
                else Close("closed from the page");
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) Host.Pages.EmitMenu("nav.back", null);
        }
    }
}
