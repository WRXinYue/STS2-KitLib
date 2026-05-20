using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DevMode.Actions;
using DevMode.Hooks;
using DevMode.Multiplayer.Cheat;
using DevMode.Presets;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.UI;

/// <summary>
/// Builds the right-side detail / action panel inside the card browser.
/// Stateless: every method receives what it needs via parameters.
/// </summary>
internal static class CardBrowserRightPanel {
    private static Color ColSubtle => DevModeTheme.Subtle;

    /// <summary>Base cost to apply on <see cref="CardActions.AddCardBuilder.BaseCost"/> (canonical library cards are not mutable in-place).</summary>
    private sealed class LibraryAddCostStaging {
        public int BaseCost { get; set; }
    }

    internal static void Build(VBoxContainer container, Label statusLabel,
        CardModel card, RunState state, Player player, NGlobalUi globalUi, Action onGridRefresh,
        bool isLibrary, CardTarget? browseTarget, bool libraryUpgradeDetailPreview = false) {
        var displayCard = ResolveLibraryDisplayCard(card, isLibrary, libraryUpgradeDetailPreview);
        LibraryAddCostStaging? libraryAddCost = null;
        if (isLibrary)
            libraryAddCost = new LibraryAddCostStaging { BaseCost = CardEditActions.GetBaseCost(card) ?? 0 };

        var cardName = CardEditActions.GetCardDisplayName(displayCard);

        var headerLabel = new Label {
            Text = cardName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        headerLabel.AddThemeFontSizeOverride("font_size", 16);
        container.AddChild(headerLabel);

        var cardIdStr = ((AbstractModel)card).Id.Entry;
        if (!string.IsNullOrEmpty(cardIdStr))
            container.AddChild(DevModeTheme.CreateCopyableIdRow(cardIdStr,
                msg => statusLabel.Text = msg));

        var infoLines = new List<string>();
        var typeName = CardBrowserUI.GetLocalizedTypeName(displayCard);
        var rarityName = CardBrowserUI.GetLocalizedRarityName(displayCard);
        if (!string.IsNullOrEmpty(typeName)) infoLines.Add(typeName);
        if (!string.IsNullOrEmpty(rarityName)) infoLines.Add(rarityName);
        var costVal = CardEditActions.GetBaseCost(displayCard);
        if (costVal.HasValue)
            infoLines.Add(string.Format(I18N.T("cardBrowser.cost", "Cost: {0}"), costVal.Value));

        if (infoLines.Count > 0) {
            var infoLabel = new Label {
                Text = string.Join("  ·  ", infoLines),
                HorizontalAlignment = HorizontalAlignment.Center
            };
            infoLabel.AddThemeFontSizeOverride("font_size", 11);
            infoLabel.AddThemeColorOverride("font_color", ColSubtle);
            container.AddChild(infoLabel);
        }

        string desc;
        if (ReferenceEquals(displayCard, card) == false) {
            try { desc = displayCard.GetDescriptionForUpgradePreview(); }
            catch { desc = CardEditActions.GetDescriptionText(displayCard); }
        }
        else {
            try { desc = card.GetDescriptionForPile(PileType.None); }
            catch { desc = CardEditActions.GetDescriptionText(card); }
        }
        if (!string.IsNullOrWhiteSpace(desc)) {
            var descLabel = DevModeTheme.CreateGameBbcodeLabel();
            descLabel.Text = DevModeTheme.ConvertGameBbcode(desc);
            descLabel.AddThemeFontSizeOverride("normal_font_size", 12);
            descLabel.AddThemeColorOverride("default_color", DevModeTheme.TextSecondary);
            container.AddChild(descLabel);
        }

        container.AddChild(new HSeparator());

        if (isLibrary) {
            BuildAddSection(container, statusLabel, card, displayCard, state, player, libraryUpgradeDetailPreview,
                libraryAddCost!);
        }
        else {
            BuildOwnedCardActions(container, statusLabel, card, state, player, globalUi, onGridRefresh, browseTarget);
        }

        container.AddChild(new HSeparator());
        // Inline editors apply to the pile instance, or stage values for Add when browsing the library.
        BuildEditSection(container, statusLabel, card, libraryAddCost);
    }

    /// <summary>When browsing the card library with "view upgrades" on, match grid + NCard: clone and UpgradeInternal for read-only UI.</summary>
    private static CardModel ResolveLibraryDisplayCard(CardModel card, bool isLibrary, bool libraryUpgradeDetailPreview) {
        if (!isLibrary || !libraryUpgradeDetailPreview)
            return card;
        try {
            if (!card.IsUpgradable)
                return card;
            var upgraded = (CardModel)card.MutableClone();
            upgraded.UpgradeInternal();
            return upgraded;
        }
        catch {
            return card;
        }
    }

    // ── Add section (for library source) ──

    private static void BuildAddSection(VBoxContainer container, Label statusLabel,
        CardModel card, CardModel displayCard, RunState state, Player player, bool libraryUpgradeDetailPreview,
        LibraryAddCostStaging addCostStaging) {
        var upgradeLevelsToApply = 0;
        if (libraryUpgradeDetailPreview && !ReferenceEquals(displayCard, card)) {
            try {
                upgradeLevelsToApply = displayCard.CurrentUpgradeLevel - card.CurrentUpgradeLevel;
                if (upgradeLevelsToApply < 0) upgradeLevelsToApply = 0;
            }
            catch { /* keep 0 */ }
        }

        var nameAfterAdd = upgradeLevelsToApply > 0
            ? CardEditActions.GetCardDisplayName(displayCard)
            : CardEditActions.GetCardDisplayName(card);

        // Co-op host: default RunContext player is local; allow adding to any peer's piles/deck.
        var addTargetPlayer = player;
        if (MpCheatSession.InMultiplayerRun && MpCheatSession.IsHost && state.Players.Count > 1) {
            var players = state.Players.ToList();
            var playerRow = new HBoxContainer();
            playerRow.AddThemeConstantOverride("separation", 4);
            var playerLbl = new Label {
                Text = I18N.T("mpcheat.cardAdd.targetPlayer", "Player"),
            };
            playerLbl.AddThemeFontSizeOverride("font_size", 12);
            playerRow.AddChild(playerLbl);
            var playerPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
            var defaultIdx = 0;
            var firstRemoteIdx = -1;
            for (var i = 0; i < players.Count; i++) {
                var p = players[i];
                playerPicker.AddItem(MpCheatPlayerLabels.FormatPickerLabel(p), i);
                if (p.NetId == localNetId)
                    defaultIdx = i;
                else if (firstRemoteIdx < 0)
                    firstRemoteIdx = i;
            }
            // Co-op: default to first remote player so host does not accidentally buff only themselves.
            var selectedIdx = firstRemoteIdx >= 0 ? firstRemoteIdx : defaultIdx;
            playerPicker.Selected = selectedIdx;
            addTargetPlayer = players[selectedIdx];
            playerPicker.ItemSelected += idx => addTargetPlayer = players[(int)idx];
            playerRow.AddChild(playerPicker);
            container.AddChild(playerRow);

            var targetHint = new Label {
                Text = string.Format(
                    I18N.T("mpcheat.cardAdd.targetHint", "Adds to: {0}"),
                    MpCheatPlayerLabels.FormatPickerLabel(addTargetPlayer)),
            };
            targetHint.AddThemeFontSizeOverride("font_size", 11);
            targetHint.AddThemeColorOverride("font_color", new Color(0.75f, 0.8f, 0.85f));
            playerPicker.ItemSelected += idx => targetHint.Text = string.Format(
                I18N.T("mpcheat.cardAdd.targetHint", "Adds to: {0}"),
                MpCheatPlayerLabels.FormatPickerLabel(players[(int)idx]));
            container.AddChild(targetHint);
        }

        var targetRow = new HBoxContainer();
        targetRow.AddThemeConstantOverride("separation", 4);
        var targetLbl = new Label { Text = I18N.T("cardBrowser.sidebarTarget", "Target") };
        targetLbl.AddThemeFontSizeOverride("font_size", 12);
        targetRow.AddChild(targetLbl);
        var targetPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        targetPicker.AddItem(I18N.T("topbar.card.hand", "Hand"), 0);
        targetPicker.AddItem(I18N.T("topbar.card.drawPile", "Draw Pile"), 1);
        targetPicker.AddItem(I18N.T("topbar.card.discardPile", "Discard"), 2);
        targetPicker.AddItem(I18N.T("topbar.card.exhaustPile", "Exhaust"), 3);
        targetPicker.AddItem(I18N.T("topbar.card.deck", "Deck"), 4);
        targetPicker.Selected = DevModeState.CardTarget switch {
            CardTarget.Hand => 0,
            CardTarget.DrawPile => 1,
            CardTarget.DiscardPile => 2,
            CardTarget.ExhaustPile => 3,
            CardTarget.Deck => 4,
            _ => 0
        };
        targetPicker.ItemSelected += idx => {
            DevModeState.CardTarget = idx switch {
                0 => CardTarget.Hand,
                1 => CardTarget.DrawPile,
                2 => CardTarget.DiscardPile,
                3 => CardTarget.ExhaustPile,
                _ => CardTarget.Deck
            };
        };
        targetRow.AddChild(targetPicker);
        container.AddChild(targetRow);

        var durRow = new HBoxContainer();
        durRow.AddThemeConstantOverride("separation", 4);
        var durLbl = new Label { Text = I18N.T("cardBrowser.sidebarDuration", "Duration") };
        durLbl.AddThemeFontSizeOverride("font_size", 12);
        durRow.AddChild(durLbl);
        var durPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        durPicker.AddItem(I18N.T("topbar.card.temporary", "Temp"), 0);
        durPicker.AddItem(I18N.T("topbar.card.permanent", "Perm"), 1);
        durPicker.Selected = DevModeState.EffectDuration == EffectDuration.Permanent ? 1 : 0;
        durPicker.ItemSelected += idx => {
            DevModeState.EffectDuration = idx == 1 ? EffectDuration.Permanent : EffectDuration.Temporary;
        };
        durRow.AddChild(durPicker);
        container.AddChild(durRow);

        container.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4) });

        var addBtn = CreateActionButton(
            I18N.T("cardBrowser.addCard", "Add Card"),
            new Color(0.25f, 0.55f, 0.35f, 0.9f));
        if (MpCheatSession.InMultiplayerRun && !MpCheatSession.CanUseMultiplayerCheats) {
            addBtn.Disabled = true;
            addBtn.TooltipText = I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");
        }
        if (MpCheatSession.InMultiplayerRun && !MpCheatSession.IsHost) {
            addBtn.TooltipText = I18N.T(
                "mpcheat.cardAdd.clientTooltip",
                "Adds to your character via host sync.");
        }

        async Task SyncAddCardInMultiplayerAsync() {
            var addRequest = new AddCardRequest {
                Target = DevModeState.CardTarget,
                Duration = DevModeState.EffectDuration,
                UpgradeLevelsToApply = upgradeLevelsToApply,
                CustomBaseCost = addCostStaging.BaseCost,
            };
            var result = MpCheatSession.IsHost
                ? await MpCheatCardAddCoordinator.TryHostAddCardAsync(
                    state, addTargetPlayer, card, addRequest, CardPreviewStyle.HorizontalLayout)
                : await MpCheatCardAddCoordinator.TryClientRequestAddCardAsync(
                    state, addTargetPlayer, card, addRequest, CardPreviewStyle.HorizontalLayout);
            statusLabel.Text = result;
        }

        addBtn.Pressed += () => {
            if (MpCheatSession.InMultiplayerRun) {
                statusLabel.Text = MpCheatSession.IsHost
                    ? (MpCheatParticipants.RemotePeerCount > 0
                        ? string.Format(
                            I18N.T("mpcheat.cardAdd.pendingWithPeers", "Syncing add card… waiting for {0} player(s)."),
                            MpCheatParticipants.RemotePeerCount)
                        : I18N.T("mpcheat.cardAdd.pending", "Syncing add card to all players…"))
                    : I18N.T("mpcheat.cardAdd.clientPending", "Requesting host to sync add card…");
                TaskHelper.RunSafely(SyncAddCardInMultiplayerAsync());
                return;
            }
            TaskHelper.RunSafely(CardActions.Add(state, addTargetPlayer, card)
                .BaseCost(addCostStaging.BaseCost)
                .UpgradeLevels(upgradeLevelsToApply)
                .UpgradePreviewStyle(CardPreviewStyle.HorizontalLayout)
                .RunAsync());
            statusLabel.Text = string.Format(I18N.T("cardBrowser.addedCard", "Added: {0}"), nameAfterAdd);
        };
        container.AddChild(addBtn);

        var autoApplyBtn = CreateActionButton(
            I18N.T("cardBrowser.autoApply", "Add to Auto-Apply"),
            new Color(0.25f, 0.55f, 0.38f, 0.85f));
        autoApplyBtn.Pressed += () => {
            var cardId = ((AbstractModel)card).Id.Entry;
            var cardName = CardEditActions.GetCardDisplayName(card);
            var entry = new HookEntry {
                Name = cardName,
                Trigger = TriggerType.CombatStart,
                Actions = [new HookAction
                {
                    Type     = ActionType.AddCard,
                    TargetId = cardId,
                }],
            };
            SettingsStore.Current.Hooks.Add(entry);
            SettingsStore.Save();
            statusLabel.Text = string.Format(
                I18N.T("cardBrowser.autoApplyAdded", "Auto-apply added: {0}"), cardName);
        };
        container.AddChild(autoApplyBtn);
    }

    // ── Owned card actions (Upgrade + Remove) ──

    private static void BuildOwnedCardActions(VBoxContainer container, Label statusLabel,
        CardModel card, RunState state, Player player, NGlobalUi globalUi, Action onGridRefresh,
        CardTarget? browseTarget) {
        int upgradeLevel = 0, maxUpgrade = 0;
        try { upgradeLevel = card.CurrentUpgradeLevel; maxUpgrade = card.MaxUpgradeLevel; } catch { }
        bool canUpgrade = upgradeLevel < maxUpgrade;

        var upgradeRow = new HBoxContainer();
        upgradeRow.AddThemeConstantOverride("separation", 4);
        var upgradeLbl = new Label {
            Text = $"Lv {upgradeLevel}/{maxUpgrade}",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            VerticalAlignment = VerticalAlignment.Center
        };
        upgradeLbl.AddThemeFontSizeOverride("font_size", 12);
        upgradeRow.AddChild(upgradeLbl);

        var upgradeBtn = new Button {
            Text = I18N.T("cardBrowser.upgradeCard", "Upgrade"),
            Disabled = !canUpgrade,
            CustomMinimumSize = new Vector2(70, 30)
        };
        ApplySmallActionStyle(upgradeBtn, canUpgrade ? new Color(0.30f, 0.50f, 0.70f, 0.9f) : new Color(0.3f, 0.3f, 0.3f, 0.5f));
        upgradeBtn.Pressed += () => {
            try {
                CardCmd.Upgrade(new[] { card }, CardPreviewStyle.HorizontalLayout);
                statusLabel.Text = string.Format(I18N.T("cardBrowser.upgraded", "Upgraded: {0}"),
                    CardEditActions.GetCardDisplayName(card));
                onGridRefresh();
            }
            catch (Exception ex) {
                statusLabel.Text = $"Upgrade failed: {ex.Message}";
            }
        };
        upgradeRow.AddChild(upgradeBtn);
        container.AddChild(upgradeRow);

        var removeBtn = CreateActionButton(
            I18N.T("cardBrowser.deleteCard", "Remove Card"),
            new Color(0.65f, 0.25f, 0.25f, 0.9f));
        removeBtn.Pressed += () => {
            try {
                if (browseTarget == CardTarget.Deck) {
                    TaskHelper.RunSafely(CardPileCmd.RemoveFromDeck(
                        (IReadOnlyList<CardModel>)new[] { card }, true));
                }
                else if (browseTarget.HasValue) {
                    TaskHelper.RunSafely(CardPileCmd.RemoveFromCombat(new[] { card }));
                    if (state.ContainsCard(card))
                        state.RemoveCard(card);
                }

                statusLabel.Text = string.Format(I18N.T("cardBrowser.deleted", "Removed: {0}"),
                    CardEditActions.GetCardDisplayName(card));
                onGridRefresh();
            }
            catch (Exception ex) {
                statusLabel.Text = $"Remove failed: {ex.Message}";
            }
        };
        container.AddChild(removeBtn);
    }

    // ── Edit section (inline property editor) ──

    private static void BuildEditSection(VBoxContainer container, Label statusLabel, CardModel card,
        LibraryAddCostStaging? libraryAddCost = null) {
        if (libraryAddCost != null) {
            AddIntEditor(container, I18N.T("cardEdit.cost", "Base Cost"), libraryAddCost.BaseCost,
                v => {
                    libraryAddCost.BaseCost = v;
                    statusLabel.Text = I18N.T("cardBrowser.costAppliesOnAdd", "Base cost applies when you add this card.");
                });
        }
        else {
            AddIntEditor(container, I18N.T("cardEdit.cost", "Base Cost"),
                CardEditActions.GetBaseCost(card) ?? 0,
                v => {
                    if (CardEditActions.TrySetBaseCost(card, v))
                        statusLabel.Text = I18N.T("cardBrowser.costSet", "Cost set.");
                    else
                        statusLabel.Text = I18N.T("cardBrowser.costSetFailed", "Could not set cost.");
                });
        }

        AddIntEditor(container, I18N.T("cardEdit.replay", "Replay Count"),
            CardEditActions.GetReplayCount(card) ?? 0,
            v => { CardEditActions.TrySetReplayCount(card, v); statusLabel.Text = I18N.T("cardBrowser.replaySet", "Replay set."); });

        AddIntEditor(container, I18N.T("cardEdit.damage", "Base Damage"),
            CardEditActions.GetDamage(card) ?? 0,
            v => { CardEditActions.TrySetDamage(card, v); statusLabel.Text = I18N.T("cardBrowser.damageSet", "Damage set."); });

        AddIntEditor(container, I18N.T("cardEdit.block", "Base Block"),
            CardEditActions.GetBlock(card) ?? 0,
            v => { CardEditActions.TrySetBlock(card, v); statusLabel.Text = I18N.T("cardBrowser.blockSet", "Block set."); });

        AddBoolToggle(container, I18N.T("cardEdit.exhaust", "Exhaust"),
            CardEditActions.GetExhaust(card) ?? false,
            v => { CardEditActions.TrySetExhaust(card, v); statusLabel.Text = I18N.T("cardBrowser.exhaustToggled", "Exhaust toggled."); });
        AddBoolToggle(container, I18N.T("cardEdit.ethereal", "Ethereal"),
            CardEditActions.GetEthereal(card) ?? false,
            v => { CardEditActions.TrySetEthereal(card, v); statusLabel.Text = I18N.T("cardBrowser.etherealToggled", "Ethereal toggled."); });
        AddBoolToggle(container, I18N.T("cardEdit.unplayable", "Unplayable"),
            CardEditActions.GetUnplayable(card) ?? false,
            v => { CardEditActions.TrySetUnplayable(card, v); statusLabel.Text = I18N.T("cardBrowser.unplayableToggled", "Unplayable toggled."); });
        AddBoolToggle(container, I18N.T("cardEdit.exhaustOnNextPlay", "Exhaust On Next Play"),
            CardEditActions.GetExhaustOnNextPlay(card) ?? false,
            v => { CardEditActions.TrySetExhaustOnNextPlay(card, v); statusLabel.Text = "Exhaust-on-next-play toggled."; });
        AddBoolToggle(container, I18N.T("cardEdit.singleTurnRetain", "Single-Turn Retain"),
            CardEditActions.GetSingleTurnRetain(card) ?? false,
            v => { CardEditActions.TrySetSingleTurnRetain(card, v); statusLabel.Text = "Single-turn retain toggled."; });
        AddBoolToggle(container, I18N.T("cardEdit.singleTurnSly", "Single-Turn Sly"),
            CardEditActions.GetSingleTurnSly(card) ?? false,
            v => { CardEditActions.TrySetSingleTurnSly(card, v); statusLabel.Text = "Single-turn sly toggled."; });

        var dynamicKeys = CardEditActions.GetDynamicVarKeys(card);
        if (dynamicKeys.Count > 0) {
            container.AddChild(new HSeparator());
            container.AddChild(new Label { Text = I18N.T("cardEdit.dynamicVars", "Dynamic Vars") });
            foreach (var key in dynamicKeys) {
                var displayKey = CardEditActions.GetDynamicVarDisplayName(key);
                AddIntEditor(container, displayKey, CardEditActions.GetDynamicVar(card, key) ?? 0,
                    v => { CardEditActions.TrySetDynamicVar(card, key, v); statusLabel.Text = $"{displayKey} set."; });
            }
        }

        container.AddChild(new HSeparator());

        AddTextEditor(container, I18N.T("cardEdit.titleText", "Name Override"),
            CardEditActions.GetTitleText(card),
            v => { CardEditActions.TrySetTitleText(card, v); statusLabel.Text = "Name override set."; });
        AddTextEditor(container, I18N.T("cardEdit.descText", "Description Override"),
            CardEditActions.GetDescriptionText(card),
            v => { CardEditActions.TrySetDescriptionText(card, v); statusLabel.Text = "Description override set."; });

        var enchantTypes = CardEditActions.GetEnchantmentTypes();
        if (enchantTypes.Count > 0) {
            container.AddChild(new HSeparator());
            container.AddChild(new Label { Text = I18N.T("cardEdit.enchantment", "Enchantment") });

            var enchantRow = new HBoxContainer();
            enchantRow.AddThemeConstantOverride("separation", 4);

            var enchantDropdown = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
            for (int i = 0; i < enchantTypes.Count; i++)
                enchantDropdown.AddItem(enchantTypes[i].Name, i);
            enchantRow.AddChild(enchantDropdown);

            var applyBtn = new Button { Text = I18N.T("cardEdit.applyEnchant", "Apply"), CustomMinimumSize = new Vector2(50, 26) };
            applyBtn.Pressed += () => {
                int idx = enchantDropdown.Selected;
                if (idx >= 0 && idx < enchantTypes.Count) {
                    bool ok = CardEditActions.TryApplyEnchantment(card, enchantTypes[idx]);
                    statusLabel.Text = ok ? "Enchantment applied." : "Failed to apply.";
                }
            };
            enchantRow.AddChild(applyBtn);

            var forceBtn = new Button { Text = I18N.T("cardEdit.forceEnchant", "Force"), CustomMinimumSize = new Vector2(50, 26) };
            forceBtn.Pressed += () => {
                int idx = enchantDropdown.Selected;
                if (idx >= 0 && idx < enchantTypes.Count) {
                    bool ok = CardEditActions.TryApplyEnchantment(card, enchantTypes[idx], force: true);
                    statusLabel.Text = ok ? "Enchantment force-applied." : "Failed.";
                }
            };
            enchantRow.AddChild(forceBtn);

            container.AddChild(enchantRow);

            var clearBtn = new Button { Text = I18N.T("cardEdit.clearEnchant", "Clear Enchantment"), CustomMinimumSize = new Vector2(0, 26) };
            clearBtn.Pressed += () => {
                CardEditActions.TryClearEnchantment(card);
                statusLabel.Text = "Enchantment cleared.";
            };
            container.AddChild(clearBtn);
        }

        container.AddChild(new HSeparator());
        BuildPresetRow(container, statusLabel, card);
    }

    // ──────── Preset Row ────────

    private static void BuildPresetRow(VBoxContainer container, Label statusLabel, CardModel card) {
        var presetRow = new HBoxContainer();
        presetRow.AddThemeConstantOverride("separation", 4);

        var presetPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        var presetNameInput = new LineEdit {
            PlaceholderText = I18N.T("cardEdit.presetName", "Preset name..."),
            CustomMinimumSize = new Vector2(100, 0)
        };
        var saveBtn = new Button { Text = I18N.T("cardEdit.savePreset", "Save"), CustomMinimumSize = new Vector2(50, 26) };
        var applyBtn = new Button { Text = I18N.T("cardEdit.applyPreset", "Apply"), CustomMinimumSize = new Vector2(50, 26) };
        var delBtn = new Button { Text = I18N.T("cardEdit.deletePreset", "Delete"), CustomMinimumSize = new Vector2(50, 26) };

        void RebuildPresets() {
            presetPicker.Clear();
            var names = CardEditPresetManager.Store.All.Keys
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase).ToArray();
            foreach (var n in names) presetPicker.AddItem(n);
            applyBtn.Disabled = names.Length == 0;
            delBtn.Disabled = names.Length == 0;
        }

        saveBtn.Pressed += () => {
            var pName = presetNameInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(pName)) { statusLabel.Text = "Enter preset name."; return; }
            var cardId = ((AbstractModel)card).Id.Entry ?? "";
            var payload = new CardEditNamedPreset { CardId = cardId, Template = CardEditActions.CaptureTemplate(card) };
            CardEditPresetManager.Store.Set(pName, payload);
            statusLabel.Text = $"Preset saved: {pName}";
            RebuildPresets();
        };

        applyBtn.Pressed += () => {
            if (presetPicker.ItemCount == 0) { statusLabel.Text = "No preset."; return; }
            var pName = presetPicker.GetItemText(presetPicker.Selected);
            if (!CardEditPresetManager.Store.TryGet(pName, out var preset)) { statusLabel.Text = "Preset not found."; return; }
            CardEditActions.ApplyTemplate(card, preset.Template);
            statusLabel.Text = $"Preset applied: {pName}";
        };

        delBtn.Pressed += () => {
            if (presetPicker.ItemCount == 0) { statusLabel.Text = "No preset."; return; }
            var pName = presetPicker.GetItemText(presetPicker.Selected);
            if (CardEditPresetManager.Store.Delete(pName)) {
                statusLabel.Text = $"Preset deleted: {pName}";
                RebuildPresets();
            }
        };

        presetRow.AddChild(presetPicker);
        presetRow.AddChild(presetNameInput);
        presetRow.AddChild(saveBtn);
        presetRow.AddChild(applyBtn);
        presetRow.AddChild(delBtn);
        container.AddChild(presetRow);

        RebuildPresets();
    }

    // ──────── Widget Helpers ────────

    private static void AddStatRow(VBoxContainer parent, string label, string value) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var lbl = new Label { Text = label, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(lbl);
        var val = new Label { Text = value, HorizontalAlignment = HorizontalAlignment.Right };
        val.AddThemeFontSizeOverride("font_size", 12);
        row.AddChild(val);
        parent.AddChild(row);
    }

    private static void AddIntEditor(VBoxContainer parent, string label, int currentValue, Action<int> onApply) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        var spin = new SpinBox {
            MinValue = -999,
            MaxValue = 9999,
            Value = currentValue,
            Step = 1,
            CustomMinimumSize = new Vector2(70, 26),
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

    private static void AddTextEditor(VBoxContainer parent, string label, string currentValue, Action<string> onApply) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        row.AddChild(new Label { Text = label, CustomMinimumSize = new Vector2(80, 0) });
        var input = new LineEdit { Text = currentValue ?? string.Empty, SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        void Apply() => onApply(input.Text ?? string.Empty);
        input.TextSubmitted += _ => Apply();
        input.FocusExited += () => Apply();
        row.AddChild(input);
        parent.AddChild(row);
    }

    internal static Button CreateActionButton(string text, Color bgColor) {
        var btn = new Button {
            Text = text,
            CustomMinimumSize = new Vector2(0, 40),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        var style = new StyleBoxFlat {
            BgColor = bgColor,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        var hover = new StyleBoxFlat {
            BgColor = bgColor.Lightened(0.15f),
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 6,
            ContentMarginBottom = 6
        };
        btn.AddThemeStyleboxOverride("normal", style);
        btn.AddThemeStyleboxOverride("hover", hover);
        btn.AddThemeStyleboxOverride("pressed", hover);
        btn.AddThemeStyleboxOverride("focus", style);
        btn.AddThemeFontSizeOverride("font_size", 14);
        return btn;
    }

    private static void ApplySmallActionStyle(Button btn, Color bgColor) {
        var s = new StyleBoxFlat {
            BgColor = bgColor,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        var h = new StyleBoxFlat {
            BgColor = bgColor.Lightened(0.15f),
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 3,
            ContentMarginBottom = 3
        };
        btn.AddThemeStyleboxOverride("normal", s);
        btn.AddThemeStyleboxOverride("hover", h);
        btn.AddThemeStyleboxOverride("pressed", h);
        btn.AddThemeStyleboxOverride("focus", s);
        btn.AddThemeFontSizeOverride("font_size", 12);
    }

}
