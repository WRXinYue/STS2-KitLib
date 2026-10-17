using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const float DockLeftGap = 16f;

    /// <summary>
    /// Modal panel with slide-in animation. Used for main-menu toggle overlays and similar
    /// full-screen contexts — not the rail-spliced <see cref="CreateBrowserPanel"/> layout.
    /// Set <paramref name="dockLeft"/> to dock it against the left rail instead of centering it.
    /// </summary>
    internal static PanelContainer CreateMainMenuModalPanel(float width = 560f, bool dockLeft = false) {
        var panel = new PanelContainer {
            Name = "MainMenuModalPanel",
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorTop = 0.15f,
            AnchorBottom = 0.85f,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        if (dockLeft) {
            panel.AnchorLeft = 0;
            panel.AnchorRight = 0;
            panel.OffsetLeft = MainMenuRailHost.MenuRailInset + DockLeftGap;
            panel.OffsetRight = panel.OffsetLeft + width;
        }
        else {
            float halfW = width / 2f;
            panel.AnchorLeft = 0.5f;
            panel.AnchorRight = 0.5f;
            panel.OffsetLeft = -halfW;
            panel.OffsetRight = halfW;
        }

        var style = new StyleBoxFlat {
            BgColor = ColOverlayBg,
            CornerRadiusTopLeft = Radius,
            CornerRadiusTopRight = Radius,
            CornerRadiusBottomLeft = Radius,
            CornerRadiusBottomRight = Radius,
            ContentMarginLeft = 24,
            ContentMarginRight = 24,
            ContentMarginTop = 20,
            ContentMarginBottom = 20,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = ColOverlayBorder,
            ShadowColor = new Color(0, 0, 0, 0.40f),
            ShadowSize = 20
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var content = new VBoxContainer { Name = "Content" };
        content.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        content.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        content.AddThemeConstantOverride("separation", 8);
        panel.AddChild(content);

        panel.Ready += () => {
            float slideOffset = 40f;
            panel.OffsetTop -= slideOffset;
            panel.OffsetBottom -= slideOffset;
            panel.Modulate = new Color(1, 1, 1, 0);

            var tween = panel.CreateTween();
            tween.TweenProperty(panel, "offset_top", 0f, 0.22f)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(panel, "offset_bottom", 0f, 0.22f)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            tween.Parallel()
                 .TweenProperty(panel, "modulate:a", 1f, 0.18f)
                 .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        };

        return panel;
    }

    /// <summary>
    /// Transparent click-to-close layer for <see cref="CreateMainMenuModalPanel"/> stacks.
    /// Pins the rail while open; leaves the rail strip clickable.
    /// </summary>
    internal static ColorRect CreateMainMenuModalBackdrop(NGlobalUi globalUi, Action onClose) {
        bool closed = false;
        void SafeClose() {
            if (closed) return;
            closed = true;
            onClose();
        }

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 0,
            AnchorRight = 1,
            AnchorTop = 0,
            AnchorBottom = 1,
            OffsetLeft = RailW + 32,
            OffsetRight = 0,
            OffsetTop = 0,
            OffsetBottom = 0
        };

        PinRail();
        HoldBrowserRail(globalUi);
        backdrop.TreeExited += () => {
            UnpinRail();
            ReleaseBrowserRail(globalUi);
        };

        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                SafeClose();
        };

        return backdrop;
    }
}
