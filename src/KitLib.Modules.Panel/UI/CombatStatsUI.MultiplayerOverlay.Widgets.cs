using System;
using System.Collections.Generic;
using Godot;
using KitLib.CombatStats;

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
            _barTrack.RebuildLayout();
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

    /// <summary>Node-based track (same approach as pie/turn charts — avoids missed <c>_Draw</c> on overlays).</summary>
    private sealed partial class MpOverlayBarTrack : Control {
        private readonly PanelContainer _track;
        private readonly HBoxContainer _segmentsBox;
        private readonly List<(string Key, float Amount, Color Color)> _segments = new();
        private readonly List<(string Key, int Amount, Color Color)> _targetSegments = new();
        private float _displayFillWidth;
        private float _targetFillWidth;
        private int _targetTotal = 1;
        private int _maxScore = 1;
        private bool _initialized;
        private Tween? _animTween;

        public MpOverlayBarTrack() {
            MouseFilter = MouseFilterEnum.Ignore;
            ClipContents = false;

            _track = new PanelContainer {
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _track.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            _track.AddThemeStyleboxOverride("panel", MpOverlayBarStyles.Track());
            AddChild(_track);

            _segmentsBox = new HBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _segmentsBox.AddThemeConstantOverride("separation", (int)MpOverlayLayout.SegmentGap);
            AddChild(_segmentsBox);

            Resized += RebuildLayout;
            TreeEntered += RebuildLayout;
        }

        public void SetData(
            IReadOnlyList<(string Key, int Amount, Color Color)> segments,
            int total,
            int maxScore,
            bool animate) {
            _targetTotal = Math.Max(total, 1);
            _maxScore = Math.Max(maxScore, 1);
            _targetFillWidth = ComputeFillWidth(total, _maxScore);

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

        internal void RebuildLayout() {
            _targetFillWidth = ComputeFillWidth(_targetTotal, _maxScore);
            SnapToTarget();
        }

        private float ComputeFillWidth(int total, int maxScore) {
            float trackW = Size.X > 1f ? Size.X : MpOverlayLayout.BarTrackWidth;
            return Math.Max(
                6f,
                trackW * total / (float)Math.Max(maxScore, 1));
        }

        private void RunFillAnimation() {
            float startFill = _displayFillWidth;
            var startMap = new Dictionary<string, float>(_segments.Count);
            foreach (var (key, amount, _) in _segments)
                startMap[key] = amount;

            _animTween?.Kill();
            _animTween = CreateTween();
            _animTween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            _animTween.TweenMethod(Callable.From((float t) => {
                _displayFillWidth = Mathf.Lerp(startFill, _targetFillWidth, t);
                LerpSegmentsIntoDisplay(startMap, t);
                ApplySegmentLayout();
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
            _segments.Clear();
            foreach (var (key, amount, color) in _targetSegments) {
                if (amount > 0)
                    _segments.Add((key, amount, color));
            }
            ApplySegmentLayout();
        }

        private void ApplySegmentLayout() {
            float trackW = Size.X > 1f ? Size.X : MpOverlayLayout.BarTrackWidth;
            float barH = MpOverlayLayout.BarHeight;
            float y = Math.Max(0f, (Size.Y - barH) * 0.5f);
            float fillW = Math.Min(_displayFillWidth, trackW);

            _segmentsBox.Position = new Vector2(0f, y);
            _segmentsBox.CustomMinimumSize = new Vector2(fillW, barH);
            _segmentsBox.Size = new Vector2(fillW, barH);

            while (_segmentsBox.GetChildCount() > 0) {
                var child = _segmentsBox.GetChild(0);
                _segmentsBox.RemoveChild(child);
                child.QueueFree();
            }

            if (fillW < 4f || _segments.Count == 0)
                return;

            for (int i = 0; i < _segments.Count; i++) {
                var (_, amount, color) = _segments[i];
                if (amount <= 0f)
                    continue;

                bool first = i == 0;
                bool last = i == _segments.Count - 1;
                var segment = new PanelContainer {
                    SizeFlagsHorizontal = SizeFlags.ExpandFill,
                    SizeFlagsStretchRatio = amount,
                    SizeFlagsVertical = SizeFlags.ExpandFill,
                    MouseFilter = MouseFilterEnum.Ignore,
                };
                segment.AddThemeStyleboxOverride(
                    "panel",
                    MpOverlayBarStyles.Segment(
                        color,
                        first ? MpOverlayLayout.BarCornerRadius : 0f,
                        last ? MpOverlayLayout.BarCornerRadius : 0f));
                _segmentsBox.AddChild(segment);
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
