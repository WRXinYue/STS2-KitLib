using System;
using System.Collections.Generic;
using KitLib.EnemyIntent;
using Godot;
using MegaCrit.Sts2.Core.Assets;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Entities.Intents;
using MegaCrit.Sts2.Core.HoverTips;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.UI;

internal static class IntentOverlayLayout {
    public const float BadgeSize = 40f;
    public const float CompactBadgeSize = 32f;
    public const int AnimFps = 15;
}

internal static class IntentTooltip {
    public static string Format(AbstractIntent intent, IReadOnlyList<Creature> targets, Creature owner) {
        if (!MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState()))
            return "";

        HoverTip tip = intent.GetHoverTip(targets, owner);
        var lines = new List<string>();
        string? title = tip.Title ?? intent.GetIntentLabel(targets, owner).GetFormattedText();
        if (!string.IsNullOrWhiteSpace(title))
            lines.Add(KitLibTheme.ToPlainTooltipText(title));
        if (!string.IsNullOrWhiteSpace(tip.Description))
            lines.Add(KitLibTheme.ToPlainTooltipText(tip.Description));
        return string.Join("\n", lines);
    }

    public static string FormatStep(MonsterIntentStep step, IReadOnlyList<Creature> targets, Creature owner) {
        var lines = new List<string> { step.MoveName };
        if (MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState())) {
            foreach (AbstractIntent intent in step.Intents)
                lines.Add(Format(intent, targets, owner));
        }
        if (step.IsUncertain)
            lines.Add(I18N.T("enemyIntent.uncertainHint", "Estimated — enemy AI may branch randomly."));
        else if (step.IsCurrent)
            lines.Add(I18N.T("enemyIntent.currentHint", "Currently displayed intent."));
        else
            lines.Add(I18N.T("enemyIntent.nextTurnHint", "Predicted next enemy turn."));
        return string.Join("\n", lines);
    }
}

internal static class IntentPreviewRows {
    internal static void Sync(
        VBoxContainer list,
        IReadOnlyList<MonsterIntentEntry> entries,
        bool displayedOnly,
        float badgeSize = IntentOverlayLayout.BadgeSize,
        bool stackMultipleIntents = false) {
        var existing = new Dictionary<string, IntentEnemyPreviewRow>(StringComparer.Ordinal);
        foreach (var child in list.GetChildren()) {
            if (child is IntentEnemyPreviewRow row)
                existing[row.EnemyKey] = row;
        }

        var keepKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            keepKeys.Add(entry.EnemyKey);

            if (!existing.TryGetValue(entry.EnemyKey, out var row) || !GodotObject.IsInstanceValid(row)) {
                row = new IntentEnemyPreviewRow(badgeSize, stackMultipleIntents);
                list.AddChild(row);
            }

            row.Bind(entry, displayedOnly);
            if (row.GetIndex() != i)
                list.MoveChild(row, i);
        }

        foreach (var child in list.GetChildren()) {
            if (child is IntentEnemyPreviewRow row && !keepKeys.Contains(row.EnemyKey))
                row.QueueFree();
        }
    }
}

internal sealed partial class IntentEnemyPreviewRow : VBoxContainer {
    public string EnemyKey { get; private set; } = "";

    private readonly Label _nameLabel;
    private readonly ScrollContainer _stepScroll;
    private readonly HBoxContainer _stepRow;
    private readonly float _badgeSize;
    private readonly bool _stackMultipleIntents;
    private string _lastBindKey = "";

    public IntentEnemyPreviewRow(float badgeSize, bool stackMultipleIntents = false) {
        _badgeSize = badgeSize;
        _stackMultipleIntents = stackMultipleIntents;
        AddThemeConstantOverride("separation", 4);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _nameLabel = new Label {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", badgeSize <= IntentOverlayLayout.CompactBadgeSize ? 9 : 11);
        _nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        AddChild(_nameLabel);

        _stepScroll = new ScrollContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.ShowNever,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, badgeSize + 6f),
        };
        _stepRow = new HBoxContainer {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _stepRow.AddThemeConstantOverride("separation", 4);
        _stepScroll.AddChild(_stepRow);
        AddChild(_stepScroll);
    }

    public void Bind(MonsterIntentEntry entry, bool displayedOnly) {
        string bindKey = BuildBindKey(entry, displayedOnly);
        if (bindKey == _lastBindKey)
            return;

        _lastBindKey = bindKey;
        EnemyKey = entry.EnemyKey;
        _nameLabel.Text = entry.DisplayName;

        foreach (var child in _stepRow.GetChildren())
            child.QueueFree();

        if (displayedOnly) {
            MonsterIntentStep? step = null;
            foreach (var candidate in entry.Steps) {
                if (candidate.IsCurrent) {
                    step = candidate;
                    break;
                }
            }
            step ??= entry.Steps.Count > 0 ? entry.Steps[0] : null;
            UpdateScrollHeight(step?.Intents.Count ?? 0);
            if (step != null && step.Intents.Count > 0)
                _stepRow.AddChild(MakeStepChip(step, entry.Targets, entry.Owner));
            TooltipText = step == null ? entry.DisplayName : BuildStepTooltip(entry, step);
            return;
        }

        UpdateScrollHeight(1);
        for (int i = 0; i < entry.Steps.Count; i++) {
            var step = entry.Steps[i];

            if (i > 0)
                _stepRow.AddChild(MakeArrow());

            _stepRow.AddChild(MakeStepChip(step, entry.Targets, entry.Owner));
        }

        TooltipText = BuildTooltip(entry);
    }

    private static string BuildBindKey(MonsterIntentEntry entry, bool displayedOnly) {
        if (displayedOnly) {
            MonsterIntentStep? step = null;
            foreach (var candidate in entry.Steps) {
                if (candidate.IsCurrent) {
                    step = candidate;
                    break;
                }
            }
            step ??= entry.Steps.Count > 0 ? entry.Steps[0] : null;
            if (step == null)
                return $"{entry.EnemyKey}|none";
            return $"{entry.EnemyKey}|{step.MoveId}|{step.IsCurrent}|{step.IsUncertain}|{step.Intents.Count}";
        }

        var parts = new List<string>(entry.Steps.Count + 1) { entry.EnemyKey };
        foreach (var step in entry.Steps)
            parts.Add($"{step.MoveId}:{step.IsCurrent}:{step.IsUncertain}:{step.Intents.Count}");
        return string.Join('/', parts);
    }

    private static string BuildStepTooltip(MonsterIntentEntry entry, MonsterIntentStep step) {
        var lines = new List<string> { entry.DisplayName, step.MoveName };
        return string.Join("\n", lines);
    }

    private void UpdateScrollHeight(int intentCount) {
        if (intentCount <= 0) {
            _stepScroll.CustomMinimumSize = Vector2.Zero;
            return;
        }

        float height;
        if (_stackMultipleIntents && intentCount > 1) {
            const float stackSeparation = 2f;
            const float padding = 6f;
            height = _badgeSize * intentCount + stackSeparation * (intentCount - 1) + padding;
        }
        else {
            height = _badgeSize + 6f;
        }

        _stepScroll.CustomMinimumSize = new Vector2(0, height);
    }

    private static Control MakeArrow() {
        var arrow = new Label {
            Text = "→",
            VerticalAlignment = VerticalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        arrow.AddThemeFontSizeOverride("font_size", 12);
        arrow.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return arrow;
    }

    private Control MakeStepChip(
        MonsterIntentStep step,
        IReadOnlyList<Creature> targets,
        Creature owner) {
        var panel = new PanelContainer {
            MouseFilter = MouseFilterEnum.Stop,
        };

        var style = new StyleBoxFlat {
            BgColor = step.IsCurrent
                ? new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.18f)
                : new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.65f),
            BorderColor = step.IsCurrent
                ? KitLibTheme.Accent
                : step.IsUncertain
                    ? KitLibTheme.Subtle
                    : KitLibTheme.PanelBorder,
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

        bool stackIntents = _stackMultipleIntents && step.Intents.Count > 1;
        BoxContainer intentsRow = stackIntents
            ? new VBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center,
            }
            : new HBoxContainer {
                MouseFilter = MouseFilterEnum.Ignore,
                Alignment = BoxContainer.AlignmentMode.Center,
            };
        intentsRow.AddThemeConstantOverride("separation", stackIntents ? 2 : 0);

        foreach (AbstractIntent intent in step.Intents) {
            var badge = new IntentOverlayBadge(_badgeSize, step.IsCurrent);
            badge.Bind(intent, targets, owner);
            intentsRow.AddChild(badge);
        }

        if (step.IsUncertain)
            panel.Modulate = new Color(1f, 1f, 1f, 0.55f);

        panel.AddChild(intentsRow);
        if (MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState()))
            panel.TooltipText = IntentTooltip.FormatStep(step, targets, owner);
        return panel;
    }

    private static string BuildTooltip(MonsterIntentEntry entry) {
        var lines = new List<string> { entry.DisplayName };
        foreach (var step in entry.Steps)
            lines.Add(step.MoveName);
        return string.Join("\n", lines);
    }
}

internal sealed partial class IntentOverlayBadge : Control {
    private readonly float _badgeSize;
    private readonly Sprite2D _sprite;
    private readonly Label _valueLabel;
    private readonly bool _animate;

    private AbstractIntent? _intent;
    private IReadOnlyList<Creature> _targets = Array.Empty<Creature>();
    private Creature? _owner;
    private string? _animationName;
    private int? _animationFrame;
    private float _timeAccumulator;

    public IntentOverlayBadge(float badgeSize, bool animate) {
        _badgeSize = badgeSize;
        _animate = animate;

        CustomMinimumSize = new Vector2(badgeSize, badgeSize);
        MouseFilter = MouseFilterEnum.Stop;

        _sprite = new Sprite2D {
            Centered = true,
            Position = new Vector2(badgeSize * 0.5f, badgeSize * 0.5f - 2f),
        };
        AddChild(_sprite);

        float valueBand = badgeSize <= IntentOverlayLayout.CompactBadgeSize ? 13f : 16f;
        _valueLabel = new Label {
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Position = new Vector2(0f, badgeSize - valueBand),
            Size = new Vector2(badgeSize, valueBand),
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _valueLabel.AddThemeFontSizeOverride("font_size", badgeSize <= IntentOverlayLayout.CompactBadgeSize ? 8 : 9);
        _valueLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        AddChild(_valueLabel);
    }

    public void Bind(AbstractIntent intent, IReadOnlyList<Creature> targets, Creature owner) {
        _intent = intent;
        _targets = targets;
        _owner = owner;
        if (MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState()))
            TooltipText = IntentTooltip.Format(intent, targets, owner);
        else
            TooltipText = "";
        UpdateVisuals();
    }

    private void UpdateVisuals() {
        if (_intent == null || _owner == null)
            return;
        if (!MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState()))
            return;

        _animationName = _intent.GetAnimation(_targets, _owner);
        _animationFrame = null;
        _timeAccumulator = 0f;
        ApplyValueLabel(ResolveValueLabel(_intent, _targets, _owner));
        ApplyAnimationFrame(0);
    }

    private void ApplyValueLabel(string raw) {
        string text = KitLibTheme.StripFontSizeBbcode(raw);
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
        if (!MonsterIntentReader.IsOverlayCombatReady(CombatManager.Instance?.DebugOnlyGetState()))
            return "";
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
        float target = _badgeSize * 0.72f;
        float scale = target / maxDim;
        _sprite.Scale = new Vector2(scale, scale);
    }

    public override void _Process(double delta) {
        if (!_animate || _intent == null || _owner == null || _animationName == null)
            return;

        int frameCount = IntentAnimData.GetAnimationFrameCount(_animationName);
        if (frameCount <= 0)
            return;

        int frame = (int)(_timeAccumulator * IntentOverlayLayout.AnimFps) % frameCount;
        if (_animationFrame != frame) {
            _animationFrame = frame;
            ApplyAnimationFrame(frame);
        }

        _timeAccumulator += (float)delta;
    }
}
