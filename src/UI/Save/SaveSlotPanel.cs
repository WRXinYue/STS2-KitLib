using System;
using System.Collections.Generic;
using System.Linq;
using KitLib;
using KitLib.Combat;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

/// <summary>Save/load slot picker content — one layout for embedded and fullscreen (latter wraps in <see cref="SaveSlotFullscreenShell"/>).</summary>
internal sealed partial class SaveSlotPanel : Control, ISaveSlotDialogRoot {

    private HBoxContainer? _center;
    private readonly Action<int> _onConfirm;
    private readonly bool _isSaveMode;
    private readonly bool _embedded;
    private int _selectedSlot = -1;
    private Tween? _rightTween;
    private readonly Action? _onEmbeddedCancel;
    private readonly Action? _onEmbeddedAfterLoadClose;

    private VBoxContainer? _slotList;
    private ScrollContainer? _slotScroll;
    private readonly Dictionary<int, PanelContainer> _slotCards = new();

    private Control? _rightPanel;
    private VBoxContainer? _detailContainer;
    private Control? _placeholderPanel;
    private Label? _detailName;
    private Label? _detailTime;
    private Label? _detailFloor;
    private Label? _detailHp;
    private Label? _detailGold;
    private Label? _detailSeed;
    private Label? _detailCards;
    private Label? _detailRelics;
    private Label? _detailMods;
    private LineEdit? _nameInput;
    private Label? _nameLabel;
    private Button? _confirmBtn;
    private Button? _deleteBtn;

    internal SaveSlotPanel(
        bool saveMode,
        Action<int> onConfirm,
        bool embedded,
        Action? onEmbeddedCancel,
        Action? onEmbeddedAfterLoadClose) {
        _isSaveMode = saveMode;
        _onConfirm = onConfirm;
        _embedded = embedded;
        _onEmbeddedCancel = embedded ? onEmbeddedCancel : null;
        _onEmbeddedAfterLoadClose = embedded ? onEmbeddedAfterLoadClose : null;
        BuildLayout();
    }

    public override void _Ready() {
        if (SaveSlotManager.HasQuickSnapshot) {
            SelectSlot(SaveSlotManager.QuickSlotId);
            return;
        }

        var ids = SaveSlotManager.GetAllSlotIds()
            .Where(id => id != SaveSlotManager.QuickSlotId)
            .ToList();
        if (ids.Count > 0)
            SelectSlot(ids[0]);
        else if (_isSaveMode)
            SelectSlot(SaveSlotManager.QuickSlotId);
    }

    public override void _ExitTree() {
        _rightTween?.Kill();
        _rightTween = null;
        base._ExitTree();
    }

    /// <summary>Only used when embedded (fullscreen root is <see cref="SaveSlotFullscreenShell"/>).</summary>
    public void HideFromFacade() {
        if (!_embedded) return;
        _onEmbeddedCancel?.Invoke();
    }

    private void BuildLayout() {
        MouseFilter = Control.MouseFilterEnum.Stop;
        if (_embedded) {
            // Parent `SaveLoadSlotHost` is a plain Control — size flags are ignored there; anchors
            // are required so the picker fills the extension column (otherwise ~minimum height strip).
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
            SizeFlagsVertical = Control.SizeFlags.ExpandFill;
            SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        }

        _center = new HBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        _center.AddThemeConstantOverride("separation", 8);

        _center.AddChild(BuildLeftPanel());
        _rightPanel = BuildRightPanel();
        _center.AddChild(_rightPanel);

        AddChild(_center);
        _center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
    }

    // ──────── Animations ────────

    private void AnimateDetailTransition(Action updateContent) {
        if (_rightPanel == null || !GodotObject.IsInstanceValid(_rightPanel)) {
            updateContent();
            return;
        }

        _rightTween?.Kill();
        _rightTween = _rightPanel.CreateTween();

        _rightTween.TweenProperty(_rightPanel, "modulate", new Color(1, 1, 1, 0f), 0.07f)
                   .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        _rightTween.TweenCallback(Callable.From(updateContent));
        _rightTween.TweenProperty(_rightPanel, "modulate", Colors.White, 0.10f)
                   .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.Out);
    }

    private void AnimateConfirmButton() {
        if (_confirmBtn == null || !GodotObject.IsInstanceValid(_confirmBtn)) return;

        var tween = _confirmBtn.CreateTween().SetParallel();
        tween.TweenProperty(_confirmBtn, "scale", new Vector2(0.88f, 0.88f), 0.06f)
             .SetTrans(Tween.TransitionType.Quad).SetEase(Tween.EaseType.In);
        tween.Chain().SetParallel();
        tween.TweenProperty(_confirmBtn, "scale", Vector2.One, 0.14f)
             .SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    // ══════════════════════════════════════════════════════════════
    //  LEFT PANEL — slot card list
    // ══════════════════════════════════════════════════════════════

    private PanelContainer BuildLeftPanel() {
        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", MakePanelFlat(10, 0, 0, 10));

        var outerVbox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        outerVbox.AddThemeConstantOverride("separation", 8);

        // Header row: title + mode badge
        var headerRow = new HBoxContainer();
        headerRow.AddThemeConstantOverride("separation", 8);

        var title = new Label {
            Text = _isSaveMode
                ? I18N.T("snapshot.titleSave", "SAVE")
                : I18N.T("snapshot.titleLoad", "LOAD"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        title.AddThemeFontSizeOverride("font_size", 20);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        headerRow.AddChild(title);
        outerVbox.AddChild(headerRow);

        outerVbox.AddChild(MakeThinSep());

        // Scrollable slot list
        _slotScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        _slotList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        _slotList.AddThemeConstantOverride("separation", 6);

        RebuildSlotList();

        _slotScroll.AddChild(_slotList);
        outerVbox.AddChild(_slotScroll);

        // "+ New Slot" button (save mode only shows this, load mode also shows it dimmed)
        var addBtn = new Button {
            Text = I18N.T("snapshot.newSlot", "+ New Slot"),
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = Control.FocusModeEnum.None,
        };
        addBtn.AddThemeFontSizeOverride("font_size", 13);
        addBtn.Pressed += OnAddSlotPressed;
        if (!_isSaveMode) addBtn.Disabled = true;
        ApplySecondaryButton(addBtn);
        outerVbox.AddChild(addBtn);

        // Cancel button
        var cancelBtn = new Button {
            Text = I18N.T("snapshot.cancel", "Cancel"),
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = Control.FocusModeEnum.None,
        };
        cancelBtn.AddThemeFontSizeOverride("font_size", 13);
        cancelBtn.Pressed += OnCancelPressed;
        ApplySecondaryButton(cancelBtn);
        outerVbox.AddChild(cancelBtn);

        panel.AddChild(outerVbox);
        return panel;
    }

    /// <summary>Rebuilds all slot cards in the left-panel list from disk.</summary>
    private void RebuildSlotList() {
        if (_slotList == null) return;

        foreach (var child in _slotList.GetChildren())
            ((Node)child).QueueFree();
        _slotCards.Clear();

        var quickCard = BuildQuickSlotCard();
        _slotList.AddChild(quickCard);
        _slotCards[SaveSlotManager.QuickSlotId] = quickCard;

        _slotList.AddChild(BuildCombatSessionSection());

        var ids = SaveSlotManager.GetAllSlotIds()
            .Where(id => id != SaveSlotManager.QuickSlotId)
            .ToList();

        if (ids.Count == 0 && _isSaveMode)
            ids.Add(SaveSlotManager.NextSlotId());

        foreach (var id in ids) {
            var card = BuildSlotCard(id);
            _slotList.AddChild(card);
            _slotCards[id] = card;
        }
    }

    private PanelContainer BuildQuickSlotCard() {
        int slotId = SaveSlotManager.QuickSlotId;
        var meta = SaveSlotManager.LoadMeta(slotId);
        bool empty = meta == null;

        var card = new PanelContainer {
            CustomMinimumSize = new Vector2(228, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        var normalStyle = MakeSlotCardStyle(false);
        var hoverStyle = MakeSlotCardStyle(false, hover: true);
        card.AddThemeStyleboxOverride("panel", normalStyle);

        card.MouseEntered += () => {
            if (_selectedSlot != slotId)
                card.AddThemeStyleboxOverride("panel", hoverStyle);
        };
        card.MouseExited += () => {
            if (_selectedSlot != slotId)
                card.AddThemeStyleboxOverride("panel", normalStyle);
        };

        card.GuiInput += evt => {
            if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                SelectSlot(slotId);
            if (evt is InputEventMouseButton { DoubleClick: true, ButtonIndex: MouseButton.Left }) {
                SelectSlot(slotId);
                if (_confirmBtn is not { Disabled: true })
                    OnConfirmPressed();
            }
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);

        var topRow = new HBoxContainer();
        var nameLabel = new Label {
            Text = SaveSlotManager.QuickSlotDisplayName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 14);
        nameLabel.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        topRow.AddChild(nameLabel);

        if (!empty) {
            var timeLabel = new Label {
                Text = meta!.FormattedTime,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            timeLabel.AddThemeFontSizeOverride("font_size", 11);
            timeLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            topRow.AddChild(timeLabel);
        }
        vbox.AddChild(topRow);

        var hintLabel = new Label { Text = FormatQuickHint() };
        hintLabel.AddThemeFontSizeOverride("font_size", 11);
        hintLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(hintLabel);

        if (empty) {
            var emptyLabel = new Label {
                Text = I18N.T("snapshot.empty", "(empty)"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 12);
            emptyLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            vbox.AddChild(emptyLabel);
        }
        else {
            var statsRow = new HBoxContainer();
            statsRow.AddThemeConstantOverride("separation", 12);

            var floorLabel = new Label {
                Text = I18N.T("snapshot.floorShort", "F{0}", meta!.TotalFloor),
            };
            floorLabel.AddThemeFontSizeOverride("font_size", 12);
            floorLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            statsRow.AddChild(floorLabel);

            var hpLabel = new Label {
                Text = I18N.T("snapshot.hpShort", "{0}/{1}", meta.Hp, meta.MaxHp),
            };
            hpLabel.AddThemeFontSizeOverride("font_size", 12);
            hpLabel.AddThemeColorOverride("font_color", HpColor(meta.Hp, meta.MaxHp));
            statsRow.AddChild(hpLabel);

            var goldLabel = new Label {
                Text = I18N.T("snapshot.goldShort", "{0}g", meta.Gold),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            goldLabel.AddThemeFontSizeOverride("font_size", 12);
            goldLabel.AddThemeColorOverride("font_color", KitLibTheme.RarityRare);
            statsRow.AddChild(goldLabel);
            vbox.AddChild(statsRow);

            vbox.AddChild(BuildMiniHpBar(meta.Hp, meta.MaxHp));
        }

        card.AddChild(vbox);
        return card;
    }

    private static string FormatQuickHint() {
        var saveKey = SettingsStore.GetHotkeyBinding(HotkeyActionId.QuickSave).FormatLabel();
        var loadKey = SettingsStore.GetHotkeyBinding(HotkeyActionId.QuickLoad).FormatLabel();
        return I18N.T("snapshot.quickHint", "{0} save · {1} load", saveKey, loadKey);
    }

    private Control BuildCombatSessionSection() {
        var section = new VBoxContainer();
        section.AddThemeConstantOverride("separation", 4);

        var title = new Label {
            Text = I18N.T("snapshot.combatSessionTitle", "This combat"),
        };
        title.AddThemeFontSizeOverride("font_size", 13);
        title.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        section.AddChild(title);

        var hint = new Label { Text = FormatCombatSessionHint() };
        hint.AddThemeFontSizeOverride("font_size", 11);
        hint.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        section.AddChild(hint);

        var nodes = CombatCheckpointStore.GetNodes()
            .OrderByDescending(n => n.SaveTime)
            .ToList();

        if (nodes.Count == 0) {
            var empty = new Label {
                Text = I18N.T("snapshot.combatSessionEmpty", "(none yet)"),
            };
            empty.AddThemeFontSizeOverride("font_size", 12);
            empty.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            section.AddChild(empty);
            return section;
        }

        foreach (var node in nodes)
            section.AddChild(BuildCombatNodeRow(node));

        return section;
    }

    private static string FormatCombatSessionHint() {
        var combatKey = SettingsStore.GetHotkeyBinding(HotkeyActionId.QuickReplayCombat).FormatLabel();
        var turnKey = SettingsStore.GetHotkeyBinding(HotkeyActionId.QuickReplayTurn).FormatLabel();
        return I18N.T(
            "snapshot.combatSessionHint",
            "{0} replay combat · {1} replay turn",
            combatKey, turnKey);
    }

    private Control BuildCombatNodeRow(CombatCheckpointNode node) {
        var row = new PanelContainer {
            CustomMinimumSize = new Vector2(228, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        row.AddThemeStyleboxOverride("panel", MakeSlotCardStyle(false));

        if (!_isSaveMode) {
            var hoverStyle = MakeSlotCardStyle(false, hover: true);
            row.MouseEntered += () => row.AddThemeStyleboxOverride("panel", hoverStyle);
            row.MouseExited += () => row.AddThemeStyleboxOverride("panel", MakeSlotCardStyle(false));
            row.GuiInput += evt => {
                if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                    OnCombatNodePressed(node.Id);
            };
        }

        var inner = new HBoxContainer();
        inner.AddThemeConstantOverride("separation", 8);

        var label = new Label {
            Text = node.Label,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipText = true,
        };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        inner.AddChild(label);

        var time = new Label {
            Text = CombatCheckpointStore.FormatNodeTime(node.SaveTime),
            HorizontalAlignment = HorizontalAlignment.Right,
        };
        time.AddThemeFontSizeOverride("font_size", 11);
        time.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        inner.AddChild(time);

        row.AddChild(inner);
        return row;
    }

    private void OnCombatNodePressed(string nodeId) {
        if (_isSaveMode)
            return;
        if (!CombatCheckpointStore.TryLoadNodeById(nodeId))
            return;

        if (_embedded) {
            if (_onEmbeddedAfterLoadClose != null)
                Callable.From(_onEmbeddedAfterLoadClose).CallDeferred();
        }
        else {
            SaveSlotUI.Hide();
        }
    }

    private PanelContainer BuildSlotCard(int slotId) {
        var meta = SaveSlotManager.LoadMeta(slotId);
        bool empty = meta == null;

        var card = new PanelContainer {
            CustomMinimumSize = new Vector2(228, 0),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };

        var normalStyle = MakeSlotCardStyle(false);
        var hoverStyle = MakeSlotCardStyle(false, hover: true);
        card.AddThemeStyleboxOverride("panel", normalStyle);

        // Hover effects
        card.MouseEntered += () => {
            if (_selectedSlot != slotId)
                card.AddThemeStyleboxOverride("panel", hoverStyle);
        };
        card.MouseExited += () => {
            if (_selectedSlot != slotId)
                card.AddThemeStyleboxOverride("panel", normalStyle);
        };

        // Click to select
        card.GuiInput += evt => {
            if (evt is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) {
                SelectSlot(slotId);
            }
            if (evt is InputEventMouseButton { DoubleClick: true, ButtonIndex: MouseButton.Left }) {
                SelectSlot(slotId);
                if (_confirmBtn is not { Disabled: true })
                    OnConfirmPressed();
            }
        };

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);

        if (empty) {
            // Empty slot card
            var margin = new MarginContainer();
            margin.AddThemeConstantOverride("margin_top", 8);
            margin.AddThemeConstantOverride("margin_bottom", 8);

            var emptyLabel = new Label {
                Text = I18N.T("snapshot.empty", "(empty)"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            emptyLabel.AddThemeFontSizeOverride("font_size", 13);
            emptyLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            margin.AddChild(emptyLabel);
            vbox.AddChild(margin);
        }
        else {
            // Top row: name + time
            var topRow = new HBoxContainer();
            var nameLabel = new Label {
                Text = meta!.DisplayName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
            };
            nameLabel.AddThemeFontSizeOverride("font_size", 14);
            nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            topRow.AddChild(nameLabel);

            var timeLabel = new Label {
                Text = meta.FormattedTime,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            timeLabel.AddThemeFontSizeOverride("font_size", 11);
            timeLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            topRow.AddChild(timeLabel);
            vbox.AddChild(topRow);

            // Stats row: Floor / HP
            var statsRow = new HBoxContainer();
            statsRow.AddThemeConstantOverride("separation", 12);

            var floorLabel = new Label {
                Text = I18N.T("snapshot.floorShort", "F{0}", meta.TotalFloor),
            };
            floorLabel.AddThemeFontSizeOverride("font_size", 12);
            floorLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
            statsRow.AddChild(floorLabel);

            var hpLabel = new Label {
                Text = I18N.T("snapshot.hpShort", "{0}/{1}", meta.Hp, meta.MaxHp),
            };
            hpLabel.AddThemeFontSizeOverride("font_size", 12);
            hpLabel.AddThemeColorOverride("font_color", HpColor(meta.Hp, meta.MaxHp));
            statsRow.AddChild(hpLabel);

            var goldLabel = new Label {
                Text = I18N.T("snapshot.goldShort", "{0}g", meta.Gold),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                HorizontalAlignment = HorizontalAlignment.Right,
            };
            goldLabel.AddThemeFontSizeOverride("font_size", 12);
            goldLabel.AddThemeColorOverride("font_color", KitLibTheme.RarityRare);
            statsRow.AddChild(goldLabel);
            vbox.AddChild(statsRow);

            // HP bar
            var hpBar = BuildMiniHpBar(meta.Hp, meta.MaxHp);
            vbox.AddChild(hpBar);
        }

        card.AddChild(vbox);
        return card;
    }

    // ══════════════════════════════════════════════════════════════
    //  RIGHT PANEL — detail view
    // ══════════════════════════════════════════════════════════════

    private PanelContainer BuildRightPanel() {
        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        panel.AddThemeStyleboxOverride("panel", MakePanelFlat(0, 10, 10, 0));

        var outerVbox = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        outerVbox.AddThemeConstantOverride("separation", 0);

        // ── Detail container (all content, toggled as a single unit) ──
        _detailContainer = new VBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        _detailContainer.AddThemeConstantOverride("separation", 8);

        // Fixed header
        var headerRow = new HBoxContainer();
        _detailName = new Label { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, ClipText = true };
        _detailName.AddThemeFontSizeOverride("font_size", 20);
        _detailName.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        headerRow.AddChild(_detailName);

        _detailTime = new Label { HorizontalAlignment = HorizontalAlignment.Right };
        _detailTime.AddThemeFontSizeOverride("font_size", 13);
        _detailTime.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        headerRow.AddChild(_detailTime);
        _detailContainer.AddChild(headerRow);

        _detailContainer.AddChild(MakeThinSep());

        // Stats badges row
        var badgeRow = new HBoxContainer();
        badgeRow.AddThemeConstantOverride("separation", 10);

        _detailFloor = MakeBadgeLabel();
        _detailHp = MakeBadgeLabel();
        _detailGold = MakeBadgeLabel();
        badgeRow.AddChild(MakeBadge(_detailFloor, BadgeTint(KitLibTheme.Accent, 0.20f)));
        badgeRow.AddChild(MakeBadge(_detailHp, BadgeTint(KitLibTheme.RarityCurse, 0.18f)));
        badgeRow.AddChild(MakeBadge(_detailGold, BadgeTint(KitLibTheme.RarityRare, 0.22f)));
        _detailContainer.AddChild(badgeRow);

        // Seed
        _detailSeed = MutedLineLabel(12);
        _detailContainer.AddChild(_detailSeed);

        _detailContainer.AddChild(MakeThinSep());

        // Scrollable detail body
        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        var body = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 6);

        body.AddChild(SectionHeader(I18N.T("snapshot.cards", "Cards")));
        _detailCards = DetailWrapLabel(12, KitLibTheme.TextPrimary);
        body.AddChild(_detailCards);

        body.AddChild(SectionHeader(I18N.T("snapshot.relics", "Relics")));
        _detailRelics = DetailWrapLabel(12, KitLibTheme.TextPrimary);
        body.AddChild(_detailRelics);

        body.AddChild(SectionHeader(I18N.T("snapshot.mods", "Mods")));
        _detailMods = DetailWrapLabel(11, KitLibTheme.Subtle);
        body.AddChild(_detailMods);

        scroll.AddChild(body);
        _detailContainer.AddChild(scroll);

        // Fixed footer
        _detailContainer.AddChild(MakeThinSep());

        var footerRow = new HBoxContainer();
        footerRow.AddThemeConstantOverride("separation", 8);

        _nameLabel = new Label { Text = I18N.T("snapshot.nameLabel", "Name:") };
        _nameLabel.AddThemeFontSizeOverride("font_size", 14);
        _nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        footerRow.AddChild(_nameLabel);

        _nameInput = new LineEdit {
            PlaceholderText = I18N.T("snapshot.namePlaceholder", "optional name"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        _nameInput.AddThemeFontSizeOverride("font_size", 14);
        ApplyThemedLineEdit(_nameInput);
        footerRow.AddChild(_nameInput);

        _confirmBtn = new Button {
            Text = _isSaveMode ? I18N.T("snapshot.confirmSave", "Save") : I18N.T("snapshot.confirmLoad", "Load"),
            CustomMinimumSize = new Vector2(80, 0),
            PivotOffset = new Vector2(40, 20),
            FocusMode = Control.FocusModeEnum.None,
        };
        _confirmBtn.AddThemeFontSizeOverride("font_size", 15);
        _confirmBtn.Pressed += OnConfirmPressed;
        ApplyPrimaryButton(_confirmBtn);
        footerRow.AddChild(_confirmBtn);

        _deleteBtn = new Button {
            Text = I18N.T("snapshot.delete", "Delete"),
            CustomMinimumSize = new Vector2(70, 0),
            FocusMode = Control.FocusModeEnum.None,
        };
        _deleteBtn.AddThemeFontSizeOverride("font_size", 13);
        _deleteBtn.Pressed += OnDeletePressed;
        ApplyDangerButton(_deleteBtn);
        footerRow.AddChild(_deleteBtn);

        _detailContainer.AddChild(footerRow);

        outerVbox.AddChild(_detailContainer);

        // ── Placeholder (shown when no slot selected) ──
        _placeholderPanel = new CenterContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        var placeholderLabel = new Label {
            Text = I18N.T("snapshot.noSelection", "Select a save slot"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        placeholderLabel.AddThemeFontSizeOverride("font_size", 16);
        placeholderLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        _placeholderPanel.AddChild(placeholderLabel);
        outerVbox.AddChild(_placeholderPanel);

        // Start with detail hidden, placeholder visible
        SetDetailVisible(false);

        panel.AddChild(outerVbox);
        return panel;
    }

    /// <summary>Toggle between detail view and placeholder. No tree navigation needed.</summary>
    private void SetDetailVisible(bool visible) {
        if (_detailContainer != null) _detailContainer.Visible = visible;
        if (_placeholderPanel != null) _placeholderPanel.Visible = !visible;
    }

    // ──────── Interaction ────────

    private void SelectSlot(int slotId) {
        bool sameSlot = _selectedSlot == slotId;
        _selectedSlot = slotId;

        // Update card styles
        foreach (var (id, card) in _slotCards) {
            if (!GodotObject.IsInstanceValid(card)) continue;
            card.AddThemeStyleboxOverride("panel", MakeSlotCardStyle(id == slotId));
        }

        SetDetailVisible(true);
        if (!sameSlot)
            AnimateDetailTransition(() => RefreshDetail(slotId));
    }

    private void RefreshDetail(int slotId) {
        var meta = SaveSlotManager.LoadMeta(slotId);
        bool empty = meta == null;

        if (_nameLabel != null) _nameLabel.Visible = _isSaveMode;
        if (_nameInput != null) _nameInput.Visible = _isSaveMode;
        if (_confirmBtn != null)
            _confirmBtn.Disabled = !_isSaveMode && empty;
        if (_deleteBtn != null) {
            _deleteBtn.Visible = !empty;
            _deleteBtn.Text = I18N.T("snapshot.delete", "Delete");
        }

        if (empty) {
            var title = slotId == SaveSlotManager.QuickSlotId
                ? SaveSlotManager.QuickSlotDisplayName
                : I18N.T("snapshot.emptySlot", "Empty Save");
            SetDetail(
                title,
                "",
                I18N.T("snapshot.floorDash", "Floor —"),
                I18N.T("snapshot.hpDash", "HP —"),
                I18N.T("snapshot.goldDash", "Gold —"),
                "", "", "", "");
            if (_nameInput != null) {
                _nameInput.Text = slotId == SaveSlotManager.QuickSlotId && _isSaveMode
                    ? SaveSlotManager.QuickSlotDisplayName
                    : "";
            }
            return;
        }

        var seedText = string.IsNullOrEmpty(meta!.Seed)
            ? ""
            : I18N.T("snapshot.seed", "Seed: {0}", meta.Seed);
        var modsText = meta.ModList.Count > 0
            ? string.Join("\n", meta.ModList)
            : I18N.T("snapshot.modsNone", "(none)");

        SetDetail(
            meta.DisplayName,
            meta.FormattedTime,
            I18N.T("snapshot.floor", "Floor {0}", meta.TotalFloor),
            I18N.T("snapshot.hp", "HP  {0} / {1}", meta.Hp, meta.MaxHp),
            I18N.T("snapshot.gold", "Gold  {0}", meta.Gold),
            seedText,
            modsText,
            string.Join("  ", meta.CardTitles),
            string.Join("  ", meta.RelicTitles)
        );

        if (_nameInput != null)
            _nameInput.Text = meta.Name;
    }

    private void SetDetail(string name, string time, string floor, string hp, string gold,
        string seed, string mods, string cards, string relics) {
        if (_detailName != null) _detailName.Text = name;
        if (_detailTime != null) _detailTime.Text = time;
        if (_detailFloor != null) _detailFloor.Text = floor;
        if (_detailHp != null) _detailHp.Text = hp;
        if (_detailGold != null) _detailGold.Text = gold;
        if (_detailSeed != null) _detailSeed.Text = seed;
        if (_detailMods != null) _detailMods.Text = mods;
        if (_detailCards != null) _detailCards.Text = cards;
        if (_detailRelics != null) _detailRelics.Text = relics;
    }

    private void OnCancelPressed() {
        if (_embedded) {
            _onEmbeddedCancel?.Invoke();
            return;
        }
        SaveSlotUI.Hide();
    }

    private void OnConfirmPressed() {
        if (_selectedSlot < 0) return;

        AnimateConfirmButton();

        if (_isSaveMode)
            SaveSlotManager.RenameSlot(_selectedSlot, _nameInput?.Text ?? "");

        _onConfirm.Invoke(_selectedSlot);

        if (_isSaveMode) {
            // Refresh both the card and detail after saving
            RebuildSlotList();
            HighlightSlotCard(_selectedSlot);
            AnimateDetailTransition(() => RefreshDetail(_selectedSlot));
        }
        else {
            if (_embedded) {
                if (_onEmbeddedAfterLoadClose != null)
                    Callable.From(_onEmbeddedAfterLoadClose).CallDeferred();
            }
            else {
                SaveSlotUI.Hide();
            }
        }
    }

    private void OnDeletePressed() {
        if (_selectedSlot < 0) return;
        if (_deleteBtn == null) return;

        // Two-click confirmation: first click changes text, second actually deletes
        if (_deleteBtn.Text == I18N.T("snapshot.deleteConfirm", "Confirm Delete")) {
            SaveSlotManager.DeleteSlot(_selectedSlot);
            _selectedSlot = -1;

            RebuildSlotList();
            SetDetailVisible(false);

            // Auto-select first remaining slot
            var ids = SaveSlotManager.GetAllSlotIds();
            if (ids.Count > 0)
                SelectSlot(ids[0]);
        }
        else {
            _deleteBtn.Text = I18N.T("snapshot.deleteConfirm", "Confirm Delete");
        }
    }

    private void OnAddSlotPressed() {
        int newId = SaveSlotManager.NextSlotId();
        RebuildSlotList();

        // Ensure the new empty slot card appears
        if (!_slotCards.ContainsKey(newId)) {
            var card = BuildSlotCard(newId);
            _slotList?.AddChild(card);
            _slotCards[newId] = card;
        }

        SelectSlot(newId);

        // Scroll to bottom to show the new card
        if (_slotScroll != null)
            _slotScroll.CallDeferred("set_v_scroll", (int)_slotScroll.GetVScrollBar().MaxValue);
    }

    /// <summary>Re-apply the selected style to the given slot's card after a rebuild.</summary>
    private void HighlightSlotCard(int slotId) {
        foreach (var (id, card) in _slotCards) {
            if (!GodotObject.IsInstanceValid(card)) continue;
            card.AddThemeStyleboxOverride("panel", MakeSlotCardStyle(id == slotId));
        }
    }
}
