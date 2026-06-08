using KitLib.EnemyIntent;
using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.UI;

internal static partial class EnemyIntentUI {
    internal const string PanelContextId = "enemyIntent.nextTurn";

    private static NextTurnSidebarPanel? _nextTurnSidebar;
    private static DevPanelSidebarHost? _gameHost;
    private static bool _panelOpen;

    internal static bool IsPanelOpen => _panelOpen;

    internal static void EnsureGameContextPane(DevPanelSidebarHost host) {
        DevPanelUI.EnsureContextProvider(
            ref _gameHost,
            host,
            ref _nextTurnSidebar,
            PanelContextId,
            () => new NextTurnSidebarPanel());
    }

    internal static void RefreshDefaultContext() {
        _nextTurnSidebar?.Refresh();
    }

    internal static void OnContextChanged() {
        MonsterIntentOverlayUI.SyncState();
        RefreshDefaultContext();

        if (_panelOpen) {
            DevPanelUI.SetContextPaneActive(PanelContextId);
            DevPanelUI.RefreshContextPane();
            RefreshBrowserPreview();
            return;
        }

        if (CombatStatsUI.IsPanelOpen)
            return;

        DevPanelUI.RefreshContextProviders(
            PanelContextId,
            EnemySelectUI.CombatToolsContextId);
    }

    internal sealed partial class NextTurnSidebarPanel : IDevPanelSidebarProvider {
        private readonly VBoxContainer _list;
        private bool _hasContent;

        public NextTurnSidebarPanel() {
            _list = new VBoxContainer {
                Name = "EnemyIntentNextTurnList",
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            };
            _list.AddThemeConstantOverride("separation", 6);
        }

        public Control Root => _list;

        public string Title => I18N.T("enemyIntent.sidebar.title", "Next turn");

        public string Hint => I18N.T("enemyIntent.sidebar.hint",
            "Predicted intent for the enemy turn after the one currently shown.");

        public bool HasContent => _hasContent;

        public void Refresh() {
            if (!KitLibState.IsActive
                || !MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState())) {
                Clear();
                _hasContent = false;
                return;
            }

            var state = CombatManager.Instance!.DebugOnlyGetState();
            var entries = MonsterIntentReader.CaptureNextTurn(state);
            if (entries.Count == 0) {
                Clear();
                _hasContent = false;
                return;
            }

            IntentPreviewRows.Sync(
                _list,
                entries,
                displayedOnly: true,
                IntentOverlayLayout.CompactBadgeSize,
                stackMultipleIntents: true);
            _hasContent = true;
        }

        private void Clear() => ContextRailWidgets.ClearChildren(_list);
    }
}
