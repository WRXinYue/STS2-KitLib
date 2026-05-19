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

    internal static void EnsureGameContextPane(DevPanelSidebarHost host) {
        if (_gameHost == host && _gamePlayers != null)
            return;

        _gameHost = host;
        _gamePlayers = new PlayerContributionSidebarPanel(railCompact: true);
        _gamePie = new CategoryPieSidebarPanel("game.stats.pie", DevPanelUI.RefreshContextPaneChrome, railCompact: true);

        DevPanelUI.SetDefaultContextId(DefaultContextId);
        DevPanelUI.RegisterContextProvider(DefaultContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPlayersContextId, _gamePlayers);
        DevPanelUI.RegisterContextProvider(PanelPieContextId, _gamePie);
    }

    internal static void OnGameContextTrackerChanged() {
        if (ShouldUseMultiplayerOverlay()) {
            RefreshMultiplayerOverlay();
            if (!_panelOpen && _gamePlayers != null) {
                _gamePlayers.Refresh();
                DevPanelUI.RefreshContextPaneChrome();
            }
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        HideMultiplayerOverlay();
        if (_panelOpen)
            return;
        RefreshDefaultGameContext();
    }

    internal static void RefreshDefaultGameContext() {
        if (ShouldUseMultiplayerOverlay()) {
            RefreshMultiplayerOverlay();
            if (_gamePlayers != null) {
                _gamePlayers.SetContext(
                    CombatStatsTracker.IsTracking ? CombatStatsTracker.Current : CombatStatsTracker.Last,
                    isRunView: false);
                _gamePlayers.Refresh();
            }
            DevPanelUI.RefreshContextPaneChrome();
            DevPanelUI.UpdateContextPaneVisibility();
            return;
        }

        HideMultiplayerOverlay();
        if (_gamePlayers == null)
            return;
        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;
        _gamePlayers.SetContext(snap, isRunView: false);
        _gamePlayers.Refresh();
        DevPanelUI.RefreshContextPaneChrome();
        DevPanelUI.UpdateContextPaneVisibility();
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
            DevPanelUI.SetContextPaneActive(DefaultContextId);
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
    }
}
