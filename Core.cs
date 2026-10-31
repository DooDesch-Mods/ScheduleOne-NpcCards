using System.Runtime.CompilerServices;
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
            bool personnel = Host.PersonnelCheck.Detect();
            if (personnel) StartCards();
            Host.Calls.RegisterPhone();
            Menu.MenuEntry.TryRegister();
            if (personnel) LogReady();
            else Log.Msg($"NPC Cards {Info.Version} ready without Personnel: the app only shows the notice.");
        }

        // Kept out of OnInitializeMelon: the JIT resolves Personnel's types when it compiles a method that names them,
        // and a missing Personnel.dll must not take the phone app down with it.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartCards() => Host.Library.Start();

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void LogReady() => Log.Msg($"NPC Cards {Info.Version} ready (Personnel cards API, vault {Host.Vault.BaseUrl}).");

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
