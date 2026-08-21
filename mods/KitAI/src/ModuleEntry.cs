using KitLib;
using KitLib.Abstractions.Host;
using KitLib.AI.AutoPlay;
using KitLib.AI.Core;
using KitLib.AI.Planning;
using KitLib.AI.UI;
using KitLib.Companion;
using KitLib.Host;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Panels;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

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
        KitLibHost.RegisterNetIdStrategyDelegate = (netId, strategy) =>
            CompanionRegistry.Register(netId, (IDecisionMaker)strategy);
        KitLibHost.UnregisterNetIdStrategyDelegate = CompanionRegistry.Unregister;
        KitLibHost.RegisterDeckPlanContributorHandler = contributor =>
            DeckPlanContributorHub.Register((IDeckPlanContributor)contributor);
        KitLibHost.StopAiPlayLoop = () => AiPlayModule.Instance.StopLoop();
        KitLibHost.OnCompanionRunEnded = CompanionRegistry.ClearOnRunEnd;
        KitLibNetPlayOps.OnCombatActionFinished = netId => MpAiTeammateHost.NotifyCombatActionFinished(netId);
        KitLibNetPlayOps.OnRunEnded = () => {
            MpAiTeammateHost.OnRunEnded();
            SpvCompanionAiHost.OnRunEnded();
        };

        AiPlayInitializer.Initialize();
        WireAiHudDelegates();

        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.Ai);
        MainFile.Logger.Info("KitLib.AI module initialized.");
    }

    static void WireAiHudDelegates() {
        KitLibPanelOps.OnPanelAttach = ui => AiHudOverlayUI.Attach((NGlobalUi)ui);
        KitLibPanelOps.OnPanelSync = ui => AiHudOverlayUI.SyncState((NGlobalUi)ui);
        KitLibPanelOps.OnPanelDetach = ui => AiHudOverlayUI.Detach((NGlobalUi)ui);
        KitLibHost.SyncAiHudOverlay = () => AiHudOverlayUI.SyncState();
    }
}
