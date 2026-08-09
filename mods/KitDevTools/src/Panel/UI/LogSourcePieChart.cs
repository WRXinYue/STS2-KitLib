using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Log source pie chart rendered to a <see cref="TextureRect"/> (avoids missed <c>_Draw</c> during panel slide-in).
/// </summary>
internal sealed partial class LogSourcePieChart : Control {
    private const int RasterSize = 140;

    private readonly List<(string Name, int Count, int PaletteIndex)> _slices = new();
    private readonly TextureRect _texture;
    private int _total;

    public LogSourcePieChart() {
        CustomMinimumSize = new Vector2(RasterSize, RasterSize);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;
        ClipContents = false;

        _texture = new TextureRect {
            Name = "PieTexture",
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _texture.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(_texture);
    }

    public override void _Ready() {
        ThemeManager.OnThemeChanged += OnThemeChanged;
        TreeExiting += OnTreeExiting;
        RefreshTexture();
    }

    private void OnTreeExiting() => ThemeManager.OnThemeChanged -= OnThemeChanged;

    private void OnThemeChanged() => RefreshTexture();

    /// <summary>Same ordering as the textual stats list (Game first, then by count desc).</summary>
    public void SetData(Dictionary<string, int>? modStats) {
        _slices.Clear();
        _total = 0;
        TooltipText = "";

        if (modStats == null || modStats.Count == 0) {
            RefreshTexture();
            return;
        }

        foreach (var kv in modStats)
            _total += kv.Value;

        if (_total <= 0) {
            RefreshTexture();
            return;
        }

        var sorted = new List<KeyValuePair<string, int>>(modStats);
        sorted.Sort((a, b) => {
            if (a.Key == "Game") return -1;
            if (b.Key == "Game") return 1;
            return b.Value.CompareTo(a.Value);
        });

        var tip = new StringBuilder();
        int paletteIdx = 0;
        foreach (var kv in sorted) {
            _slices.Add((kv.Key, kv.Value, paletteIdx++));
            float pct = 100f * kv.Value / _total;
            tip.Append(kv.Key).Append(": ").Append(kv.Value).Append(" (")
                .Append(pct.ToString("0.#")).Append("%)\n");
        }

        TooltipText = tip.ToString().TrimEnd();
        RefreshTexture();
        Callable.From(RefreshTexture).CallDeferred();
    }

    internal void RefreshAfterOverlayOpen() {
        Callable.From(RefreshTexture).CallDeferred();
        var timer = GetTree()?.CreateTimer(0.9);
        if (timer != null)
            timer.Timeout += RefreshTexture;
    }

    private void RefreshTexture() {
        _texture.Texture = RasterizePie();
    }

    private ImageTexture? RasterizePie() {
        var image = Image.CreateEmpty(RasterSize, RasterSize, false, Image.Format.Rgba8);
        if (image == null)
            return null;

        var center = new Vector2(RasterSize / 2f, RasterSize / 2f);
        float radius = RasterSize * 0.42f;
        float innerEmpty = radius * 0.38f;

        if (_slices.Count == 0 || _total <= 0) {
            FillDisk(image, center, innerEmpty, new Color(0.22f, 0.22f, 0.26f, 0.85f));
            StrokeRing(image, center, radius, KitLibTheme.Separator);
            return ImageTexture.CreateFromImage(image);
        }

        float start = -Mathf.Pi / 2f;
        foreach (var (name, count, pIdx) in _slices) {
            Color col = SliceColor(name, pIdx);
            float sweep = count / (float)_total * Mathf.Tau;
            float end = start + sweep;
            FillWedge(image, center, radius, start, end, col);
            start = end;
        }

        StrokeRing(image, center, radius, KitLibTheme.PanelBorder);
        return ImageTexture.CreateFromImage(image);
    }

    private static Color SliceColor(string name, int paletteIndex)
        => LogSourceColors.GetSliceColor(name, paletteIndex);

    private static void FillDisk(Image image, Vector2 center, float radius, Color color) {
        int r = Mathf.CeilToInt(radius);
        int cx = Mathf.RoundToInt(center.X);
        int cy = Mathf.RoundToInt(center.Y);
        float r2 = radius * radius;
        for (int y = cy - r; y <= cy + r; y++) {
            if (y < 0 || y >= RasterSize) continue;
            for (int x = cx - r; x <= cx + r; x++) {
                if (x < 0 || x >= RasterSize) continue;
                float dx = x - center.X;
                float dy = y - center.Y;
                if (dx * dx + dy * dy <= r2)
                    image.SetPixel(x, y, color);
            }
        }
    }

    private static void FillWedge(Image image, Vector2 center, float radius, float fromRad, float toRad, Color color) {
        int r = Mathf.CeilToInt(radius);
        int cx = Mathf.RoundToInt(center.X);
        int cy = Mathf.RoundToInt(center.Y);
        float r2 = radius * radius;
        for (int y = cy - r; y <= cy + r; y++) {
            if (y < 0 || y >= RasterSize) continue;
            for (int x = cx - r; x <= cx + r; x++) {
                if (x < 0 || x >= RasterSize) continue;
                float dx = x - center.X;
                float dy = y - center.Y;
                if (dx * dx + dy * dy > r2)
                    continue;
                if (AngleInSweep(Mathf.Atan2(dy, dx), fromRad, toRad))
                    image.SetPixel(x, y, color);
            }
        }
    }

    private static bool AngleInSweep(float angle, float fromRad, float toRad) {
        angle = NormalizeAngle(angle);
        fromRad = NormalizeAngle(fromRad);
        toRad = NormalizeAngle(toRad);
        if (fromRad <= toRad)
            return angle >= fromRad && angle <= toRad;
        return angle >= fromRad || angle <= toRad;
    }

    private static float NormalizeAngle(float angle) {
        while (angle < 0f) angle += Mathf.Tau;
        while (angle >= Mathf.Tau) angle -= Mathf.Tau;
        return angle;
    }

    private static void StrokeRing(Image image, Vector2 center, float radius, Color color) {
        int cx = Mathf.RoundToInt(center.X);
        int cy = Mathf.RoundToInt(center.Y);
        int ri = Mathf.RoundToInt(radius);
        float outer2 = (radius + 0.5f) * (radius + 0.5f);
        float inner2 = (radius - 0.5f) * (radius - 0.5f);
        for (int y = cy - ri - 1; y <= cy + ri + 1; y++) {
            if (y < 0 || y >= RasterSize) continue;
            for (int x = cx - ri - 1; x <= cx + ri + 1; x++) {
                if (x < 0 || x >= RasterSize) continue;
                float dx = x - center.X;
                float dy = y - center.Y;
                float d2 = dx * dx + dy * dy;
                if (d2 <= outer2 && d2 >= inner2)
                    image.SetPixel(x, y, color);
            }
        }
    }
}

/// <summary>Log source colors: unified mod-tag highlight and pie-chart palette.</summary>
internal static class LogSourceColors {
    internal static readonly Color[] ModPalette =
    {
        new(0.45f, 0.62f, 0.95f, 1f),
        new(0.52f, 0.80f, 0.52f, 1f),
        new(0.95f, 0.66f, 0.38f, 1f),
        new(0.82f, 0.52f, 0.92f, 1f),
        new(0.48f, 0.86f, 0.86f, 1f),
        new(0.92f, 0.48f, 0.55f, 1f),
        new(0.82f, 0.76f, 0.42f, 1f),
        new(0.62f, 0.60f, 0.95f, 1f),
    };

    internal static Color GetSliceColor(string name, int _) {
        if (name == "Game")
            return new Color(KitLibTheme.Subtle.R, KitLibTheme.Subtle.G, KitLibTheme.Subtle.B, 0.95f);
        return ModPalette[GetModPaletteIndex(name) % ModPalette.Length];
    }

    internal static Color GetModHighlightColor(string canonicalModId) {
        if (string.IsNullOrEmpty(canonicalModId) || canonicalModId == "Game")
            return KitLibTheme.Subtle;

        return ModPalette[GetModPaletteIndex(canonicalModId) % ModPalette.Length];
    }

    internal static int GetModPaletteIndex(string modId) {
        unchecked {
            int hash = 17;
            foreach (char c in modId)
                hash = hash * 31 + c;
            return Math.Abs(hash);
        }
    }

    internal static string ColorToBbHex(Color c) {
        int r = (int)(c.R * 255f + 0.5f);
        int g = (int)(c.G * 255f + 0.5f);
        int b = (int)(c.B * 255f + 0.5f);
        return $"#{r:X2}{g:X2}{b:X2}";
    }

    internal static string DimBbHex(string hex, float amount = 0.18f)
        => ColorToBbHex(new Color(hex).Darkened(amount));
}
