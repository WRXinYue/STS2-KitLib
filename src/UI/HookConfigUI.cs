using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using KitLib.Hooks;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Hook rule manager — list + detail editor for Trigger → Condition → Action rules.</summary>
internal static class HookConfigUI {
    private const string RootName = "KitLibHookConfig";
    private const float PanelW = 780f;

    private static Color ColAccent => KitLibTheme.Accent;
    private static Color ColLight => KitLibTheme.TextPrimary;
    private static Color ColSubtle => KitLibTheme.Subtle;
    private static Color ColBg => KitLibTheme.ButtonBgNormal;

    // ─────────────────────────── State ───────────────────────────

    private sealed class State {
        public VBoxContainer ListBox = null!;
        public VBoxContainer DetailBox = null!;
        public HookEntry? Selected;
        public int SelectedIdx = -1;
        public NGlobalUi GlobalUi = null!;
    }

    // ─────────────────────────── Public API ───────────────────────────

    public static void Show(NGlobalUi globalUi) {
        if (MpCheatUi.IsHooksDisabledInMultiplayer)
            return;
        Remove(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 8);

        var s = new State { GlobalUi = globalUi };

        // ── Title bar ──
        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 10);

        var title = new Label {
            Text = I18N.T("hook.title", "Hook Rules"),
            VerticalAlignment = VerticalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 14);
        title.AddThemeColorOverride("font_color", ColAccent);
        titleRow.AddChild(title);
        vbox.AddChild(titleRow);

        vbox.AddChild(MakeDivider());

        // ── Body: list (left) + detail (right) ──
        var body = new HBoxContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        body.AddThemeConstantOverride("separation", 12);

        // Left: rule list
        var leftColumn = new VBoxContainer {
            CustomMinimumSize = new Vector2(260, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        leftColumn.AddThemeConstantOverride("separation", 6);

        var listScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        s.ListBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.ListBox.AddThemeConstantOverride("separation", 4);
        listScroll.AddChild(s.ListBox);
        leftColumn.AddChild(listScroll);

        var addRuleBtn = new Button {
            Text = I18N.T("hook.add", "+ New Rule"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        addRuleBtn.AddThemeFontSizeOverride("font_size", 12);
        addRuleBtn.AddThemeColorOverride("font_color", ColAccent);
        addRuleBtn.Pressed += () => CreateNewRule(s);
        leftColumn.AddChild(addRuleBtn);
        body.AddChild(leftColumn);

        // Right: detail editor
        var detailScroll = new ScrollContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        s.DetailBox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        s.DetailBox.AddThemeConstantOverride("separation", 8);
        detailScroll.AddChild(s.DetailBox);
        body.AddChild(detailScroll);

        vbox.AddChild(body);

        RebuildList(s);
        ShowEmptyDetail(s);

        ((Node)globalUi).AddChild(root);
    }

    public static void Remove(NGlobalUi globalUi)
        => ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();

    // ─────────────────────────── List ───────────────────────────

    private static void RebuildList(State s) {
        foreach (var child in s.ListBox.GetChildren())
            ((Node)child).QueueFree();

        var hooks = SettingsStore.Current.Hooks;
        if (hooks.Count == 0) {
            var empty = new Label { Text = I18N.T("hook.empty", "No rules configured.") };
            empty.AddThemeFontSizeOverride("font_size", 12);
            empty.AddThemeColorOverride("font_color", ColSubtle);
            s.ListBox.AddChild(empty);
            return;
        }

        for (int i = 0; i < hooks.Count; i++) {
            var hook = hooks[i];
            var idx = i;

            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 6);

            var trigIcon = new Label {
                Text = GetTriggerIcon(hook.Trigger),
                VerticalAlignment = VerticalAlignment.Center,
                CustomMinimumSize = new Vector2(20, 0),
            };
            trigIcon.AddThemeFontSizeOverride("font_size", 12);
            trigIcon.AddThemeColorOverride("font_color", ColAccent);
            row.AddChild(trigIcon);

            var isSelected = s.Selected == hook;
            var nameBtn = new Button {
                Text = string.IsNullOrEmpty(hook.Name) ? GetTriggerLabel(hook.Trigger) : hook.Name,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(0, 28),
            };
            nameBtn.AddThemeFontSizeOverride("font_size", 11);
            nameBtn.AddThemeColorOverride("font_color", isSelected ? ColAccent : ColLight);
            nameBtn.Pressed += () => {
                s.Selected = hook;
                s.SelectedIdx = idx;
                RebuildList(s);
                ShowDetail(s);
            };
            row.AddChild(nameBtn);

            var toggleBtn = new CheckButton {
                ButtonPressed = hook.Enabled,
                FocusMode = Control.FocusModeEnum.None,
                CustomMinimumSize = new Vector2(40, 22),
            };
            var capturedHook = hook;
            toggleBtn.Toggled += on => {
                capturedHook.Enabled = on;
                SettingsStore.Save();
            };
            row.AddChild(toggleBtn);

            s.ListBox.AddChild(row);
        }
    }

    // ─────────────────────────── Detail ───────────────────────────

    private static void ShowEmptyDetail(State s) {
        foreach (var child in s.DetailBox.GetChildren())
            ((Node)child).QueueFree();

        var hint = new Label {
            Text = I18N.T("hook.selectHint", "Select a rule or create a new one"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        hint.AddThemeFontSizeOverride("font_size", 12);
        hint.AddThemeColorOverride("font_color", ColSubtle);
        s.DetailBox.AddChild(hint);

        var addBtn = new Button {
            Text = I18N.T("hook.add", "+ New Rule"),
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 34),
        };
        addBtn.AddThemeFontSizeOverride("font_size", 12);
        addBtn.AddThemeColorOverride("font_color", ColAccent);
        addBtn.Pressed += () => CreateNewRule(s);
        s.DetailBox.AddChild(addBtn);
    }

    private static void CreateNewRule(State s) {
        var entry = new HookEntry {
            Name = I18N.T("hook.newRule", "New Rule"),
            Trigger = TriggerType.CombatStart,
            Actions = [new HookAction { Type = ActionType.ApplyPower }],
        };
        SettingsStore.Current.Hooks.Add(entry);
        SettingsStore.Save();
        s.Selected = entry;
        s.SelectedIdx = SettingsStore.Current.Hooks.Count - 1;
        RebuildList(s);
        ShowDetail(s);
    }

    private static void ShowDetail(State s) {
        foreach (var child in s.DetailBox.GetChildren())
            ((Node)child).QueueFree();

        var hook = s.Selected;
        if (hook == null) { ShowEmptyDetail(s); return; }

        // ── Name ──
        var nameRow = MakeLabeledRow(I18N.T("hook.name", "Name"));
        var nameInput = new LineEdit {
            Text = hook.Name,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28),
        };
        nameInput.TextChanged += text => {
            hook.Name = text;
            SettingsStore.Save();
            RebuildList(s);
        };
        nameRow.AddChild(nameInput);
        s.DetailBox.AddChild(nameRow);

        // ── Trigger ──
        var trigRow = MakeLabeledRow(I18N.T("hook.trigger", "Trigger"));
        var trigPicker = new OptionButton {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 28),
        };
        trigPicker.AddThemeFontSizeOverride("font_size", 12);
        var trigValues = Enum.GetValues<TriggerType>();
        for (int ti = 0; ti < trigValues.Length; ti++)
            trigPicker.AddItem(GetTriggerLabel(trigValues[ti]), ti);
        trigPicker.Selected = Array.IndexOf(trigValues, hook.Trigger);
        trigPicker.ItemSelected += idx => {
            hook.Trigger = trigValues[(int)idx];
            SettingsStore.Save();
            RebuildList(s);
        };
        trigRow.AddChild(trigPicker);
        s.DetailBox.AddChild(trigRow);

        s.DetailBox.AddChild(MakeDivider());

        // ── Conditions ──
        var condHdr = new HBoxContainer();
        var condLbl = new Label { Text = I18N.T("hook.conditions", "Conditions"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        condLbl.AddThemeFontSizeOverride("font_size", 12);
        condLbl.AddThemeColorOverride("font_color", ColAccent);
        condHdr.AddChild(condLbl);
        var addCondBtn = new Button { Text = "+", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 24) };
        addCondBtn.AddThemeFontSizeOverride("font_size", 13);
        addCondBtn.Pressed += () => {
            hook.Conditions.Add(new HookCondition { Type = ConditionType.HpBelow, Value = "50" });
            SettingsStore.Save();
            ShowDetail(s);
        };
        condHdr.AddChild(addCondBtn);
        s.DetailBox.AddChild(condHdr);

        if (hook.Conditions.Count == 0) {
            var noCondLbl = new Label { Text = I18N.T("hook.noConditions", "(Always execute)") };
            noCondLbl.AddThemeFontSizeOverride("font_size", 11);
            noCondLbl.AddThemeColorOverride("font_color", ColSubtle);
            s.DetailBox.AddChild(noCondLbl);
        }
        else {
            for (int ci = 0; ci < hook.Conditions.Count; ci++) {
                var cond = hook.Conditions[ci];
                var condIdx = ci;
                var condRow = new HBoxContainer();
                condRow.AddThemeConstantOverride("separation", 6);

                var condPicker = new OptionButton {
                    CustomMinimumSize = new Vector2(130, 26),
                };
                condPicker.AddThemeFontSizeOverride("font_size", 11);
                var condValues = Enum.GetValues<ConditionType>();
                for (int cvi = 0; cvi < condValues.Length; cvi++)
                    condPicker.AddItem(GetConditionLabel(condValues[cvi]), cvi);
                condPicker.Selected = Array.IndexOf(condValues, cond.Type);
                condPicker.ItemSelected += idx => {
                    cond.Type = condValues[(int)idx];
                    SettingsStore.Save();
                    ShowDetail(s);
                };
                condRow.AddChild(condPicker);

                var condValInput = new LineEdit {
                    Text = cond.Value,
                    PlaceholderText = GetConditionPlaceholder(cond.Type),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 26),
                };
                condValInput.TextChanged += text => {
                    cond.Value = text;
                    SettingsStore.Save();
                };
                condRow.AddChild(condValInput);

                var condDelBtn = new Button { Text = "×", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(24, 24) };
                condDelBtn.AddThemeFontSizeOverride("font_size", 13);
                condDelBtn.Pressed += () => {
                    hook.Conditions.RemoveAt(condIdx);
                    SettingsStore.Save();
                    ShowDetail(s);
                };
                condRow.AddChild(condDelBtn);

                s.DetailBox.AddChild(condRow);
            }
        }

        s.DetailBox.AddChild(MakeDivider());

        // ── Actions ──
        var actHdr = new HBoxContainer();
        var actLbl = new Label { Text = I18N.T("hook.actions", "Actions"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        actLbl.AddThemeFontSizeOverride("font_size", 12);
        actLbl.AddThemeColorOverride("font_color", ColAccent);
        actHdr.AddChild(actLbl);
        var addActBtn = new Button { Text = "+", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(28, 24) };
        addActBtn.AddThemeFontSizeOverride("font_size", 13);
        addActBtn.Pressed += () => {
            hook.Actions.Add(new HookAction { Type = ActionType.ApplyPower });
            SettingsStore.Save();
            ShowDetail(s);
        };
        actHdr.AddChild(addActBtn);
        s.DetailBox.AddChild(actHdr);

        for (int ai = 0; ai < hook.Actions.Count; ai++) {
            var action = hook.Actions[ai];
            var actIdx = ai;

            var actBox = new VBoxContainer();
            actBox.AddThemeConstantOverride("separation", 4);

            var actPanel = new PanelContainer();
            var actPanelStyle = new StyleBoxFlat {
                BgColor = ColBg,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 6,
                ContentMarginBottom = 6,
            };
            actPanel.AddThemeStyleboxOverride("panel", actPanelStyle);

            var actInner = new VBoxContainer();
            actInner.AddThemeConstantOverride("separation", 5);

            // Action type + delete
            var actTypeRow = new HBoxContainer();
            actTypeRow.AddThemeConstantOverride("separation", 6);
            var actTypePicker = new OptionButton {
                CustomMinimumSize = new Vector2(130, 26),
            };
            actTypePicker.AddThemeFontSizeOverride("font_size", 11);
            var actValues = Enum.GetValues<ActionType>();
            for (int avi = 0; avi < actValues.Length; avi++)
                actTypePicker.AddItem(GetActionLabel(actValues[avi]), avi);
            actTypePicker.Selected = Array.IndexOf(actValues, action.Type);
            actTypePicker.ItemSelected += idx => {
                action.Type = actValues[(int)idx];
                SettingsStore.Save();
                ShowDetail(s);
            };
            actTypeRow.AddChild(actTypePicker);
            actTypeRow.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

            var actDelBtn = new Button { Text = "×", FocusMode = Control.FocusModeEnum.None, CustomMinimumSize = new Vector2(24, 24) };
            actDelBtn.AddThemeFontSizeOverride("font_size", 13);
            actDelBtn.Pressed += () => {
                hook.Actions.RemoveAt(actIdx);
                SettingsStore.Save();
                ShowDetail(s);
            };
            actTypeRow.AddChild(actDelBtn);
            actInner.AddChild(actTypeRow);

            // Target ID
            if (action.Type != ActionType.SaveSlot) {
                var idRow = MakeLabeledRow(I18N.T("hook.action.targetId", "ID"));
                var idInput = new LineEdit {
                    Text = action.TargetId,
                    PlaceholderText = GetIdPlaceholder(action.Type),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                    CustomMinimumSize = new Vector2(0, 26),
                };
                idInput.TextChanged += text => {
                    action.TargetId = text;
                    SettingsStore.Save();
                };
                idRow.AddChild(idInput);

                var browseBtn = new Button {
                    Text = I18N.T("hook.action.browse", "..."),
                    FocusMode = Control.FocusModeEnum.None,
                    CustomMinimumSize = new Vector2(32, 26),
                };
                browseBtn.AddThemeFontSizeOverride("font_size", 11);
                var capturedAction = action;
                browseBtn.Pressed += () => ShowIdPicker(s, capturedAction, idInput);
                idRow.AddChild(browseBtn);

                actInner.AddChild(idRow);
            }

            // Amount (for ApplyPower)
            if (action.Type == ActionType.ApplyPower) {
                var amtRow = MakeLabeledRow(I18N.T("hook.action.amount", "Amount"));
                var amtSpin = new SpinBox {
                    MinValue = 1,
                    MaxValue = 999,
                    Value = action.Amount,
                    Step = 1,
                    CustomMinimumSize = new Vector2(80, 26),
                };
                amtSpin.ValueChanged += v => {
                    action.Amount = (int)v;
                    SettingsStore.Save();
                };
                amtRow.AddChild(amtSpin);
                actInner.AddChild(amtRow);
            }

            // Target type (Player / Enemies / Allies)
            if (action.Type == ActionType.ApplyPower || action.Type == ActionType.AddCard) {
                var tgtRow = MakeLabeledRow(I18N.T("hook.action.target", "Target"));
                var tgtPicker = new OptionButton {
                    CustomMinimumSize = new Vector2(110, 26),
                };
                tgtPicker.AddThemeFontSizeOverride("font_size", 11);
                var tgtValues = Enum.GetValues<HookTargetType>();
                for (int tvi = 0; tvi < tgtValues.Length; tvi++)
                    tgtPicker.AddItem(GetTargetLabel(tgtValues[tvi]), tvi);
                tgtPicker.Selected = Array.IndexOf(tgtValues, action.Target);
                tgtPicker.ItemSelected += idx => {
                    action.Target = tgtValues[(int)idx];
                    SettingsStore.Save();
                };
                tgtRow.AddChild(tgtPicker);
                actInner.AddChild(tgtRow);
            }

            // Slot index (for SaveSlot)
            if (action.Type == ActionType.SaveSlot) {
                var slotRow = MakeLabeledRow(I18N.T("hook.action.slot", "Slot"));
                var slotSpin = new SpinBox {
                    MinValue = 0,
                    MaxValue = 99,
                    Value = action.SlotIndex,
                    Step = 1,
                    CustomMinimumSize = new Vector2(80, 26),
                };
                slotSpin.ValueChanged += v => {
                    action.SlotIndex = (int)v;
                    SettingsStore.Save();
                };
                slotRow.AddChild(slotSpin);
                actInner.AddChild(slotRow);
            }

            actPanel.AddChild(actInner);
            actBox.AddChild(actPanel);
            s.DetailBox.AddChild(actBox);
        }

        s.DetailBox.AddChild(MakeDivider());

        // ── Delete rule ──
        var deleteBtn = new Button {
            Text = I18N.T("hook.delete", "Delete Rule"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
            FocusMode = Control.FocusModeEnum.None,
        };
        deleteBtn.AddThemeFontSizeOverride("font_size", 12);
        deleteBtn.AddThemeColorOverride("font_color", new Color(0.9f, 0.35f, 0.3f));
        deleteBtn.Pressed += () => {
            SettingsStore.Current.Hooks.Remove(hook);
            SettingsStore.Save();
            s.Selected = null;
            s.SelectedIdx = -1;
            RebuildList(s);
            ShowEmptyDetail(s);
        };
        s.DetailBox.AddChild(deleteBtn);
    }

    // ─────────────────────────── Label helpers ───────────────────────────

    private static string GetTriggerLabel(TriggerType t) => t switch {
        TriggerType.CombatStart => I18N.T("hook.trigger.combatStart", "Combat Start"),
        TriggerType.CombatEnd => I18N.T("hook.trigger.combatEnd", "Combat End"),
        TriggerType.TurnStart => I18N.T("hook.trigger.turnStart", "Turn Start"),
        TriggerType.TurnEnd => I18N.T("hook.trigger.turnEnd", "Turn End"),
        TriggerType.OnDraw => I18N.T("hook.trigger.onDraw", "On Draw"),
        TriggerType.OnDamageDealt => I18N.T("hook.trigger.onDamageDealt", "On Damage Dealt"),
        TriggerType.OnDamageTaken => I18N.T("hook.trigger.onDamageTaken", "On Damage Taken"),
        TriggerType.OnPotionUsed => I18N.T("hook.trigger.onPotionUsed", "On Potion Used"),
        TriggerType.OnCardPlayed => I18N.T("hook.trigger.onCardPlayed", "On Card Played"),
        TriggerType.OnShuffle => I18N.T("hook.trigger.onShuffle", "On Shuffle"),
        _ => "?"
    };

    private static string GetTriggerIcon(TriggerType t) => t switch {
        TriggerType.CombatStart => "⚔",
        TriggerType.CombatEnd => "🏁",
        TriggerType.TurnStart => "▶",
        TriggerType.TurnEnd => "⏸",
        TriggerType.OnDraw => "🂠",
        TriggerType.OnDamageDealt => "💥",
        TriggerType.OnDamageTaken => "🩸",
        TriggerType.OnPotionUsed => "🧪",
        TriggerType.OnCardPlayed => "🃏",
        TriggerType.OnShuffle => "🔀",
        _ => "?"
    };

    private static string GetActionLabel(ActionType t) => t switch {
        ActionType.ApplyPower => I18N.T("hook.action.applyPower", "Apply Power"),
        ActionType.AddCard => I18N.T("hook.action.addCard", "Add Card"),
        ActionType.SaveSlot => I18N.T("hook.action.saveSlot", "Save Slot"),
        ActionType.UsePotion => I18N.T("hook.action.usePotion", "Use Potion"),
        _ => "?"
    };

    private static string GetConditionLabel(ConditionType t) => t switch {
        ConditionType.None => I18N.T("hook.condition.none", "None"),
        ConditionType.HpBelow => I18N.T("hook.condition.hpBelow", "HP Below %"),
        ConditionType.HpAbove => I18N.T("hook.condition.hpAbove", "HP Above %"),
        ConditionType.FloorAbove => I18N.T("hook.condition.floorAbove", "Floor Above"),
        ConditionType.FloorBelow => I18N.T("hook.condition.floorBelow", "Floor Below"),
        ConditionType.HasPower => I18N.T("hook.condition.hasPower", "Has Power"),
        ConditionType.NotHasPower => I18N.T("hook.condition.notHasPower", "Not Has Power"),
        _ => "?"
    };

    private static string GetConditionPlaceholder(ConditionType t) => t switch {
        ConditionType.HpBelow or ConditionType.HpAbove => "50",
        ConditionType.FloorAbove or ConditionType.FloorBelow => "5",
        ConditionType.HasPower or ConditionType.NotHasPower => "PowerId",
        _ => ""
    };

    private static string GetTargetLabel(HookTargetType t) => t switch {
        HookTargetType.Player => I18N.T("hook.target.player", "Player"),
        HookTargetType.AllEnemies => I18N.T("hook.target.enemies", "All Enemies"),
        HookTargetType.Allies => I18N.T("hook.target.allies", "Allies"),
        _ => "?"
    };

    private static string GetIdPlaceholder(ActionType t) => t switch {
        ActionType.ApplyPower => "e.g. Strength",
        ActionType.AddCard => "e.g. Strike",
        ActionType.UsePotion => "e.g. FirePotion",
        _ => ""
    };

    // ─────────────────────────── ID picker popup ───────────────────────────

    private static void ShowIdPicker(State s, HookAction action, LineEdit idInput) {
        var items = GetAvailableItems(action.Type);
        if (items.Count == 0) return;

        var popup = new Window {
            Title = I18N.T("hook.action.pickId", "Select ID"),
            Size = new Vector2I(320, 420),
            Transient = true,
            Exclusive = true,
            WrapControls = true,
        };

        var vbox = new VBoxContainer();
        vbox.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var margin = new MarginContainer();
        margin.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        margin.AddThemeConstantOverride("margin_left", 8);
        margin.AddThemeConstantOverride("margin_right", 8);
        margin.AddThemeConstantOverride("margin_top", 8);
        margin.AddThemeConstantOverride("margin_bottom", 8);
        margin.AddChild(vbox);
        popup.AddChild(margin);

        var searchInput = new LineEdit {
            PlaceholderText = I18N.T("hook.action.searchId", "Search..."),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 30),
        };
        vbox.AddChild(searchInput);

        var itemList = new ItemList {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        vbox.AddChild(itemList);

        void Populate(string filter) {
            itemList.Clear();
            foreach (var (displayName, itemId) in items) {
                if (!string.IsNullOrEmpty(filter) &&
                    !displayName.Contains(filter, StringComparison.OrdinalIgnoreCase) &&
                    !itemId.Contains(filter, StringComparison.OrdinalIgnoreCase))
                    continue;
                itemList.AddItem($"{displayName}  [{itemId}]");
                itemList.SetItemMetadata(itemList.ItemCount - 1, Variant.From(itemId));
            }
        }

        Populate("");
        searchInput.TextChanged += text => Populate(text);

        itemList.ItemActivated += idx => {
            var selectedId = itemList.GetItemMetadata((int)idx).AsString();
            idInput.Text = selectedId;
            action.TargetId = selectedId;
            SettingsStore.Save();
            popup.QueueFree();
        };

        popup.CloseRequested += () => popup.QueueFree();

        ((Node)s.GlobalUi).AddChild(popup);
        popup.PopupCentered();
    }

    private static List<(string displayName, string id)> GetAvailableItems(ActionType type) {
        try {
            return type switch {
                ActionType.ApplyPower => PowerActions.GetAllPowers()
                    .Select(p => (PowerActions.GetPowerDisplayName(p), ((AbstractModel)p).Id.Entry ?? ""))
                    .Where(t => !string.IsNullOrEmpty(t.Item2))
                    .OrderBy(t => t.Item1)
                    .ToList(),
                ActionType.AddCard => ModelDb.AllCards
                    .Select(c => (CardEditActions.GetCardDisplayName(c), ((AbstractModel)c).Id.Entry ?? ""))
                    .Where(t => !string.IsNullOrEmpty(t.Item2))
                    .OrderBy(t => t.Item1)
                    .ToList(),
                ActionType.UsePotion => ModelDb.AllPotions
                    .Select(p => (p.Title?.GetFormattedText() ?? ((AbstractModel)p).Id.Entry ?? "?",
                                  ((AbstractModel)p).Id.Entry ?? ""))
                    .Where(t => !string.IsNullOrEmpty(t.Item2))
                    .OrderBy(t => t.Item1)
                    .ToList(),
                _ => []
            };
        }
        catch { return []; }
    }

    // ─────────────────────────── Widget helpers ───────────────────────────

    private static HBoxContainer MakeLabeledRow(string label) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        var lbl = new Label {
            Text = label,
            CustomMinimumSize = new Vector2(60, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        lbl.AddThemeFontSizeOverride("font_size", 11);
        lbl.AddThemeColorOverride("font_color", ColSubtle);
        row.AddChild(lbl);
        return row;
    }

    private static ColorRect MakeDivider() => new() {
        Color = KitLibTheme.Separator,
        CustomMinimumSize = new Vector2(0, 1),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };
}
