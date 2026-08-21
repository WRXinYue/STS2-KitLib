using System;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.DevPerf;
using KitLib.Host;
using KitLib.Interop;
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
        KitLibHost.SyncPerfHudOverlay = () => {
            KitLibRootServices.EnsureRootServicesNode();
            DevPerfOverlayUI.SyncVisibility();
        };
        KitLibHost.RebuildDevPanelRail = DevPanelUI.RebuildRailIfAttached;
        TryWireDevPanelHotkeys();

        DevPerfBuiltinProviders.RegisterAll();

        KitLibHarmony.Apply(
            typeof(ModuleEntry).Assembly,
            KitLibModuleIds.Panel,
            typeof(MainMenuPatch),
            typeof(GlobalUiReadyPatch),
            typeof(ErrorPopupKitLibLogExportPatch),
            typeof(MpUiDebugRestSiteReadyPatch),
            typeof(MpUiDebugRestSiteExitPatch),
            typeof(MpUiDebugTreasureRelicInitPatch),
            typeof(MpUiDebugTreasureRelicPickedPatch),
            typeof(MpUiDebugTreasureTreeExitPatch),
            typeof(MpUiDebugMapOpenPatch),
            typeof(MpUiDebugMapVotesRefreshPatch));

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

    static bool IsCheatAssemblyAvailable() =>
        KitLibHost.IsModuleLoaded(KitLibModuleIds.Cheat);

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
        KitLibPseudoCoopOps.EnsureMultiplayerDevActive = DualInstanceTestBootstrap.EnsureMultiplayerDevActive;
    }

    static void WirePanelDelegates() {
        KitLibPanelOps.TryDismissCurrent = ui => DevPanel.TryDismissCurrent();

        if (IsCheatAssemblyAvailable()) {
            KitLibCheatApi.OpenCards = DevPanel.OpenCards;
            KitLibCheatApi.OpenRelics = DevPanel.OpenRelics;
            KitLibCheatApi.OpenEnemies = DevPanel.OpenEnemies;
            KitLibCheatApi.OpenPowers = DevPanel.OpenPowers;
            KitLibCheatApi.OpenPotions = DevPanel.OpenPotions;
            KitLibCheatApi.OpenEvents = DevPanel.OpenEvents;
            KitLibCheatApi.OpenRooms = DevPanel.OpenRooms;
            KitLibCheatApi.OpenConsole = DevPanel.OpenConsole;
            KitLibCheatApi.OpenPresets = DevPanel.OpenPresets;
            KitLibCheatApi.OpenCardTest = DevPanel.OpenCardTest;
            KitLibCheatApi.ResetSkipAnim = SkipAnimControl.Reset;
            KitLibCheatApi.IsSkipAnimSkipping = () => SkipAnimControl.IsSkipping;
            KitLibCheatApi.IsMpHooksDisabledInMultiplayer = () => MpCheatUi.IsHooksDisabledInMultiplayer;
        }

        KitLibPanelOps.IsProgressLossPromptVisible = () => ProgressLossPromptUI.IsVisible;
        KitLibPanelOps.HideDevMainMenuIfVisible = () => {
            if (DevMainMenuUI.IsVisible)
                DevMainMenuUI.Hide();
        };

        KitLibDevOps.OpenHooks = DevPanel.OpenHooks;
        KitLibDevOps.OpenEnemyIntent = DevPanel.OpenEnemyIntent;
        KitLibDevOps.OpenLogExport = DevPanel.OpenLogExport;

        KitLibUserOps.OpenLogs = DevPanel.OpenLogs;

        TryWireDevPanelUiDelegates();
        KitLibPanelUiOps.BuildProgressGuardModSettingsPage = host =>
            ProgressGuardModSettingsPage.Build(host as Node);
        KitLibPanelUiOps.QueryModHarmonyPatchStats = ModHarmonyOwnerMatcher.TryGetStats;
        KitLibPanelUiOps.BuildModHarmonyDetailReport = ModHarmonyOwnerMatcher.BuildDetailReport;
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
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib.Panel: dev panel UI delegates unavailable ({ex.Message}).");
        }
    }
}
