using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.AutoPlay.Strategies;

/// <summary>
/// Deck-plan-aware strategy for solo A10-capable play.
/// Combat: emergency potions (unmodeled heal/block) → beam (simulatable potions) → fallback (non-sim) → card scorer.
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
        try {
            var emergency = PotionScorer.TryEmergencyPotion(snapshot);
            if (emergency != null)
                return emergency;

            var slotClear = PotionScorer.TryProactiveSlotClear(snapshot);
            if (slotClear != null)
                return slotClear;

            var move = CombatSearch.PickBestMove(snapshot);
            if (move != null && move.Type != ActionType.EndTurn)
                return move;

            if (move?.Type == ActionType.EndTurn && CombatCardCost.HasAffordablePlay(CombatState.FromSnapshot(snapshot))) {
                var scorerMove = CombatScorer.PickBestCombatMove(snapshot);
                if (scorerMove is { Type: not ActionType.EndTurn })
                    return scorerMove;
            }

            var fallback = PotionScorer.TryFallbackPotion(snapshot);
            if (fallback != null)
                return fallback;

            return move
                ?? CombatScorer.PickBestCombatMove(snapshot)
                ?? new GameAction { Type = ActionType.EndTurn, Reason = "No combat move" };
        }
        catch (Exception ex) {
            return CombatScorer.PickBestCombatMove(snapshot)
                ?? new GameAction { Type = ActionType.EndTurn, Reason = $"Combat planner error: {ex.Message}" };
        }
    }
}
