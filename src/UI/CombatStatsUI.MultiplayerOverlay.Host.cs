using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.CombatStats;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    private static class MpOverlayLayout {
        public const float PanelWidth = 400f;
        public const float Margin = 10f;
        public const float ContentMarginH = 10f;
        public const float RowSeparation = 8f;
        public const float BarHeight = 12f;
        public const float RowHeight = 26f;
        public const float NameWidth = 96f;
        public const float ScoreWidth = 40f;
        public const float ScoreRightPadding = 8f;
        public const float BarCornerRadius = 5f;
        public const float SegmentGap = 1f;
        /// <summary>Panel inner width minus fixed row columns, separations, and score inset.</summary>
        public const float BarTrackWidth =
            PanelWidth
            - ContentMarginH * 2f
            - NameWidth
            - ScoreWidth
            - ScoreRightPadding
            - RowSeparation * 2f;
        /// <summary>Above browser overlays (1250), below card edit overlays (1400).</summary>
        public const int ZIndex = 1310;
    }

    /// <summary>Top-right floating panel for multiplayer combat score comparison.</summary>
    private sealed partial class MultiplayerOverlayHost : Control {
        private readonly PanelContainer _panel;
        private readonly StyleBoxFlat _panelStyle;
        private readonly VBoxContainer _playerList;
        private readonly FloatingCombatOverlay.DraggablePanelBinding _drag;

        private bool _usingFreePosition;

        public bool IsPanelVisible => _panel.Visible;

        public MultiplayerOverlayHost() {
            Name = MultiplayerOverlayRootName;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = MpOverlayLayout.ZIndex;
            SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _panel = new PanelContainer {
                Name = "MpStatsPanel",
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false,
                CustomMinimumSize = new Vector2(MpOverlayLayout.PanelWidth, 0),
            };
            ApplyDefaultPanelLayout();
            ApplySavedPanelLayout();

            _panelStyle = FloatingCombatOverlay.CreatePanelStyle();
            _panel.AddThemeStyleboxOverride("panel", _panelStyle);

            _drag = new FloatingCombatOverlay.DraggablePanelBinding(
                this,
                _panel,
                MpOverlayLayout.PanelWidth,
                () => _usingFreePosition,
                v => _usingFreePosition = v,
                SavePanelPosition);

            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 4);
            body.AddChild(BuildTitleRow());
            _playerList = new VBoxContainer();
            _playerList.AddThemeConstantOverride("separation", 5);
            body.AddChild(_playerList);
            _panel.AddChild(body);

            AddChild(_panel);

            TreeEntered += OnTreeEntered;
            ThemeManager.OnThemeChanged += OnThemeChanged;
            TreeExiting += () => {
                TreeEntered -= OnTreeEntered;
                ThemeManager.OnThemeChanged -= OnThemeChanged;
            };
        }

        private void OnTreeEntered() {
            if (_usingFreePosition)
                _drag.ClampAndCommit();
        }

        public void Refresh() {
            if (!IsMultiplayerOverlayEnabled() || !ShouldUseMultiplayerOverlay()) {
                HidePanel();
                return;
            }

            var players = ResolveOverlayPlayers();
            if (players.Count < 2) {
                ClearPlayerRows();
                _panel.Visible = true;
                MoveToFront();
                return;
            }

            int maxScore = Math.Max(1, players.Max(CombatScoreCalculator.TotalScore));
            SyncPlayerRows(players, maxScore);

            _panel.Visible = true;
            MoveToFront();
        }

        public void HidePanel() => _panel.Visible = false;

        private void ClearPlayerRows() {
            foreach (var child in _playerList.GetChildren())
                child.QueueFree();
        }

        private void OnThemeChanged() {
            var theme = ThemeManager.Current;
            _panelStyle.BgColor = theme.RailBg;
            _panelStyle.BorderColor = theme.RailBorder;

            foreach (var child in _playerList.GetChildren()) {
                if (child is MpOverlayPlayerRow row)
                    row.RefreshTheme();
            }
        }

        private void SyncPlayerRows(List<PlayerCombatStats> players, int maxScore) {
            var ordered = players
                .OrderByDescending(CombatScoreCalculator.TotalScore)
                .ThenBy(p => p.DisplayName)
                .ToList();

            var existing = new Dictionary<string, MpOverlayPlayerRow>(StringComparer.Ordinal);
            foreach (var child in _playerList.GetChildren()) {
                if (child is MpOverlayPlayerRow row)
                    existing[row.PlayerKey] = row;
            }

            var keepKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < ordered.Count; i++) {
                var player = ordered[i];
                keepKeys.Add(player.Key);

                if (!existing.TryGetValue(player.Key, out var row) || !GodotObject.IsInstanceValid(row)) {
                    row = new MpOverlayPlayerRow();
                    _playerList.AddChild(row);
                }

                row.Bind(
                    player,
                    CombatScoreCalculator.TotalScore(player),
                    maxScore,
                    isLeader: i == 0);
                if (row.GetIndex() != i) {
                    var targetIndex = i;
                    var rowRef = row;
                    Callable.From(() => {
                        if (!GodotObject.IsInstanceValid(_playerList) || !GodotObject.IsInstanceValid(rowRef))
                            return;
                        if (rowRef.GetIndex() != targetIndex)
                            _playerList.MoveChild(rowRef, targetIndex);
                    }).CallDeferred();
                }
            }

            foreach (var child in _playerList.GetChildren()) {
                if (child is MpOverlayPlayerRow row && !keepKeys.Contains(row.PlayerKey))
                    row.QueueFree();
            }
        }

        private Control BuildTitleRow() {
            var titleRow = new HBoxContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.Move,
                TooltipText = I18N.T("combatStats.mpOverlay.dragHint", "Drag to move panel"),
            };
            titleRow.AddThemeConstantOverride("separation", 4);
            _drag.WireHandle(titleRow);

            var title = new Label {
                Text = I18N.T("combatStats.sidebar.players", "Player scores"),
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            title.AddThemeFontSizeOverride("font_size", 10);
            title.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            titleRow.AddChild(title);
            return titleRow;
        }

        private void ApplyDefaultPanelLayout() {
            _panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
            _panel.OffsetTop = MpOverlayLayout.Margin;
            _panel.OffsetRight = -MpOverlayLayout.Margin;
            _panel.OffsetLeft = -(MpOverlayLayout.PanelWidth + MpOverlayLayout.Margin);
            _usingFreePosition = false;
        }

        private void ApplySavedPanelLayout() {
            var (x, y) = SettingsStore.GetCombatStatsMpOverlayPosition();
            if (x == null || y == null)
                return;

            ApplyFreePosition(new Vector2(x.Value, y.Value));
        }

        private void ApplyFreePosition(Vector2 pos) {
            var size = _panel.Size;
            if (size.X <= 0f)
                size.X = MpOverlayLayout.PanelWidth;
            if (size.Y <= 0f)
                size.Y = _panel.GetCombinedMinimumSize().Y;

            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _panel.Size = size;
            _panel.Position = pos;
            _usingFreePosition = true;
        }

        private static void SavePanelPosition(Vector2 pos) =>
            SettingsStore.SetCombatStatsMpOverlayPosition(pos.X, pos.Y);

        public override void _Process(double delta) => _drag.Process();
    }
}
