using System;
using System.Collections.Generic;
using System.Text;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Simple pie chart for log source counts (drawn in <see cref="_Draw"/>).
/// </summary>
internal sealed partial class LogSourcePieChart : Control {

    private readonly List<(string Name, int Count, int PaletteIndex)> _slices = new();
    private int _total;

    public LogSourcePieChart() {
        CustomMinimumSize = new Vector2(140, 140);
        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        MouseFilter = MouseFilterEnum.Stop;
    }

    public override void _Ready() {
        ThemeManager.OnThemeChanged += OnThemeChanged;
        TreeExiting += OnTreeExiting;
    }

    private void OnTreeExiting() {
        ThemeManager.OnThemeChanged -= OnThemeChanged;
    }

    private void OnThemeChanged() => QueueRedraw();

    /// <summary>Same ordering as the textual stats list (Game first, then by count desc).</summary>
    public void SetData(Dictionary<string, int>? modStats) {
        _slices.Clear();
        _total = 0;
        TooltipText = "";

        if (modStats == null || modStats.Count == 0) {
            QueueRedraw();
            return;
        }

        foreach (var kv in modStats)
            _total += kv.Value;

        if (_total <= 0) {
            QueueRedraw();
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
        QueueRedraw();
    }

    public override void _Draw() {
        var size = Size;
        var center = size / 2f;
        float radius = Mathf.Min(size.X, size.Y) * 0.42f;
        if (radius < 4f) return;

        if (_slices.Count == 0 || _total <= 0) {
            DrawCircle(center, radius * 0.38f, new Color(0.22f, 0.22f, 0.26f, 0.85f));
            DrawArc(center, radius, 0f, Mathf.Tau, 48, KitLibTheme.Separator, 1f, true);
            return;
        }

        float start = -Mathf.Pi / 2f;
        foreach (var (name, count, pIdx) in _slices) {
            Color col = SliceColor(name, pIdx);
            float sweep = count / (float)_total * Mathf.Tau;
            float end = start + sweep;
            DrawWedge(center, radius, start, end, col);
            start = end;
        }

        DrawArc(center, radius + 0.5f, 0f, Mathf.Tau, 64, KitLibTheme.PanelBorder, 1f, true);
    }

    private static Color SliceColor(string name, int paletteIndex)
        => LogSourceColors.GetSliceColor(name, paletteIndex);

    private void DrawWedge(Vector2 center, float radius, float fromRad, float toRad, Color color) {
        const int Segments = 40;
        var pts = new Vector2[Segments + 2];
        pts[0] = center;
        for (int i = 0; i <= Segments; i++) {
            float t = i / (float)Segments;
            float ang = Mathf.Lerp(fromRad, toRad, t);
            pts[i + 1] = center + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * radius;
        }

        DrawColoredPolygon(pts, color);
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

    internal static Color GetSliceColor(string name, int paletteIndex) {
        if (name == "Game")
            return new Color(KitLibTheme.Subtle.R, KitLibTheme.Subtle.G, KitLibTheme.Subtle.B, 0.95f);
        return ModPalette[paletteIndex % ModPalette.Length];
    }

    internal static Color GetModHighlightColor(string canonicalModId) {
        if (string.IsNullOrEmpty(canonicalModId) || canonicalModId == "Game")
            return KitLibTheme.Subtle;

        return KitLibTheme.Accent;
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

