using Godot;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;

namespace KitLib.UI;

internal sealed partial class MainMenuCornerIconButton : NButton {
    internal const float ButtonSize = 64f;
    const int CornerRadius = 10;
    const int BorderWidth = 2;
    static readonly Color FrameBorder = new(0.91f, 0.86f, 0.75f, 1f);

    public static MainMenuCornerIconButton Create(Texture2D? icon, string tooltip) {
        var button = new MainMenuCornerIconButton {
            CustomMinimumSize = new(ButtonSize, ButtonSize),
            FocusMode = FocusModeEnum.All,
            MouseFilter = MouseFilterEnum.Stop,
            PivotOffset = new(ButtonSize / 2f, ButtonSize / 2f),
            TooltipText = tooltip,
        };
        button.AddChild(CreateFramedIcon(icon, button.PivotOffset));
        return button;
    }

    static Control CreateFramedIcon(Texture2D? iconTexture, Vector2 pivotOffset) {
        var frame = new Panel {
            Name = "IconFrame",
            MouseFilter = MouseFilterEnum.Ignore,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            PivotOffset = pivotOffset,
        };

        var style = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            BorderColor = FrameBorder,
            BorderWidthTop = BorderWidth,
            BorderWidthBottom = BorderWidth,
            BorderWidthLeft = BorderWidth,
            BorderWidthRight = BorderWidth,
            CornerRadiusTopLeft = CornerRadius,
            CornerRadiusTopRight = CornerRadius,
            CornerRadiusBottomLeft = CornerRadius,
            CornerRadiusBottomRight = CornerRadius,
            AntiAliasing = true,
        };
        frame.AddThemeStyleboxOverride("panel", style);

        var icon = new TextureRect {
            Name = "Icon",
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
            OffsetLeft = BorderWidth,
            OffsetTop = BorderWidth,
            OffsetRight = -BorderWidth,
            OffsetBottom = -BorderWidth,
            MouseFilter = MouseFilterEnum.Ignore,
            Texture = iconTexture,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        frame.AddChild(icon);
        return frame;
    }
}
