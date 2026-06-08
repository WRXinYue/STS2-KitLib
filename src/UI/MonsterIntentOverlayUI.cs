using System.Collections.Generic;
using KitLib.EnemyIntent;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Draggable overlay that predicts enemy move/intent sequences during combat.</summary>
internal static partial class MonsterIntentOverlayUI {
    internal const string RootName = "KitLibMonsterIntentOverlay";

    private static MonsterIntentOverlayHost? _overlay;
    private static NGlobalUi? _globalUi;

    internal static bool IsEnabled() =>
        SettingsStore.Current.CombatStatsMonsterIntentOverlayEnabled;

    internal static void SyncState(NGlobalUi? globalUi = null) {
        if (globalUi != null)
            _globalUi = globalUi;
        EnsureAttached();

        if (!IsEnabled() || !ShouldShow()) {
            Hide();
            return;
        }

        Refresh();
    }

    internal static void Attach(NGlobalUi globalUi) {
        _globalUi = globalUi;
        EnsureAttached();
    }

    internal static void Detach(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        _overlay = null;
        if (_globalUi == globalUi)
            _globalUi = null;
    }

    internal static void Refresh() {
        if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
            EnsureAttached();
        _overlay?.Refresh();
    }

    internal static void Hide() => _overlay?.HidePanel();

    private static bool ShouldShow() {
        if (!KitLibState.IsActive)
            return false;
        return MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState());
    }

    private static void EnsureAttached() {
        if (_globalUi == null)
            return;

        var parent = (Node)_globalUi;
        var existing = parent.GetNodeOrNull<Control>(RootName);
        if (existing is MonsterIntentOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _overlay = host;
            return;
        }

        if (_overlay != null && GodotObject.IsInstanceValid(_overlay)
            && _overlay.IsInsideTree() && _overlay.GetParent() == parent)
            return;

        _overlay = null;
        var overlay = new MonsterIntentOverlayHost();
        _overlay = overlay;
        overlay.TreeExiting += () => {
            if (_overlay == overlay)
                _overlay = null;
        };
        Callable.From(() => {
            if (_globalUi == null || !GodotObject.IsInstanceValid(_globalUi))
                return;
            var attachParent = (Node)_globalUi;
            if (!GodotObject.IsInstanceValid(overlay) || overlay.GetParent() != null)
                return;
            if (attachParent.GetNodeOrNull<Control>(RootName) != null)
                return;
            attachParent.AddChild(overlay);
        }).CallDeferred();
    }

    private static class Layout {
        public const float PanelWidth = 480f;
        public const float Margin = 10f;
        public const int ZIndex = 1309;
    }

    private sealed partial class MonsterIntentOverlayHost : Control {
        private readonly PanelContainer _panel;
        private readonly StyleBoxFlat _panelStyle;
        private readonly VBoxContainer _enemyList;
        private readonly FloatingCombatOverlay.DraggablePanelBinding _drag;
        private bool _usingFreePosition;

        public MonsterIntentOverlayHost() {
            Name = RootName;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = Layout.ZIndex;
            SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _panel = new PanelContainer {
                Name = "MonsterIntentPanel",
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false,
                CustomMinimumSize = new Vector2(Layout.PanelWidth, 0),
            };
            ApplyDefaultPanelLayout();
            ApplySavedPanelLayout();

            _panelStyle = FloatingCombatOverlay.CreatePanelStyle();
            _panel.AddThemeStyleboxOverride("panel", _panelStyle);

            _drag = new FloatingCombatOverlay.DraggablePanelBinding(
                this,
                _panel,
                Layout.PanelWidth,
                () => _usingFreePosition,
                v => _usingFreePosition = v,
                SavePanelPosition);

            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 6);
            body.AddChild(BuildTitleRow());

            _enemyList = new VBoxContainer();
            _enemyList.AddThemeConstantOverride("separation", 8);
            body.AddChild(_enemyList);
            _panel.AddChild(body);

            AddChild(_panel);

            TreeEntered += OnTreeEntered;
            ThemeManager.OnThemeChanged += OnThemeChanged;
            MonsterIntentOverlayTracker.Changed += OnTrackerChanged;
            TreeExiting += () => {
                TreeEntered -= OnTreeEntered;
                ThemeManager.OnThemeChanged -= OnThemeChanged;
                MonsterIntentOverlayTracker.Changed -= OnTrackerChanged;
            };
        }

        private void OnTreeEntered() {
            if (_usingFreePosition)
                _drag.ClampAndCommit();
            Refresh();
        }

        private void OnTrackerChanged() => Refresh();

        private void OnThemeChanged() {
            var theme = ThemeManager.Current;
            _panelStyle.BgColor = theme.RailBg;
            _panelStyle.BorderColor = theme.RailBorder;
        }

        public void Refresh() {
            if (!IsEnabled() || !ShouldShow()) {
                ClearEnemyRows();
                HidePanel();
                return;
            }

            var state = CombatManager.Instance.DebugOnlyGetState();
            var entries = MonsterIntentReader.CaptureCurrent(state);
            if (entries.Count == 0) {
                ClearEnemyRows();
                _panel.Visible = true;
                MoveToFrontDeferred();
                return;
            }

            IntentPreviewRows.Sync(_enemyList, entries, displayedOnly: false, IntentOverlayLayout.BadgeSize);
            _panel.Visible = true;
            MoveToFrontDeferred();
        }

        public void HidePanel() {
            ClearEnemyRows();
            _panel.Visible = false;
        }

        private void MoveToFrontDeferred() {
            if (IsInsideTree())
                Callable.From(MoveToFront).CallDeferred();
        }

        private void ClearEnemyRows() {
            foreach (var child in _enemyList.GetChildren())
                child.QueueFree();
        }

        private Control BuildTitleRow() {
            var titleRow = new HBoxContainer {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.Move,
                TooltipText = I18N.T("enemyIntent.overlay.dragHint", "Drag to move panel"),
            };
            titleRow.AddThemeConstantOverride("separation", 4);
            _drag.WireHandle(titleRow);

            var title = new Label {
                Text = I18N.T("enemyIntent.overlay.title", "Enemy intents"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            title.AddThemeFontSizeOverride("font_size", 10);
            title.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            titleRow.AddChild(title);
            return titleRow;
        }

        private void ApplyDefaultPanelLayout() {
            _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _panel.OffsetTop = Layout.Margin;
            _panel.OffsetLeft = Layout.Margin;
            _panel.OffsetRight = Layout.PanelWidth + Layout.Margin;
            _usingFreePosition = false;
        }

        private void ApplySavedPanelLayout() {
            var (x, y) = SettingsStore.GetCombatStatsMonsterIntentOverlayPosition();
            if (x == null || y == null)
                return;

            ApplyFreePosition(new Vector2(x.Value, y.Value));
        }

        private void ApplyFreePosition(Vector2 pos) {
            var size = _panel.Size;
            if (size.X <= 0f)
                size.X = Layout.PanelWidth;
            if (size.Y <= 0f)
                size.Y = _panel.GetCombinedMinimumSize().Y;

            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _panel.Size = size;
            _panel.Position = pos;
            _usingFreePosition = true;
        }

        private static void SavePanelPosition(Vector2 pos) =>
            SettingsStore.SetCombatStatsMonsterIntentOverlayPosition(pos.X, pos.Y);

        public override void _Process(double delta) => _drag.Process();
    }
}
