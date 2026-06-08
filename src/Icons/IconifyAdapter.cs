using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using Godot;

namespace KitLib.Icons;

/// <summary>
/// Loads icons from the tree-shaken mdi-used.json (EmbeddedResource) and rasterises via
/// <see cref="Image.LoadSvgFromString"/> (engine ThorVG).
/// </summary>
public static class IconifyAdapter {
    private static readonly Dictionary<string, string> _bodies = new();
    private static readonly Dictionary<(string name, int size, uint color), ImageTexture> _cache = new();
    private static int _viewBox = 24;
    private static bool _loaded;

    /// <summary>Load the embedded mdi-used.json. Safe to call multiple times.</summary>
    public static void EnsureLoaded() {
        if (_loaded) return;
        _loaded = true;

        var asm = typeof(IconifyAdapter).Assembly;
        using var stream = asm.GetManifestResourceStream("KitLib.Icons.mdi-used.json");
        if (stream is null) {
            MainFile.Logger.Warn("mdi-used.json not found as embedded resource. No icons available.");
            return;
        }

        using var doc = JsonDocument.Parse(stream);
        var root = doc.RootElement;

        if (root.TryGetProperty("viewBox", out var vb))
            _viewBox = vb.GetInt32();

        if (!root.TryGetProperty("icons", out var icons)) return;

        foreach (var prop in icons.EnumerateObject()) {
            if (prop.Value.TryGetProperty("body", out var body))
                _bodies[prop.Name] = body.GetString() ?? "";
        }

        MainFile.Logger.Info($"IconifyAdapter: loaded {_bodies.Count} icon(s).");
    }

    /// <summary>
    /// Get a rasterised icon as <see cref="ImageTexture"/>.
    /// Results are cached by (name, size, color).
    /// </summary>
    /// <param name="kebabName">Icon name in kebab-case, e.g. "account-check"</param>
    /// <param name="size">Pixel size (square)</param>
    /// <param name="color">RGBA colour</param>
    public static ImageTexture? Get(string kebabName, int size = 24, Color? color = null) {
        EnsureLoaded();

        var col = color ?? Colors.White;
        var key = (kebabName, size, col.ToRgba32());

        if (_cache.TryGetValue(key, out var cached))
            return cached;

        if (!_bodies.TryGetValue(kebabName, out var svgBody)) {
            MainFile.Logger.Warn($"IconifyAdapter: icon '{kebabName}' not found.");
            return null;
        }

        var engineImg = TryLoadSvgViaEngine(svgBody, _viewBox, size, col);
        if (engineImg == null) {
            MainFile.Logger.Warn($"IconifyAdapter: SVG rasterise failed for '{kebabName}' (size={size}).");
            return null;
        }

        var tex = ImageTexture.CreateFromImage(engineImg);
        _cache[key] = tex;
        return tex;
    }

    /// <summary>
    /// Wrap Iconify <c>body</c> fragment in a minimal SVG and load via Godot's SVG module (ThorVG).
    /// </summary>
    private static Image? TryLoadSvgViaEngine(string svgBody, int viewBoxSize, int outputSize, Color color) {
        if (string.IsNullOrWhiteSpace(svgBody))
            return null;

        string rgb = SvgRgbHex(color);
        string body = svgBody.Replace("currentColor", rgb, StringComparison.OrdinalIgnoreCase);
        string a = color.A.ToString(CultureInfo.InvariantCulture);
        string svg =
            $"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 {viewBoxSize} {viewBoxSize}\">" +
            $"<g fill=\"{rgb}\" fill-opacity=\"{a}\">{body}</g></svg>";

        var img = new Image();
        float scale = outputSize / (float)viewBoxSize;
        var err = img.LoadSvgFromString(svg, scale);
        if (err != Error.Ok)
            return null;

        if (img.GetWidth() < 1 || img.GetHeight() < 1)
            return null;

        if (img.GetWidth() != outputSize || img.GetHeight() != outputSize)
            img.Resize(outputSize, outputSize, Image.Interpolation.Cubic);

        return img;
    }

    private static string SvgRgbHex(Color c) {
        static byte B(float f) => (byte)Mathf.Clamp(Mathf.RoundToInt(f * 255f), 0, 255);
        return $"#{B(c.R):X2}{B(c.G):X2}{B(c.B):X2}";
    }

    /// <summary>Check if an icon name is available.</summary>
    public static bool Has(string kebabName) {
        EnsureLoaded();
        return _bodies.ContainsKey(kebabName);
    }

    /// <summary>Clear the texture cache (e.g. on theme change).</summary>
    public static void ClearCache() => _cache.Clear();
}
