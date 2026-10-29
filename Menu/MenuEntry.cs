using System;
using System.Linq;
using System.Runtime.CompilerServices;
using SideHustle;

namespace NpcCards.Menu
{
    /// <summary>
    /// The Side Hustle main-menu gamemode. Every Side Hustle type stays in this class, and its methods are only
    /// reached after the assembly was found, so the mod loads without Side Hustle and keeps the phone app.
    /// </summary>
    internal static class MenuEntry
    {
        internal const string GamemodeId = "doodesch.npccards";

        private static LaunchContext _ctx;

        internal static void TryRegister()
        {
            bool loaded = AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name == "SideHustle");
            if (!loaded)
            {
                Core.Log.Msg("[menu] Side Hustle is not installed - NPC Cards is a phone app only.");
                return;
            }
            try { Register(); }
            catch (Exception e) { Core.Log.Warning($"[menu] registering with Side Hustle failed ({e.Message}) - NPC Cards is a phone app only."); }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Register()
        {
            API.Register(new GamemodeDescriptor
            {
                Id = GamemodeId,
                DisplayName = "NPC Cards",
                Description = Host.Strings.T("Add new NPCs in one click."),
                Author = "DooDesch",
                Support = GamemodeSupport.Singleplayer,
                Surface = GamemodeSurface.MenuSpace,
                OnLaunchSingleplayer = OnLaunch,
                OnExitToHub = OnExit,
            });
            Core.Log.Msg("[menu] registered with Side Hustle as a main-menu gamemode.");
        }

        private static void OnLaunch(LaunchContext ctx)
        {
            _ctx = ctx;
            MenuSurface.Open(ReturnToHub);
        }

        private static void OnExit(LaunchContext ctx)
        {
            _ctx = null;
            MenuSurface.Close("Side Hustle left the gamemode");
        }

        private static void ReturnToHub()
        {
            var ctx = _ctx;
            if (ctx == null) { Core.Log.Warning("[menu] back to the hub asked without a launch context - closing the page only."); MenuSurface.Close("no launch context"); return; }
            try { ctx.ReturnToHub(); }
            catch (Exception e)
            {
                Core.Log.Error($"[menu] returning to the Side Hustle hub failed: {e.Message}");
                MenuSurface.Close("returning to the hub failed");
            }
        }
    }
}
