using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    private const string TurnChartNodeName = "turn.damage.chart";

    private static List<(int Turn, int Amount)> BuildTurnSeries(
        IEnumerable<KeyValuePair<string, int>> data,
        int maxTurn,
        int? turnLimit) {
        int endTurn = maxTurn > 0 ? maxTurn : 1;
        if (turnLimit.HasValue)
            endTurn = Math.Min(endTurn, turnLimit.Value);

        var lookup = data.ToDictionary(
            kv => int.TryParse(kv.Key, out int t) ? t : 0,
            kv => kv.Value);

        var series = new List<(int Turn, int Amount)>(endTurn);
        for (int turn = 1; turn <= endTurn; turn++)
            series.Add((turn, lookup.GetValueOrDefault(turn)));
        return series;
    }

    private static int SeriesPeak(IReadOnlyList<(int Turn, int Amount)> series) {
        int peak = 0;
        foreach (var (_, amount) in series)
            peak = Math.Max(peak, amount);
        return Math.Max(peak, 1);
    }

    private static TurnTimeSeriesChart MakeTurnDamageChart(
        IEnumerable<KeyValuePair<string, int>> data,
        int maxTurn,
        bool animate,
        int? turnLimit = null) {
        var series = BuildTurnSeries(data, maxTurn, turnLimit);
        var chart = new TurnTimeSeriesChart(TurnChartNodeName);
        chart.SetData(series, SeriesPeak(series), animate);
        return chart;
    }

    private static void RefreshTurnChart(
        Node root,
        Dictionary<string, int> data,
        int maxTurn,
        bool animate,
        int? turnLimit = null) {
        if (root.FindChild(TurnChartNodeName, recursive: true, owned: false) is not TurnTimeSeriesChart chart)
            return;
        var series = BuildTurnSeries(data, maxTurn, turnLimit);
        chart.SetData(series, SeriesPeak(series), animate);
    }

    /// <summary>Time-series chart: X = turn, Y = damage (line + area, no plot background).</summary>
    private sealed partial class TurnTimeSeriesChart : VBoxContainer {
        private const float YAxisWidth = 28f;
        private const float PlotHeight = 128f;

        private readonly Label _yMaxLabel;
        private readonly Label _yMidLabel;
        private readonly TurnSeriesCanvas _canvas;
        private readonly HBoxContainer _xLabels;

        public TurnTimeSeriesChart(string name) {
            Name = name;
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            CustomMinimumSize = new Vector2(0, PlotHeight + 22);
            AddThemeConstantOverride("separation", 4);

            var plotRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            plotRow.AddThemeConstantOverride("separation", 4);

            var yAxis = new VBoxContainer {
                CustomMinimumSize = new Vector2(YAxisWidth, PlotHeight),
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
            };

            _yMaxLabel = MakeAxisLabel("0");
            var ySpacer = new Control { SizeFlagsVertical = SizeFlags.ExpandFill };
            _yMidLabel = MakeAxisLabel("0");
            var yZero = MakeAxisLabel("0");

            yAxis.AddChild(_yMaxLabel);
            yAxis.AddChild(ySpacer);
            yAxis.AddChild(_yMidLabel);
            yAxis.AddChild(yZero);

            _canvas = new TurnSeriesCanvas {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
                CustomMinimumSize = new Vector2(0, PlotHeight),
            };

            plotRow.AddChild(yAxis);
            plotRow.AddChild(_canvas);
            AddChild(plotRow);

            var xRow = new HBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            xRow.AddChild(new Control { CustomMinimumSize = new Vector2(YAxisWidth + 4, 0) });
            _xLabels = new HBoxContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
            };
            _xLabels.AddThemeConstantOverride("separation", 0);
            xRow.AddChild(_xLabels);
            AddChild(xRow);
        }

        public void SetData(IReadOnlyList<(int Turn, int Amount)> series, int scaleMax, bool animate) {
            int max = Math.Max(scaleMax, 1);
            _yMaxLabel.Text = max.ToString();
            _yMidLabel.Text = (max / 2).ToString();
            RebuildXLabels(series);
            _canvas.SetSeries(series, max, animate);
        }

        private void RebuildXLabels(IReadOnlyList<(int Turn, int Amount)> series) {
            while (_xLabels.GetChildCount() > series.Count) {
                var extra = _xLabels.GetChild(_xLabels.GetChildCount() - 1);
                _xLabels.RemoveChild(extra);
                extra.Free();
            }

            for (int i = 0; i < series.Count; i++) {
                var (turn, _) = series[i];
                Label label;
                if (i < _xLabels.GetChildCount()) {
                    label = (Label)_xLabels.GetChild(i);
                }
                else {
                    label = new Label {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    };
                    label.AddThemeFontSizeOverride("font_size", 9);
                    label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
                    _xLabels.AddChild(label);
                }
                label.Text = turn.ToString();
            }
        }

        private static Label MakeAxisLabel(string text) {
            var label = new Label {
                Text = text,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            label.AddThemeFontSizeOverride("font_size", 9);
            label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            return label;
        }
    }

    private sealed partial class TurnSeriesCanvas : Control {
        private IReadOnlyList<(int Turn, int Amount)> _series = Array.Empty<(int, int)>();
        private int _scaleMax = 1;
        private float _anim = 1f;
        private Tween? _tween;

        public TurnSeriesCanvas() {
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
        }

        public override void _Ready() => Resized += () => QueueRedraw();

        public override void _ExitTree() {
            _tween?.Kill();
            base._ExitTree();
        }

        public void SetSeries(IReadOnlyList<(int Turn, int Amount)> series, int scaleMax, bool animate) {
            _series = series;
            _scaleMax = Math.Max(scaleMax, 1);

            if (!animate || _anim >= 0.999f) {
                _tween?.Kill();
                _anim = 1f;
                QueueRedraw();
                return;
            }

            _tween?.Kill();
            _anim = 0f;
            _tween = CreateTween();
            _tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _tween.TweenMethod(Callable.From((float t) => {
                _anim = t;
                if (IsInsideTree())
                    QueueRedraw();
            }), 0f, 1f, BarAnimDuration);
            _tween.Finished += () => {
                _anim = 1f;
                QueueRedraw();
            };
        }

        public override void _Draw() {
            var plot = GetPlotRect();
            if (plot.Size.X <= 1f || plot.Size.Y <= 1f || _series.Count == 0)
                return;

            var gridColor = new Color(KitLibTheme.PanelBorder.R, KitLibTheme.PanelBorder.G,
                KitLibTheme.PanelBorder.B, 0.28f);
            for (int i = 1; i <= 2; i++) {
                float y = plot.Position.Y + plot.Size.Y * i / 3f;
                DrawLine(new Vector2(plot.Position.X, y), new Vector2(plot.End.X, y), gridColor, 1f);
            }

            DrawLine(new Vector2(plot.Position.X, plot.End.Y), plot.End, gridColor, 1f);

            var points = new Vector2[_series.Count];
            for (int i = 0; i < _series.Count; i++)
                points[i] = new Vector2(XForIndex(i, plot), YForAmount(_series[i].Amount, plot));

            if (points.Length >= 2) {
                var area = new Vector2[points.Length + 2];
                area[0] = new Vector2(points[0].X, plot.End.Y);
                Array.Copy(points, 0, area, 1, points.Length);
                area[^1] = new Vector2(points[^1].X, plot.End.Y);
                DrawColoredPolygon(area, new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G,
                    KitLibTheme.Accent.B, 0.14f));
            }

            var lineColor = KitLibTheme.Accent;
            for (int i = 1; i < points.Length; i++)
                DrawLine(points[i - 1], points[i], lineColor, 2f);

            foreach (var p in points) {
                DrawCircle(p, 3f, lineColor);
                DrawCircle(p, 1.5f, KitLibTheme.TextPrimary);
            }
        }

        private Rect2 GetPlotRect() {
            float w = Math.Max(Size.X, 1f);
            float h = Math.Max(Size.Y, 1f);
            return new Rect2(0f, 0f, w, h);
        }

        private float XForIndex(int index, Rect2 plot) {
            if (_series.Count <= 1)
                return plot.Position.X + plot.Size.X * 0.5f;
            return plot.Position.X + index / (float)(_series.Count - 1) * plot.Size.X;
        }

        private float YForAmount(int amount, Rect2 plot) {
            float frac = _scaleMax > 0 ? Math.Clamp((float)amount / _scaleMax, 0f, 1f) : 0f;
            return plot.End.Y - frac * _anim * plot.Size.Y;
        }
    }
}
