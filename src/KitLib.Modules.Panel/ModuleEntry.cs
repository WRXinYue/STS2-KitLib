using KitLib;
using KitLib.Abstractions.Host;
using KitLib.DevPerf;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.LanTest;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Panels;
using KitLib.Patches;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.PanelMod;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Panel)) return;
        KitLibHost.AnnounceModule(KitLibModuleIds.Panel);
        SettingsStore.Load();
        WirePanelDelegates();
        WirePseudoCoopDelegates();
        KitLibHost.TryEnsurePseudoCoopPresetHandler = () => {
            if (!Companion.CompanionBridge.IsAvailable) return false;
            PseudoCoopBootstrap.ApplyPreset();
            return true;
        };
        KitLibPanelOps.OnPanelAttach = ui => AiHudOverlayUI.Attach(ui);
        KitLibPanelOps.OnPanelSync = ui => AiHudOverlayUI.SyncState(ui);
        KitLibPanelOps.OnPanelDetach = ui => AiHudOverlayUI.Detach(ui);
        KitLibHost.SyncAiHudOverlay = () => AiHudOverlayUI.SyncState();
        KitLibHost.SyncPerfHudOverlay = () => {
            KitLibRootServices.EnsureRootServicesNode();
            DevPerfOverlayUI.SyncVisibility();
        };
        KitLibHost.NotifyGameContextPaneChanged = DevPanelUI.OnGameContextPaneSettingChanged;
        KitLibHost.NotifyHotkeySettingsChanged = DevPanelUI.RefreshPeekTabHotkeyHint;

        DevPerfBuiltinProviders.RegisterAll();

        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.Panel);
        MainFile.Logger.Info("KitLib.Panel module initialized.");
    }

    static void WirePseudoCoopDelegates() {
        KitLibPseudoCoopOps.EnsureGlobalUiProcessNode = globalUi =>
            GlobalUiReadyPatch.EnsureProcessNodeOnly(globalUi as NGlobalUi);
        KitLibPseudoCoopOps.AttachDeferredDevPanel = () =>
            GlobalUiReadyPatch.TryAttachDeferred(NRun.Instance?.GlobalUi, skipWarmup: true);
        KitLibPseudoCoopOps.AttachDualInstanceMinimalDevPanel = () =>
            GlobalUiReadyPatch.TryAttachDualInstanceMinimal(NRun.Instance?.GlobalUi);
        KitLibPseudoCoopOps.IsDevPanelRailAttached = () => DevPanelUI.IsRailAttached;
        KitLibPseudoCoopOps.RunDualInstanceLanPresets = DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch;
        KitLibPseudoCoopOps.EnsureMultiplayerDevActive = DualInstanceTestBootstrap.EnsureMultiplayerDevActive;
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
        KitLibCheatOps.ResetSkipAnim = SkipAnimControl.Reset;
        KitLibCheatOps.IsSkipAnimSkipping = () => SkipAnimControl.IsSkipping;
        KitLibCheatOps.IsMpHooksDisabledInMultiplayer = () => MpCheatUi.IsHooksDisabledInMultiplayer;

        KitLibPanelOps.ShowErrorFeedbackFromCrash = report =>
            ErrorFeedbackPromptUI.TryShowFromCrash((KitLib.Feedback.CrashReport)report);
        KitLibPanelOps.IsCrashRecoveryPromptVisible = () => CrashRecoveryPromptUI.IsVisible;
        KitLibPanelOps.IsProgressLossPromptVisible = () => ProgressLossPromptUI.IsVisible;
        KitLibPanelOps.HideDevMainMenuIfVisible = () => {
            if (DevMainMenuUI.IsVisible)
                DevMainMenuUI.Hide();
        };

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
        KitLibPanelUiOps.BuildProgressGuardModSettingsPage = () => ProgressGuardModSettingsPage.Build();
    }
}
