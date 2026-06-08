using System;
using System.Collections.Generic;
using KitLib.EnemyIntent;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.UI;

internal static class IntentEditorRows {
    internal static void Sync(
        VBoxContainer list,
        IReadOnlyList<MonsterIntentEntry> entries,
        bool preserveSelection) {
        var existing = new Dictionary<string, IntentEnemyEditRow>(StringComparer.Ordinal);
        foreach (var child in list.GetChildren()) {
            if (child is IntentEnemyEditRow row)
                existing[row.EnemyKey] = row;
        }

        var keepKeys = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < entries.Count; i++) {
            var entry = entries[i];
            keepKeys.Add(entry.EnemyKey);

            if (!existing.TryGetValue(entry.EnemyKey, out var row) || !GodotObject.IsInstanceValid(row)) {
                row = new IntentEnemyEditRow();
                list.AddChild(row);
                preserveSelection = false;
            }

            row.Bind(entry, preserveSelection);
            if (row.GetIndex() != i)
                list.MoveChild(row, i);
        }

        foreach (var child in list.GetChildren()) {
            if (child is IntentEnemyEditRow row && !keepKeys.Contains(row.EnemyKey))
                row.QueueFree();
        }
    }
}

internal sealed partial class IntentEnemyEditRow : VBoxContainer {
    public string EnemyKey { get; private set; } = "";

    private readonly Label _nameLabel;
    private readonly ScrollContainer _turnScroll;
    private readonly HBoxContainer _turnRow;
    private readonly Label _editHint;
    private readonly ScrollContainer _moveScroll;
    private readonly HBoxContainer _moveRow;
    private readonly Label _feedback;

    private MonsterIntentEntry? _entry;
    private IReadOnlyList<MonsterIntentEditor.MoveOption> _moves = Array.Empty<MonsterIntentEditor.MoveOption>();
    private int _selectedTurnIndex;
    private string _moveCatalogFingerprint = "";
    private string _stepsFingerprint = "";

    public IntentEnemyEditRow() {
        AddThemeConstantOverride("separation", 6);
        SizeFlagsHorizontal = SizeFlags.ExpandFill;

        _nameLabel = new Label {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            ClipText = true,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
        };
        _nameLabel.AddThemeFontSizeOverride("font_size", 11);
        _nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        AddChild(_nameLabel);

        _turnScroll = new ScrollContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, IntentOverlayLayout.BadgeSize + 22f),
        };
        _turnRow = new HBoxContainer {
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
        };
        _turnRow.AddThemeConstantOverride("separation", 6);
        _turnScroll.AddChild(_turnRow);
        AddChild(_turnScroll);

        _editHint = new Label {
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _editHint.AddThemeFontSizeOverride("font_size", 10);
        _editHint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        AddChild(_editHint);

        _moveScroll = new ScrollContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            CustomMinimumSize = new Vector2(0, 34),
        };
        _moveRow = new HBoxContainer();
        _moveRow.AddThemeConstantOverride("separation", 6);
        _moveScroll.AddChild(_moveRow);
        AddChild(_moveScroll);

        _feedback = new Label {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        _feedback.AddThemeFontSizeOverride("font_size", 10);
        _feedback.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        AddChild(_feedback);

        AddChild(new HSeparator());
    }

    public void Bind(MonsterIntentEntry entry, bool preserveSelection) {
        EnemyKey = entry.EnemyKey;
        _entry = entry;
        _nameLabel.Text = entry.DisplayName;
        _feedback.Visible = false;

        if (!preserveSelection)
            _selectedTurnIndex = 0;

        if (_selectedTurnIndex >= entry.Steps.Count)
            _selectedTurnIndex = Math.Max(0, entry.Steps.Count - 1);

        RebuildTurnRow(entry);

        if (entry.Owner.Monster is not { } monster) {
            _moves = Array.Empty<MonsterIntentEditor.MoveOption>();
            _moveCatalogFingerprint = "";
            ClearMoveRow();
            UpdateEditHint();
            return;
        }

        _moves = MonsterIntentEditor.ListMoves(monster, entry.Owner);
        string fingerprint = BuildMoveCatalogFingerprint(_moves);
        string stepsFingerprint = BuildStepsFingerprint(entry.Steps);
        if (!preserveSelection
            || fingerprint != _moveCatalogFingerprint
            || stepsFingerprint != _stepsFingerprint) {
            RebuildMoveRow();
            _moveCatalogFingerprint = fingerprint;
            _stepsFingerprint = stepsFingerprint;
        }

        UpdateEditHint();
    }

    private void RebuildTurnRow(MonsterIntentEntry entry) {
        foreach (var child in _turnRow.GetChildren())
            child.QueueFree();

        for (int i = 0; i < entry.Steps.Count; i++) {
            if (i > 0)
                _turnRow.AddChild(MakeArrow());

            int turnIndex = i;
            _turnRow.AddChild(MakeTurnChip(entry, entry.Steps[i], turnIndex));
        }
    }

    private void SelectTurn(MonsterIntentEntry entry, int turnIndex) {
        if (_selectedTurnIndex == turnIndex)
            return;

        _selectedTurnIndex = turnIndex;
        RebuildTurnRow(entry);
        RebuildMoveRow();
        UpdateEditHint();
    }

    private Control MakeTurnChip(MonsterIntentEntry entry, MonsterIntentStep step, int turnIndex) {
        bool selected = turnIndex == _selectedTurnIndex;
        bool overridden = MonsterIntentOverrides.HasOverride(entry.EnemyKey, turnIndex);

        var root = new VBoxContainer {
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
        };
        root.AddThemeConstantOverride("separation", 2);

        var turnLabel = new Label {
            Text = turnIndex == 0
                ? I18N.T("enemyIntent.edit.turnNow", "Now")
                : I18N.T("enemyIntent.edit.turnN", "+{0}", turnIndex),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        turnLabel.AddThemeFontSizeOverride("font_size", 9);
        turnLabel.AddThemeColorOverride("font_color", selected ? KitLibTheme.Accent : KitLibTheme.Subtle);
        root.AddChild(turnLabel);

        var chip = new PanelContainer {
            MouseFilter = MouseFilterEnum.Ignore,
        };
        chip.AddThemeStyleboxOverride("panel", BuildTurnChipStyle(step, selected, overridden));

        var intentsRow = new HBoxContainer {
            MouseFilter = MouseFilterEnum.Ignore,
            Alignment = BoxContainer.AlignmentMode.Center,
        };
        intentsRow.AddThemeConstantOverride("separation", 0);
        foreach (AbstractIntent intent in step.Intents) {
            var badge = new IntentOverlayBadge(IntentOverlayLayout.BadgeSize, step.IsCurrent);
            badge.MouseFilter = MouseFilterEnum.Ignore;
            badge.Bind(intent, entry.Targets, entry.Owner);
            intentsRow.AddChild(badge);
        }
        chip.AddChild(intentsRow);
        root.AddChild(chip);

        root.TooltipText = IntentTooltip.FormatStep(step, entry.Targets, entry.Owner);
        root.GuiInput += e => {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                return;
            SelectTurn(entry, turnIndex);
            root.AcceptEvent();
        };
        return root;
    }

    private static StyleBoxFlat BuildTurnChipStyle(MonsterIntentStep step, bool selected, bool overridden) {
        Color accent = KitLibTheme.Accent;
        Color border = selected
            ? accent
            : overridden
                ? new Color(accent.R, accent.G, accent.B, 0.75f)
                : step.IsCurrent
                    ? accent
                    : step.IsUncertain
                        ? KitLibTheme.Subtle
                        : KitLibTheme.PanelBorder;

        return new StyleBoxFlat {
            BgColor = selected
                ? new Color(accent.R, accent.G, accent.B, 0.28f)
                : step.IsCurrent
                    ? new Color(accent.R, accent.G, accent.B, 0.18f)
                    : new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.65f),
            BorderColor = border,
            BorderWidthLeft = selected ? 2 : 1,
            BorderWidthTop = selected ? 2 : 1,
            BorderWidthBottom = selected ? 2 : 1,
            BorderWidthRight = selected ? 2 : 1,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 2,
            ContentMarginRight = 2,
            ContentMarginTop = 2,
            ContentMarginBottom = 2,
        };
    }

    private void RebuildMoveRow() {
        ClearMoveRow();
        string? selectedMoveId = null;
        if (_entry != null && _selectedTurnIndex >= 0 && _selectedTurnIndex < _entry.Steps.Count)
            selectedMoveId = _entry.Steps[_selectedTurnIndex].MoveId;

        foreach (var move in _moves) {
            bool active = selectedMoveId != null
                && string.Equals(move.Id, selectedMoveId, StringComparison.Ordinal);
            var btn = DevPanelUI.CreateFilterChip(move.DisplayName, active: active);
            btn.AddThemeFontSizeOverride("font_size", 10);
            btn.TooltipText = MonsterIntentEditor.FormatMoveOptionLabel(move);
            string moveId = move.Id;
            btn.Pressed += () => OnMoveSelected(moveId, move.DisplayName);
            _moveRow.AddChild(btn);
        }
    }

    private void ClearMoveRow() {
        foreach (var child in _moveRow.GetChildren())
            child.QueueFree();
    }

    private void OnMoveSelected(string moveId, string displayName) {
        if (_entry == null)
            return;

        if (MonsterIntentEditor.TrySetMoveAtTurn(_entry.Owner, _selectedTurnIndex, moveId, out string? error)) {
            _feedback.Text = I18N.T(
                "enemyIntent.edit.appliedTurn",
                "Turn {0} set to {1}.",
                _selectedTurnIndex == 0
                    ? I18N.T("enemyIntent.edit.turnNow", "Now")
                    : I18N.T("enemyIntent.edit.turnN", "+{0}", _selectedTurnIndex),
                displayName);
            _feedback.AddThemeColorOverride("font_color", KitLibTheme.Accent);
            _feedback.Visible = true;
            EnemyIntentUI.RefreshAfterApply(_entry);
            return;
        }

        _feedback.Text = error ?? I18N.T("enemyIntent.edit.failed", "Failed to apply move.");
        _feedback.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
        _feedback.Visible = true;
    }

    private void UpdateEditHint() {
        if (_entry == null || _moves.Count == 0) {
            _editHint.Text = I18N.T("enemyIntent.edit.noMoves", "No editable moves for this enemy.");
            return;
        }

        string turnLabel = _selectedTurnIndex == 0
            ? I18N.T("enemyIntent.edit.turnNow", "Now")
            : I18N.T("enemyIntent.edit.turnN", "+{0}", _selectedTurnIndex);
        _editHint.Text = I18N.T(
            "enemyIntent.edit.hint",
            "Selected: turn {0}. Pick a move below to override that turn.",
            turnLabel);
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

    private static string BuildMoveCatalogFingerprint(IReadOnlyList<MonsterIntentEditor.MoveOption> moves) {
        if (moves.Count == 0)
            return "";
        var parts = new string[moves.Count];
        for (int i = 0; i < moves.Count; i++)
            parts[i] = moves[i].Id;
        return string.Join('\u001f', parts);
    }

    private static string BuildStepsFingerprint(IReadOnlyList<MonsterIntentStep> steps) {
        if (steps.Count == 0)
            return "";
        var parts = new string[steps.Count];
        for (int i = 0; i < steps.Count; i++)
            parts[i] = steps[i].MoveId;
        return string.Join('\u001f', parts);
    }
}
