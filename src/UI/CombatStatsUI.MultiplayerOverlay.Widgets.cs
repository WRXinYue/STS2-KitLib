using System;
using System.Collections.Generic;
using KitLib.CombatStats;
using Godot;

namespace KitLib.UI;

internal static partial class CombatStatsUI {
    private sealed partial class MpOverlayPlayerRow : Control {
        private readonly Label _nameLabel;
        private readonly MpOverlayBarTrack _barTrack;
        private readonly Label _scoreLabel;
        private bool _isLeader;
        private bool _initialized;
        private float _displayScore;
        private Tween? _scoreTween;

        private string _tooltipName = "";
        private int _tooltipTotal;
        private CombatScoreBreakdown? _tooltipBreakdown;

        public string PlayerKey { get; private set; } = "";

        public MpOverlayPlayerRow() {
            MouseFilter = MouseFilterEnum.Stop;
            CustomMinimumSize = new Vector2(0, MpOverlayLayout.RowHeight);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            var content = new HBoxContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            content.AddThemeConstantOverride("separation", (int)MpOverlayLayout.RowSeparation);
            AddChild(content);

            _nameLabel = new Label {
                CustomMinimumSize = new Vector2(MpOverlayLayout.NameWidth, 0),
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 10);

            _barTrack = new MpOverlayBarTrack {
                CustomMinimumSize = new Vector2(MpOverlayLayout.BarTrackWidth, MpOverlayLayout.BarHeight + 4f),
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };

            _scoreLabel = new Label {
                CustomMinimumSize = new Vector2(MpOverlayLayout.ScoreWidth, 0),
                SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
                HorizontalAlignment = HorizontalAlignment.Right,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _scoreLabel.AddThemeFontSizeOverride("font_size", 10);

            var scoreWrap = new MarginContainer {
                SizeFlagsHorizontal = SizeFlags.ShrinkEnd,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            scoreWrap.AddThemeConstantOverride("margin_right", (int)MpOverlayLayout.ScoreRightPadding);
            scoreWrap.AddChild(_scoreLabel);

            content.AddChild(_nameLabel);
            content.AddChild(_barTrack);
            content.AddChild(scoreWrap);
        }

        public void Bind(PlayerCombatStats player, int total, int maxScore, bool isLeader) {
            PlayerKey = player.Key;
            _isLeader = isLeader;

            string name = ResolvePlayerDisplayName(player);
            var bd = CombatScoreCalculator.BreakdownForDisplay(player);

            _nameLabel.Text = name;
            _barTrack.SetData(ScoreBreakdownSegments(bd), total, maxScore, animate: _initialized);
            AnimateScore(total, _initialized);
            ApplyLeaderStyle();
            ApplyTooltip(name, total, bd);

            _initialized = true;
        }

        internal void RefreshTheme() {
            ApplyLeaderStyle();
            _barTrack.QueueRedraw();
        }

        private void ApplyTooltip(string name, int total, CombatScoreBreakdown bd) {
            _tooltipName = name;
            _tooltipTotal = total;
            _tooltipBreakdown = bd;
            TooltipText = name;
            MouseFilter = MouseFilterEnum.Stop;
        }

        public override Control _MakeCustomTooltip(string forText) {
            if (_tooltipBreakdown == null)
                return new Label { Text = forText };
            return BuildScoreBreakdownTooltipControl(_tooltipName, _tooltipTotal, _tooltipBreakdown);
        }

        private void ApplyLeaderStyle() {
            _nameLabel.AddThemeColorOverride(
                "font_color",
                _isLeader ? KitLibTheme.TextPrimary : KitLibTheme.TextSecondary);
            _scoreLabel.AddThemeColorOverride(
                "font_color",
                _isLeader ? KitLibTheme.Accent : KitLibTheme.TextSecondary);
        }

        private void AnimateScore(int target, bool animate) {
            _scoreTween?.Kill();
            if (!animate) {
                _displayScore = target;
                _scoreLabel.Text = target.ToString();
                return;
            }

            float start = _displayScore;
            _scoreTween = CreateTween();
            _scoreTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _scoreTween.TweenMethod(Callable.From((float t) => {
                _displayScore = Mathf.Lerp(start, target, t);
                _scoreLabel.Text = ((int)Math.Round(_displayScore)).ToString();
            }), 0f, 1f, ValueAnimDuration);
            _scoreTween.Finished += () => {
                _displayScore = target;
                _scoreLabel.Text = target.ToString();
            };
        }

        public override void _ExitTree() {
            _scoreTween?.Kill();
            base._ExitTree();
        }
    }

    /// <summary>Rounded track with animated proportional fill and stacked segments.</summary>
    private sealed partial class MpOverlayBarTrack : Control {
        private readonly List<(string Key, float Amount, Color Color)> _segments = new();
        private readonly List<(string Key, int Amount, Color Color)> _targetSegments = new();
        private float _displayFillWidth;
        private float _targetFillWidth;
        private float _displayTotal = 1f;
        private int _targetTotal = 1;
        private bool _initialized;
        private Tween? _animTween;

        public MpOverlayBarTrack() => MouseFilter = MouseFilterEnum.Ignore;

        public void SetData(
            IReadOnlyList<(string Key, int Amount, Color Color)> segments,
            int total,
            int maxScore,
            bool animate) {
            _targetTotal = Math.Max(total, 1);
            _targetFillWidth = Math.Max(
                6f,
                (Size.X > 1f ? Size.X : MpOverlayLayout.BarTrackWidth) * total / (float)Math.Max(maxScore, 1));

            _targetSegments.Clear();
            foreach (var seg in segments)
                _targetSegments.Add(seg);

            if (!animate || !_initialized) {
                SnapToTarget();
                _initialized = true;
                return;
            }

            RunFillAnimation();
            _initialized = true;
        }

        private void RunFillAnimation() {
            float startFill = _displayFillWidth;
            float startTotal = _displayTotal;
            var startMap = new Dictionary<string, float>(_segments.Count);
            foreach (var (key, amount, _) in _segments)
                startMap[key] = amount;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _animTween.TweenMethod(Callable.From((float t) => {
                _displayFillWidth = Mathf.Lerp(startFill, _targetFillWidth, t);
                _displayTotal = Mathf.Lerp(startTotal, _targetTotal, t);
                LerpSegmentsIntoDisplay(startMap, t);
                QueueRedraw();
            }), 0f, 1f, BarAnimDuration);
            _animTween.Finished += SnapToTarget;
        }

        private void LerpSegmentsIntoDisplay(Dictionary<string, float> startMap, float t) {
            _segments.Clear();
            foreach (var (key, targetAmount, color) in _targetSegments) {
                float amount = Mathf.Lerp(startMap.GetValueOrDefault(key, 0f), targetAmount, t);
                if (amount > 0.01f)
                    _segments.Add((key, amount, color));
            }
        }

        private void SnapToTarget() {
            _animTween?.Kill();
            _displayFillWidth = _targetFillWidth;
            _displayTotal = Math.Max(_targetTotal, 1);
            _segments.Clear();
            foreach (var (key, amount, color) in _targetSegments) {
                if (amount > 0)
                    _segments.Add((key, amount, color));
            }
            QueueRedraw();
        }

        public override void _Draw() {
            float trackW = Size.X > 1f ? Size.X : MpOverlayLayout.BarTrackWidth;
            float barH = MpOverlayLayout.BarHeight;
            float y = Math.Max(0f, (Size.Y - barH) * 0.5f);
            var trackRect = new Rect2(0f, y, trackW, barH);

            DrawStyleBox(MpOverlayBarStyles.Track(), trackRect);

            if (_displayFillWidth < 4f || _segments.Count == 0)
                return;

            var fillRect = new Rect2(0f, y, Math.Min(_displayFillWidth, trackW), barH);
            DrawSegments(fillRect);
        }

        private void DrawSegments(Rect2 fillRect) {
            float total = Math.Max(_displayTotal, 0.01f);
            float gap = MpOverlayLayout.SegmentGap;
            int count = _segments.Count;
            float usableW = fillRect.Size.X - gap * Math.Max(0, count - 1);
            if (usableW <= 1f)
                return;

            float x = fillRect.Position.X;
            float y = fillRect.Position.Y;
            float barH = fillRect.Size.Y;

            for (int i = 0; i < count; i++) {
                var (_, amount, color) = _segments[i];
                float segW = usableW * amount / total;
                if (segW <= 0.5f)
                    continue;

                bool first = i == 0;
                bool last = i == count - 1;
                DrawStyleBox(
                    MpOverlayBarStyles.Segment(
                        color,
                        first ? MpOverlayLayout.BarCornerRadius : 0f,
                        last ? MpOverlayLayout.BarCornerRadius : 0f),
                    new Rect2(x, y, segW, barH));

                var highlight = color.Lerp(Colors.White, 0.18f);
                DrawLine(
                    new Vector2(x + (first ? MpOverlayLayout.BarCornerRadius * 0.35f : 0f), y + 0.5f),
                    new Vector2(x + segW - (last ? MpOverlayLayout.BarCornerRadius * 0.35f : 0f), y + 0.5f),
                    new Color(highlight, 0.55f),
                    1f);

                x += segW + gap;
            }
        }

        public override void _ExitTree() {
            _animTween?.Kill();
            base._ExitTree();
        }
    }

    private static class MpOverlayBarStyles {
        public static StyleBoxFlat Track() =>
            RoundedBox(
                new Color(KitLibTheme.Subtle, 0.22f),
                new Color(KitLibTheme.PanelBorder, 0.45f),
                MpOverlayLayout.BarCornerRadius);

        public static StyleBoxFlat Segment(Color color, float radiusLeft, float radiusRight) =>
            new() {
                BgColor = color,
                CornerRadiusTopLeft = (int)radiusLeft,
                CornerRadiusBottomLeft = (int)radiusLeft,
                CornerRadiusTopRight = (int)radiusRight,
                CornerRadiusBottomRight = (int)radiusRight,
            };

        private static StyleBoxFlat RoundedBox(Color bg, Color border, float radius) =>
            new() {
                BgColor = bg,
                BorderColor = border,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = (int)radius,
                CornerRadiusTopRight = (int)radius,
                CornerRadiusBottomLeft = (int)radius,
                CornerRadiusBottomRight = (int)radius,
            };
    }
}
