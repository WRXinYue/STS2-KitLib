using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Combat;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.AutoPlay.Strategies;

/// <summary>
/// Deck-plan-aware strategy for solo A10-capable play.
/// Macro phases delegate to dedicated scorers; combat uses shallow search + lethal check.
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
        var potion = PotionScorer.TryUsePotion(snapshot);
        if (potion != null)
            return potion;

        return CombatSearch.PickBestMove(snapshot)
            ?? CombatScorer.PickBestCombatMove(snapshot)
            ?? new GameAction { Type = ActionType.EndTurn, Reason = "No combat move" };
    }
}
