using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.CombatStats;
using Godot;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.UI;

internal static partial class CombatStatsUI {
    internal const string MultiplayerOverlayRootName = "DevModeCombatStatsMpOverlay";

    private const float OverlayWidth = 400f;
    private const float OverlayMargin = 10f;
    private const float OverlayBarHeight = 12f;
    private const float OverlayBarTrackWidth = 240f;
    private const float OverlayRowHeight = 24f;
    private const float OverlayNameWidth = 96f;
    private const float OverlayScoreWidth = 40f;

    private static MpOverlayRoot? _mpOverlayRoot;
    private static PanelContainer? _mpOverlayPanel;
    private static VBoxContainer? _mpOverlayList;
    private static StyleBoxFlat? _mpOverlayStyle;
    private static bool _mpOverlayActive;
    private static bool _mpOverlayUsingFreePosition;
    private static bool _mpOverlayDragging;
    private static Vector2 _mpOverlayDragOffset;

    internal static bool IsMultiplayerOverlayActive() => _mpOverlayActive;

    internal static bool ShouldUseMultiplayerOverlay() {
        var run = RunManager.Instance;
        if (run?.IsInProgress != true)
            return false;
        if (run.NetService?.Type == NetGameType.Singleplayer)
            return false;

        var combat = CombatManager.Instance?.DebugOnlyGetState();
        if (combat != null && combat.Players.Count > 1)
            return true;

        var state = run.DebugOnlyGetState();
        return state != null && state.Players.Count > 1;
    }

    internal static void AttachMultiplayerOverlay(NGlobalUi globalUi) {
        if (((Node)globalUi).GetNodeOrNull<Control>(MultiplayerOverlayRootName) != null)
            return;

        _mpOverlayRoot = new MpOverlayRoot(ProcessOverlayDrag) {
            Name = MultiplayerOverlayRootName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 1250,
        };
        _mpOverlayRoot.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        _mpOverlayPanel = new PanelContainer {
            Name = "MpStatsPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            Visible = false,
        };
        ApplyDefaultOverlayPanelLayout(_mpOverlayPanel);

        _mpOverlayStyle = CreateOverlayPanelStyle();
        _mpOverlayPanel.AddThemeStyleboxOverride("panel", _mpOverlayStyle);

        var body = new VBoxContainer();
        body.AddThemeConstantOverride("separation", 4);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 4);
        titleRow.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        titleRow.MouseFilter = Control.MouseFilterEnum.Stop;
        titleRow.MouseDefaultCursorShape = Control.CursorShape.Move;
        titleRow.TooltipText = I18N.T("combatStats.mpOverlay.dragHint", "Drag to move panel");

        var title = new Label {
            Text = I18N.T("combatStats.sidebar.players", "Player scores"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        title.AddThemeFontSizeOverride("font_size", 10);
        title.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
        titleRow.AddChild(title);
        WireOverlayDrag(titleRow, _mpOverlayPanel);
        body.AddChild(titleRow);

        _mpOverlayList = new VBoxContainer();
        _mpOverlayList.AddThemeConstantOverride("separation", 5);
        body.AddChild(_mpOverlayList);

        _mpOverlayPanel.AddChild(body);
        _mpOverlayRoot.AddChild(_mpOverlayPanel);

        ThemeManager.OnThemeChanged += ApplyOverlayTheme;
        _mpOverlayRoot.TreeExiting += () => {
            ThemeManager.OnThemeChanged -= ApplyOverlayTheme;
            _mpOverlayRoot = null;
            _mpOverlayPanel = null;
            _mpOverlayList = null;
            _mpOverlayStyle = null;
            _mpOverlayActive = false;
            _mpOverlayUsingFreePosition = false;
            _mpOverlayDragging = false;
        };

        ((Node)globalUi).AddChild(_mpOverlayRoot);
    }

    internal static void DetachMultiplayerOverlay(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(MultiplayerOverlayRootName)?.QueueFree();
        _mpOverlayRoot = null;
        _mpOverlayPanel = null;
        _mpOverlayList = null;
        _mpOverlayStyle = null;
        _mpOverlayActive = false;
        _mpOverlayUsingFreePosition = false;
        _mpOverlayDragging = false;
    }

    internal static void RefreshMultiplayerOverlay() {
        if (!ShouldUseMultiplayerOverlay() || _panelOpen) {
            HideMultiplayerOverlay();
            return;
        }

        if (_mpOverlayList == null || _mpOverlayPanel == null)
            return;

        var snap = CombatStatsTracker.IsTracking
            ? CombatStatsTracker.Current
            : CombatStatsTracker.Last;

        var players = snap?.Players.Values.ToList() ?? new List<PlayerCombatStats>();
        if (players.Count < 2) {
            HideMultiplayerOverlay();
            return;
        }

        int maxScore = Math.Max(1, players.Max(p => CombatScoreCalculator.TotalScore(p)));
        var ordered = players
            .OrderByDescending(CombatScoreCalculator.TotalScore)
            .ThenBy(p => p.DisplayName)
            .ToList();

        while (_mpOverlayList.GetChildCount() > 0) {
            var child = _mpOverlayList.GetChild(0);
            _mpOverlayList.RemoveChild(child);
            child.Free();
        }

        foreach (var player in ordered) {
            int total = CombatScoreCalculator.TotalScore(player);
            _mpOverlayList.AddChild(MakeOverlayPlayerRow(player, total, maxScore));
        }

        _mpOverlayActive = true;
        _mpOverlayPanel.Visible = true;
    }

    internal static void HideMultiplayerOverlay() {
        _mpOverlayActive = false;
        if (_mpOverlayPanel != null)
            _mpOverlayPanel.Visible = false;
    }

    private static StyleBoxFlat CreateOverlayPanelStyle() {
        var theme = ThemeManager.Current;
        return new StyleBoxFlat {
            BgColor = theme.RailBg,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = theme.RailBorder,
            ShadowColor = new Color(0, 0, 0, 0.25f),
            ShadowSize = 6,
        };
    }

    private static void ApplyOverlayTheme() {
        if (_mpOverlayStyle == null || _mpOverlayPanel == null)
            return;
        var theme = ThemeManager.Current;
        _mpOverlayStyle.BgColor = theme.RailBg;
        _mpOverlayStyle.BorderColor = theme.RailBorder;
    }

    private static void ApplyDefaultOverlayPanelLayout(PanelContainer panel) {
        panel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        panel.OffsetTop = OverlayMargin;
        panel.OffsetRight = -OverlayMargin;
        panel.OffsetLeft = -(OverlayWidth + OverlayMargin);
        _mpOverlayUsingFreePosition = false;
    }

    private static void EnsureOverlayFreePosition(PanelContainer panel) {
        if (_mpOverlayUsingFreePosition)
            return;

        var parent = panel.GetParent() as Control;
        if (parent == null)
            return;

        var globalPos = panel.GlobalPosition;
        panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        panel.GlobalPosition = globalPos;
        _mpOverlayUsingFreePosition = true;
    }

    private static void WireOverlayDrag(Control handle, PanelContainer panel) {
        handle.GuiInput += e => {
            if (e is not InputEventMouseButton mb || mb.ButtonIndex != MouseButton.Left)
                return;

            if (mb.Pressed) {
                EnsureOverlayFreePosition(panel);
                var parent = panel.GetParent() as Control;
                if (parent == null)
                    return;

                var mouseLocal = parent.GetGlobalTransformWithCanvas().AffineInverse() * parent.GetGlobalMousePosition();
                _mpOverlayDragOffset = mouseLocal - panel.Position;
                _mpOverlayDragging = true;
                handle.AcceptEvent();
                return;
            }

            if (_mpOverlayDragging) {
                _mpOverlayDragging = false;
                ClampOverlayPanel(panel);
                handle.AcceptEvent();
            }
        };
    }

    private static void ProcessOverlayDrag() {
        if (!_mpOverlayDragging || _mpOverlayPanel == null || _mpOverlayRoot == null)
            return;

        if (!Input.IsMouseButtonPressed(MouseButton.Left)) {
            _mpOverlayDragging = false;
            ClampOverlayPanel(_mpOverlayPanel);
            return;
        }

        var mouseLocal = _mpOverlayRoot.GetGlobalTransformWithCanvas().AffineInverse()
            * _mpOverlayRoot.GetGlobalMousePosition();
        _mpOverlayPanel.Position = mouseLocal - _mpOverlayDragOffset;
    }

    private static void ClampOverlayPanel(PanelContainer panel) {
        var parent = panel.GetParent() as Control;
        if (parent == null)
            return;

        var size = panel.Size;
        if (size.X <= 0f || size.Y <= 0f)
            return;

        var pos = panel.Position;
        pos.X = Math.Clamp(pos.X, 0f, Math.Max(0f, parent.Size.X - size.X));
        pos.Y = Math.Clamp(pos.Y, 0f, Math.Max(0f, parent.Size.Y - size.Y));
        panel.Position = pos;
    }

    private sealed partial class MpOverlayRoot : Control {
        private readonly Action _onProcess;

        public MpOverlayRoot(Action onProcess) {
            _onProcess = onProcess;
        }

        public override void _Process(double delta) => _onProcess();
    }

    private static Control MakeOverlayPlayerRow(PlayerCombatStats player, int total, int maxScore) {
        string name = ResolvePlayerDisplayName(player);
        var bd = CombatScoreCalculator.BreakdownForDisplay(player);
        string tooltip = FormatPlayerTooltip(name, total, bd);
        float barWidth = Math.Max(6f, OverlayBarTrackWidth * total / (float)maxScore);

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.CustomMinimumSize = new Vector2(0, OverlayRowHeight);

        var nameLabel = new Label {
            Text = name,
            CustomMinimumSize = new Vector2(OverlayNameWidth, 0),
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 10);
        nameLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);

        var barTrack = new HBoxContainer {
            CustomMinimumSize = new Vector2(OverlayBarTrackWidth, OverlayBarHeight + 4f),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        barTrack.AddThemeConstantOverride("separation", 0);

        var bar = new HorizontalScoreStack {
            BarHeight = OverlayBarHeight,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(barWidth, OverlayBarHeight + 2f),
        };
        bar.SetSegments(ScoreBreakdownSegments(bd), Math.Max(total, 1));
        barTrack.AddChild(bar);
        barTrack.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var scoreLabel = new Label {
            Text = total.ToString(),
            CustomMinimumSize = new Vector2(OverlayScoreWidth, 0),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        scoreLabel.AddThemeFontSizeOverride("font_size", 10);
        scoreLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);

        row.AddChild(nameLabel);
        row.AddChild(barTrack);
        row.AddChild(scoreLabel);

        ApplyBarTooltip(row, tooltip);
        ApplyBarTooltip(nameLabel, tooltip);
        ApplyBarTooltip(barTrack, tooltip);
        ApplyBarTooltip(bar, tooltip);
        ApplyBarTooltip(scoreLabel, tooltip);
        return row;
    }

    /// <summary>Horizontal stacked bar: segment widths sum to total score.</summary>
    private sealed partial class HorizontalScoreStack : Control {
        private readonly List<(string Key, float Amount, Color Color)> _segments = new();
        private float _displayTotal = 1f;

        public HorizontalScoreStack() {
            MouseFilter = MouseFilterEnum.Stop;
        }

        public float BarHeight { get; set; } = 8f;

        public void SetSegments(
            IReadOnlyList<(string Key, int Amount, Color Color)> segments,
            int total) {
            _segments.Clear();
            foreach (var (key, amount, color) in segments)
                _segments.Add((key, amount, color));
            _displayTotal = Math.Max(total, 1);
            QueueRedraw();
        }

        public override void _Draw() {
            float w = Size.X;
            float h = Size.Y;
            float barH = BarHeight;
            float y = Math.Max(0f, (h - barH) * 0.5f);

            if (w < 4f || _segments.Count == 0) {
                if (w >= 4f)
                    DrawRect(new Rect2(0, y, w, barH), new Color(0.22f, 0.22f, 0.26f, 0.85f));
                return;
            }

            float total = Math.Max(_displayTotal, 0.01f);
            float x = 0f;
            foreach (var (_, amount, color) in _segments) {
                float segW = w * amount / total;
                if (segW <= 0.01f)
                    continue;
                DrawRect(new Rect2(x, y, segW, barH), color);
                x += segW;
            }
        }

        public override void _Notification(int what) {
            if (what == NotificationResized)
                QueueRedraw();
        }
    }
}
