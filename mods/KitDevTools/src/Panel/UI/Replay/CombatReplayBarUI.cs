using Godot;
using KitLib.Icons;
using KitLib.Replay;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

/// <summary>Opaque bottom bar for <see cref="KitLib.Replay.CombatReplayPlayback"/>.</summary>
internal static class CombatReplayBarUI {
    const string NodeName = "KitLibCombatReplayBar";
    const int LayerId = 128;
    const float BarHeight = 56f;
    const float TimelineHeight = 40f;
    const float DockHeight = TimelineHeight + BarHeight;
    const int IconPx = 22;
    static readonly Color BarBg = new(0.07f, 0.07f, 0.08f);
    static readonly Color BarBorder = new(0.22f, 0.22f, 0.24f);
    static readonly Color TrackColor = new(0.18f, 0.18f, 0.20f);
    static readonly Color FillPast = new(0.78f, 0.78f, 0.80f);
    static readonly Color FillCurrent = Colors.White;

    static CanvasLayer? _layer;
    static Button? _playBtn;
    static Button? _restartBtn;
    static Button? _prevBtn;
    static Button? _nextBtn;
    static Button? _modeBtn;
    static Button? _liveBtn;
    static Button? _speedBtn;
    static Button? _exitBtn;
    static Label? _status;
    static Label? _matchLabel;
    static HBoxContainer? _timeline;
    static readonly List<ColorRect> _roomFills = new();
    static ShaderMaterial? _whiteIconMat;
    static Control? _hudRoot;
    static readonly List<(Control Ctrl, float Top, float Bottom, float LastTop)> _shifted = new();
    static readonly List<(Control Ctrl, float Bottom, float LastBottom)> _contentInset = new();
    static SceneTree? _tree;
    static bool _applyQueued;
    static float _handLastY = float.NaN;
    static float _handBaseY;

    internal static void Show(SceneTree tree) {
        Hide();
        var layer = new CanvasLayer {
            Name = NodeName,
            Layer = LayerId,
        };

        var dock = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Stop,
            CustomMinimumSize = new Vector2(0, DockHeight),
        };
        dock.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.BottomWide);
        dock.OffsetTop = -DockHeight;
        dock.AddThemeStyleboxOverride("panel", MakeBarStyle());

        var stack = new VBoxContainer();
        stack.AddThemeConstantOverride("separation", 4);

        var row = new HBoxContainer {
            CustomMinimumSize = new Vector2(0, 36),
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        row.AddThemeConstantOverride("separation", 8);

        _playBtn = MakeIconButton(MdiIcon.Pause, I18N.T("replay.bar.pause", "Pause"));
        _playBtn.Pressed += CombatReplayPlayback.TogglePause;
        row.AddChild(_playBtn);

        _status = MakeLabel(13, Colors.White);
        _status.CustomMinimumSize = new Vector2(88, 0);
        row.AddChild(_status);

        _restartBtn = MakeIconButton(MdiIcon.SkipBackward, I18N.T("replay.bar.restart", "Restart from beginning"));
        _restartBtn.Pressed += CombatReplayPlayback.RestartFromBeginning;
        row.AddChild(_restartBtn);

        _prevBtn = MakeIconButton(MdiIcon.SkipPrevious, I18N.T("replay.bar.stepPrev", "Cannot step backward"));
        _prevBtn.Disabled = true;
        row.AddChild(_prevBtn);

        _nextBtn = MakeIconButton(MdiIcon.SkipNext, I18N.T("replay.bar.stepNext", "Next sequence"));
        _nextBtn.Pressed += CombatReplayPlayback.StepNext;
        row.AddChild(_nextBtn);

        _modeBtn = MakeTextButton(I18N.T("replay.bar.auto", "Auto"));
        _modeBtn.CustomMinimumSize = new Vector2(88, 36);
        _modeBtn.Pressed += CombatReplayPlayback.ToggleMode;
        row.AddChild(_modeBtn);

        _liveBtn = MakeTextButton(I18N.T("replay.bar.live", "Live"));
        _liveBtn.CustomMinimumSize = new Vector2(88, 36);
        _liveBtn.Pressed += CombatReplayPlayback.ToggleLive;
        row.AddChild(_liveBtn);

        _matchLabel = MakeLabel(13, new Color(0.85f, 0.85f, 0.88f));
        row.AddChild(_matchLabel);

        var spacer = new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        row.AddChild(spacer);

        _speedBtn = MakeTextButton("1×");
        _speedBtn.Pressed += CombatReplayPlayback.CycleSpeed;
        row.AddChild(_speedBtn);

        _exitBtn = MakeTextButton(I18N.T("replay.bar.exit", "End Replay"), MdiIcon.ExitToApp);
        _exitBtn.Pressed += () => {
            Hide();
            CombatReplayPlayback.ExitToMainMenu();
        };
        row.AddChild(_exitBtn);

        stack.AddChild(MakeTimeline());
        stack.AddChild(row);
        dock.AddChild(stack);
        layer.AddChild(dock);
        tree.Root.AddChild(layer);
        _layer = layer;
        _tree = tree;
        tree.ProcessFrame += TickCombatUiInset;

        CombatReplayPlayback.Changed += Refresh;
        Refresh();
    }

    internal static void Hide() {
        CombatReplayPlayback.Changed -= Refresh;
        if (_tree != null) {
            _tree.ProcessFrame -= TickCombatUiInset;
            _tree = null;
        }
        _applyQueued = false;
        RestoreCombatUiInset();
        RestoreContentInset();
        if (_layer != null && GodotObject.IsInstanceValid(_layer))
            _layer.QueueFree();
        _layer = null;
        _playBtn = null;
        _restartBtn = null;
        _prevBtn = null;
        _nextBtn = null;
        _modeBtn = null;
        _liveBtn = null;
        _speedBtn = null;
        _exitBtn = null;
        _status = null;
        _matchLabel = null;
        _timeline = null;
        _roomFills.Clear();
    }

    static Button MakeIconButton(MdiIcon icon, string tooltip) {
        var btn = new Button {
            CustomMinimumSize = new Vector2(40, 36),
            FocusMode = Control.FocusModeEnum.None,
            TooltipText = tooltip,
            IconAlignment = HorizontalAlignment.Center,
            ExpandIcon = true,
            Icon = icon.Texture(IconPx, Colors.White),
        };
        ApplyFlatButton(btn);
        return btn;
    }

    static Button MakeTextButton(string text, MdiIcon? icon = null) {
        var btn = new Button {
            Text = text,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(icon == null ? 56 : 108, 36),
            IconAlignment = HorizontalAlignment.Left,
        };
        if (icon is { } mdi)
            btn.Icon = mdi.Texture(18, Colors.White);
        ApplyFlatButton(btn);
        btn.AddThemeColorOverride("font_color", Colors.White);
        btn.AddThemeColorOverride("font_hover_color", Colors.White);
        btn.AddThemeColorOverride("font_pressed_color", Colors.White);
        btn.AddThemeColorOverride("font_disabled_color", new Color(0.55f, 0.55f, 0.58f));
        btn.AddThemeFontSizeOverride("font_size", 13);
        return btn;
    }

    static void ApplyFlatButton(Button btn) {
        var normal = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        var hover = new StyleBoxFlat {
            BgColor = new Color(1f, 1f, 1f, 0.08f),
            CornerRadiusTopLeft = 4,
            CornerRadiusTopRight = 4,
            CornerRadiusBottomLeft = 4,
            CornerRadiusBottomRight = 4,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4,
        };
        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeStyleboxOverride("disabled", normal);
    }

    static Label MakeLabel(int size, Color color) {
        var label = new Label {
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        return label;
    }

    static void Refresh() {
        if (_playBtn == null || _restartBtn == null || _nextBtn == null || _modeBtn == null ||
            _liveBtn == null || _speedBtn == null || _status == null || _matchLabel == null)
            return;

        var finished = CombatReplayPlayback.IsFinished;
        var active = CombatReplayPlayback.IsActive;
        var manual = CombatReplayPlayback.IsManual;
        bool paused = CombatReplayPlayback.IsPaused;
        _playBtn.Disabled = finished || !active;
        _speedBtn.Disabled = finished || !active || manual;
        _restartBtn.Disabled = !CombatReplayPlayback.CanRestart;
        _nextBtn.Disabled = !CombatReplayPlayback.CanStep;
        _playBtn.Icon = (paused
            ? MdiIcon.Play
            : MdiIcon.Pause).Texture(IconPx, Colors.White);
        _playBtn.TooltipText = paused
            ? I18N.T("replay.bar.play", "Play")
            : I18N.T("replay.bar.pause", "Pause");
        _modeBtn.Text = manual
            ? I18N.T("replay.bar.manual", "Manual")
            : I18N.T("replay.bar.auto", "Auto");
        bool live = CombatReplayPlayback.IsLive;
        _liveBtn.Text = live
            ? I18N.T("replay.bar.live", "Live")
            : I18N.T("replay.bar.liveOff", "Game speed");
        _liveBtn.TooltipText = I18N.T(
            "replay.bar.liveTip",
            "On: real-player pace. Off: game speed.");
        _liveBtn.Disabled = finished || !active;
        string speed = CombatReplayPlayback.SpeedLabel;
        _speedBtn.Text = speed;
        _speedBtn.TooltipText = I18N.T("replay.bar.speed", "Speed {0}", speed);
        _matchLabel.Text = CombatReplayPlayback.IsRunSession
            ? I18N.T("replay.bar.run", "DevTools")
            : I18N.T("replay.bar.match", "Official");
        int index = CombatReplayPlayback.EventIndex;
        int count = CombatReplayPlayback.EventCount;
        _status.Text = finished
            ? I18N.T("replay.bar.done", "Replay finished")
            : I18N.T("replay.bar.progress", "{0} / {1}", index, count);
        RefreshTimeline();
    }

    static StyleBoxFlat MakeBarStyle() {
        return new StyleBoxFlat {
            BgColor = BarBg,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            BorderWidthTop = 1,
            BorderColor = BarBorder,
        };
    }

    static Control MakeTimeline() {
        _timeline = new HBoxContainer {
            CustomMinimumSize = new Vector2(0, TimelineHeight),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.Fill,
        };
        _timeline.AddThemeConstantOverride("separation", 4);
        return _timeline;
    }

    static void RefreshTimeline() {
        if (_timeline == null)
            return;

        var rooms = CombatReplayPlayback.Rooms;
        if (_roomFills.Count != rooms.Count) {
            foreach (var child in _timeline.GetChildren()) {
                _timeline.RemoveChild(child);
                child.Free();
            }
            _roomFills.Clear();
            for (int i = 0; i < rooms.Count; i++)
                _timeline.AddChild(MakeRoomSegment(rooms[i], i));
        }

        int current = CombatReplayPlayback.CurrentRoomIndex;
        bool finished = CombatReplayPlayback.IsFinished;
        float progress = CombatReplayPlayback.RoomProgress;
        for (int i = 0; i < _roomFills.Count; i++) {
            float ratio = finished || i < current ? 1f : i == current ? progress : 0f;
            _roomFills[i].AnchorRight = ratio;
            _roomFills[i].Color = i == current && !finished ? FillCurrent : FillPast;
        }
    }

    static Control MakeRoomSegment(ReplayRoomSegment room, int index) {
        var col = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            MouseDefaultCursorShape = Control.CursorShape.PointingHand,
            TooltipText = RoomTooltip(room),
        };
        col.AddThemeConstantOverride("separation", 3);
        col.GuiInput += e => {
            if (e is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                return;
            col.AcceptEvent();
            CombatReplayPlayback.SeekToRoom(index);
        };

        var icon = new TextureRect {
            CustomMinimumSize = new Vector2(18, 18),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = LoadRoomIcon(room),
            Material = WhiteIconMaterial(),
        };
        col.AddChild(icon);

        var track = new ColorRect {
            CustomMinimumSize = new Vector2(0, 6),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkEnd,
            Color = TrackColor,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        var fill = new ColorRect {
            Color = FillPast,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        fill.SetAnchor(Side.Left, 0);
        fill.SetAnchor(Side.Top, 0);
        fill.SetAnchor(Side.Bottom, 1);
        fill.SetAnchor(Side.Right, 0);
        fill.OffsetLeft = 0;
        fill.OffsetTop = 0;
        fill.OffsetRight = 0;
        fill.OffsetBottom = 0;
        track.AddChild(fill);
        _roomFills.Add(fill);
        col.AddChild(track);
        return col;
    }

    static string RoomTooltip(ReplayRoomSegment room) {
        string name = room.IsStartingBonus
            ? I18N.T("replay.bar.roomNeow", "Starting bonus")
            : room.RoomType switch {
                RoomType.Monster => I18N.T("map.roomNormal", "Normal"),
                RoomType.Elite => I18N.T("map.roomElite", "Elite"),
                RoomType.Boss => I18N.T("map.roomBoss", "Boss"),
                RoomType.Event => I18N.T("map.roomEvent", "Event"),
                RoomType.Shop => I18N.T("map.roomShop", "Shop"),
                RoomType.Treasure => I18N.T("map.roomTreasure", "Treasure"),
                RoomType.RestSite => I18N.T("map.roomRest", "Rest Site"),
                _ => room.PointType == MapPointType.Unknown
                    ? I18N.T("replay.bar.roomUnknown", "Unknown")
                    : I18N.T("replay.bar.room", "{0}", room.RoomType),
            };
        string seek = CombatReplayPlayback.IsRunSession
            ? I18N.T("replay.bar.seekRoom", "Click to jump to this room")
            : I18N.T("replay.bar.seekCombatOnly", "This replay is a single combat. Click to restart.");
        return $"{name}\n{seek}";
    }

    static Texture2D? LoadRoomIcon(ReplayRoomSegment room) {
        string? path = ImageHelper.GetRoomIconPath(room.PointType, room.RoomType, room.ModelId)
            ?? ImageHelper.GetRoomIconPath(room.PointType, room.RoomType, null);
        if (string.IsNullOrEmpty(path) && room.IsStartingBonus)
            path = ImageHelper.GetRoomIconPath(MapPointType.Unknown, RoomType.Event, null);
        if (string.IsNullOrEmpty(path))
            return null;
        try {
            if (PreloadManager.Cache.ContainsKey(path)) {
                try {
                    return PreloadManager.Cache.GetCompressedTexture2D(path);
                }
                catch (Exception) {
                    return PreloadManager.Cache.GetTexture2D(path);
                }
            }
            return ResourceLoader.Load<Texture2D>(path);
        }
        catch (Exception) {
            return null;
        }
    }

    static ShaderMaterial WhiteIconMaterial() {
        if (_whiteIconMat != null)
            return _whiteIconMat;
        var shader = new Shader {
            Code =
                "shader_type canvas_item;\n" +
                "void fragment() {\n" +
                "  vec4 c = texture(TEXTURE, UV);\n" +
                "  COLOR = vec4(1.0, 1.0, 1.0, c.a);\n" +
                "}\n",
        };
        _whiteIconMat = new ShaderMaterial { Shader = shader };
        return _whiteIconMat;
    }

    // ProcessFrame runs before node _Process / tweens; apply after those so we sample live offsets.
    static void TickCombatUiInset() {
        if (_applyQueued || _tree == null)
            return;
        _applyQueued = true;
        Callable.From(ApplyReplayInsets).CallDeferred();
    }

    static void ApplyReplayInsets() {
        _applyQueued = false;
        if (_tree == null)
            return;

        Control? ui = NCombatRoom.Instance?.Ui;
        bool inCombat = ui != null && GodotObject.IsInstanceValid(ui);
        ApplyContentInset(includeRoomContainer: !inCombat);
        if (!inCombat) {
            RestoreCombatUiInset();
            return;
        }

        ApplyCombatHudInset((Control)ui!);
    }

    // Official NTopBar occupies a top strip; RoomContainer lays out in the remaining rect.
    // The dock is a CanvasLayer overlay, so shrink that remaining rect from the bottom.
    static void ApplyContentInset(bool includeRoomContainer) {
        var wanted = new List<Control>();
        if (includeRoomContainer && NRun.Instance is Node run) {
            var room = run.GetNodeOrNull<Control>("RoomContainer");
            if (room != null)
                wanted.Add(room);
        }

        if (NOverlayStack.Instance?.Peek() is Control overlay)
            wanted.Add(overlay);
        else if (NOverlayStack.Instance is Control stack)
            wanted.Add(stack);
        if (NMapScreen.Instance is Control map)
            wanted.Add(map);

        for (int i = _contentInset.Count - 1; i >= 0; i--) {
            var (ctrl, bottom, _) = _contentInset[i];
            if (wanted.Contains(ctrl))
                continue;
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.OffsetBottom = bottom;
            _contentInset.RemoveAt(i);
        }

        foreach (var ctrl in wanted) {
            int i = _contentInset.FindIndex(s => s.Ctrl == ctrl);
            if (i < 0) {
                _contentInset.Add((ctrl, ctrl.OffsetBottom, float.NaN));
                i = _contentInset.Count - 1;
            }

            var (c, bottom, lastBottom) = _contentInset[i];
            bool gameOwned = float.IsNaN(lastBottom) || !Mathf.IsEqualApprox(c.OffsetBottom, lastBottom);
            if (gameOwned)
                bottom = c.OffsetBottom;

            float wantedBottom = bottom - DockHeight;
            if (!Mathf.IsEqualApprox(c.OffsetBottom, wantedBottom))
                c.OffsetBottom = wantedBottom;
            _contentInset[i] = (c, bottom, wantedBottom);
        }
    }

    static void RestoreContentInset() {
        foreach (var (ctrl, bottom, _) in _contentInset) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.OffsetBottom = bottom;
        }
        _contentInset.Clear();
    }

    // Hand / piles are bottom-anchored children; shrinking CombatUi does not move them.
    static void ApplyCombatHudInset(Control ui) {

        if (_hudRoot != ui) {
            RestoreCombatUiInset();
            _hudRoot = ui;
        }

        foreach (var ctrl in CollectBottomHud(ui)) {
            int i = _shifted.FindIndex(s => s.Ctrl == ctrl);
            if (i < 0) {
                _shifted.Add((ctrl, ctrl.OffsetTop, ctrl.OffsetBottom, float.NaN));
                i = _shifted.Count - 1;
            }

            var (c, top, bottom, lastTop) = _shifted[i];
            bool gameOwned = float.IsNaN(lastTop) || !Mathf.IsEqualApprox(c.OffsetTop, lastTop);
            if (gameOwned) {
                top = c.OffsetTop;
                bottom = c.OffsetBottom;
            }

            float wantedTop = top - DockHeight;
            float wantedBottom = bottom - DockHeight;
            if (!Mathf.IsEqualApprox(c.OffsetTop, wantedTop))
                c.OffsetTop = wantedTop;
            if (!Mathf.IsEqualApprox(c.OffsetBottom, wantedBottom))
                c.OffsetBottom = wantedBottom;
            _shifted[i] = (c, top, bottom, wantedTop);
        }

        LiftHandRoot(ui);
    }

    // AnimIn writes Hand.position to an absolute value each tick; re-apply after it.
    // Do not write holder Position — SetTargetPosition lerps from current and accumulates.
    static void LiftHandRoot(Control ui) {
        var hand = ui.GetNodeOrNull<Control>("%Hand");
        if (hand == null)
            return;

        bool gameOwned = float.IsNaN(_handLastY) || !Mathf.IsEqualApprox(hand.Position.Y, _handLastY);
        if (gameOwned)
            _handBaseY = hand.Position.Y;

        float wanted = _handBaseY - DockHeight;
        if (!Mathf.IsEqualApprox(hand.Position.Y, wanted))
            hand.Position = new Vector2(hand.Position.X, wanted);
        _handLastY = wanted;
    }

    static List<Control> CollectBottomHud(Control ui) {
        var nodes = new List<Control>();
        var hand = ui.GetNodeOrNull<Control>("%Hand");
        Add(hand?.GetNodeOrNull<Control>("%CardHolderContainer"));
        Add(hand?.GetNodeOrNull<Control>("%PeekButton"));

        var piles = ui.GetNodeOrNull<Control>("%CombatPileContainer");
        Add(piles?.GetNodeOrNull<Control>("%DrawPile"));
        Add(piles?.GetNodeOrNull<Control>("%DiscardPile"));
        Add(piles?.GetNodeOrNull<Control>("%ExhaustPile"));

        Add(ui.GetNodeOrNull<Control>("%EnergyCounterContainer"));
        Add(ui.GetNodeOrNull<Control>("%EndTurnButton"));
        Add(ui.GetNodeOrNull<Control>("%PingButton"));
        Add(ui.GetNodeOrNull<Control>("%StarCounter"));
        return nodes;

        void Add(Control? node) {
            if (node != null)
                nodes.Add((Control)node);
        }
    }

    static void RestoreCombatUiInset() {
        foreach (var (ctrl, top, bottom, _) in _shifted) {
            if (GodotObject.IsInstanceValid(ctrl)) {
                ctrl.OffsetTop = top;
                ctrl.OffsetBottom = bottom;
            }
        }
        _shifted.Clear();
        _hudRoot = null;
        _handLastY = float.NaN;
    }
}
