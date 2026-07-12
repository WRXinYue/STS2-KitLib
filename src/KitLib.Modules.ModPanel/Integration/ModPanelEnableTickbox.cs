using System;
using Godot;
using KitLib.UI;

namespace KitLib.Integration;

/// <summary>Sidebar mod enable toggle; built eagerly in <see cref="Create" /> like other ModPanel chrome.</summary>
public partial class ModPanelEnableTickbox : PanelContainer {
    private Label _check = null!;

    public static ModPanelEnableTickbox Create(bool ticked) {
        var hit = ModPanelUiMetrics.SidebarModEnableToggleHitSize;
        var box = new ModPanelEnableTickbox {
            Name = "ModPanelEnableTickbox",
            CustomMinimumSize = new Vector2(hit, hit),
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.All,
            MouseDefaultCursorShape = CursorShape.Arrow,
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            ClipContents = false,
        };
        box._isTicked = ticked;
        box.BuildChrome();
        return box;
    }

    public event Action<ModPanelEnableTickbox>? Toggled;

    public bool IsTicked => _isTicked;

    private bool _isTicked;
    private bool _hovered;
    private bool _pressing;

    public void SetReadOnly(bool readOnly) {
        MouseFilter = readOnly ? MouseFilterEnum.Ignore : MouseFilterEnum.Stop;
        FocusMode = readOnly ? FocusModeEnum.None : FocusModeEnum.All;
        Modulate = readOnly ? new Color(1f, 1f, 1f, 0.45f) : Colors.White;
    }

    public void SetTicked(bool ticked, bool emit = false) {
        if (_isTicked == ticked)
            return;
        _isTicked = ticked;
        ApplyVisual();
        if (emit)
            Toggled?.Invoke(this);
    }

    private void BuildChrome() {
        _check = new Label {
            Name = "Check",
            Text = "✓",
            MouseFilter = MouseFilterEnum.Ignore,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        _check.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _check.GrowHorizontal = GrowDirection.Both;
        _check.GrowVertical = GrowDirection.Both;
        _check.AddThemeFontSizeOverride("font_size", 12);
        _check.AddThemeColorOverride("font_color", new Color(0.10f, 0.08f, 0.06f, 1f));
        AddChild(_check);

        MouseEntered += () => {
            _hovered = true;
            ApplyVisual();
        };
        MouseExited += () => {
            _hovered = false;
            _pressing = false;
            ApplyVisual();
        };
        FocusEntered += ApplyVisual;
        FocusExited += ApplyVisual;
        GuiInput += OnGuiInput;
        ApplyVisual();
    }

    private void OnGuiInput(InputEvent @event) {
        if (@event is InputEventMouseButton mb && mb.ButtonIndex == MouseButton.Left) {
            if (mb.Pressed) {
                _pressing = true;
                ApplyVisual();
            }
            else if (_pressing) {
                _pressing = false;
                SetTicked(!_isTicked, emit: true);
            }
            AcceptEvent();
            return;
        }
        if (@event is InputEventKey key && key.Pressed && !key.Echo) {
            if (key.Keycode is Key.Space or Key.Enter) {
                SetTicked(!_isTicked, emit: true);
                AcceptEvent();
            }
        }
    }

    private void ApplyVisual() {
        var radius = (int)ModPanelUiMetrics.SidebarModEnableToggleRadius;
        var accent = ModPanelUiPalette.SidebarModActiveAccent;
        var focused = HasFocus();

        if (_isTicked) {
            var fill = accent;
            if (_pressing)
                fill = fill.Darkened(0.12f);
            else if (_hovered)
                fill = fill.Lightened(0.08f);
            AddThemeStyleboxOverride("panel", FilledStyle(fill, radius));
            _check.Visible = true;
        }
        else {
            var borderAlpha = _hovered || focused ? 0.82f : 0.68f;
            var bgAlpha = _pressing ? 0.24f : _hovered ? 0.18f : 0.12f;
            AddThemeStyleboxOverride("panel", BorderStyle(
                new Color(1f, 1f, 1f, bgAlpha),
                new Color(1f, 1f, 1f, borderAlpha),
                radius));
            _check.Visible = false;
        }
    }

    private static StyleBoxFlat FilledStyle(Color bg, int radius) => new() {
        BgColor = bg,
        BorderColor = bg.Lightened(0.18f),
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomRight = radius,
        CornerRadiusBottomLeft = radius,
        AntiAliasing = true,
        ContentMarginLeft = 0,
        ContentMarginRight = 0,
        ContentMarginTop = 0,
        ContentMarginBottom = 0,
    };

    private static StyleBoxFlat BorderStyle(Color bg, Color border, int radius) => new() {
        BgColor = bg,
        BorderColor = border,
        BorderWidthLeft = 1,
        BorderWidthTop = 1,
        BorderWidthRight = 1,
        BorderWidthBottom = 1,
        CornerRadiusTopLeft = radius,
        CornerRadiusTopRight = radius,
        CornerRadiusBottomRight = radius,
        CornerRadiusBottomLeft = radius,
        AntiAliasing = true,
        ContentMarginLeft = 0,
        ContentMarginRight = 0,
        ContentMarginTop = 0,
        ContentMarginBottom = 0,
    };
}
