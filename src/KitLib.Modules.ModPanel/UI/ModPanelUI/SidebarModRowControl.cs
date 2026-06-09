using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.UI;

/// <summary>Focusable sidebar mod row; mirrors vanilla <c>NModMenuRow</c> controller behavior.</summary>
public partial class SidebarModRowControl : NClickableControl {
    private Panel _bgPanel = null!;
    private StyleBoxFlat _innerStyle = null!;
    private Action? _onSelect;
    private bool _selected;
    private bool _pressing;

    public string ModId { get; private set; } = "";

    public void Configure(string modId, string displayName, string tooltip, StyleBoxFlat innerStyle, Action onSelect) {
        ModId = modId;
        _innerStyle = innerStyle;
        _onSelect = onSelect;
        FocusMode = FocusModeEnum.All;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(0f, 62f);
        TooltipText = tooltip;

        _bgPanel = new Panel {
            MouseFilter = MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        _bgPanel.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _bgPanel.AddThemeStyleboxOverride("panel", innerStyle);
        AddChild(_bgPanel);

        var titleLbl = new Label {
            MouseFilter = MouseFilterEnum.Ignore,
            Text = displayName,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Center,
            LabelSettings = new LabelSettings {
                FontSize = 22,
                FontColor = ModPanelUiPalette.LabelPrimary,
            },
        };
        titleLbl.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var labelLeft = 18 + (int)ModPanelUiMetrics.SidebarModAccentBarWidth +
                        ModPanelUiMetrics.SidebarModAccentTextGutter;
        titleLbl.OffsetLeft = labelLeft;
        titleLbl.OffsetRight = -18;
        titleLbl.OffsetTop = 10;
        titleLbl.OffsetBottom = -10;
        AddChild(titleLbl);
    }

    public override void _Ready() {
        ConnectSignals();
        RefreshChrome();
    }

    public void SetSelected(bool selected) {
        if (_selected == selected)
            return;
        _selected = selected;
        RefreshChrome();
    }

    protected override void OnFocus() => RefreshChrome();

    protected override void OnUnfocus() => RefreshChrome();

    protected override void OnPress() {
        _pressing = true;
        RefreshChrome();
    }

    protected override void OnRelease() {
        _pressing = false;
        _onSelect?.Invoke();
        RefreshChrome();
    }

    private void RefreshChrome() {
        ModPanelUI.ApplySidebarModGroupInnerRowStyle(_innerStyle, _selected, _pressing, HasFocus());
    }
}
