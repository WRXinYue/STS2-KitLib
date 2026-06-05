using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Combat;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.AutoPlay.Strategies;

/// <summary>
/// Deck-plan-aware strategy for solo A10-capable play.
/// Macro phases delegate to dedicated scorers; combat uses beam-only search (no lethal fast-path).
/// </summary>
public sealed class StrongStrategy : IDecisionMaker {
    public Task<GameAction> DecideAsync(JsonObject snapshot, GamePhase phase) {
        var action = phase switch {
            GamePhase.Combat => DecideCombat(snapshot),
            GamePhase.MapSelection => MapScorer.PickBest(snapshot),
            GamePhase.CardReward => DeckSelectScorer.PickBest(snapshot),
            GamePhase.EventChoice => EventChoiceScorer.PickBest(snapshot),
            GamePhase.Shop => ShopScorer.PickBest(snapshot),
            GamePhase.RestSite => RestScorer.PickBest(snapshot),
            GamePhase.RewardScreen => RewardScorer.PickBest(snapshot),
            GamePhase.RelicSelection => RelicScorer.PickBest(snapshot),
            GamePhase.PostCombatTransition => new GameAction { Type = ActionType.Proceed, Reason = "Advance post-combat screen" },
            GamePhase.TreasureRoom => new GameAction { Type = ActionType.HandleTreasureRoom, Reason = "Open chest and collect" },
            GamePhase.Unknown => new GameAction { Type = ActionType.AdvanceOverlay, Reason = "Unrecognized overlay" },
            _ => new GameAction { Type = ActionType.Wait, Reason = "Idle phase" },
        };
        return Task.FromResult(action);
    }

    static GameAction DecideCombat(JsonObject snapshot) {
        var emergency = PotionScorer.TryEmergencyPotion(snapshot);
        if (emergency != null)
            return emergency;

        var move = CombatSearch.PickBestMove(snapshot);
        if (move != null && move.Type != ActionType.EndTurn)
            return move;

        var fallback = PotionScorer.TryFallbackPotion(snapshot);
        if (fallback != null)
            return fallback;

        return move
            ?? CombatScorer.PickBestCombatMove(snapshot)
            ?? new GameAction { Type = ActionType.EndTurn, Reason = "No combat move" };
    }
}
