using System;
using Godot;
using MegaCrit.Sts2.addons.mega_text;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Nodes.Screens.Settings;

namespace KitLib.UI;

/// <summary>NSettingsButton-styled control for the settings General-tab KitLib entry (RitsuLib pattern).</summary>
internal sealed partial class ModPanelSettingsEntryButton : NSettingsButton {
    private const string SelectionReticleScenePath = "res://scenes/ui/selection_reticle.tscn";

    private readonly Action? _action;
    private readonly string? _text;
    private MegaLabel? _buttonLabel;

    public ModPanelSettingsEntryButton(string text, Action action) {
        _text = text;
        _action = action;

        CustomMinimumSize = new Vector2(320f, 64f);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        FocusMode = FocusModeEnum.All;

        var image = new TextureRect {
            Name = "Image",
            Material = ModPanelSettingsEntryResources.CreateAccentButtonMaterial(),
            CustomMinimumSize = new Vector2(64f, 64f),
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            PivotOffset = new Vector2(140f, 32f),
            Texture = ModPanelSettingsEntryResources.ButtonTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        AddChild(image);

        var label = new MegaLabel {
            Name = "Label",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            PivotOffset = new Vector2(140f, 32f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeColorOverride("font_color", new Color(0.91f, 0.86359f, 0.7462f));
        label.AddThemeColorOverride("font_shadow_color", new Color(0f, 0f, 0f, 0.25098f));
        label.AddThemeColorOverride("font_outline_color", ModPanelSettingsEntryResources.AccentButtonOutlineColor);
        label.AddThemeConstantOverride("shadow_offset_x", 4);
        label.AddThemeConstantOverride("shadow_offset_y", 3);
        label.AddThemeConstantOverride("outline_size", 12);
        label.AddThemeConstantOverride("shadow_outline_size", 0);
        label.AddThemeFontOverride("font", ModPanelSettingsEntryResources.ButtonFont);
        label.AddThemeFontSizeOverride("font_size", 28);
        label.MinFontSize = 16;
        label.MaxFontSize = 28;
        AddChild(label);

        AddChild(CreateSelectionReticle());
    }

    public ModPanelSettingsEntryButton() { }

    public override void _Ready() {
        ConnectSignals();
        _buttonLabel = GetNode<MegaLabel>("Label");
        if (_text != null)
            _buttonLabel.SetTextAutoSize(_text);

        Callable.From(SyncLayoutDependentPivots).CallDeferred();
    }

    protected override void OnRelease() {
        base.OnRelease();
        _action?.Invoke();
        if (HasFocus())
            ReleaseFocus();
    }

    static Control CreateSelectionReticle() {
        var reticle = PreloadManager.Cache.GetScene(SelectionReticleScenePath).Instantiate<Control>();
        reticle.Name = "SelectionReticle";
        reticle.AnchorRight = 1f;
        reticle.AnchorBottom = 1f;
        reticle.OffsetLeft = 0f;
        reticle.OffsetTop = 0f;
        reticle.OffsetRight = 0f;
        reticle.OffsetBottom = 0f;
        reticle.GrowHorizontal = GrowDirection.Both;
        reticle.GrowVertical = GrowDirection.Both;
        reticle.MouseFilter = MouseFilterEnum.Ignore;
        return reticle;
    }

    void SyncLayoutDependentPivots() {
        if (!IsInsideTree())
            return;

        PivotOffset = Size * 0.5f;
        if (GetNodeOrNull<TextureRect>("Image") is { } image)
            image.PivotOffset = image.Size * 0.5f;
        if (_buttonLabel != null)
            _buttonLabel.PivotOffset = _buttonLabel.Size * 0.5f;
    }
}
