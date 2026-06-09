using KitLib;
using KitLib.Abstractions.Host;
using KitLib.AI.AutoPlay;
using KitLib.AI.Planning;
using KitLib.Companion;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.SyncBot;

namespace KitLib.AI;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai)) return;
        KitLibHost.AnnounceModule(KitLibModuleIds.Ai);
        KitLibHost.TrySummonCompanion = request => CompanionSpawnService.TrySpawn(request);
        KitLibHost.TryDismissCompanion = CompanionSpawnService.TryDismissViaBridge;
        KitLibHost.ListCompanionsHandler = CompanionSpawnService.ListForBridge;
        KitLibHost.IsHostMultiplayerRun = () =>
            MultiplayerRunProbe.InMultiplayerRun && MultiplayerRunProbe.IsHost;
        KitLibHost.RegisterNetIdStrategyDelegate = CompanionRegistry.Register;
        KitLibHost.UnregisterNetIdStrategyDelegate = CompanionRegistry.Unregister;
        KitLibHost.RegisterDeckPlanContributorHandler = DeckPlanContributorHub.Register;
        KitLibHost.StopAiPlayLoop = () => AiPlayModule.Instance.StopLoop();
        KitLibHost.OnCompanionRunEnded = CompanionRegistry.ClearOnRunEnd;

        KitLibSyncBotOps.IsEnabled = () => MpCheatSyncBot.IsEnabled;
        KitLibSyncBotOps.OnRunEnded = MpCheatSyncBot.OnRunEnded;
        KitLibSyncBotOps.InjectPrepareAcks = message => {
            if (message is MpCheatCommandMessage cmd)
                MpCheatSyncBot.InjectPrepareAcks(cmd);
        };

        AiPlayInitializer.Initialize();
        AiTabRegistration.Register();

        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.Ai);
        MainFile.Logger.Info("KitLib.AI module initialized.");
    }
}
