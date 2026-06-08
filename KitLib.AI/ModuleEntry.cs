using HarmonyLib;
using KitLib.AI.AutoPlay;
using KitLib.AI.Planning;
using KitLib.Abstractions.Host;
using KitLib.Companion;
using KitLib.Host;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.AI;

[ModInitializer(nameof(Initialize))]
public static class ModuleEntry {
    public static void Initialize() {
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

        AiPlayInitializer.Initialize();
        AiTabRegistration.Register();

        KitLibFeaturesPatches.EnsureApplied();
        MainFile.Logger.Info("KitLib.AI module initialized.");
    }
}
