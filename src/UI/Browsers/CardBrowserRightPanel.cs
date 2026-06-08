using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Hooks;
using KitLib.Modding;
using KitLib.Multiplayer.Cheat;
using KitLib.Presets;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

/// <summary>
/// Builds the right-side detail / action panel inside the card browser.
/// Stateless: every method receives what it needs via parameters.
/// </summary>
internal static class CardBrowserRightPanel {
    private static Color ColSubtle => KitLibTheme.Subtle;

    /// <summary>Staged edits applied when adding from the library (canonical cards are not mutated in-place in MP).</summary>
    private sealed class LibraryAddStaging {
        public CardEditTemplate Template { get; } = new();

        public void ResetToDefault(CardModel card) {
            var t = Template;
            t.BaseCost = CardEditActions.GetBaseCost(card);
            t.ReplayCount = null;
            t.Damage = null;
            t.Block = null;
            t.Exhaust = null;
            t.Ethereal = null;
            t.Unplayable = null;
            t.ExhaustOnNextPlay = null;
            t.SingleTurnRetain = null;
            t.SingleTurnSly = null;
            t.DynamicVars = null;
            t.ClearEnchantment = null;
            t.EnchantmentTypeName = null;
            t.EnchantmentAmount = null;
        }
    }

    /// <summary>Locks library stat editors when Duration is Permanent (multiplayer add flow only).</summary>
    private sealed class LibraryAddUiState {
        public required CardModel Card { get; init; }
        public required LibraryAddStaging Staging { get; init; }
        public List<Control> StatEditRows { get; } = [];
        public Label? MpDurHint;
        public bool MpAddCard;

        public void Refresh() {
            var lockEdits = ShouldLockLibraryStatEdits;
            if (lockEdits)
                Staging.ResetToDefault(Card);
            var tooltip = I18N.T(
                "cardBrowser.permLocksEdits",
                "Card stat edits are disabled in Permanent mode.");
            foreach (var row in StatEditRows)
                SetStatEditRowLocked(row, lockEdits, tooltip);
            RefreshMpDurHint();
        }

        public void RefreshMpDurHintOnly() => RefreshMpDurHint();

        private void RefreshMpDurHint() {
            if (MpDurHint == null) return;
            MpDurHint.Text = KitLibState.EffectDuration == EffectDuration.Permanent
                ? I18N.T(
                    "mpcheat.cardAdd.permLocksEdits",
                    "Permanent mode: card stat edits are disabled.")
                : CardActions.HasStagedEdits(Card, new AddCardRequest { StagedTemplate = Staging.Template })
                    ? I18N.T(
                        "mpcheat.cardAdd.permEditedHint",
                        "Multiplayer: switch to Temporary to edit stats before add.")
                    : I18N.T(
                        "mpcheat.cardAdd.permAllowedHint",
                        "Multiplayer: Permanent is allowed for unedited cards.");
        }
    }

    private static bool ShouldLockLibraryStatEdits =>
        MpCheatSession.InMultiplayerRun && KitLibState.EffectDuration == EffectDuration.Permanent;

    internal static void Build(VBoxContainer container, Label statusLabel,
        CardModel card, RunState state, Player player, NGlobalUi globalUi, Action onCardEdited,
        Action onCardListChanged, bool isLibrary, CardTarget? browseTarget, bool libraryUpgradeDetailPreview = false) {
        var displayCard = ResolveLibraryDisplayCard(card, isLibrary, libraryUpgradeDetailPreview);
        LibraryAddStaging? libraryAddStaging = null;
        if (isLibrary) {
            libraryAddStaging = new LibraryAddStaging();
            var initialCost = CardEditActions.GetBaseCost(card);
            if (initialCost.HasValue)
                libraryAddStaging.Template.BaseCost = initialCost.Value;
        }

        var cardName = CardEditActions.GetCardDisplayName(displayCard);

        var headerLabel = new Label {
            Text = cardName,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        headerLabel.AddThemeFontSizeOverride("font_size", 16);
        container.AddChild(headerLabel);

        var cardIdStr = ((AbstractModel)card).Id.Entry;
        if (!string.IsNullOrEmpty(cardIdStr))
            container.AddChild(KitLibTheme.CreateCopyableIdRow(cardIdStr,
                msg => statusLabel.Text = msg));

        container.AddChild(BrowserDetailHelpers.CreateModSourceRow(ContentModResolver.Resolve(card)));

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

        var desc = CardPreviewHelper.GetDescription(card, forUpgradePreview: !ReferenceEquals(displayCard, card));
        if (!string.IsNullOrWhiteSpace(desc)) {
            var descLabel = KitLibTheme.CreateGameBbcodeLabel();
            descLabel.Text = KitLibTheme.ConvertGameBbcode(desc);
            descLabel.AddThemeFontSizeOverride("normal_font_size", 12);
            descLabel.AddThemeColorOverride("default_color", KitLibTheme.TextSecondary);
            container.AddChild(descLabel);
        }

        container.AddChild(new HSeparator());

        LibraryAddUiState? libraryUi = null;
        if (isLibrary) {
            libraryUi = new LibraryAddUiState { Card = card, Staging = libraryAddStaging! };
            BuildAddSection(container, statusLabel, card, displayCard, state, player,
                libraryUpgradeDetailPreview, libraryAddStaging!, onCardListChanged, libraryUi);
        }
        else {
            BuildOwnedCardActions(container, statusLabel, card, state, player, globalUi, onCardListChanged, browseTarget);
        }

        container.AddChild(new HSeparator());
        // Inline editors apply to the pile instance, or stage values for Add when browsing the library.
        BuildEditSection(container, statusLabel, card, state, player, onCardEdited, libraryAddStaging,
            isLibrary ? null : browseTarget, libraryUi);
    }

    /// <summary>When browsing the card library with "view upgrades" on, match grid + NCard: clone and UpgradeInternal for read-only UI.</summary>
    private static CardModel ResolveLibraryDisplayCard(CardModel card, bool isLibrary, bool libraryUpgradeDetailPreview) =>
        CardPreviewHelper.GetDisplayModel(card, isLibrary && libraryUpgradeDetailPreview);

    // ── Add section (for library source) ──

    private static void BuildAddSection(VBoxContainer container, Label statusLabel,
        CardModel card, CardModel displayCard, RunState state, Player player, bool libraryUpgradeDetailPreview,
        LibraryAddStaging addStaging, Action onCardListChanged, LibraryAddUiState libraryUi) {
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
            var localIdx = 0;
            for (var i = 0; i < players.Count; i++) {
                var p = players[i];
                playerPicker.AddItem(MpCheatPlayerLabels.FormatPickerLabel(p), i);
                if (p.NetId == localNetId)
                    localIdx = i;
            }
            playerPicker.Selected = localIdx;
            addTargetPlayer = players[localIdx];
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
        targetPicker.Selected = KitLibState.CardTarget switch {
            CardTarget.Hand => 0,
            CardTarget.DrawPile => 1,
            CardTarget.DiscardPile => 2,
            CardTarget.ExhaustPile => 3,
            CardTarget.Deck => 4,
            _ => 0
        };
        targetPicker.ItemSelected += idx => {
            KitLibState.CardTarget = idx switch {
                0 => CardTarget.Hand,
                1 => CardTarget.DrawPile,
                2 => CardTarget.DiscardPile,
                3 => CardTarget.ExhaustPile,
                _ => CardTarget.Deck
            };
        };
        targetRow.AddChild(targetPicker);
        container.AddChild(targetRow);

        var mpAddCard = MpCheatSession.InMultiplayerRun;
        var durRow = new HBoxContainer();
        durRow.AddThemeConstantOverride("separation", 4);
        var durLbl = new Label { Text = I18N.T("cardBrowser.sidebarDuration", "Duration") };
        durLbl.AddThemeFontSizeOverride("font_size", 12);
        durRow.AddChild(durLbl);
        var durPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        durPicker.AddItem(I18N.T("topbar.card.temporary", "Temp"), 0);
        durPicker.AddItem(I18N.T("topbar.card.permanent", "Perm"), 1);
        durPicker.Selected = KitLibState.EffectDuration == EffectDuration.Permanent ? 1 : 0;
        durPicker.ItemSelected += idx => {
            KitLibState.EffectDuration = idx == 1 ? EffectDuration.Permanent : EffectDuration.Temporary;
            libraryUi.Refresh();
        };
        durRow.AddChild(durPicker);
        container.AddChild(durRow);
        libraryUi.MpAddCard = mpAddCard;
        if (mpAddCard) {
            var durHint = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
            durHint.AddThemeFontSizeOverride("font_size", 11);
            durHint.AddThemeColorOverride("font_color", new Color(0.95f, 0.75f, 0.35f));
            libraryUi.MpDurHint = durHint;
            container.AddChild(durHint);
        }

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
                Target = KitLibState.CardTarget,
                Duration = KitLibState.EffectDuration,
                UpgradeLevelsToApply = upgradeLevelsToApply,
                StagedTemplate = addStaging.Template,
            };
            if (addRequest.Duration == EffectDuration.Permanent && CardActions.HasStagedEdits(card, addRequest)) {
                statusLabel.Text = I18N.T(
                    "mpcheat.cardAdd.permanentEditedBlocked",
                    "Permanent add is disabled while card stats are edited — use Temporary or reset edits.");
                return;
            }
            var result = MpCheatSession.IsHost
                ? await MpCheatCardAddCoordinator.TryHostAddCardAsync(
                    state, addTargetPlayer, card, addRequest, CardPreviewStyle.HorizontalLayout)
                : await MpCheatCardAddCoordinator.TryClientRequestAddCardAsync(
                    state, addTargetPlayer, card, addRequest, CardPreviewStyle.HorizontalLayout);
            statusLabel.Text = result;
            onCardListChanged();
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
            TaskHelper.RunSafely(AddCardThenRefreshGridAsync());
            statusLabel.Text = string.Format(I18N.T("cardBrowser.addedCard", "Added: {0}"), nameAfterAdd);

            async Task AddCardThenRefreshGridAsync() {
                await CardActions.Add(state, addTargetPlayer, card)
                    .StagedTemplate(addStaging.Template)
                    .UpgradeLevels(upgradeLevelsToApply)
                    .UpgradePreviewStyle(CardPreviewStyle.HorizontalLayout)
                    .RunAsync();
                onCardListChanged();
            }
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
        libraryUi.Refresh();
    }

    // ── Owned card actions (Upgrade + Remove) ──

    private static void BuildOwnedCardActions(VBoxContainer container, Label statusLabel,
        CardModel card, RunState state, Player player, NGlobalUi globalUi, Action onCardListChanged,
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
                onCardListChanged();
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
        if (MpCheatSession.InMultiplayerRun && !MpCheatSession.CanUseMultiplayerCheats) {
            removeBtn.Disabled = true;
            removeBtn.TooltipText = I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");
        }
        else if (MpCheatSession.InMultiplayerRun && !browseTarget.HasValue) {
            removeBtn.Disabled = true;
            removeBtn.TooltipText = I18N.T(
                "mpcheat.cardRemove.libraryBlocked",
                "Remove synced cards from a pile tab (Hand, Deck, etc.), not All Cards.");
        }
        else if (MpCheatSession.InMultiplayerRun && !MpCheatSession.IsHost) {
            removeBtn.TooltipText = I18N.T(
                "mpcheat.cardRemove.clientTooltip",
                "Removes from your character via host sync.");
        }

        async Task SyncRemoveCardInMultiplayerAsync() {
            if (!browseTarget.HasValue) return;
            var target = browseTarget.Value;
            var removeFromRunState = target == CardTarget.Deck || state.ContainsCard(card);
            var result = MpCheatSession.IsHost
                ? await MpCheatCardRemoveCoordinator.TryHostRemoveCardAsync(
                    state, player, card, target, removeFromRunState)
                : await MpCheatCardRemoveCoordinator.TryClientRequestRemoveCardAsync(
                    state, player, card, target, removeFromRunState);
            statusLabel.Text = result;
            onCardListChanged();
        }

        removeBtn.Pressed += () => {
            if (MpCheatSession.InMultiplayerRun && browseTarget.HasValue) {
                statusLabel.Text = MpCheatSession.IsHost
                    ? (MpCheatParticipants.RemotePeerCount > 0
                        ? string.Format(
                            I18N.T("mpcheat.cardRemove.pendingWithPeers", "Syncing remove card… waiting for {0} player(s)."),
                            MpCheatParticipants.RemotePeerCount)
                        : I18N.T("mpcheat.cardRemove.pending", "Syncing remove card to all players…"))
                    : I18N.T("mpcheat.cardRemove.clientPending", "Requesting host to sync remove card…");
                TaskHelper.RunSafely(SyncRemoveCardInMultiplayerAsync());
                return;
            }

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
                onCardListChanged();
            }
            catch (Exception ex) {
                statusLabel.Text = $"Remove failed: {ex.Message}";
            }
        };
        container.AddChild(removeBtn);
    }

    // ── Edit section (inline property editor) ──

    private static void BuildEditSection(VBoxContainer container, Label statusLabel, CardModel card,
        RunState state, Player player, Action? onCardEdited, LibraryAddStaging? libraryAddStaging = null,
        CardTarget? browseTarget = null, LibraryAddUiState? libraryUi = null) {
        var mpLibrary = libraryAddStaging != null && MpCheatSession.InMultiplayerRun;
        var mpOwnedPile = libraryAddStaging == null && MpCheatSession.InMultiplayerRun && browseTarget.HasValue;
        var mpBlocked = libraryAddStaging == null && MpCheatSession.InMultiplayerRun && !browseTarget.HasValue;

        if (MpCheatSession.InMultiplayerRun) {
            var mpWarn = new Label {
                Text = mpOwnedPile
                    ? I18N.T(
                        "cardBrowser.editMpSync",
                        "Multiplayer: edits below sync to all players (Hand/Deck tabs).")
                    : mpLibrary
                        ? I18N.T(
                            "cardBrowser.editMpLibrarySync",
                            "Multiplayer: edits below sync when you add this card (all players).")
                        : mpBlocked
                            ? I18N.T(
                                "cardBrowser.editMpLibraryBlocked",
                                "Multiplayer: open Hand/Deck (or another pile tab) to sync edits. All Cards library edits are local-only.")
                            : I18N.T(
                                "cardBrowser.costAppliesOnAdd",
                                "Base cost applies when you add this card."),
            };
            mpWarn.AddThemeFontSizeOverride("font_size", 11);
            mpWarn.AddThemeColorOverride(
                "font_color",
                mpOwnedPile || mpLibrary ? new Color(0.55f, 0.85f, 0.55f) : new Color(0.95f, 0.75f, 0.35f));
            mpWarn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            container.AddChild(mpWarn);
        }

        void ApplyPatch(CardEditTemplate patch, string singlePlayerOk, string? singlePlayerFail = null) {
            if (libraryAddStaging != null) {
                if (ShouldLockLibraryStatEdits)
                    return;
                libraryAddStaging.Template.MergePatch(patch);
                libraryUi?.RefreshMpDurHintOnly();
                if (!MpCheatSession.InMultiplayerRun)
                    CardEditActions.ApplyTemplate(card, patch);
                statusLabel.Text = MpCheatSession.InMultiplayerRun
                    ? I18N.T(
                        "cardBrowser.editMpLibraryStaged",
                        "Staged for add — will sync to all players when you add this card.")
                    : singlePlayerFail != null && !patch.HasAnyPatch()
                        ? singlePlayerFail
                        : singlePlayerOk;
                if (!MpCheatSession.InMultiplayerRun)
                    onCardEdited?.Invoke();
                return;
            }
            if (mpBlocked) {
                statusLabel.Text = I18N.T(
                    "mpcheat.cardEdit.libraryBlocked",
                    "Edit synced cards from a pile tab (Hand, Deck, etc.), not All Cards.");
                return;
            }
            if (mpOwnedPile) {
                RunSyncedCardEdit(statusLabel, card, state, player, browseTarget!.Value, onCardEdited, patch);
                return;
            }
            CardEditActions.ApplyTemplate(card, patch);
            statusLabel.Text = singlePlayerFail != null && !patch.HasAnyPatch()
                ? singlePlayerFail
                : singlePlayerOk;
            onCardEdited?.Invoke();
        }

        void TrackLibraryRow(Control row) {
            if (libraryUi != null)
                libraryUi.StatEditRows.Add(row);
        }

        if (libraryAddStaging != null) {
            var stagedCost = libraryAddStaging.Template.BaseCost ?? CardEditActions.GetBaseCost(card) ?? 0;
            TrackLibraryRow(AddIntEditor(container, I18N.T("cardEdit.cost", "Base Cost"), stagedCost,
                v => ApplyPatch(
                    new CardEditTemplate { BaseCost = v },
                    I18N.T("cardBrowser.costAppliesOnAdd", "Base cost applies when you add this card."))));
        }
        else {
            AddIntEditor(container, I18N.T("cardEdit.cost", "Base Cost"),
                CardEditActions.GetBaseCost(card) ?? 0,
                v => ApplyPatch(
                    new CardEditTemplate { BaseCost = v },
                    I18N.T("cardBrowser.costSet", "Cost set."),
                    I18N.T("cardBrowser.costSetFailed", "Could not set cost.")));
        }

        TrackLibraryRow(AddIntEditor(container, I18N.T("cardEdit.replay", "Replay Count"),
            CardEditActions.GetReplayCount(card) ?? 0,
            v => ApplyPatch(
                new CardEditTemplate { ReplayCount = v },
                I18N.T("cardBrowser.replaySet", "Replay set."))));

        TrackLibraryRow(AddIntEditor(container, I18N.T("cardEdit.damage", "Base Damage"),
            CardEditActions.GetDamage(card) ?? 0,
            v => ApplyPatch(
                new CardEditTemplate { Damage = v },
                I18N.T("cardBrowser.damageSet", "Damage set."))));

        TrackLibraryRow(AddIntEditor(container, I18N.T("cardEdit.block", "Base Block"),
            CardEditActions.GetBlock(card) ?? 0,
            v => ApplyPatch(
                new CardEditTemplate { Block = v },
                I18N.T("cardBrowser.blockSet", "Block set."))));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.exhaust", "Exhaust"),
            CardEditActions.GetExhaust(card) ?? false,
            v => ApplyPatch(
                new CardEditTemplate { Exhaust = v },
                I18N.T("cardBrowser.exhaustToggled", "Exhaust toggled."))));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.ethereal", "Ethereal"),
            CardEditActions.GetEthereal(card) ?? false,
            v => ApplyPatch(
                new CardEditTemplate { Ethereal = v },
                I18N.T("cardBrowser.etherealToggled", "Ethereal toggled."))));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.unplayable", "Unplayable"),
            CardEditActions.GetUnplayable(card) ?? false,
            v => ApplyPatch(
                new CardEditTemplate { Unplayable = v },
                I18N.T("cardBrowser.unplayableToggled", "Unplayable toggled."))));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.exhaustOnNextPlay", "Exhaust On Next Play"),
            CardEditActions.GetExhaustOnNextPlay(card) ?? false,
            v => ApplyPatch(new CardEditTemplate { ExhaustOnNextPlay = v }, "Exhaust-on-next-play toggled.")));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.singleTurnRetain", "Single-Turn Retain"),
            CardEditActions.GetSingleTurnRetain(card) ?? false,
            v => ApplyPatch(new CardEditTemplate { SingleTurnRetain = v }, "Single-turn retain toggled.")));

        TrackLibraryRow(AddBoolToggle(container, I18N.T("cardEdit.singleTurnSly", "Single-Turn Sly"),
            CardEditActions.GetSingleTurnSly(card) ?? false,
            v => ApplyPatch(new CardEditTemplate { SingleTurnSly = v }, "Single-turn sly toggled.")));

        var dynamicKeys = CardEditActions.GetDynamicVarKeys(card);
        if (dynamicKeys.Count > 0) {
            container.AddChild(new HSeparator());
            container.AddChild(new Label { Text = I18N.T("cardEdit.dynamicVars", "Dynamic Vars") });
            foreach (var key in dynamicKeys) {
                var displayKey = CardEditActions.GetDynamicVarDisplayName(key);
                var dynKey = key;
                TrackLibraryRow(AddIntEditor(container, displayKey, CardEditActions.GetDynamicVar(card, key) ?? 0,
                    v => ApplyPatch(
                        new CardEditTemplate { DynamicVars = new Dictionary<string, int> { [dynKey] = v } },
                        $"{displayKey} set.")));
            }
        }

        var enchantEntries = CardEditActions.GetEnchantmentEntries();
        if (enchantEntries.Count > 0) {
            container.AddChild(new HSeparator());
            string? currentEnchantType = null;
            try { currentEnchantType = card.Enchantment?.GetType().FullName; } catch { /* ignore */ }

            var enchantPicker = EnchantmentPickerUI.Build(new EnchantmentPickerUI.Options {
                ShowModePicker = true,
                ShowForceButton = true,
                HeaderSubtitle = CardEditActions.GetCardEnchantmentDisplayName(card),
                InitialTypeFullName = currentEnchantType,
                InitialAmount = CardEditActions.GetCardEnchantmentAmount(card),
            });
            container.AddChild(enchantPicker.Root);
            TrackLibraryRow(enchantPicker.Root);

            void RefreshEnchantHeader() {
                enchantPicker.SetHeaderSubtitle(CardEditActions.GetCardEnchantmentDisplayName(card));
            }

            void ApplyEnchantment(bool force) {
                if (libraryAddStaging != null && ShouldLockLibraryStatEdits)
                    return;

                CardEditTemplate patch;
                string okMessage;
                switch (enchantPicker.Mode) {
                    case 1:
                        patch = new CardEditTemplate { ClearEnchantment = true };
                        okMessage = I18N.T("cardBrowser.enchantCleared", "Enchantment cleared.");
                        break;
                    case 2:
                        if (string.IsNullOrWhiteSpace(enchantPicker.SelectedTypeFullName)) {
                            statusLabel.Text = I18N.T("cardEdit.enchantPickType", "Select an enchantment type.");
                            return;
                        }
                        patch = new CardEditTemplate {
                            ClearEnchantment = false,
                            EnchantmentTypeName = enchantPicker.SelectedTypeFullName,
                            EnchantmentAmount = Math.Clamp((int)enchantPicker.AmountSpin.Value, 1, 999),
                        };
                        okMessage = I18N.T("cardBrowser.enchantSet", "Enchantment set.");
                        break;
                    default:
                        statusLabel.Text = I18N.T("cardEdit.enchantModeKeep", "Keep current");
                        return;
                }

                if (libraryAddStaging != null) {
                    ApplyPatch(patch, okMessage);
                    if (!MpCheatSession.InMultiplayerRun)
                        CardEditActions.ApplyTemplate(card, patch, forceEnchantment: force);
                    RefreshEnchantHeader();
                    return;
                }

                if (mpOwnedPile) {
                    ApplyPatch(patch, okMessage);
                    RefreshEnchantHeader();
                    return;
                }

                if (patch.ClearEnchantment == true) {
                    statusLabel.Text = CardEditActions.TryClearEnchantment(card, out var clearErr)
                        ? okMessage
                        : clearErr;
                }
                else if (!string.IsNullOrWhiteSpace(patch.EnchantmentTypeName) &&
                         CardEditActions.TryResolveEnchantmentType(patch.EnchantmentTypeName, out var enchantType)) {
                    var ok = CardEditActions.TryApplyEnchantment(
                        card,
                        enchantType,
                        patch.EnchantmentAmount ?? 1,
                        force,
                        out var applyErr);
                    statusLabel.Text = ok ? okMessage : applyErr;
                }
                else {
                    statusLabel.Text = I18N.T("cardEdit.enchantFailed", "Failed to apply enchantment.");
                }

                if (statusLabel.Text == okMessage) {
                    RefreshEnchantHeader();
                    onCardEdited?.Invoke();
                }
            }

            enchantPicker.ApplyButton.Pressed += () => ApplyEnchantment(force: false);
            enchantPicker.ForceButton!.Pressed += () => ApplyEnchantment(force: true);
        }

        if (MpCheatSession.InMultiplayerRun && mpLibrary) {
            container.AddChild(new Label {
                Text = I18N.T(
                    "cardBrowser.editMpEnchantLibrary",
                    "Enchantment below stages for add — syncs to all players when you add this card."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
        }

        container.AddChild(new HSeparator());
        BuildPresetRow(container, statusLabel, card, state, player, browseTarget, onCardEdited, libraryAddStaging, libraryUi);
        libraryUi?.Refresh();
    }

    private static void RunSyncedCardEdit(
        Label statusLabel,
        CardModel card,
        RunState state,
        Player player,
        CardTarget browseTarget,
        Action? onCardEdited,
        CardEditTemplate patch) {
        if (!MpCheatSession.CanUseMultiplayerCheats) {
            statusLabel.Text = I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");
            return;
        }

        statusLabel.Text = MpCheatSession.IsHost
            ? (MpCheatParticipants.RemotePeerCount > 0
                ? string.Format(
                    I18N.T("mpcheat.cardEdit.pendingWithPeers", "Syncing edit… waiting for {0} player(s)."),
                    MpCheatParticipants.RemotePeerCount)
                : I18N.T("mpcheat.cardEdit.pending", "Syncing edit to all players…"))
            : I18N.T("mpcheat.cardEdit.clientPending", "Requesting host to sync card edit…");

        async Task SyncAsync() {
            var result = MpCheatSession.IsHost
                ? await MpCheatCardEditCoordinator.TryHostEditCardAsync(state, player, card, browseTarget, patch)
                : await MpCheatCardEditCoordinator.TryClientRequestEditCardAsync(state, player, card, browseTarget, patch);
            statusLabel.Text = result;
            onCardEdited?.Invoke();
        }

        TaskHelper.RunSafely(SyncAsync());
    }

    // ──────── Preset Row ────────

    private static void BuildPresetRow(VBoxContainer container, Label statusLabel, CardModel card,
        RunState state, Player player, CardTarget? browseTarget, Action? onCardEdited, LibraryAddStaging? libraryAddStaging,
        LibraryAddUiState? libraryUi = null) {
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
            if (libraryAddStaging != null && ShouldLockLibraryStatEdits)
                return;
            var pName = presetPicker.GetItemText(presetPicker.Selected);
            if (!CardEditPresetManager.Store.TryGet(pName, out var preset)) { statusLabel.Text = "Preset not found."; return; }
            if (libraryAddStaging != null) {
                libraryAddStaging.Template.MergePatch(preset.Template);
                libraryUi?.RefreshMpDurHintOnly();
                if (!MpCheatSession.InMultiplayerRun)
                    CardEditActions.ApplyTemplate(card, preset.Template);
                statusLabel.Text = MpCheatSession.InMultiplayerRun
                    ? I18N.T(
                        "cardBrowser.editMpLibraryStaged",
                        "Staged for add — will sync to all players when you add this card.")
                    : $"Preset applied: {pName}";
                if (!MpCheatSession.InMultiplayerRun)
                    onCardEdited?.Invoke();
                return;
            }
            if (MpCheatSession.InMultiplayerRun && browseTarget.HasValue) {
                RunSyncedCardEdit(statusLabel, card, state, player, browseTarget.Value, onCardEdited, preset.Template);
                return;
            }
            CardEditActions.ApplyTemplate(card, preset.Template);
            statusLabel.Text = $"Preset applied: {pName}";
            onCardEdited?.Invoke();
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
        if (libraryUi != null)
            libraryUi.StatEditRows.Add(presetRow);

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

    private static readonly Color StatEditLockedModulate = new(0.55f, 0.55f, 0.55f, 0.85f);

    private static void SetStatEditRowLocked(Control row, bool locked, string tooltip) {
        SetInteractiveDescendants(row, enabled: !locked);
        row.Modulate = locked ? StatEditLockedModulate : Colors.White;
        row.TooltipText = locked ? tooltip : "";
    }

    private static void SetInteractiveDescendants(Node node, bool enabled) {
        switch (node) {
            case BaseButton button:
                button.Disabled = !enabled;
                break;
            case SpinBox spinBox:
                spinBox.Editable = enabled;
                break;
            case Slider slider:
                slider.Editable = enabled;
                break;
            case LineEdit lineEdit:
                lineEdit.Editable = enabled;
                break;
        }
        foreach (var child in node.GetChildren())
            SetInteractiveDescendants(child, enabled);
    }

    private static Control AddIntEditor(VBoxContainer parent, string label, int currentValue, Action<int> onApply) {
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
        return row;
    }

    private static Control AddBoolToggle(VBoxContainer parent, string label, bool currentValue, Action<bool> onToggle) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 4);
        var check = new CheckBox { Text = label, ButtonPressed = currentValue };
        check.Toggled += v => onToggle(v);
        row.AddChild(check);
        parent.AddChild(row);
        return row;
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
