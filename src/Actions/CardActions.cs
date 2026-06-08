using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.Multiplayer.Cheat;
using KitLib.Navigation;
using KitLib.Presets;
using MegaCrit.Sts2.Core.CardSelection;
using MegaCrit.Sts2.Core.Commands;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;
using MegaCrit.Sts2.Core.Nodes.Screens.Overlays;
using MegaCrit.Sts2.Core.Nodes.Vfx;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Actions;

/// <summary>
/// Parameter object for <see cref="CardActions.Add"/> / <see cref="CardActions.AddCardBuilder"/> — target pile,
/// duration, and post-create upgrade steps. Defaults from <see cref="FromDevPanelState"/>.
/// </summary>
/// <remarks>
/// This is a <see langword="struct"/>; use <see cref="AddCardRequestConfigure"/> (<see langword="ref"/> delegate)
/// or <see cref="CardActions.AddCardBuilder.Configure"/> to mutate fields. <c>Action&lt;AddCardRequest&gt;</c> would operate on a copy.
/// </remarks>
internal struct AddCardRequest {
    public CardTarget Target;
    public EffectDuration Duration;
    public int UpgradeLevelsToApply;
    /// <summary>Legacy cost-only field; prefer <see cref="StagedTemplate"/>.</summary>
    public int? CustomBaseCost;
    /// <summary>Applied to each new instance after upgrade steps (<see cref="CardEditActions.ApplyTemplate"/>).</summary>
    public CardEditTemplate? StagedTemplate;

    public static AddCardRequest FromDevPanelState(int upgradeLevelsToApply = 0) => new() {
        Target = KitLibState.CardTarget,
        Duration = KitLibState.EffectDuration,
        UpgradeLevelsToApply = upgradeLevelsToApply,
        CustomBaseCost = null,
        StagedTemplate = null,
    };
}

/// <summary>Callback that receives an <see cref="AddCardRequest"/> by <see langword="ref"/> for mutation.</summary>
internal delegate void AddCardRequestConfigure(ref AddCardRequest request);

internal static class CardActions {
    /// <summary>Begins adding a card; chain options then call <see cref="AddCardBuilder.RunAsync"/>.</summary>
    /// <example><code><![CDATA[
    /// await CardActions.Add(state, player, template).RunAsync();
    /// await CardActions.Add(state, player, template)
    ///     .BaseCost(1)
    ///     .UpgradeLevels(2)
    ///     .UpgradePreviewStyle(CardPreviewStyle.HorizontalLayout)
    ///     .RunAsync();
    /// ]]></code></example>
    public static AddCardBuilder Add(RunState state, Player player, CardModel canonicalCard) =>
        new(state, player, canonicalCard);

    /// <summary>Fluent builder for <see cref="CardActions.Add"/>.</summary>
    public readonly struct AddCardBuilder {
        private readonly RunState _state;
        private readonly Player _player;
        private readonly CardModel _canonical;
        private readonly AddCardRequest _request;
        private readonly CardPreviewStyle? _upgradePreviewStyle;

        internal AddCardBuilder(RunState state, Player player, CardModel canonical) {
            _state = state;
            _player = player;
            _canonical = canonical;
            _request = AddCardRequest.FromDevPanelState();
            _upgradePreviewStyle = null;
        }

        private AddCardBuilder(RunState state, Player player, CardModel canonical, AddCardRequest request,
            CardPreviewStyle? upgradePreviewStyle) {
            _state = state;
            _player = player;
            _canonical = canonical;
            _request = request;
            _upgradePreviewStyle = upgradePreviewStyle;
        }

        public AddCardBuilder FromDevPanel(int upgradeLevelsToApply = 0) =>
            new(_state, _player, _canonical, AddCardRequest.FromDevPanelState(upgradeLevelsToApply), _upgradePreviewStyle);

        public AddCardBuilder WithRequest(AddCardRequest request) =>
            new(_state, _player, _canonical, request, _upgradePreviewStyle);

        public AddCardBuilder Target(CardTarget target) {
            var r = _request;
            r.Target = target;
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        public AddCardBuilder Duration(EffectDuration duration) {
            var r = _request;
            r.Duration = duration;
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        public AddCardBuilder UpgradeLevels(int levels) {
            var r = _request;
            r.UpgradeLevelsToApply = levels;
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        /// <summary>Stages energy cost on each spawned instance (applied after upgrade steps).</summary>
        public AddCardBuilder BaseCost(int cost) {
            var r = _request;
            r.CustomBaseCost = null;
            var template = r.StagedTemplate ?? new CardEditTemplate();
            template.BaseCost = cost;
            r.StagedTemplate = template;
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        public AddCardBuilder Configure(AddCardRequestConfigure configure) {
            var r = _request;
            configure(ref r);
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        /// <summary>Each post-create upgrade step uses <c>CardCmd.Upgrade(new[] { card }, style)</c>.</summary>
        public AddCardBuilder UpgradePreviewStyle(CardPreviewStyle style) =>
            new(_state, _player, _canonical, _request, style);

        /// <summary>Stats/keywords to apply on each spawned instance after upgrade steps.</summary>
        public AddCardBuilder StagedTemplate(CardEditTemplate template) {
            var r = _request;
            r.StagedTemplate = template;
            r.CustomBaseCost = null;
            return new(_state, _player, _canonical, r, _upgradePreviewStyle);
        }

        public Task RunAsync() => ExecuteAddAsync(_state, _player, _canonical, _request, _upgradePreviewStyle, mpSync: false);
    }

    /// <summary>Validates whether an add-card operation can run (no state mutation).</summary>
    internal static bool TryValidateAdd(RunState state, Player player, CardModel canonicalCard, AddCardRequest request,
        out string error) {
        error = "";
        if (canonicalCard == null) {
            error = "card not found";
            return false;
        }
        if (player == null) {
            error = "player not found";
            return false;
        }
        if (request.Target != CardTarget.Deck && player.Creature?.CombatState == null) {
            error = "not in combat";
            return false;
        }
        if (MpCheatSession.InMultiplayerRun
            && request.Duration == EffectDuration.Permanent
            && HasStagedEdits(canonicalCard, request)) {
            error = I18N.T(
                "mpcheat.cardAdd.permanentEditedBlocked",
                "Permanent add is disabled while card stats are edited — use Temporary or reset edits.");
            return false;
        }
        return true;
    }

    /// <summary>Edited stats must stay temporary in MP; unedited cards may use Permanent.</summary>
    internal static bool HasStagedEdits(CardModel canonicalCard, AddCardRequest request) {
        var staged = ResolveStagedTemplate(request);
        if (staged == null) return false;
        return staged.DiffersFrom(CardEditActions.CaptureTemplate(canonicalCard));
    }

    internal static EffectDuration ResolveAddDuration(AddCardRequest request, CardModel canonicalCard) {
        if (!MpCheatSession.InMultiplayerRun)
            return request.Duration;
        if (request.Duration == EffectDuration.Permanent && HasStagedEdits(canonicalCard, request))
            return EffectDuration.Temporary;
        return request.Duration;
    }

    /// <summary>Executes add-card from multiplayer sync (all peers must call with identical parameters).</summary>
    internal static Task ExecuteAddFromMpSync(RunState state, Player player, CardModel canonicalCard,
        AddCardRequest request, CardPreviewStyle? upgradePreviewStyle = null) =>
        ExecuteAddAsync(state, player, canonicalCard, request, upgradePreviewStyle, mpSync: true);

    internal static CardModel? FindCardById(string cardId) {
        if (string.IsNullOrEmpty(cardId)) return null;
        return ModelDb.AllCards
            .FirstOrDefault(c => string.Equals(((AbstractModel)c).Id.Entry, cardId, StringComparison.OrdinalIgnoreCase));
    }

    internal static Player? FindPlayerByNetId(ulong netId) =>
        RunManager.Instance?.DebugOnlyGetState()?.Players.FirstOrDefault(p => p.NetId == netId);

    internal static bool TryBuildRemovePayload(
        Player player,
        CardModel card,
        CardTarget target,
        bool removeFromRunState,
        out MpCheatRemoveCardPayload payload,
        out string error) {
        payload = new MpCheatRemoveCardPayload();
        error = "";
        if (card == null) {
            error = "card not found";
            return false;
        }

        var pileIndex = GetPileIndex(player, target, card);
        if (pileIndex < 0) {
            error = "card not in target pile";
            return false;
        }

        payload = new MpCheatRemoveCardPayload {
            CardId = ((AbstractModel)card).Id.Entry,
            TargetPlayerNetId = player.NetId,
            Target = (int)target,
            PileIndex = pileIndex,
            RemoveFromRunState = removeFromRunState,
        };
        return true;
    }

    internal static bool TryValidateRemove(
        RunState state,
        Player player,
        CardModel card,
        CardTarget target,
        bool removeFromRunState,
        out string error) {
        error = "";
        if (card == null) {
            error = "card not found";
            return false;
        }
        if (player == null) {
            error = "player not found";
            return false;
        }
        if (target != CardTarget.Deck && player.Creature?.CombatState == null) {
            error = "not in combat";
            return false;
        }
        if (GetPileIndex(player, target, card) < 0) {
            error = "card not in target pile";
            return false;
        }
        return true;
    }

    internal static CardModel? ResolveCardFromRemovePayload(Player player, MpCheatRemoveCardPayload payload) {
        var cards = GetCardsForTarget(player, (CardTarget)payload.Target);
        if (payload.PileIndex < 0 || payload.PileIndex >= cards.Count)
            return null;
        var card = cards[payload.PileIndex];
        if (!string.Equals(((AbstractModel)card).Id.Entry, payload.CardId, StringComparison.OrdinalIgnoreCase))
            return null;
        return card;
    }

    internal static bool TryBuildEditPayload(
        Player player,
        CardModel card,
        CardTarget target,
        CardEditTemplate template,
        out MpCheatEditCardPayload payload,
        out string error) {
        payload = new MpCheatEditCardPayload();
        error = "";
        if (card == null) {
            error = "card not found";
            return false;
        }
        if (!template.HasAnyPatch()) {
            error = "empty edit patch";
            return false;
        }

        var pileIndex = GetPileIndex(player, target, card);
        if (pileIndex < 0) {
            error = "card not in target pile";
            return false;
        }

        payload = new MpCheatEditCardPayload {
            CardId = ((AbstractModel)card).Id.Entry,
            TargetPlayerNetId = player.NetId,
            Target = (int)target,
            PileIndex = pileIndex,
            TemplateJson = MpCheatNetJson.SerializeEditTemplate(template),
        };
        return true;
    }

    internal static bool TryValidateEdit(
        RunState state,
        Player player,
        CardModel card,
        CardTarget target,
        CardEditTemplate template,
        out string error) {
        error = "";
        if (card == null) {
            error = "card not found";
            return false;
        }
        if (player == null) {
            error = "player not found";
            return false;
        }
        if (!template.HasAnyPatch()) {
            error = "empty edit patch";
            return false;
        }
        if (target != CardTarget.Deck && player.Creature?.CombatState == null) {
            error = "not in combat";
            return false;
        }
        if (GetPileIndex(player, target, card) < 0) {
            error = "card not in target pile";
            return false;
        }
        return true;
    }

    internal static CardModel? ResolveCardFromEditPayload(Player player, MpCheatEditCardPayload payload) =>
        ResolveCardFromRemovePayload(player, new MpCheatRemoveCardPayload {
            CardId = payload.CardId,
            TargetPlayerNetId = payload.TargetPlayerNetId,
            Target = payload.Target,
            PileIndex = payload.PileIndex,
            RemoveFromRunState = false,
        });

    internal static int GetPileIndex(Player player, CardTarget target, CardModel card) {
        var cards = GetCardsForTarget(player, target);
        for (var i = 0; i < cards.Count; i++) {
            if (ReferenceEquals(cards[i], card))
                return i;
        }
        var cardId = ((AbstractModel)card).Id.Entry;
        var upgrade = 0;
        try { upgrade = card.CurrentUpgradeLevel; } catch { }
        for (var i = 0; i < cards.Count; i++) {
            var c = cards[i];
            if (!string.Equals(((AbstractModel)c).Id.Entry, cardId, StringComparison.OrdinalIgnoreCase))
                continue;
            try {
                if (c.CurrentUpgradeLevel == upgrade)
                    return i;
            }
            catch {
                return i;
            }
        }
        return -1;
    }

    internal static async Task ExecuteRemoveFromMpSync(
        RunState state,
        Player player,
        CardModel card,
        CardTarget target,
        bool removeFromRunState) {
        if (MpCheatSession.InMultiplayerRun) {
            await ExecuteRemoveAsync(state, player, card, target, removeFromRunState, mpSync: true);
            return;
        }
        await ExecuteRemoveAsync(state, player, card, target, removeFromRunState, mpSync: false);
    }

    private static async Task ExecuteRemoveAsync(
        RunState state,
        Player player,
        CardModel card,
        CardTarget target,
        bool removeFromRunState,
        bool mpSync) {
        if (MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn(
                $"CardActions: Cannot remove {((AbstractModel)card).Id.Entry} locally in multiplayer — use host remove-card sync.");
            return;
        }

        if (target == CardTarget.Deck) {
            try {
                await CardPileCmd.RemoveFromDeck((IReadOnlyList<CardModel>)new[] { card }, true);
            }
            catch {
                card.RemoveFromState();
                if (state.ContainsCard(card))
                    state.RemoveCard(card);
            }
        }
        else {
            await CardPileCmd.RemoveFromCombat(new[] { card });
            if (removeFromRunState && state.ContainsCard(card))
                state.RemoveCard(card);
        }

        var who = MpCheatPlayerLabels.FormatLogLabel(player);
        MainFile.Logger.Info(
            $"CardActions: Removed {((AbstractModel)card).Id.Entry} from {who} pile {target}");
    }

    public static async Task RemoveCards(RunState state, Player player) {
        await Task.Yield();

        var target = KitLibState.CardTarget;
        var duration = KitLibState.EffectDuration;

        var cards = CollectCardsForTarget(player, target);
        if (cards.Count == 0) {
            MainFile.Logger.Info("CardActions: No cards to remove.");
            return;
        }

        var prefs = new CardSelectorPrefs(CardSelectorPrefs.RemoveSelectionPrompt, 1, cards.Count) {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        var screen = NDeckCardSelectScreen.Create((IReadOnlyList<CardModel>)cards, prefs);
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return;

        overlayStack.Push((IOverlayScreen)screen);
        var selected = (await screen.CardsSelected())
            .Where(c => c != null).Distinct().ToList();

        if (selected.Count == 0) return;

        if (MpCheatSession.InMultiplayerRun) {
            MainFile.Logger.Warn(
                "CardActions: Bulk remove in multiplayer is not synced — remove cards from the card browser.");
            return;
        }

        if (target == CardTarget.Deck) {
            // RemoveFromDeck handles preview animation + permanent state removal.
            try {
                await CardPileCmd.RemoveFromDeck((IReadOnlyList<CardModel>)selected, true);
            }
            catch {
                foreach (var card in selected) {
                    card.RemoveFromState();
                    if (state.ContainsCard(card))
                        state.RemoveCard(card);
                }
            }
        }
        else {
            // Combat piles (Hand / DrawPile / DiscardPile):
            // RemoveFromCombat handles the hand-UI visual update and animation.
            await CardPileCmd.RemoveFromCombat(selected);

            if (duration == EffectDuration.Permanent) {
                // Also purge from the permanent deck.
                foreach (var card in selected) {
                    if (state.ContainsCard(card))
                        state.RemoveCard(card);
                }
            }
        }

        MainFile.Logger.Info($"CardActions: Removed {selected.Count} card(s) ({duration})");
    }

    public static async Task UpgradeCards(Player player) {
        await Task.Yield();

        var target = KitLibState.CardTarget;
        var cards = CollectCardsForTarget(player, target);
        var upgradable = cards
            .Where(c => c.IsUpgradable)
            .ToList();

        if (upgradable.Count == 0) {
            MainFile.Logger.Info("CardActions: No upgradable cards found.");
            return;
        }

        var prefs = new CardSelectorPrefs(CardSelectorPrefs.UpgradeSelectionPrompt, 1, upgradable.Count) {
            Cancelable = true,
            RequireManualConfirmation = true
        };

        var screen = NDeckCardSelectScreen.Create((IReadOnlyList<CardModel>)upgradable, prefs);
        var overlayStack = NOverlayStack.Instance;
        if (overlayStack == null) return;

        overlayStack.Push((IOverlayScreen)screen);
        var selected = (await screen.CardsSelected())
            .Where(c => c != null).Distinct().ToList();

        if (selected.Count == 0) return;

        // NDeckCardSelectScreen removes itself from NOverlayStack on confirm/cancel before
        // CardsSelected completes; do not Remove again here (double AfterOverlayClosed crashes).
        await Task.Yield(); // let scene tree settle after overlay teardown

        CardCmd.Upgrade(selected, CardPreviewStyle.HorizontalLayout);

        // CardCmd.Upgrade only creates NCardUpgradeVfx for PileType.Deck cards.
        // For combat piles (DrawPile, Hand, DiscardPile), manually add the VFX.
        var previewContainer = NRun.Instance?.GlobalUi?.CardPreviewContainer;
        if (previewContainer != null) {
            foreach (var card in selected) {
                if (card.Pile?.Type != PileType.Deck)
                    previewContainer.AddChildSafely(NCardUpgradeVfx.Create(card));
            }
        }

        MainFile.Logger.Info($"CardActions: Upgraded {selected.Count} card(s)");
    }

    /// <summary>Core add implementation used by <see cref="AddCardBuilder.RunAsync"/>.</summary>
    /// <param name="request">Where to add, temp vs permanent deck mirror, and post-create upgrade steps.</param>
    /// <param name="upgradePreviewStyle">When set, each upgrade step uses <c>CardCmd.Upgrade(new[] { instance }, style)</c>; otherwise the single-card overload.</param>
    private static async Task ExecuteAddAsync(RunState state, Player player, CardModel canonicalCard, AddCardRequest request,
        CardPreviewStyle? upgradePreviewStyle = null, bool mpSync = false) {
        if (Multiplayer.Cheat.MpCheatSession.InMultiplayerRun && !mpSync) {
            MainFile.Logger.Warn(
                $"CardActions: Cannot add {canonicalCard.Id.Entry} locally in multiplayer — use host add-card sync.");
            return;
        }

        var target = request.Target;
        var duration = ResolveAddDuration(request, canonicalCard);
        var upgradeLevelsToApply = request.UpgradeLevelsToApply;

        if (target == CardTarget.Deck) {
            var card = state.CreateCard(canonicalCard.CanonicalInstance, player);
            ApplyUpgradeSteps(card, upgradeLevelsToApply, upgradePreviewStyle);
            ApplyStagedTemplateIfAny(card, request);
            var result = await CardPileCmd.Add(card, PileType.Deck);
            CardCmd.PreviewCardPileAdd(result);
        }
        else {
            var combatState = player.Creature.CombatState;
            if (combatState == null) {
                MainFile.Logger.Info("CardActions: Cannot add to combat pile — not in combat.");
                return;
            }

            var pileType = target switch {
                CardTarget.Hand => PileType.Hand,
                CardTarget.DrawPile => PileType.Draw,
                CardTarget.DiscardPile => PileType.Discard,
                CardTarget.ExhaustPile => PileType.Exhaust,
                _ => PileType.Draw
            };

            var combatCard = combatState.CreateCard(canonicalCard.CanonicalInstance, player);
            ApplyUpgradeSteps(combatCard, upgradeLevelsToApply, upgradePreviewStyle);
            ApplyStagedTemplateIfAny(combatCard, request);
#if STS2_BETA
            await CardPileCmd.AddGeneratedCardToCombat(combatCard, pileType, player);
#else
            await CardPileCmd.AddGeneratedCardToCombat(combatCard, pileType, addedByPlayer: true);
#endif

            // AddGeneratedCardToCombat silently calls AddInternal() for brand-new cards added to
            // Draw/Discard without creating any VFX. The pile-count UI (NCombatCardPile) only
            // updates via CardAddFinished, which is normally fired by the fly animation (NCardFlyVfx /
            // NCardFlyShuffleVfx). For the silent path we must fire it manually.
            if (pileType is PileType.Draw or PileType.Discard or PileType.Exhaust)
                combatCard.Pile?.InvokeCardAddFinished();

            if (duration == EffectDuration.Permanent) {
                // Separate deck instance from combat instance (same as vanilla “also write to deck” semantics).
                var deckCard = state.CreateCard(canonicalCard.CanonicalInstance, player);
                ApplyUpgradeSteps(deckCard, upgradeLevelsToApply, upgradePreviewStyle);
                ApplyStagedTemplateIfAny(deckCard, request);
                await CardPileCmd.Add(deckCard, PileType.Deck, skipVisuals: true);
            }
        }

        var who = Multiplayer.Cheat.MpCheatPlayerLabels.FormatLogLabel(player);
        MainFile.Logger.Info(
            $"CardActions: Added {canonicalCard.Id.Entry} for {who} to {target} ({duration})");
    }

    internal static CardEditTemplate? ResolveStagedTemplate(AddCardRequest request) {
        if (request.StagedTemplate?.HasAnyPatch() == true)
            return request.StagedTemplate;
        if (request.CustomBaseCost.HasValue)
            return new CardEditTemplate { BaseCost = request.CustomBaseCost };
        return null;
    }

    private static void ApplyStagedTemplateIfAny(CardModel instance, AddCardRequest request) {
        var template = ResolveStagedTemplate(request);
        if (template == null) return;
        CardEditActions.ApplyTemplate(instance, template);
    }

    private static void ApplyUpgradeSteps(CardModel instance, int count, CardPreviewStyle? previewStyle = null) {
        for (var i = 0; i < count; i++) {
            if (previewStyle.HasValue)
                CardCmd.Upgrade(new[] { instance }, previewStyle.Value);
            else
                CardCmd.Upgrade(instance);
        }
    }

    public static bool HasRelevantCards(Player player, CardTarget target, CardMode mode) {
        if (mode == CardMode.Add)
            return target == CardTarget.Deck || player.PlayerCombatState != null;
        var cards = CollectCardsForTarget(player, target);
        if (mode == CardMode.Upgrade)
            return cards.Any(c => c.IsUpgradable);
        return cards.Count > 0;
    }

    public static List<CardModel> GetCardsForTarget(Player player, CardTarget target) {
        return CollectCardsForTarget(player, target);
    }

    private static List<CardModel> CollectCardsForTarget(Player player, CardTarget target) {
        if (target == CardTarget.Deck)
            return player.Deck.Cards.ToList();

        var combatState = player.PlayerCombatState;
        if (combatState == null) return new List<CardModel>();

        return target switch {
            CardTarget.DrawPile => combatState.DrawPile?.Cards.ToList() ?? new List<CardModel>(),
            CardTarget.Hand => combatState.Hand?.Cards.ToList() ?? new List<CardModel>(),
            CardTarget.DiscardPile => combatState.DiscardPile?.Cards.ToList() ?? new List<CardModel>(),
            CardTarget.ExhaustPile => combatState.ExhaustPile?.Cards.ToList() ?? new List<CardModel>(),
            _ => new List<CardModel>()
        };
    }
}
