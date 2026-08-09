using System;
using System.Threading.Tasks;
using KitLib.Abstractions.Host;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Cheat;

/// <summary>Wires <see cref="KitLibRunInventoryApi"/> to Card/Relic/Potion actions (SP + MP host/client sync).</summary>
internal static class RunInventoryApiBridge {
    public static void Wire() {
        KitLibRunInventoryApi.IsAvailable = () => true;
        KitLibRunInventoryApi.TryAddCard = TryAddCard;
        KitLibRunInventoryApi.TryRemoveCard = TryRemoveCard;
        KitLibRunInventoryApi.TryAddRelic = TryAddRelic;
        KitLibRunInventoryApi.TryRemoveRelic = TryRemoveRelic;
        KitLibRunInventoryApi.TryAddPotion = TryAddPotion;
        KitLibRunInventoryApi.TryDiscardPotionAtSlot = TryDiscardPotionAtSlot;
    }

    static async Task<KitLibRunItemResult> TryAddCard(KitLibAddCardRequest request) {
        if (!TryResolveRunPlayer(request.TargetPlayerNetId, out var state, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (string.IsNullOrWhiteSpace(request.CardId))
            return KitLibRunItemResult.Fail("Missing or invalid card id.");

        var card = CardActions.FindCardById(request.CardId.Trim());
        if (card == null)
            return KitLibRunItemResult.Fail($"Card not found: '{request.CardId}'.");

        if (!TryMapPile(request.Pile, out var target, out var pileError))
            return KitLibRunItemResult.Fail(pileError);
        if (!TryMapDuration(request.Duration, out var duration, out var durationError))
            return KitLibRunItemResult.Fail(durationError);

        var addRequest = new AddCardRequest {
            Target = target,
            Duration = duration,
            UpgradeLevelsToApply = Math.Max(0, request.UpgradeLevels),
        };
        if (!CardActions.TryValidateAdd(state, player, card, addRequest, out var validateError))
            return KitLibRunItemResult.Fail(validateError);

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = MpCheatSession.IsHost
                ? await MpCheatCardAddCoordinator.TryHostAddCardWithResultAsync(
                    state, player, card, addRequest, upgradePreviewStyle: null)
                : await MpCheatCardAddCoordinator.TryClientRequestAddCardWithResultAsync(
                    state, player, card, addRequest, upgradePreviewStyle: null);
            return ok
                ? KitLibRunItemResult.Success(((AbstractModel)card).Id.Entry)
                : KitLibRunItemResult.Fail(msg);
        }

        await CardActions.Add(state, player, card)
            .Target(target)
            .Duration(duration)
            .UpgradeLevels(addRequest.UpgradeLevelsToApply)
            .RunAsync();

        return KitLibRunItemResult.Success(((AbstractModel)card).Id.Entry);
    }

    static async Task<KitLibRunItemResult> TryRemoveCard(KitLibRemoveCardRequest request) {
        if (!TryResolveRunPlayer(request.TargetPlayerNetId, out var state, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (!TryMapPile(request.Pile, out var target, out var pileError))
            return KitLibRunItemResult.Fail(pileError);

        var cards = CardActions.GetCardsForTarget(player, target);
        var card = ResolveCardInPile(cards, request.CardId, request.PileIndex, out var cardError);
        if (card == null)
            return KitLibRunItemResult.Fail(cardError);

        var removeFromRunState = target == CardTarget.Deck
            || (request.RemoveFromRun && state.ContainsCard(card));
        if (!CardActions.TryValidateRemove(state, player, card, target, removeFromRunState, out var validateError))
            return KitLibRunItemResult.Fail(validateError);

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = MpCheatSession.IsHost
                ? await MpCheatCardRemoveCoordinator.TryHostRemoveCardWithResultAsync(
                    state, player, card, target, removeFromRunState)
                : await MpCheatCardRemoveCoordinator.TryClientRequestRemoveCardWithResultAsync(
                    state, player, card, target, removeFromRunState);
            return ok
                ? KitLibRunItemResult.Success(((AbstractModel)card).Id.Entry)
                : KitLibRunItemResult.Fail(msg);
        }

        await CardActions.ExecuteRemoveFromMpSync(state, player, card, target, removeFromRunState);
        return KitLibRunItemResult.Success(((AbstractModel)card).Id.Entry);
    }

    static async Task<KitLibRunItemResult> TryAddRelic(KitLibRelicRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (string.IsNullOrWhiteSpace(request.RelicId))
            return KitLibRunItemResult.Fail("Missing or invalid relic id.");

        var relic = RelicActions.FindRelicById(request.RelicId.Trim());
        if (relic == null)
            return KitLibRunItemResult.Fail($"Relic not found: '{request.RelicId}'.");

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatRelicCoordinator.TryAddWithResultAsync(player, relic);
            return ok
                ? KitLibRunItemResult.Success(((AbstractModel)relic).Id.Entry)
                : KitLibRunItemResult.Fail(msg);
        }

        await RelicActions.AddRelic(relic, player);
        return KitLibRunItemResult.Success(((AbstractModel)relic).Id.Entry);
    }

    static async Task<KitLibRunItemResult> TryRemoveRelic(KitLibRelicRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (string.IsNullOrWhiteSpace(request.RelicId))
            return KitLibRunItemResult.Fail("Missing or invalid relic id.");

        var relicId = request.RelicId.Trim();
        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatRelicCoordinator.TryRemoveWithResultAsync(player, relicId);
            return ok ? KitLibRunItemResult.Success(relicId) : KitLibRunItemResult.Fail(msg);
        }

        return await RelicActions.RemoveRelicById(player, relicId);
    }

    static async Task<KitLibRunItemResult> TryAddPotion(KitLibPotionAddRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);
        if (string.IsNullOrWhiteSpace(request.PotionId))
            return KitLibRunItemResult.Fail("Missing or invalid potion id.");

        var potion = PotionActions.FindPotionById(request.PotionId.Trim());
        if (potion == null)
            return KitLibRunItemResult.Fail($"Potion not found: '{request.PotionId}'.");

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatPotionCoordinator.TryAddWithResultAsync(player, potion);
            return ok
                ? KitLibRunItemResult.Success(((AbstractModel)potion).Id.Entry)
                : KitLibRunItemResult.Fail(msg);
        }

        await PotionActions.AddPotion(player, potion);
        return KitLibRunItemResult.Success(((AbstractModel)potion).Id.Entry);
    }

    static async Task<KitLibRunItemResult> TryDiscardPotionAtSlot(KitLibPotionDiscardRequest request) {
        if (!TryResolvePlayer(request.TargetPlayerNetId, out var player, out var resolveError))
            return KitLibRunItemResult.Fail(resolveError);

        if (MpCheatSession.InMultiplayerRun) {
            var (ok, msg) = await MpCheatPotionCoordinator.TryDiscardWithResultAsync(player, request.SlotIndex);
            return ok ? KitLibRunItemResult.Success() : KitLibRunItemResult.Fail(msg);
        }

        return await PotionActions.DiscardPotionAtSlot(player, request.SlotIndex);
    }

    static bool TryResolveRunPlayer(
        ulong? targetPlayerNetId,
        out RunState state,
        out Player player,
        out string error) {
        state = null!;
        player = null!;
        error = "";
        if (!RunContext.TryGetRunAndPlayer(out state, out var local) || local == null) {
            error = "No active run.";
            return false;
        }

        if (!targetPlayerNetId.HasValue || targetPlayerNetId.Value == 0 || targetPlayerNetId.Value == local.NetId) {
            player = local;
            return true;
        }

        var found = CardActions.FindPlayerByNetId(targetPlayerNetId.Value);
        if (found == null) {
            error = "Target player not found.";
            return false;
        }

        player = found;
        return true;
    }

    static bool TryResolvePlayer(ulong? targetPlayerNetId, out Player player, out string error) =>
        TryResolveRunPlayer(targetPlayerNetId, out _, out player, out error);

    static bool TryMapPile(KitLibCardPile pile, out CardTarget target, out string error) {
        error = "";
        switch (pile) {
            case KitLibCardPile.Deck:
                target = CardTarget.Deck;
                return true;
            case KitLibCardPile.Hand:
                target = CardTarget.Hand;
                return true;
            case KitLibCardPile.Draw:
                target = CardTarget.DrawPile;
                return true;
            case KitLibCardPile.Discard:
                target = CardTarget.DiscardPile;
                return true;
            case KitLibCardPile.Exhaust:
                target = CardTarget.ExhaustPile;
                return true;
            default:
                target = CardTarget.Hand;
                error = $"Unknown card pile '{pile}'.";
                return false;
        }
    }

    static bool TryMapDuration(KitLibCardDuration duration, out EffectDuration mapped, out string error) {
        error = "";
        switch (duration) {
            case KitLibCardDuration.Temporary:
                mapped = EffectDuration.Temporary;
                return true;
            case KitLibCardDuration.Permanent:
                mapped = EffectDuration.Permanent;
                return true;
            default:
                mapped = EffectDuration.Permanent;
                error = $"Unknown card duration '{duration}'.";
                return false;
        }
    }

    static CardModel? ResolveCardInPile(
        System.Collections.Generic.IReadOnlyList<CardModel> cards,
        string? cardId,
        int? pileIndex,
        out string error) {
        error = "";
        if (pileIndex.HasValue) {
            if (pileIndex.Value < 0 || pileIndex.Value >= cards.Count) {
                error = $"pile index {pileIndex.Value} out of range (count {cards.Count}).";
                return null;
            }
            return cards[pileIndex.Value];
        }

        if (string.IsNullOrWhiteSpace(cardId)) {
            error = "Provide card id or pile index.";
            return null;
        }

        for (var i = 0; i < cards.Count; i++) {
            if (string.Equals(((AbstractModel)cards[i]).Id.Entry, cardId.Trim(), StringComparison.OrdinalIgnoreCase))
                return cards[i];
        }

        error = $"Card '{cardId}' not found in target pile.";
        return null;
    }
}
