using System;
using Godot;
using KitLib;
using KitLib.Abstractions.Host;
using KitLib.DevPerf;
using KitLib.Host;
using KitLib.Hotkeys;
using KitLib.Interop;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.LanTest;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Panels;
using KitLib.Patches;
using KitLib.Replay;
using KitLib.Settings;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Hud;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Hud)) return;

        SettingsStore.Load();
        WirePanelDelegates();
        WireHudChromeDelegates();
        DevToolsModSettingsPage.Register();
        DevToolsMainMenuCornerButtonRegistration.Register();
        RegisterRailTabs();
        WirePseudoCoopDelegates();
        KitLibHost.TryEnsurePseudoCoopPresetHandler = () => {
            // PseudoCoop relied on the KitAI companion module (removed); kept as a no-op stub.
            return false;
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
            KitLibModuleIds.Hud,
            typeof(MainMenuPatch),
            typeof(GlobalUiReadyPatch),
            typeof(ErrorPopupKitLibLogExportPatch),
            typeof(MpUiDebugRestSiteReadyPatch),
            typeof(MpUiDebugRestSiteExitPatch),
            typeof(MpUiDebugTreasureRelicInitPatch),
            typeof(MpUiDebugTreasureRelicPickedPatch),
            typeof(MpUiDebugTreasureTreeExitPatch),
            typeof(MpUiDebugMapOpenPatch),
            typeof(MpUiDebugMapVotesRefreshPatch),
            typeof(AutoSlayQuitGamePatch));

        KitLibHost.AnnounceModule(KitLibModuleIds.Hud);
        MainFile.Logger.Info("KitLib.Hud module initialized.");
    }

    /// <summary>Hand the window frame, main-menu rail and notification island to Core's call sites.</summary>
    static void WireHudChromeDelegates() {
        KitLibHudOps.EnsureScreenFrame = KitLibScreenFrame.EnsureAttached;
        KitLibHudOps.AttachMainMenuChrome = menu => {
            KitLibScreenFrame.EnsureAttached();
            KitLibScreenFrame.ApplyMainMenuInsets(menu);
            MainMenuRailHost.EnsureAttached(menu);
            MainMenuTopIslandHost.EnsureAttached(menu);
        };
        KitLibHudOps.SyncMainMenuChrome = menu => {
            MainMenuRailHost.EnsureAttached(menu);
            MainMenuTopIslandHost.EnsureAttached(menu);
        };
        KitLibHudOps.IsRailAttachedTo = MainMenuRailHost.IsAttachedTo;
        KitLibHudOps.IsRailOnScreen = () => MainMenuRailHost.IsRailOnScreen;
        KitLibHudOps.SlideRailOutForEntry = MainMenuRailHost.SlideOutForEntry;
        KitLibHudOps.HasWrapHost = () => KitLibScreenFrame.HasWrapHost;
        KitLibHudOps.ToggleWrap = () => KitLibScreenFrame.ToggleWrapShown(animated: true);
    }

    static void TryWireDevPanelHotkeys() {
        try {
            KitLibPanelHotkeyOps.TryHandleHotkey = (keyObj, viewportObj) =>
                KitLibPanelHotkeys.TryHandleAll((InputEvent)keyObj, (Viewport)viewportObj);
            KitLibHost.NotifyHotkeySettingsChanged = DevPanelUI.RefreshPeekTabHotkeyHint;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"KitLib.Hud: Dev panel hotkeys unavailable ({ex.Message}).");
        }
    }

    static bool IsCheatAssemblyAvailable() =>
        KitLibHost.IsModuleLoaded(KitLibModuleIds.Cheat);

    static void RegisterRailTabs() {
        if (IsCheatAssemblyAvailable())
            PanelTabRegistration.RegisterCheatTabs();
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
            MainFile.Logger.Warn($"KitLib.Hud: pseudo-coop rail probe unavailable ({ex.Message}).");
        }
        KitLibPseudoCoopOps.EnsureMultiplayerDevActive = DualInstanceTestBootstrap.EnsureMultiplayerDevActive;
    }

    static void WirePanelDelegates() {
        KitLibPanelOps.TryDismissCurrent = ui => DevPanel.TryDismissCurrent();

        if (IsCheatAssemblyAvailable()) {
            KitLibCheatApi.ResetSkipAnim = SkipAnimControl.Reset;
            KitLibCheatApi.IsSkipAnimSkipping = () => SkipAnimControl.IsSkipping;
            KitLibCheatApi.IsMpHooksDisabledInMultiplayer = () => MpCheatUi.IsHooksDisabledInMultiplayer;
        }

        KitLibPanelOps.IsProgressLossPromptVisible = () => ProgressLossPromptUI.IsVisible;
        KitLibPanelOps.HideDevMainMenuIfVisible = () => DevMainMenuUI.DismissOverlays(null);

        KitLibDevOps.OpenHooks = DevPanel.OpenHooks;
        KitLibDevOps.OpenEnemyIntent = DevPanel.OpenEnemyIntent;
        KitLibDevOps.OpenLogExport = DevPanel.OpenLogExport;

        CombatReplayPlayback.ShowHud = CombatReplayBarUI.Show;
        CombatReplayPlayback.HideHud = CombatReplayBarUI.Hide;

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
            MainFile.Logger.Warn($"KitLib.Hud: dev panel UI delegates unavailable ({ex.Message}).");
        }
    }
}
