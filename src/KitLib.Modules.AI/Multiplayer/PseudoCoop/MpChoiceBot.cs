using System.Text.Json.Nodes;
using KitLib.AI;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2;
using KitLib.AI.Sts2.Snapshots;
using KitLib.Multiplayer.SyncBot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Remote / SP-companion choices via strategy scorers (not fixed index 0).</summary>
internal static class MpChoiceBot {
    public static NetPlayerChoiceResult Decide(Player player, uint choiceId) {
        if (MpPendingPlayerChoice.TryConsume(player.NetId, choiceId, out var options)
            && options.HasFlag(PlayerChoiceOptions.CancelPlayCardActions))
            return CombatHandChoice(player);

        return ScoreChoice(player);
    }

    static NetPlayerChoiceResult ScoreChoice(Player player) {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null)
            return MpCheatSyncBot.DefaultIndexChoice();

        var priorNetId = AiHostContext.ActiveNetId;
        AiHostContext.ActiveNetId = player.NetId;
        try {
            if (!TryInferChoicePhase(state, player, out var phase, out var snapshot))
                return HeuristicFallback(snapshot ?? GameSnapshot.Capture(state, player));

            var strategy = StrategyResolver.Resolve(player);
            var action = strategy.DecideAsync(snapshot, phase).GetAwaiter().GetResult();
            return FromGameAction(action, snapshot);
        }
        finally {
            AiHostContext.ActiveNetId = priorNetId;
        }
    }

    static bool TryInferChoicePhase(
        RunState state,
        Player player,
        out GamePhase phase,
        out JsonObject snapshot) {
        var cardSnap = GameSnapshot.Capture(state, player, GamePhase.CardReward);
        if (cardSnap["offeredCards"]?.AsArray() is { Count: > 0 }) {
            phase = GamePhase.CardReward;
            snapshot = cardSnap;
            return true;
        }

        var relicSnap = GameSnapshot.Capture(state, player, GamePhase.RelicSelection);
        if (relicSnap["offeredRelics"]?.AsArray() is { Count: > 0 }) {
            phase = GamePhase.RelicSelection;
            snapshot = relicSnap;
            return true;
        }

        var eventSnap = GameSnapshot.Capture(state, player, GamePhase.EventChoice);
        if (eventSnap["eventOptions"]?.AsArray() is { Count: > 0 }) {
            phase = GamePhase.EventChoice;
            snapshot = eventSnap;
            return true;
        }

        phase = GamePhase.None;
        snapshot = null!;
        return false;
    }

    static NetPlayerChoiceResult HeuristicFallback(JsonObject snapshot) {
        var deck = snapshot["deck"]?.AsArray();
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 1;
        return deck != null && floor <= 3
            ? IndexChoice(0)
            : MpCheatSyncBot.DefaultIndexChoice();
    }

    static NetPlayerChoiceResult FromGameAction(GameAction action, JsonObject snapshot) {
        return action.Type switch {
            ActionType.SkipCardReward => IndexChoice(SkipCardRewardIndex(snapshot)),
            ActionType.PickCardReward or ActionType.PickRelic or ActionType.SelectEventChoice
                or ActionType.PurchaseShopItem or ActionType.Rest or ActionType.UpgradeCard
                => IndexChoice(action.TargetIndex >= 0 ? action.TargetIndex : 0),
            _ => IndexChoice(0),
        };
    }

    static int SkipCardRewardIndex(JsonObject snapshot) {
        var offered = snapshot["offeredCards"]?.AsArray();
        return offered?.Count ?? 0;
    }

    static NetPlayerChoiceResult CombatHandChoice(Player player) {
        var hand = PileType.Hand.GetPile(player).Cards;
        if (hand.Count == 0)
            return MpCheatSyncBot.DefaultIndexChoice();

        var priorNetId = AiHostContext.ActiveNetId;
        AiHostContext.ActiveNetId = player.NetId;
        try {
            var selector = new AiCombatCardSelector(AiPlayServices.StateProvider);
            var picked = selector.GetSelectedCards(hand, 1, 1).GetAwaiter().GetResult().ToList();
            if (picked.Count == 0)
                picked = [hand[0]];

            return new NetPlayerChoiceResult {
                type = PlayerChoiceType.CombatCard,
                combatCards = picked.Select(NetCombatCard.FromModel).ToList(),
            };
        }
        finally {
            AiHostContext.ActiveNetId = priorNetId;
        }
    }

    static NetPlayerChoiceResult IndexChoice(int index) => new() {
        type = PlayerChoiceType.Index,
        indexes = [index],
    };
}
