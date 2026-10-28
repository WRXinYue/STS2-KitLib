using System;
using Godot;

namespace KitLib.UI;

/// <summary>Browser panel width resolution for dual-column overlays.</summary>
internal static partial class DevPanelUI {
    public const float BrowserPanelWidthMin = 320f;
    private const float BrowserPanelMaxWidthFraction = 0.9f;
    private const float DefaultMaxFallback = 4000f;

    internal static float GetBrowserPanelMaxWidth(Node? viewportForClamp) {
        Viewport? viewport = GetViewport(viewportForClamp);
        if (viewport == null)
            return DefaultMaxFallback;

        float visibleWidth = viewport.GetVisibleRect().Size.X;
        return Math.Max(BrowserPanelWidthMin, visibleWidth * BrowserPanelMaxWidthFraction);
    }

    internal static float GetDualColumnClipHostWidth(Node? viewportForClamp) {
        Viewport? viewport = GetViewport(viewportForClamp);
        if (viewport == null)
            return DefaultMaxFallback;

        float visibleWidth = viewport.GetVisibleRect().Size.X;
        return Math.Max(BrowserPanelWidthMin, visibleWidth - BrowserPanelLeft - BrowserPanelRight);
    }

    internal static (float Main, float Ext) ResolveDualColumnWidths(
        float mainDefault,
        float extDefault,
        bool mainUseMaxWidth,
        Node? viewportForClamp) {
        float maxW = GetBrowserPanelMaxWidth(viewportForClamp);
        float available = Math.Min(GetDualColumnClipHostWidth(viewportForClamp), maxW);

        float extW = Math.Clamp(extDefault, BrowserPanelWidthMin, maxW);
        float mainW = mainUseMaxWidth
            ? Math.Max(BrowserPanelWidthMin, available - extW)
            : Math.Clamp(mainDefault, BrowserPanelWidthMin, maxW);

        if (mainW + extW > available) {
            extW = Math.Clamp(extW, BrowserPanelWidthMin, available - BrowserPanelWidthMin);
            mainW = Math.Max(BrowserPanelWidthMin, available - extW);
        }

        return (mainW, extW);
    }

    private static Viewport? GetViewport(Node? node) {
        if (node?.GetViewport() is { } viewport)
            return viewport;

        if (Engine.GetMainLoop() is SceneTree sceneTree)
            return sceneTree.Root?.GetViewport();

        return null;
    }
}
