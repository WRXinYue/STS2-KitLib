using System;
using Godot;
using KitLib.Settings;

namespace KitLib.UI;

/// <summary>Browser panel width persistence for dual-column overlays.</summary>
internal static partial class DevPanelUI {
    public const float BrowserPanelWidthMin = 320f;
    private const float GripPadding = 8f;
    private const float DefaultMaxFallback = 4000f;

    public static float GetMaxBrowserPanelWidth(Node? onTree) {
        Viewport? viewport = GetViewport(onTree);

        if (viewport == null)
            return DefaultMaxFallback;

        float visibleWidth = viewport.GetVisibleRect().Size.X;
        return Math.Max(BrowserPanelWidthMin, visibleWidth - BrowserPanelLeft - GripPadding);
    }

    internal static float ResolveBrowserPanelWidth(string rootName, float codeDefault, Node? viewportForClamp) {
        float maxWidth = GetMaxBrowserPanelWidth(viewportForClamp);

        if (TryGetSavedWidth(rootName, out int savedWidth))
            return Math.Clamp(savedWidth, BrowserPanelWidthMin, maxWidth);

        return codeDefault > 0f ? Math.Min(codeDefault, maxWidth) : codeDefault;
    }

    /// <summary>Clip-host width budget for main + ext columns (matches <see cref="CreateBrowserPanelClipHost"/>).</summary>
    internal static float GetDualColumnClipHostWidth(Node? viewportForClamp) {
        Viewport? viewport = GetViewport(viewportForClamp);
        if (viewport == null)
            return DefaultMaxFallback;

        float visibleWidth = viewport.GetVisibleRect().Size.X;
        return Math.Max(BrowserPanelWidthMin, visibleWidth - BrowserPanelLeft - EffectiveBrowserContentRight);
    }

    internal static (float Main, float Ext) ResolveDualColumnWidths(
        string mainKey,
        string extKey,
        float mainDefault,
        float extDefault,
        Node? viewportForClamp) {
        float available = GetDualColumnClipHostWidth(viewportForClamp);
        float mainW = ResolveBrowserPanelWidth(mainKey, mainDefault, viewportForClamp);
        float extW = ResolveBrowserPanelWidth(extKey, extDefault, viewportForClamp);

        if (mainW + extW <= available)
            return (mainW, extW);

        extW = Math.Clamp(extW, BrowserPanelWidthMin, extW);
        mainW = Math.Max(BrowserPanelWidthMin, available - extW);
        if (mainW + extW > available)
            mainW = Math.Max(BrowserPanelWidthMin, available - extW);
        return (mainW, extW);
    }

    private static Viewport? GetViewport(Node? node) {
        if (node?.GetViewport() is { } viewport)
            return viewport;

        if (Engine.GetMainLoop() is SceneTree sceneTree)
            return sceneTree.Root?.GetViewport();

        return null;
    }

    private static bool TryGetSavedWidth(string rootName, out int width) {
        width = default;
        return SettingsStore.Current.BrowserPanelWidths is { } widths
            && widths.TryGetValue(rootName, out width)
            && width > 0;
    }
}
