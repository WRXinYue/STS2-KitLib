using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Screenshots;

/// <summary>One captured viewport image plus the metadata callers report to players/agents.</summary>
public sealed record ScreenshotCapture(byte[] Bytes, int Width, int Height, string Format, string? Node);

/// <summary>
/// Captures the game viewport for feedback reports and MCP agents. Must run on the Godot main
/// thread: it waits two process frames so the current UI state is fully drawn before reading the
/// viewport texture.
/// </summary>
public static class ViewportScreenshot {
    public const string PngFormat = "png";
    public const string JpegFormat = "jpeg";

    /// <summary>Capture the viewport. <paramref name="nodeFilter"/> crops to the first matching
    /// <see cref="Control"/> (exact name first, then case-insensitive substring).</summary>
    public static async Task<ScreenshotCapture?> CaptureAsync(
        string? format = PngFormat,
        bool hideKitLibChrome = false,
        string? nodeFilter = null) {
        if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
            return null;

        var hidden = hideKitLibChrome ? HideKitLibChrome(tree) : null;
        try {
            // Two frames: the first applies the hide/show changes, the second lets the renderer
            // present the frame we read back.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);

            var viewport = tree.Root.GetViewport();
            if (viewport == null)
                return null;
            var texture = viewport.GetTexture();
            var image = texture?.GetImage();
            if (image == null || image.GetWidth() < 8 || image.GetHeight() < 8)
                return null;

            var resolvedFormat = NormalizeFormat(format);
            if (!string.IsNullOrWhiteSpace(nodeFilter) && FindControl(tree.Root, nodeFilter) is { } control) {
                var cropped = CropToControl(image, viewport, control);
                if (cropped != null)
                    image = cropped;
            }

            var bytes = resolvedFormat == JpegFormat
                ? image.SaveJpgToBuffer(0.85f)
                : image.SavePngToBuffer();
            if (bytes is not { Length: > 0 })
                return null;

            return new ScreenshotCapture(bytes, image.GetWidth(), image.GetHeight(), resolvedFormat, nodeFilter);
        }
        catch (Exception ex) {
            KitLog.Warn("Screenshot", $"Capture failed: {ex.Message}");
            return null;
        }
        finally {
            Restore(hidden);
        }
    }

    public static Task<ScreenshotCapture?> CapturePngAsync(bool hideKitLibChrome = false, string? nodeFilter = null) =>
        CaptureAsync(PngFormat, hideKitLibChrome, nodeFilter);

    private static string NormalizeFormat(string? format) {
        if (string.Equals(format?.Trim(), "jpeg", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(format?.Trim(), "jpg", StringComparison.OrdinalIgnoreCase)) {
            return JpegFormat;
        }
        return PngFormat;
    }

    private static Image? CropToControl(Image image, Viewport viewport, Control control) {
        var rect = control.GetGlobalRect();
        if (rect.Size.X < 8 || rect.Size.Y < 8)
            return null;

        // The viewport texture may be larger than the viewport's logical size (content scale), so
        // convert the control's logical rect into image pixels before cropping.
        var visible = viewport.GetVisibleRect().Size;
        if (visible.X <= 0 || visible.Y <= 0)
            return null;

        var scaleX = image.GetWidth() / visible.X;
        var scaleY = image.GetHeight() / visible.Y;
        var region = new Rect2I(
            (int)MathF.Round(rect.Position.X * scaleX),
            (int)MathF.Round(rect.Position.Y * scaleY),
            (int)MathF.Round(rect.Size.X * scaleX),
            (int)MathF.Round(rect.Size.Y * scaleY));
        var bounds = new Rect2I(0, 0, image.GetWidth(), image.GetHeight());
        region = region.Intersection(bounds);
        if (region.Size.X < 8 || region.Size.Y < 8)
            return null;

        return image.GetRegion(region);
    }

    /// <summary>Exact node name first (a caller asking for "CombatRings" wants that HUD), then a
    /// case-insensitive substring fallback for type-like queries such as "NMapPoint".</summary>
    private static Control? FindControl(Node root, string target) {
        Control? exact = null;
        Control? partial = null;
        Walk(root, target, ref exact, ref partial);
        return exact ?? partial;
    }

    private static void Walk(Node node, string target, ref Control? exact, ref Control? partial) {
        if (node is Control control && control.IsVisibleInTree()) {
            var name = node.Name.ToString();
            if (name.Equals(target, StringComparison.OrdinalIgnoreCase))
                exact ??= control;
            else if (partial == null && name.Contains(target, StringComparison.OrdinalIgnoreCase))
                partial = control;
        }

        foreach (var child in node.GetChildren())
            Walk(child, target, ref exact, ref partial);
    }

    private static List<(CanvasItem Node, bool WasVisible)> HideKitLibChrome(SceneTree tree) {
        var hidden = new List<(CanvasItem, bool)>();

        void HideDirect(Node? parent) {
            if (parent == null || !GodotObject.IsInstanceValid(parent))
                return;
            foreach (var child in parent.GetChildren()) {
                if (child is not CanvasItem canvas || !GodotObject.IsInstanceValid(canvas))
                    continue;
                if (!canvas.Name.ToString().StartsWith("KitLib", StringComparison.Ordinal))
                    continue;
                hidden.Add((canvas, canvas.Visible));
                canvas.Visible = false;
            }
        }

        HideDirect(tree.Root);
        HideDirect(NRun.Instance?.GlobalUi as Node);
        HideDirect(NGame.Instance);
        HideDirect(NGame.Instance?.MainMenu);

        return hidden;
    }

    private static void Restore(List<(CanvasItem Node, bool WasVisible)>? hidden) {
        if (hidden == null)
            return;
        foreach (var (node, wasVisible) in hidden) {
            if (GodotObject.IsInstanceValid(node))
                node.Visible = wasVisible;
        }
    }
}
