using System;
using KitLib.Icons;
using Godot;

namespace KitLib.UI;

internal static class ContextRailWidgets {
    internal const float IconBtnSize = 36f;

    internal static Button CreateContextIconButton(
        MdiIcon icon,
        string tooltip,
        Action? onPressed = null,
        Color? tint = null,
        int iconSize = 18) {
        var btn = new Button {
            CustomMinimumSize = new Vector2(IconBtnSize, IconBtnSize),
            FocusMode = Control.FocusModeEnum.None,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = tooltip,
            IconAlignment = HorizontalAlignment.Center,
            Icon = icon.Texture(iconSize, tint ?? ThemeManager.Current.IconNormal),
        };

        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = iconSize >= 20 ? 6 : 4,
            ContentMarginRight = iconSize >= 20 ? 6 : 4,
            ContentMarginTop = iconSize >= 20 ? 6 : 4,
            ContentMarginBottom = iconSize >= 20 ? 6 : 4,
        };
        btn.AddThemeStyleboxOverride("normal", flat);
        btn.AddThemeStyleboxOverride("hover", flat);
        btn.AddThemeStyleboxOverride("pressed", flat);
        btn.AddThemeStyleboxOverride("focus", flat);

        if (onPressed != null)
            btn.Pressed += onPressed;

        return btn;
    }

    internal static void ClearChildren(Node host) {
        foreach (var child in host.GetChildren())
            child.QueueFree();
    }

    internal static Control CreateRailDivider() {
        var wrap = new CenterContainer();
        var line = new ColorRect {
            CustomMinimumSize = new Vector2(24, 1),
            Color = new Color(KitLibTheme.Subtle.R, KitLibTheme.Subtle.G, KitLibTheme.Subtle.B, 0.35f),
        };
        wrap.AddChild(line);
        return wrap;
    }
}
