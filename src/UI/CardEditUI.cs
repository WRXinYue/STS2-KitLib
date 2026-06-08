using System;
using System.Linq;
using KitLib.Actions;
using KitLib.Presets;
using Godot;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Full-screen overlay for deep card editing.</summary>
internal static class CardEditUI {
    private const string RootName = "KitLibCardEdit";

    public static void ShowForCard(NGlobalUi globalUi, CardModel card) {
        Remove(globalUi);

        var root = new Control { Name = RootName, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 1400 };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var panel = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorLeft = 1,
            AnchorRight = 1,
            AnchorTop = 0.10f,
            AnchorBottom = 0.90f,
            OffsetLeft = -520,
            OffsetRight = -16,
            OffsetTop = 0,
            OffsetBottom = 0
        };
        var style = new StyleBoxFlat {
            BgColor = new Color(0.10f, 0.10f, 0.13f, 0.96f),
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = new Color(1, 1, 1, 0.10f)
        };
        panel.AddThemeStyleboxOverride("panel", style);
        root.AddChild(panel);

        var vbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        vbox.AddThemeConstantOverride("separation", 6);
        panel.AddChild(vbox);

        var titleRow = new HBoxContainer();
        titleRow.AddThemeConstantOverride("separation", 6);
        titleRow.AddChild(new Label { Text = I18N.T("cardEdit.title", "Card Editor"), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        var closeBtn = new Button { Text = "X", CustomMinimumSize = new Vector2(28, 24) };
        closeBtn.Pressed += () => Remove(globalUi);
        titleRow.AddChild(closeBtn);
        vbox.AddChild(titleRow);

        var cardName = new Label { Text = CardEditActions.GetCardDisplayName(card), HorizontalAlignment = HorizontalAlignment.Center };
        vbox.AddChild(cardName);

        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 4);
        scroll.AddChild(content);
        vbox.AddChild(scroll);

        var statusLabel = new Label { Text = "" };
        vbox.AddChild(statusLabel);

        BuildSingleCardEditor(content, statusLabel, card);

        ((Node)globalUi).AddChild(root);
    }

    public static void Show(NGlobalUi globalUi, Player player, System.Collections.Generic.IReadOnlyList<CardModel>? sourceCards = null, CardModel? initialCard = null) {
        Remove(globalUi);

        var root = new Control { Name = RootName, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = 1300 };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        root.AddChild(DevPanelUI.CreateMainMenuModalBackdrop(globalUi, () => Remove(globalUi)));

        var panel = DevPanelUI.CreateMainMenuModalPanel(700f);
        panel.MouseFilter = Control.MouseFilterEnum.Stop;
        root.AddChild(panel);

        // Split: left = card list, right = editor
        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 8);
        hbox.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        hbox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        panel.GetNode<VBoxContainer>("Content").AddChild(hbox);

        // Left: card list
        var leftVbox = new VBoxContainer { CustomMinimumSize = new Vector2(250, 0), SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        leftVbox.AddChild(new Label { Text = I18N.T("cardEdit.title", "Card Editor"), HorizontalAlignment = HorizontalAlignment.Center });

        var search = new LineEdit { PlaceholderText = I18N.T("cardEdit.search", "Search..."), ClearButtonEnabled = true };
        leftVbox.AddChild(search);

        var cardScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var cardList = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        cardList.AddThemeConstantOverride("separation", 2);
        cardScroll.AddChild(cardList);
        leftVbox.AddChild(cardScroll);
        hbox.AddChild(leftVbox);

        // Right: editor panel
        var rightVbox = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, SizeFlagsVertical = Control.SizeFlags.ExpandFill };
        rightVbox.AddThemeConstantOverride("separation", 4);
        rightVbox.AddChild(new Label { Text = I18N.T("cardEdit.properties", "Properties"), HorizontalAlignment = HorizontalAlignment.Center });

        var editorScroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        var editorContent = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        editorContent.AddThemeConstantOverride("separation", 4);
        editorScroll.AddChild(editorContent);
        rightVbox.AddChild(editorScroll);
        hbox.AddChild(rightVbox);

        var statusLabel = new Label { Text = "" };
        rightVbox.AddChild(statusLabel);

        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 4);
        var presetPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var presetNameInput = new LineEdit { PlaceholderText = I18N.T("cardEdit.presetName", "Preset name..."), CustomMinimumSize = new Vector2(140, 0) };
        var savePresetBtn = new Button { Text = I18N.T("cardEdit.savePreset", "Save"), CustomMinimumSize = new Vector2(64, 26) };
        var applyPresetBtn = new Button { Text = I18N.T("cardEdit.applyPreset", "Apply"), CustomMinimumSize = new Vector2(64, 26) };
        var delPresetBtn = new Button { Text = I18N.T("cardEdit.deletePreset", "Delete"), CustomMinimumSize = new Vector2(64, 26) };
        presetRow.AddChild(presetPicker);
        presetRow.AddChild(presetNameInput);
        presetRow.AddChild(savePresetBtn);
        presetRow.AddChild(applyPresetBtn);
        presetRow.AddChild(delPresetBtn);
        rightVbox.AddChild(presetRow);

        CardModel? selectedCard = null;

        void RebuildPresetList() {
            presetPicker.Clear();
            var names = CardEditPresetManager.Store.All.Keys.OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var name in names) presetPicker.AddItem(name);
            applyPresetBtn.Disabled = names.Length == 0 || selectedCard == null;
            delPresetBtn.Disabled = names.Length == 0;
        }

        void ShowEditor(CardModel card) {
            selectedCard = card;
            foreach (var child in editorContent.GetChildren()) ((Node)child).QueueFree();

            editorContent.AddChild(new Label { Text = CardEditActions.GetCardDisplayName(card) });

            // Cost
            AddIntEditor(editorContent, I18N.T("cardEdit.cost", "Base Cost"), CardEditActions.GetBaseCost(card) ?? 0,
                v => { CardEditActions.TrySetBaseCost(card, v); statusLabel.Text = "Cost set."; });

            // Replay
            AddIntEditor(editorContent, I18N.T("cardEdit.replay", "Replay Count"), CardEditActions.GetReplayCount(card) ?? 0,
                v => { CardEditActions.TrySetReplayCount(card, v); statusLabel.Text = "Replay set."; });

            // Damage
            AddIntEditor(editorContent, I18N.T("cardEdit.damage", "Base Damage"), CardEditActions.GetDamage(card) ?? 0,
                v => { CardEditActions.TrySetDamage(card, v); statusLabel.Text = "Damage set."; });

            // Block
            AddIntEditor(editorContent, I18N.T("cardEdit.block", "Base Block"), CardEditActions.GetBlock(card) ?? 0,
                v => { CardEditActions.TrySetBlock(card, v); statusLabel.Text = "Block set."; });

            // Keywords
            AddBoolToggle(editorContent, I18N.T("cardEdit.exhaust", "Exhaust"), CardEditActions.GetExhaust(card) ?? false,
                v => { CardEditActions.TrySetExhaust(card, v); statusLabel.Text = "Exhaust toggled."; });
            AddBoolToggle(editorContent, I18N.T("cardEdit.ethereal", "Ethereal"), CardEditActions.GetEthereal(card) ?? false,
                v => { CardEditActions.TrySetEthereal(card, v); statusLabel.Text = "Ethereal toggled."; });
            AddBoolToggle(editorContent, I18N.T("cardEdit.unplayable", "Unplayable"), CardEditActions.GetUnplayable(card) ?? false,
                v => { CardEditActions.TrySetUnplayable(card, v); statusLabel.Text = "Unplayable toggled."; });
            AddBoolToggle(editorContent, I18N.T("cardEdit.exhaustOnNextPlay", "Exhaust On Next Play"), CardEditActions.GetExhaustOnNextPlay(card) ?? false,
                v => { CardEditActions.TrySetExhaustOnNextPlay(card, v); statusLabel.Text = "Exhaust-on-next-play toggled."; });
            AddBoolToggle(editorContent, I18N.T("cardEdit.singleTurnRetain", "Single-Turn Retain"), CardEditActions.GetSingleTurnRetain(card) ?? false,
                v => { CardEditActions.TrySetSingleTurnRetain(card, v); statusLabel.Text = "Single-turn retain toggled."; });
            AddBoolToggle(editorContent, I18N.T("cardEdit.singleTurnSly", "Single-Turn Sly"), CardEditActions.GetSingleTurnSly(card) ?? false,
                v => { CardEditActions.TrySetSingleTurnSly(card, v); statusLabel.Text = "Single-turn sly toggled."; });

            var dynamicKeys = CardEditActions.GetDynamicVarKeys(card);
            if (dynamicKeys.Count > 0) {
                editorContent.AddChild(new HSeparator());
                editorContent.AddChild(new Label { Text = I18N.T("cardEdit.dynamicVars", "Dynamic Vars") });
                foreach (var key in dynamicKeys) {
                    var displayKey = CardEditActions.GetDynamicVarDisplayName(key);
                    AddIntEditor(editorContent, displayKey, CardEditActions.GetDynamicVar(card, key) ?? 0,
                        v => { CardEditActions.TrySetDynamicVar(card, key, v); statusLabel.Text = $"{displayKey} set."; });
                }
            }

            // Enchantment
            var enchantTypes = CardEditActions.GetEnchantmentEntries();
            if (enchantTypes.Count > 0) {
                editorContent.AddChild(new HSeparator());
                string? currentEnchantType = null;
                try { currentEnchantType = card.Enchantment?.GetType().FullName; } catch { /* ignore */ }

                var enchantPicker = EnchantmentPickerUI.Build(new EnchantmentPickerUI.Options {
                    ShowClearButton = true,
                    HeaderSubtitle = CardEditActions.GetCardEnchantmentDisplayName(card),
                    InitialTypeFullName = currentEnchantType,
                    InitialAmount = CardEditActions.GetCardEnchantmentAmount(card),
                });
                editorContent.AddChild(enchantPicker.Root);

                enchantPicker.ApplyButton.Pressed += () => {
                    if (string.IsNullOrWhiteSpace(enchantPicker.SelectedTypeFullName)) {
                        statusLabel.Text = I18N.T("cardEdit.enchantPickType", "Select an enchantment type.");
                        return;
                    }
                    if (!CardEditActions.TryResolveEnchantmentType(enchantPicker.SelectedTypeFullName, out var type)) return;
                    var ok = CardEditActions.TryApplyEnchantment(
                        card, type, (int)enchantPicker.AmountSpin.Value, forceWhenIncompatible: false, out var err);
                    statusLabel.Text = ok
                        ? I18N.T("cardBrowser.enchantSet", "Enchantment set.")
                        : err;
                    if (ok)
                        enchantPicker.SetHeaderSubtitle(CardEditActions.GetCardEnchantmentDisplayName(card));
                };

                enchantPicker.ForceButton!.Pressed += () => {
                    if (string.IsNullOrWhiteSpace(enchantPicker.SelectedTypeFullName)) {
                        statusLabel.Text = I18N.T("cardEdit.enchantPickType", "Select an enchantment type.");
                        return;
                    }
                    if (!CardEditActions.TryResolveEnchantmentType(enchantPicker.SelectedTypeFullName, out var type)) return;
                    var ok = CardEditActions.TryApplyEnchantment(
                        card, type, (int)enchantPicker.AmountSpin.Value, forceWhenIncompatible: true, out var err);
                    statusLabel.Text = ok
                        ? I18N.T("cardBrowser.enchantSet", "Enchantment set.")
                        : err;
                    if (ok)
                        enchantPicker.SetHeaderSubtitle(CardEditActions.GetCardEnchantmentDisplayName(card));
                };

                enchantPicker.ClearButton!.Pressed += () => {
                    statusLabel.Text = CardEditActions.TryClearEnchantment(card, out var err)
                        ? I18N.T("cardBrowser.enchantCleared", "Enchantment cleared.")
                        : err;
                    if (statusLabel.Text == I18N.T("cardBrowser.enchantCleared", "Enchantment cleared."))
                        enchantPicker.SetHeaderSubtitle(CardEditActions.GetCardEnchantmentDisplayName(card));
                };
            }

            RebuildPresetList();
        }

        void RebuildCardList(string filter) {
            foreach (var child in cardList.GetChildren()) ((Node)child).QueueFree();
            var cards = sourceCards?.ToArray() ?? CardEditActions.GetDeckCards(player);
            var filtered = string.IsNullOrWhiteSpace(filter)
                ? cards
                : cards.Where(c => CardEditActions.GetCardDisplayName(c).Contains(filter, StringComparison.OrdinalIgnoreCase)).ToArray();

            foreach (var card in filtered) {
                var btn = new Button { Text = CardEditActions.GetCardDisplayName(card), CustomMinimumSize = new Vector2(0, 28) };
                btn.Pressed += () => ShowEditor(card);
                cardList.AddChild(btn);
            }
        }

        search.TextChanged += RebuildCardList;
        RebuildCardList("");
        if (initialCard != null)
            ShowEditor(initialCard);

        savePresetBtn.Pressed += () => {
            if (selectedCard == null) { statusLabel.Text = "Select a card first."; return; }
            var name = presetNameInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name)) { statusLabel.Text = "Enter preset name."; return; }
            var cardId = ((AbstractModel)selectedCard).Id.Entry ?? "";
            var payload = new CardEditNamedPreset { CardId = cardId, Template = CardEditActions.CaptureTemplate(selectedCard) };
            CardEditPresetManager.Store.Set(name, payload);
            statusLabel.Text = $"Preset saved: {name}";
            RebuildPresetList();
        };

        applyPresetBtn.Pressed += () => {
            if (selectedCard == null) { statusLabel.Text = "Select a card first."; return; }
            if (presetPicker.ItemCount == 0) { statusLabel.Text = "No preset."; return; }
            var name = presetPicker.GetItemText(presetPicker.Selected);
            if (!CardEditPresetManager.Store.TryGet(name, out var preset)) { statusLabel.Text = "Preset not found."; return; }
            CardEditActions.ApplyTemplate(selectedCard, preset.Template);
            statusLabel.Text = $"Preset applied: {name}";
            ShowEditor(selectedCard);
        };

        delPresetBtn.Pressed += () => {
            if (presetPicker.ItemCount == 0) { statusLabel.Text = "No preset."; return; }
            var name = presetPicker.GetItemText(presetPicker.Selected);
            if (CardEditPresetManager.Store.Delete(name)) {
                statusLabel.Text = $"Preset deleted: {name}";
                RebuildPresetList();
            }
        };

        RebuildPresetList();

        ((Node)globalUi).AddChild(root);
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    private static void BuildSingleCardEditor(VBoxContainer editorContent, Label statusLabel, CardModel card) {
        AddIntEditor(editorContent, I18N.T("cardEdit.cost", "Base Cost"), CardEditActions.GetBaseCost(card) ?? 0,
            v => { CardEditActions.TrySetBaseCost(card, v); statusLabel.Text = "Cost set."; });
        AddIntEditor(editorContent, I18N.T("cardEdit.replay", "Replay Count"), CardEditActions.GetReplayCount(card) ?? 0,
            v => { CardEditActions.TrySetReplayCount(card, v); statusLabel.Text = "Replay set."; });
        AddIntEditor(editorContent, I18N.T("cardEdit.damage", "Base Damage"), CardEditActions.GetDamage(card) ?? 0,
            v => { CardEditActions.TrySetDamage(card, v); statusLabel.Text = "Damage set."; });
        AddIntEditor(editorContent, I18N.T("cardEdit.block", "Base Block"), CardEditActions.GetBlock(card) ?? 0,
            v => { CardEditActions.TrySetBlock(card, v); statusLabel.Text = "Block set."; });

        AddBoolToggle(editorContent, I18N.T("cardEdit.exhaust", "Exhaust"), CardEditActions.GetExhaust(card) ?? false,
            v => { CardEditActions.TrySetExhaust(card, v); statusLabel.Text = "Exhaust toggled."; });
        AddBoolToggle(editorContent, I18N.T("cardEdit.ethereal", "Ethereal"), CardEditActions.GetEthereal(card) ?? false,
            v => { CardEditActions.TrySetEthereal(card, v); statusLabel.Text = "Ethereal toggled."; });
        AddBoolToggle(editorContent, I18N.T("cardEdit.unplayable", "Unplayable"), CardEditActions.GetUnplayable(card) ?? false,
            v => { CardEditActions.TrySetUnplayable(card, v); statusLabel.Text = "Unplayable toggled."; });
        AddBoolToggle(editorContent, I18N.T("cardEdit.exhaustOnNextPlay", "Exhaust On Next Play"), CardEditActions.GetExhaustOnNextPlay(card) ?? false,
            v => { CardEditActions.TrySetExhaustOnNextPlay(card, v); statusLabel.Text = "Exhaust-on-next-play toggled."; });
        AddBoolToggle(editorContent, I18N.T("cardEdit.singleTurnRetain", "Single-Turn Retain"), CardEditActions.GetSingleTurnRetain(card) ?? false,
            v => { CardEditActions.TrySetSingleTurnRetain(card, v); statusLabel.Text = "Single-turn retain toggled."; });
        AddBoolToggle(editorContent, I18N.T("cardEdit.singleTurnSly", "Single-Turn Sly"), CardEditActions.GetSingleTurnSly(card) ?? false,
            v => { CardEditActions.TrySetSingleTurnSly(card, v); statusLabel.Text = "Single-turn sly toggled."; });

        var dynamicKeys = CardEditActions.GetDynamicVarKeys(card);
        foreach (var key in dynamicKeys) {
            var displayKey = CardEditActions.GetDynamicVarDisplayName(key);
            AddIntEditor(editorContent, displayKey, CardEditActions.GetDynamicVar(card, key) ?? 0,
                v => { CardEditActions.TrySetDynamicVar(card, key, v); statusLabel.Text = $"{displayKey} set."; });
        }

    }

    private static void AddIntEditor(VBoxContainer parent, string label, int currentValue, Action<int> onApply) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(100, 0) });
        var spin = new SpinBox {
            MinValue = -999,
            MaxValue = 9999,
            Value = currentValue,
            Step = 1,
            CustomMinimumSize = new Vector2(80, 26),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        spin.ValueChanged += value => onApply((int)value);
        row.AddChild(spin);
        parent.AddChild(row);
    }

    private static void AddBoolToggle(VBoxContainer parent, string label, bool currentValue, Action<bool> onToggle) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var check = new CheckBox { Text = label, ButtonPressed = currentValue };
        check.Toggled += v => onToggle(v);
        row.AddChild(check);
        parent.AddChild(row);
    }

}
