using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Host;
using KitLib.Actions.CardModes;
using KitLib.Icons;
using KitLib.Multiplayer.Cheat;
using KitLib.Navigation;
using KitLib.Panels;
using KitLib.UI;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Cards.Holders;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.RelicCollection;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

/// <summary>
/// Facade / coordinator — delegates to specialized modules:
///   UI       → <see cref="DevPanelUI"/>
///   Cards    → <see cref="CardActions"/>
///   Relics   → <see cref="RelicActions"/>
///   Nav      → <see cref="NavigationHelper"/>
///   Context  → <see cref="RunContext"/>
/// </summary>
internal static class DevPanel {
    // ──────── State ────────

    private static readonly DevPanelActionSession _session = new();
    private static NGlobalUi? _globalUi;

    private static readonly Dictionary<CardMode, ICardModeHandler> _cardHandlers = new() {
        [CardMode.View] = new ViewModeHandler(),
        [CardMode.Add] = new AddModeHandler(),
        [CardMode.Upgrade] = new UpgradeModeHandler(),
        [CardMode.Delete] = new DeleteModeHandler(),
        [CardMode.Edit] = new EditModeHandler(),
    };

    private static ICardModeHandler CurrentCardHandler
        => _cardHandlers[KitLibState.CardMode];

    // ──────── Lifecycle ────────

    public static void Attach(NGlobalUi globalUi) {
        try {
            _globalUi = globalUi;

            var actions = new DevPanelActions {
                OnNewTest = StartNewTest,
                OnRefreshPanel = RefreshPanel,
                OnCycleGameSpeed = SpeedControl.CycleSpeed,
                GetGameSpeedLabel = SpeedControl.GetLabel,
                OnToggleSkipAnim = SkipAnimControl.Toggle,
                GetSkipAnimLabel = SkipAnimControl.GetLabel,
            };

            DevPanelSession.Actions = actions;
            KitLibPanelOps.CurrentGlobalUi = globalUi;
            KitLibPanelOps.TryDismissCurrent = _ => TryDismissCurrent();
            KitLibPanelOps.OnPanelAttach?.Invoke(globalUi);

            DevPanelUI.Attach(globalUi, actions);
            ((Node)globalUi).TreeExiting += () => Detach(globalUi);

            MainFile.Logger.Info("DevPanel: Sidebar attached.");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: Failed to attach sidebar: {ex.Message}");
        }
    }

    public static void Detach(NGlobalUi globalUi) {
        try {
            KitLibPanelOps.OnPanelDetach?.Invoke(globalUi);
            DevPanelRegistry.DeactivateAll(globalUi);
            DevPanelUI.Detach(globalUi);
            ClearState();
            SpeedControl.Reset();
            SkipAnimControl.Reset();
            _globalUi = null;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: Failed to detach: {ex.Message}");
        }
    }

    // ──────── Panel Openers (used by satellite tab registration) ────────

    internal static void OpenCards() {
        if (!TryDismissCurrent()) return;
        KitLibState.ActivePanel = ActivePanel.Cards;

        if (_globalUi == null) return;
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player)) return;
        CardBrowserUI.Show(_globalUi, state, player);
    }

    internal static void OpenRelics() {
        if (!TryDismissCurrent()) return;
        KitLibState.ActivePanel = ActivePanel.Relics;

        if (_globalUi == null) return;
        if (!RunContext.TryGetRunAndPlayer(out var state, out var player)) return;
        RelicBrowserUI.Show(_globalUi, state, player);
    }

    internal static void OpenEnemies() {
        if (_globalUi == null) return;
        if (!TryDismissCurrent()) return;
        KitLibState.ActivePanel = ActivePanel.Enemies;
        EnemySelectUI.ShowMain(_globalUi);
    }

    internal static void OpenPowers() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Powers;

        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return;

        PowerSelectUI.Show(_globalUi, player);
    }

    internal static void OpenPotions() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Potions;

        if (!RunContext.TryGetRunAndPlayer(out _, out var player)) return;

        PotionSelectUI.Show(_globalUi, player);
    }

    internal static void OpenEvents() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Events;

        EventSelectUI.Show(_globalUi, (evt, request) => EventActions.TryForceEnterEvent(evt, request));
    }

    internal static void OpenRooms() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Rooms;

        RoomSelectUI.Show(_globalUi);
    }

    internal static void OpenConsole() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Console;

        ConsoleUI.Show(_globalUi);
    }

    internal static void OpenPresets() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Presets;

        PresetUI.Show(_globalUi);
    }

    internal static void OpenHooks() {
        if (_globalUi == null) return;
        if (MpCheatUi.IsHooksDisabledInMultiplayer) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Hooks;

        HookConfigUI.Show(_globalUi);
    }

    internal static void OpenScripts() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Scripts;

        ScriptUI.Show(_globalUi);
    }

    internal static void OpenLogs() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Logs;

        LogCollector.AcknowledgeAlerts();
        LogViewerUI.Show(_globalUi);
        LogCollector.SyncLogViewerOpen(_globalUi);
        DevPanelUI.RefreshRailHintPresentation();
    }

    internal static void OpenCombatStats() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.CombatStats;

        CombatStatsUI.Show(_globalUi);
    }

    internal static void OpenEnemyIntent() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.EnemyIntent;

        EnemyIntentUI.Show(_globalUi);
    }

    internal static void OpenHarmonyAnalysis() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.HarmonyAnalysis;

        HarmonyAnalysisUI.Show(_globalUi);
    }

    internal static void OpenFrameworks() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Frameworks;

        FrameworkBridgeUI.Show(_globalUi);
    }

    internal static void OpenFeedback() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Feedback;

        FeedbackReportUI.Show(_globalUi);
    }

    internal static void OpenManual() {
        if (_globalUi == null) return;
        TryDismissCurrent();
        KitLibState.ActivePanel = ActivePanel.Manual;

        ManualUI.Show(_globalUi);
    }

    internal static void StartNewTest() {
        try {
            var game = NGame.Instance;
            var rm = RunManager.Instance;
            if (game == null || rm == null) return;

            TaskHelper.RunSafely(StartNewTestAsync(game, rm));
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"DevPanel: StartNewTest failed: {ex.Message}");
        }
    }

    private static async Task StartNewTestAsync(NGame game, RunManager rm) {
        await game.Transition.FadeOut();

        KitLibHost.StopAiPlayLoop?.Invoke();
        SkipAnimControl.Reset();

        if (rm.IsInProgress)
            rm.CleanUp();

        await game.ReturnToMainMenu();
        await game.Transition.FadeIn();

        MainFile.Logger.Info("DevPanel: Returned to main menu for new test run.");
    }

    private static void RefreshPanel() {
        switch (KitLibState.ActivePanel) {
            case ActivePanel.Cards: OpenCards(); break;
            case ActivePanel.Relics: OpenRelics(); break;
            case ActivePanel.Enemies: OpenEnemies(); break;
            case ActivePanel.Powers: OpenPowers(); break;
            case ActivePanel.Potions: OpenPotions(); break;
            case ActivePanel.Events: OpenEvents(); break;
            case ActivePanel.Rooms: OpenRooms(); break;
            case ActivePanel.Console: OpenConsole(); break;
            case ActivePanel.Presets: OpenPresets(); break;
            case ActivePanel.Hooks: OpenHooks(); break;
            case ActivePanel.Scripts: OpenScripts(); break;
            case ActivePanel.Logs: OpenLogs(); break;
            case ActivePanel.CombatStats: OpenCombatStats(); break;
            case ActivePanel.EnemyIntent: OpenEnemyIntent(); break;
            case ActivePanel.HarmonyAnalysis: OpenHarmonyAnalysis(); break;
            case ActivePanel.Frameworks: OpenFrameworks(); break;
            case ActivePanel.Feedback: OpenFeedback(); break;
            case ActivePanel.Manual: OpenManual(); break;
            case ActivePanel.CardEdit: break;
        }
    }

    // ──────── Panel Switching ────────

    internal static bool TryDismissCurrent() {
        if (_session.IsBusy) RunContext.Clear();
        _session.Cancel();                     // invalidate before closing overlays
        NavigationHelper.CloseCapstone();
        NavigationHelper.CloseOverlays();

        // Dismiss all DevMode overlay panels in one shot
        if (_globalUi != null)
            DevPanelUI.CloseAllOverlays(_globalUi);

        return true;
    }

    // ──────── Interception Handlers (called from Harmony patches) ────────

    /// <summary>
    /// Card selection is now handled entirely by CardBrowserUI's self-drawn grid.
    /// This handler remains for backward compatibility with existing Harmony patches.
    /// </summary>
    public static bool TryHandleCardSelection(NCardHolder holder) {
        return false;
    }

    public static bool TryHandleRelicSelection(NRelicCollectionEntry entry) {
        if (KitLibState.ActivePanel != ActivePanel.Relics || KitLibState.RelicMode != RelicMode.Add)
            return false;

        if (entry?.relic == null) return true;

        if (!RunContext.TryResolvePending(out _, out var player)) {
            ClearState();
            return true;
        }

        TaskHelper.RunSafely(RelicActions.AddRelic(entry.relic.CanonicalInstance, player));
        return true;
    }

    // ──────── Lifecycle Notifications ────────

    /// <summary>
    /// Called when the official NCardLibrary closes. Since CardBrowserUI is now
    /// self-drawn this is mostly a no-op, but kept for safety in case the user
    /// opens the official library through the pause menu.
    /// </summary>
    public static void NotifyCardLibraryClosed() {
        // Only reset if we were actively using the Cards panel
        if (KitLibState.ActivePanel != ActivePanel.Cards) return;
        ResetPanel();
    }

    public static void NotifyRelicCollectionClosed() {
        if (KitLibState.ActivePanel != ActivePanel.Relics) return;
        ResetPanel();
        ClearState();
    }

    // ──────── Private ────────

    internal static void ResetPanel() {
        KitLibState.ActivePanel = ActivePanel.None;
    }

    private static void ClearState() {
        RunContext.Clear();
    }
}
