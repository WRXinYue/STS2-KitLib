using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Panels;
using KitLib.Companion;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.PanelMod;

[ModInitializer(nameof(Initialize))]
public static class ModuleEntry {
    public static void Initialize() {
        KitLibHost.AnnounceModule(KitLibModuleIds.Panel);
        SettingsStore.Load();
        WirePanelDelegates();
        KitLibHost.TryEnsurePseudoCoopPresetHandler = () => {
            if (!CompanionBridge.IsAvailable) return false;
            PseudoCoopBootstrap.ApplyPreset();
            return true;
        };
        KitLibPanelOps.OnPanelAttach = ui => AiHudOverlayUI.Attach(ui);
        KitLibPanelOps.OnPanelSync = ui => AiHudOverlayUI.SyncState(ui);
        KitLibPanelOps.OnPanelDetach = ui => AiHudOverlayUI.Detach(ui);

        KitLibFeaturesPatches.EnsureApplied();
        MainFile.Logger.Info("KitLib.Panel module initialized.");
    }

    static void WirePanelDelegates() {
        KitLibPanelOps.TryDismissCurrent = ui => DevPanel.TryDismissCurrent();
        KitLibCheatOps.OpenCards = DevPanel.OpenCards;
        KitLibCheatOps.OpenRelics = DevPanel.OpenRelics;
        KitLibCheatOps.OpenEnemies = DevPanel.OpenEnemies;
        KitLibCheatOps.OpenPowers = DevPanel.OpenPowers;
        KitLibCheatOps.OpenPotions = DevPanel.OpenPotions;
        KitLibCheatOps.OpenEvents = DevPanel.OpenEvents;
        KitLibCheatOps.OpenRooms = DevPanel.OpenRooms;
        KitLibCheatOps.OpenConsole = DevPanel.OpenConsole;
        KitLibCheatOps.OpenPresets = DevPanel.OpenPresets;

        KitLibDevOps.OpenHooks = DevPanel.OpenHooks;
        KitLibDevOps.OpenScripts = DevPanel.OpenScripts;
        KitLibDevOps.OpenCombatStats = DevPanel.OpenCombatStats;
        KitLibDevOps.OpenEnemyIntent = DevPanel.OpenEnemyIntent;
        KitLibDevOps.OpenHarmonyAnalysis = DevPanel.OpenHarmonyAnalysis;
        KitLibDevOps.OpenFrameworks = DevPanel.OpenFrameworks;
        KitLibDevOps.OpenFeedback = DevPanel.OpenFeedback;

        KitLibUserOps.OpenLogs = DevPanel.OpenLogs;
        KitLibUserOps.OpenManual = DevPanel.OpenManual;

        KitLibPanelUiOps.ShowCheatsOverlay = ui => DevPanelUI.ShowCheatsOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
        KitLibPanelUiOps.ShowSaveLoadOverlay = ui => DevPanelUI.ShowSaveLoadOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
        KitLibPanelUiOps.ShowSettingsOverlay = ui => DevPanelUI.ShowSettingsOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
        KitLibPanelUiOps.ShowAiOverlay = ui => DevPanelUI.ShowAiOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
    }
}
