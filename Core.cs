using MelonLoader;

[assembly: MelonInfo(typeof(NpcCards.Core), "NPC Cards", DooDesch.ModVersion.Current, "DooDesch", "https://github.com/DooDesch-Mods/ScheduleOne-NpcCards")]
[assembly: MelonGame("TVGS", "Schedule I")]
[assembly: MelonOptionalDependencies("SideHustle")]

namespace NpcCards
{
    /// <summary>
    /// MelonLoader entry point for NPC Cards. Registers the phone app with Sideload and, when Side Hustle is installed,
    /// a main-menu gamemode that shows the same page. Card work itself is Personnel's (<c>Personnel.Cards.CardsApi</c>).
    /// </summary>
    public sealed class Core : MelonMod
    {
        public static Core Instance { get; private set; }
        public static MelonLogger.Instance Log { get; private set; }

        public override void OnInitializeMelon()
        {
            Instance = this;
            Log = LoggerInstance;
            Host.Strings.Register();
            Host.Library.Start();
            Host.Calls.RegisterPhone();
            Menu.MenuEntry.TryRegister();
            Log.Msg($"NPC Cards {Info.Version} ready (Personnel cards API, vault {Host.Vault.BaseUrl}).");
        }

        public override void OnUpdate()
        {
            Host.MainThread.Drain();
            Menu.MenuSurface.Tick();
        }

        public override void OnSceneWasUnloaded(int buildIndex, string sceneName)
        {
            if (sceneName == "Menu") Menu.MenuSurface.Close("the menu scene unloaded");
        }
    }
}
