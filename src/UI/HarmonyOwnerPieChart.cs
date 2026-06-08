using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Pie chart for Harmony patch counts by owner (top slices + Other).
/// </summary>
internal sealed partial class HarmonyOwnerPieChart : Control {
    private const int MaxNamedSlices = 8;

    private static readonly Color[] SlicePalette =
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

    private readonly List<(string Name, int Count, int PaletteIndex)> _slices = new();
    private int _total;

    public HarmonyOwnerPieChart() {
        CustomMinimumSize = new Vector2(168, 168);
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

    /// <summary>Uses patch counts per Harmony owner (e.g. <see cref="Interop.HarmonySmartAnalysis.SmartAnalysisResult.PatchesByOwner"/>).</summary>
    public void SetData(IReadOnlyList<(string Owner, int PatchCount)>? patchesByOwner) {
        _slices.Clear();
        _total = 0;
        TooltipText = "";

        if (patchesByOwner == null || patchesByOwner.Count == 0) {
            QueueRedraw();
            return;
        }

        foreach (var (_, n) in patchesByOwner)
            _total += n;

        if (_total <= 0) {
            QueueRedraw();
            return;
        }

        var ordered = patchesByOwner.OrderByDescending(x => x.PatchCount).ThenBy(x => x.Owner).ToList();
        var tip = new StringBuilder();
        int paletteIdx = 0;

        if (ordered.Count <= MaxNamedSlices) {
            foreach (var (owner, count) in ordered) {
                _slices.Add((owner, count, paletteIdx++));
                AppendTipLine(tip, owner, count);
            }
        }
        else {
            for (var i = 0; i < MaxNamedSlices; i++) {
                var (owner, count) = ordered[i];
                _slices.Add((owner, count, paletteIdx++));
                AppendTipLine(tip, owner, count);
            }

            var otherSum = 0;
            for (var i = MaxNamedSlices; i < ordered.Count; i++)
                otherSum += ordered[i].PatchCount;

            if (otherSum > 0) {
                var otherLabel = I18N.T("harmony.pie.other", "Other");
                _slices.Add((otherLabel, otherSum, paletteIdx));
                AppendTipLine(tip, otherLabel, otherSum);
            }
        }

        TooltipText = tip.ToString().TrimEnd();
        QueueRedraw();
    }

    private void AppendTipLine(StringBuilder tip, string name, int count) {
        float pct = 100f * count / _total;
        tip.Append(name).Append(": ").Append(count).Append(" (")
            .Append(pct.ToString("0.#")).Append("%)\n");
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
            Color col = SlicePalette[pIdx % SlicePalette.Length];
            float sweep = count / (float)_total * Mathf.Tau;
            float end = start + sweep;
            DrawWedge(center, radius, start, end, col);
            start = end;
        }

        DrawArc(center, radius + 0.5f, 0f, Mathf.Tau, 64, KitLibTheme.PanelBorder, 1f, true);
    }

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
