using DevMode.CombatStats;
using Godot;

namespace DevMode.UI;

internal static partial class CombatStatsUI {
    private const string DefaultContextId = "default.players";
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

        DevPanelUI.SetDefaultContextIds(EnemyIntentUI.PanelContextId, DefaultContextId);
        DevPanelUI.RegisterContextProvider(DefaultContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPlayersContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPieContextId, _gamePie);
    }

    internal static void OnGameContextTrackerChanged() {
        SyncMultiplayerOverlayState();
        MonsterIntentOverlayUI.SyncState();

        if (_panelOpen) {
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        RefreshDefaultGameContext();
    }

    internal static void RefreshDefaultGameContext() {
        SyncMultiplayerOverlayState();
        MonsterIntentOverlayUI.SyncState();
        RefreshDefaultGameStats();
        EnemyIntentUI.RefreshDefaultContext();

        if (!EnemyIntentUI.IsPanelOpen && !IsPanelOpen)
            DevPanelUI.SetContextPaneActiveMany(EnemyIntentUI.PanelContextId, DefaultContextId);

        DevPanelUI.RefreshContextPane();
    }

    private static void RefreshDefaultGameStats() {
        if (CanShowMultiplayerOverlay()) {
            if (_gamePlayers != null) {
                _gamePlayers.SetContext(
                    CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : CombatStatsTracker.Last,
                    isRunView: false);
                _gamePlayers.Refresh();
            }
            return;
        }

        if (_gamePlayers == null)
            return;
        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;
        _gamePlayers.SetContext(snap, isRunView: false);
        _gamePlayers.Refresh();
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
            DevPanelUI.SetContextPaneActiveMany(EnemyIntentUI.PanelContextId, DefaultContextId);
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
