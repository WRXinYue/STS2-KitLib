using System;
using System.Diagnostics;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

/// <summary>Browser panel width: persistence and resize grip wiring.</summary>
internal static partial class DevPanelUI {
    public const float BrowserPanelWidthMin = 320f;
    private const float GripPadding = 8f;
    private const float DefaultMaxFallback = 4000f;

    public static void ApplyFixedWidthToBrowserPanel(PanelContainer panel, float width) {
        Debug.Assert(panel != null, "Panel cannot be null");

        float targetWidth = Math.Max(BrowserPanelWidthMin, width);
        panel.AnchorLeft = 0;
        panel.AnchorRight = 0;
        panel.OffsetLeft = BrowserPanelLeft;
        panel.OffsetRight = BrowserPanelLeft + targetWidth;
    }

    /// <summary>Fixed width from the rail. <paramref name="widthPx"/> may be 0 (collapsed at rail) for open animation; no 320px minimum.</summary>
    internal static void SetFixedPixelWidthFromRailUnclamped(PanelContainer panel, float widthPx) {
        panel.AnchorLeft = 0;
        panel.AnchorRight = 0;
        panel.OffsetLeft = BrowserPanelLeft;
        panel.OffsetRight = BrowserPanelLeft + Math.Max(0f, widthPx);
    }

    /// <summary>Full-width browser panel: rail splice to the right screen margin (matches <c>CreateBrowserPanel(0)</c>).</summary>
    internal static void ApplyFullWidthBrowserPanelLayout(PanelContainer panel) {
        panel.AnchorLeft = 0;
        panel.AnchorRight = 1;
        panel.OffsetLeft = BrowserPanelLeft;
        panel.OffsetRight = -EffectiveBrowserContentRight;
    }

    public static float GetMaxBrowserPanelWidth(Node? onTree) {
        Viewport? viewport = GetViewport(onTree);

        if (viewport == null)
            return DefaultMaxFallback;

        float visibleWidth = viewport.GetVisibleRect().Size.X;
        return Math.Max(BrowserPanelWidthMin, visibleWidth - BrowserPanelLeft - GripPadding);
    }

    internal static float ResolveBrowserPanelWidth(string rootName, float codeDefault, Node? viewportForClamp) {
        float maxWidth = GetMaxBrowserPanelWidth(viewportForClamp);

        // Try to load saved width first
        if (TryGetSavedWidth(rootName, out int savedWidth)) {
            return Math.Clamp(savedWidth, BrowserPanelWidthMin, maxWidth);
        }

        return codeDefault > 0f ? Math.Min(codeDefault, maxWidth) : codeDefault;
    }

    /// <summary>Applies a previously saved width to the browser panel if one exists.</summary>
    internal static void ApplyInitialBrowserWidthFromSettings(Node? viewportForClamp, PanelContainer panel, string rootName) {
        float width = ResolveBrowserPanelWidth(rootName, 0f, viewportForClamp);
        if (width > 0.5f)
            ApplyFixedWidthToBrowserPanel(panel, width);
    }

    private static void RegisterBrowserPanelWidthGrip(Control root, PanelContainer panel, string rootName) {
        var grip = new BrowserPanelWidthGrip(root, panel, rootName);
        grip.TooltipText = I18N.T("panel.widthGrip", "Drag to resize width");
        root.AddChild(grip);
    }

    #region Helper Methods

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

    #endregion
}
