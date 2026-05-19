using System;
using System.Collections.Generic;
using DevMode.EnemyIntent;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace DevMode.UI;

/// <summary>Draggable overlay that predicts enemy move/intent sequences during combat.</summary>
internal static partial class MonsterIntentOverlayUI {
    internal const string RootName = "DevModeMonsterIntentOverlay";

    private static MonsterIntentOverlayHost? _overlay;
    private static NGlobalUi? _globalUi;

    internal static bool IsEnabled() =>
        SettingsStore.Current.CombatStatsMonsterIntentOverlayEnabled;

    internal static void SyncState(NGlobalUi? globalUi = null) {
        if (globalUi != null)
            _globalUi = globalUi;
        EnsureAttached();

        if (!IsEnabled() || !ShouldShow()) {
            Hide();
            return;
        }

        Refresh();
    }

    internal static void Attach(NGlobalUi globalUi) {
        _globalUi = globalUi;
        EnsureAttached();
    }

    internal static void Detach(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
        _overlay = null;
        if (_globalUi == globalUi)
            _globalUi = null;
    }

    internal static void Refresh() {
        if (_overlay == null || !GodotObject.IsInstanceValid(_overlay))
            EnsureAttached();
        _overlay?.Refresh();
    }

    internal static void Hide() => _overlay?.HidePanel();

    private static bool ShouldShow() {
        if (!DevModeState.IsActive)
            return false;
        if (CombatManager.Instance?.IsInProgress != true)
            return false;
        return CombatManager.Instance.DebugOnlyGetState() != null;
    }

    private static void EnsureAttached() {
        if (_globalUi == null)
            return;

        var parent = (Node)_globalUi;
        var existing = parent.GetNodeOrNull<Control>(RootName);
        if (existing is MonsterIntentOverlayHost host && GodotObject.IsInstanceValid(host)) {
            _overlay = host;
            return;
        }

        if (_overlay != null && GodotObject.IsInstanceValid(_overlay)
            && _overlay.IsInsideTree() && _overlay.GetParent() == parent)
            return;

        _overlay = null;
        var overlay = new MonsterIntentOverlayHost();
        _overlay = overlay;
        parent.AddChild(overlay);
        overlay.TreeExiting += () => {
            if (_overlay == overlay)
                _overlay = null;
        };
    }

    private static class Layout {
        public const float PanelWidth = 480f;
        public const float Margin = 10f;
        public const int ZIndex = 1309;
        public const float IntentBadgeSize = 40f;
        public const int IntentAnimFps = 15;
    }

    private static string FormatIntentTooltip(
        AbstractIntent intent,
        IReadOnlyList<Creature> targets,
        Creature owner) {
        HoverTip tip = intent.GetHoverTip(targets, owner);
        var lines = new List<string>();
        string? title = tip.Title ?? intent.GetIntentLabel(targets, owner).GetFormattedText();
        if (!string.IsNullOrWhiteSpace(title))
            lines.Add(DevModeTheme.ToPlainTooltipText(title));
        if (!string.IsNullOrWhiteSpace(tip.Description))
            lines.Add(DevModeTheme.ToPlainTooltipText(tip.Description));
        return string.Join("\n", lines);
    }

    private sealed partial class MonsterIntentOverlayHost : Control {
        private readonly PanelContainer _panel;
        private readonly StyleBoxFlat _panelStyle;
        private readonly VBoxContainer _enemyList;
        private readonly FloatingCombatOverlay.DraggablePanelBinding _drag;
        private bool _usingFreePosition;

        public MonsterIntentOverlayHost() {
            Name = RootName;
            MouseFilter = MouseFilterEnum.Ignore;
            ZIndex = Layout.ZIndex;
            SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

            _panel = new PanelContainer {
                Name = "MonsterIntentPanel",
                MouseFilter = MouseFilterEnum.Stop,
                Visible = false,
                CustomMinimumSize = new Vector2(Layout.PanelWidth, 0),
            };
            ApplyDefaultPanelLayout();
            ApplySavedPanelLayout();

            _panelStyle = FloatingCombatOverlay.CreatePanelStyle();
            _panel.AddThemeStyleboxOverride("panel", _panelStyle);

            _drag = new FloatingCombatOverlay.DraggablePanelBinding(
                this,
                _panel,
                Layout.PanelWidth,
                () => _usingFreePosition,
                v => _usingFreePosition = v,
                SavePanelPosition);

            var body = new VBoxContainer();
            body.AddThemeConstantOverride("separation", 6);
            body.AddChild(BuildTitleRow());

            _enemyList = new VBoxContainer();
            _enemyList.AddThemeConstantOverride("separation", 8);
            body.AddChild(_enemyList);
            _panel.AddChild(body);

            AddChild(_panel);

            TreeEntered += OnTreeEntered;
            ThemeManager.OnThemeChanged += OnThemeChanged;
            MonsterIntentOverlayTracker.Changed += OnTrackerChanged;
            TreeExiting += () => {
                TreeEntered -= OnTreeEntered;
                ThemeManager.OnThemeChanged -= OnThemeChanged;
                MonsterIntentOverlayTracker.Changed -= OnTrackerChanged;
            };
        }

        private void OnTreeEntered() {
            if (_usingFreePosition)
                _drag.ClampAndCommit();
            Refresh();
        }

        private void OnTrackerChanged() => Refresh();

        private void OnThemeChanged() {
            var theme = ThemeManager.Current;
            _panelStyle.BgColor = theme.RailBg;
            _panelStyle.BorderColor = theme.RailBorder;
        }

        public void Refresh() {
            if (!IsEnabled() || !ShouldShow()) {
                HidePanel();
                return;
            }

            var state = CombatManager.Instance.DebugOnlyGetState();
            var entries = MonsterIntentReader.CaptureCurrent(state);
            if (entries.Count == 0) {
                ClearEnemyRows();
                _panel.Visible = true;
                MoveToFront();
                return;
            }

            SyncEnemyRows(entries);
            _panel.Visible = true;
            MoveToFront();
        }

        public void HidePanel() => _panel.Visible = false;

        private void ClearEnemyRows() {
            foreach (var child in _enemyList.GetChildren())
                child.QueueFree();
        }

        private void SyncEnemyRows(IReadOnlyList<MonsterIntentEntry> entries) {
            var existing = new Dictionary<string, EnemyRow>(StringComparer.Ordinal);
            foreach (var child in _enemyList.GetChildren()) {
                if (child is EnemyRow row)
                    existing[row.EnemyKey] = row;
            }

            var keepKeys = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < entries.Count; i++) {
                var entry = entries[i];
                keepKeys.Add(entry.EnemyKey);

                if (!existing.TryGetValue(entry.EnemyKey, out var row) || !GodotObject.IsInstanceValid(row)) {
                    row = new EnemyRow();
                    _enemyList.AddChild(row);
                }

                row.Bind(entry);
                if (row.GetIndex() != i)
                    _enemyList.MoveChild(row, i);
            }

            foreach (var child in _enemyList.GetChildren()) {
                if (child is EnemyRow row && !keepKeys.Contains(row.EnemyKey))
                    row.QueueFree();
            }
        }

        private Control BuildTitleRow() {
            var titleRow = new HBoxContainer {
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = CursorShape.Move,
                TooltipText = I18N.T("combatStats.monsterIntent.dragHint", "Drag to move panel"),
            };
            titleRow.AddThemeConstantOverride("separation", 4);
            _drag.WireHandle(titleRow);

            var title = new Label {
                Text = I18N.T("combatStats.monsterIntent.title", "Enemy intents"),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            title.AddThemeFontSizeOverride("font_size", 10);
            title.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            titleRow.AddChild(title);
            return titleRow;
        }

        private void ApplyDefaultPanelLayout() {
            _panel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
            _panel.OffsetTop = Layout.Margin;
            _panel.OffsetLeft = Layout.Margin;
            _panel.OffsetRight = Layout.PanelWidth + Layout.Margin;
            _usingFreePosition = false;
        }

        private void ApplySavedPanelLayout() {
            float? x = SettingsStore.Current.CombatStatsMonsterIntentOverlayPosX;
            float? y = SettingsStore.Current.CombatStatsMonsterIntentOverlayPosY;
            if (x == null || y == null)
                return;

            ApplyFreePosition(new Vector2(x.Value, y.Value));
        }

        private void ApplyFreePosition(Vector2 pos) {
            var size = _panel.Size;
            if (size.X <= 0f)
                size.X = Layout.PanelWidth;
            if (size.Y <= 0f)
                size.Y = _panel.GetCombinedMinimumSize().Y;

            _panel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
            _panel.Size = size;
            _panel.Position = pos;
            _usingFreePosition = true;
        }

        private static void SavePanelPosition(Vector2 pos) =>
            SettingsStore.SetCombatStatsMonsterIntentOverlayPosition(pos.X, pos.Y);

        public override void _Process(double delta) => _drag.Process();
    }

    private sealed partial class EnemyRow : VBoxContainer {
        public string EnemyKey { get; private set; } = "";

        private readonly Label _nameLabel;
        private readonly ScrollContainer _stepScroll;
        private readonly HBoxContainer _stepRow;

        public EnemyRow() {
            AddThemeConstantOverride("separation", 4);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;

            _nameLabel = new Label {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            };
            _nameLabel.AddThemeFontSizeOverride("font_size", 11);
            _nameLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            AddChild(_nameLabel);

            _stepScroll = new ScrollContainer {
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
                VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
                CustomMinimumSize = new Vector2(0, Layout.IntentBadgeSize + 6f),
            };
            _stepRow = new HBoxContainer {
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
            };
            _stepRow.AddThemeConstantOverride("separation", 4);
            _stepScroll.AddChild(_stepRow);
            AddChild(_stepScroll);
        }

        public void Bind(MonsterIntentEntry entry) {
            EnemyKey = entry.EnemyKey;
            _nameLabel.Text = entry.DisplayName;

            foreach (var child in _stepRow.GetChildren())
                child.QueueFree();

            for (int i = 0; i < entry.Steps.Count; i++) {
                if (i > 0)
                    _stepRow.AddChild(MakeArrow());

                _stepRow.AddChild(MakeStepChip(entry.Steps[i], entry.Targets, entry.Owner));
            }

            TooltipText = BuildTooltip(entry);
        }

        private static Control MakeArrow() {
            var arrow = new Label {
                Text = "→",
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            arrow.AddThemeFontSizeOverride("font_size", 12);
            arrow.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            return arrow;
        }

        private static Control MakeStepChip(
            MonsterIntentStep step,
            IReadOnlyList<Creature> targets,
            Creature owner) {
            var panel = new PanelContainer {
                MouseFilter = MouseFilterEnum.Stop,
            };

            var style = new StyleBoxFlat {
                BgColor = step.IsCurrent
                    ? new Color(DevModeTheme.Accent.R, DevModeTheme.Accent.G, DevModeTheme.Accent.B, 0.18f)
                    : new Color(DevModeTheme.PanelBg.R, DevModeTheme.PanelBg.G, DevModeTheme.PanelBg.B, 0.65f),
                BorderColor = step.IsCurrent
                    ? DevModeTheme.Accent
                    : step.IsUncertain
                        ? DevModeTheme.Subtle
                        : DevModeTheme.PanelBorder,
                BorderWidthLeft = 1,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthRight = 1,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 2,
                ContentMarginRight = 2,
                ContentMarginTop = 2,
                ContentMarginBottom = 2,
            };
            panel.AddThemeStyleboxOverride("panel", style);

            var intentsRow = new HBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center,
            };
            intentsRow.AddThemeConstantOverride("separation", 0);

            foreach (AbstractIntent intent in step.Intents)
                intentsRow.AddChild(CreateIntentNode(intent, targets, owner, step.IsCurrent));

            if (step.IsUncertain)
                panel.Modulate = new Color(1f, 1f, 1f, 0.55f);

            panel.AddChild(intentsRow);
            panel.TooltipText = BuildStepTooltip(step, targets, owner);
            return panel;
        }

        private static string BuildStepTooltip(
            MonsterIntentStep step,
            IReadOnlyList<Creature> targets,
            Creature owner) {
            var lines = new List<string> { step.MoveName };
            foreach (AbstractIntent intent in step.Intents)
                lines.Add(FormatIntentTooltip(intent, targets, owner));
            if (step.IsUncertain)
                lines.Add(I18N.T("combatStats.monsterIntent.uncertainHint", "Estimated — enemy AI may branch randomly."));
            if (step.IsCurrent)
                lines.Add(I18N.T("combatStats.monsterIntent.currentHint", "Next enemy action."));
            return string.Join("\n", lines);
        }

        private static Control CreateIntentNode(
            AbstractIntent intent,
            IReadOnlyList<Creature> targets,
            Creature owner,
            bool isCurrent) {
            var badge = new OverlayIntentBadge(isCurrent);
            badge.Bind(intent, targets, owner);
            return badge;
        }

        private static string BuildTooltip(MonsterIntentEntry entry) {
            var lines = new List<string> { entry.DisplayName };
            foreach (var step in entry.Steps)
                lines.Add(step.MoveName);
            return string.Join("\n", lines);
        }
    }

    /// <summary>Compact intent badge using the same textures/animation as combat NIntent.</summary>
    private sealed partial class OverlayIntentBadge : Control {
        private const float ValueBandHeight = 16f;

        private readonly Sprite2D _sprite;
        private readonly Label _valueLabel;
        private readonly bool _animate;

        private AbstractIntent? _intent;
        private IReadOnlyList<Creature> _targets = Array.Empty<Creature>();
        private Creature? _owner;
        private string? _animationName;
        private int? _animationFrame;
        private float _timeAccumulator;

        public OverlayIntentBadge(bool animate) {
            _animate = animate;

            CustomMinimumSize = new Vector2(Layout.IntentBadgeSize, Layout.IntentBadgeSize);
            MouseFilter = MouseFilterEnum.Stop;

            _sprite = new Sprite2D {
                Centered = true,
                Position = new Vector2(Layout.IntentBadgeSize * 0.5f, Layout.IntentBadgeSize * 0.5f - 2f),
            };
            AddChild(_sprite);

            _valueLabel = new Label {
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Position = new Vector2(0f, Layout.IntentBadgeSize - ValueBandHeight),
                Size = new Vector2(Layout.IntentBadgeSize, ValueBandHeight),
                MouseFilter = MouseFilterEnum.Ignore,
            };
            _valueLabel.AddThemeFontSizeOverride("font_size", 9);
            _valueLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            AddChild(_valueLabel);
        }

        public void Bind(AbstractIntent intent, IReadOnlyList<Creature> targets, Creature owner) {
            _intent = intent;
            _targets = targets;
            _owner = owner;
            TooltipText = FormatIntentTooltip(intent, targets, owner);
            UpdateVisuals();
        }

        private void UpdateVisuals() {
            if (_intent == null || _owner == null)
                return;

            _animationName = _intent.GetAnimation(_targets, _owner);
            _animationFrame = null;
            _timeAccumulator = 0f;
            ApplyValueLabel(ResolveValueLabel(_intent, _targets, _owner));
            ApplyAnimationFrame(0);
        }

        private void ApplyValueLabel(string raw) {
            string text = DevModeTheme.StripFontSizeBbcode(raw);
            if (string.IsNullOrWhiteSpace(text)) {
                _valueLabel.Visible = false;
                return;
            }

            _valueLabel.Visible = true;
            _valueLabel.Text = text;
        }

        private static string ResolveValueLabel(
            AbstractIntent intent,
            IReadOnlyList<Creature> targets,
            Creature owner) {
            if (intent is AttackIntent or StatusIntent)
                return intent.GetIntentLabel(targets, owner).GetFormattedText() ?? "";
            return "";
        }

        private void ApplyAnimationFrame(int frameIndex) {
            if (_intent == null || _owner == null || string.IsNullOrEmpty(_animationName))
                return;

            string framePath = IntentAnimData.GetAnimationFrame(_animationName, frameIndex);
            var texture = PreloadManager.Cache.GetTexture2D(framePath);
            _sprite.Texture = texture;
            FitSprite(texture);
        }

        private void FitSprite(Texture2D texture) {
            float maxDim = Mathf.Max(Mathf.Max(texture.GetWidth(), texture.GetHeight()), 1f);
            float target = Layout.IntentBadgeSize * 0.72f;
            float scale = target / maxDim;
            _sprite.Scale = new Vector2(scale, scale);
        }

        public override void _Process(double delta) {
            if (!_animate || _intent == null || _owner == null || _animationName == null)
                return;

            int frameCount = IntentAnimData.GetAnimationFrameCount(_animationName);
            if (frameCount <= 0)
                return;

            int frame = (int)(_timeAccumulator * Layout.IntentAnimFps) % frameCount;
            if (_animationFrame != frame) {
                _animationFrame = frame;
                ApplyAnimationFrame(frame);
            }

            _timeAccumulator += (float)delta;
        }
    }
}
