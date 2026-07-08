using System;
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

        SettingsStore.Load();
        WirePanelDelegates();
        WirePseudoCoopDelegates();
        KitLibHost.TryEnsurePseudoCoopPresetHandler = () => {
            if (!Companion.CompanionBridge.IsAvailable) return false;
            PseudoCoopBootstrap.ApplyPreset();
            return true;
        };
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai))
            WireAiHudDelegates();
        KitLibHost.SyncPerfHudOverlay = () => {
            KitLibRootServices.EnsureRootServicesNode();
            DevPerfOverlayUI.SyncVisibility();
        };
        TryWireDevPanelHotkeys();

        DevPerfBuiltinProviders.RegisterAll();

        KitLibHarmony.Apply(
            typeof(ModuleEntry).Assembly,
            KitLibModuleIds.Panel,
            typeof(MainMenuPatch),
            typeof(GlobalUiReadyPatch));

        KitLibHost.AnnounceModule(KitLibModuleIds.Panel);
        MainFile.Logger.Info("KitLib.Panel module initialized.");
    }

    static void TryWireDevPanelHotkeys() {
        try {
            KitLibHost.NotifyHotkeySettingsChanged = DevPanelUI.RefreshPeekTabHotkeyHint;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib.Panel: Dev panel hotkeys unavailable ({ex.Message}).");
        }
    }

    static bool IsCheatAssemblyAvailable() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Cheat))
            return true;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies()) {
            if (string.Equals(assembly.GetName().Name, "KitLib.Cheat", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static void WireAiHudDelegates() {
        KitLibPanelOps.OnPanelAttach = ui => AiHudOverlayUI.Attach(ui);
        KitLibPanelOps.OnPanelSync = ui => AiHudOverlayUI.SyncState(ui);
        KitLibPanelOps.OnPanelDetach = ui => AiHudOverlayUI.Detach(ui);
        KitLibHost.SyncAiHudOverlay = () => AiHudOverlayUI.SyncState();
    }

    static void WirePseudoCoopDelegates() {
        KitLibPseudoCoopOps.EnsureGlobalUiProcessNode = globalUi =>
            GlobalUiReadyPatch.EnsureProcessNodeOnly(globalUi as NGlobalUi);
        KitLibPseudoCoopOps.AttachDeferredDevPanel = () =>
            GlobalUiReadyPatch.TryAttachDeferred(NRun.Instance?.GlobalUi, skipWarmup: true);
        KitLibPseudoCoopOps.AttachDualInstanceMinimalDevPanel = () =>
            GlobalUiReadyPatch.TryAttachDualInstanceMinimal(NRun.Instance?.GlobalUi);
        try {
            KitLibPseudoCoopOps.IsDevPanelRailAttached = () => DevPanelUI.IsRailAttached;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib.Panel: pseudo-coop rail probe unavailable ({ex.Message}).");
        }
        KitLibPseudoCoopOps.RunDualInstanceLanPresets = DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch;
        KitLibPseudoCoopOps.EnsureMultiplayerDevActive = DualInstanceTestBootstrap.EnsureMultiplayerDevActive;
    }

    static void WirePanelDelegates() {
        KitLibPanelOps.TryDismissCurrent = ui => DevPanel.TryDismissCurrent();

        if (IsCheatAssemblyAvailable()) {
            KitLibCheatOps.OpenCards = DevPanel.OpenCards;
            KitLibCheatOps.OpenRelics = DevPanel.OpenRelics;
            KitLibCheatOps.OpenEnemies = DevPanel.OpenEnemies;
            KitLibCheatOps.OpenPowers = DevPanel.OpenPowers;
            KitLibCheatOps.OpenPotions = DevPanel.OpenPotions;
            KitLibCheatOps.OpenEvents = DevPanel.OpenEvents;
            KitLibCheatOps.OpenRooms = DevPanel.OpenRooms;
            KitLibCheatOps.OpenConsole = DevPanel.OpenConsole;
            KitLibCheatOps.OpenPresets = DevPanel.OpenPresets;
            KitLibCheatOps.OpenCardTest = DevPanel.OpenCardTest;
            KitLibCheatOps.ResetSkipAnim = SkipAnimControl.Reset;
            KitLibCheatOps.IsSkipAnimSkipping = () => SkipAnimControl.IsSkipping;
            KitLibCheatOps.IsMpHooksDisabledInMultiplayer = () => MpCheatUi.IsHooksDisabledInMultiplayer;
        }

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

        TryWireDevPanelUiDelegates();
        KitLibPanelUiOps.BuildProgressGuardModSettingsPage = () => ProgressGuardModSettingsPage.Build();
    }

    static void TryWireDevPanelUiDelegates() {
        try {
            if (IsCheatAssemblyAvailable()) {
                KitLibPanelUiOps.ShowCheatsOverlay = ui =>
                    DevPanelUI.ShowCheatsOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
                KitLibPanelUiOps.ShowSaveLoadOverlay = ui =>
                    DevPanelUI.ShowSaveLoadOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
            }

            KitLibPanelUiOps.ShowSettingsOverlay = ui =>
                DevPanelUI.ShowSettingsOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
            if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Ai))
                KitLibPanelUiOps.ShowAiOverlay = ui =>
                    DevPanelUI.ShowAiOverlay((NGlobalUi)ui, DevPanelSession.Actions!);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib.Panel: dev panel UI delegates unavailable ({ex.Message}).");
        }
    }
}
