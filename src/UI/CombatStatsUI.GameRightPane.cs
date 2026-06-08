using KitLib.CombatStats;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    internal const string DefaultContextId = "default.players";
    private const string PanelPlayersContextId = "combatStats.players";
    private const string PanelPieContextId = "combatStats.pie";

    private static PlayerContributionSidebarPanel? _gamePlayers;
    private static CategoryPieSidebarPanel? _gamePie;
    private static DevPanelSidebarHost? _gameHost;
    private static bool _panelOpen;

    internal static bool IsPanelOpen => _panelOpen;

    internal static void EnsureGameContextPane(DevPanelSidebarHost host) {
        if (_gameHost == host && _gamePlayers != null)
            return;

        _gameHost = host;
        _gamePlayers = new PlayerContributionSidebarPanel(railCompact: true);
        _gamePie = new CategoryPieSidebarPanel("game.stats.pie", railCompact: true);

        DevPanelUI.SetDefaultContextIds(
            EnemyIntentUI.PanelContextId,
            EnemySelectUI.CombatToolsContextId,
            DefaultContextId);
        DevPanelUI.RegisterContextProvider(DefaultContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPlayersContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPieContextId, _gamePie);
    }

    internal static void OnGameContextTrackerChanged() {
        RefreshDefaultGameContext();
    }

    internal static void RefreshStatsContextOnly() {
        SyncMultiplayerOverlayState();

        if (_panelOpen) {
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        ApplyDefaultGameStatsContext();
        DevPanelUI.RefreshContextProviders(DefaultContextId);
    }

    internal static void RefreshIntentGameContext() {
        if (_panelOpen) {
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        SyncMultiplayerOverlayState();
        MonsterIntentOverlayUI.SyncState();
        EnsureDefaultGameContextActive();
        DevPanelUI.RefreshContextProviders(
            EnemyIntentUI.PanelContextId,
            EnemySelectUI.CombatToolsContextId);
    }

    internal static void RefreshDefaultGameContext() {
        if (_panelOpen) {
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        SyncMultiplayerOverlayState();
        MonsterIntentOverlayUI.SyncState();
        ApplyDefaultGameStatsContext();
        EnsureDefaultGameContextActive();
        DevPanelUI.RefreshContextPane();
    }

    private static void EnsureDefaultGameContextActive() {
        if (EnemyIntentUI.IsPanelOpen || IsPanelOpen)
            return;

        if (DevPanelUI.HasActiveContext(
                EnemyIntentUI.PanelContextId,
                EnemySelectUI.CombatToolsContextId,
                DefaultContextId))
            return;

        DevPanelUI.SetContextPaneActiveMany(
            EnemyIntentUI.PanelContextId,
            EnemySelectUI.CombatToolsContextId,
            DefaultContextId);
    }

    private static void ApplyDefaultGameStatsContext() {
        if (CanShowMultiplayerOverlay()) {
            if (_gamePlayers != null) {
                _gamePlayers.SetContext(
                    CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : CombatStatsTracker.Last,
                    isRunView: false);
            }
            return;
        }

        if (_gamePlayers == null)
            return;
        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;
        _gamePlayers.SetContext(snap, isRunView: false);
    }

    private static void RefreshDefaultGameStats() {
        ApplyDefaultGameStatsContext();
        _gamePlayers?.Refresh();
    }

    private static void SyncGameContextPane(
        ViewMode mode,
        CombatStatsSnapshot? snap,
        PlayerCombatStats? player,
        bool isRun) {
        if (_gamePlayers == null || _gamePie == null || _gameHost == null)
            return;

        _gamePlayers.SetContext(snap, isRun);
        _gamePie.SetContext(player);
        _gamePie.PrepareForViewMode(mode);

        if (!_panelOpen) {
            DevPanelUI.SetContextPaneActiveMany(
                EnemyIntentUI.PanelContextId,
                EnemySelectUI.CombatToolsContextId,
                DefaultContextId);
            _gamePlayers.Refresh();
        }
        else if (SidebarUsesPie(mode)) {
            DevPanelUI.SetContextPaneActive(PanelPieContextId);
            _gamePie.Refresh();
        }
        else {
            DevPanelUI.SetContextPaneActive(PanelPlayersContextId);
            _gamePlayers.Refresh();
        }
        DevPanelUI.RefreshContextPaneChrome();
        DevPanelUI.UpdateContextPaneVisibility();
        SyncMultiplayerOverlayState();
        MonsterIntentOverlayUI.SyncState();
    }
}
