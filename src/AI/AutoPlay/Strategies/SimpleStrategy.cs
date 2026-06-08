using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.AutoPlay.Strategies;

/// <summary>
/// Basic rule-based strategy for autonomous play.
///
/// Combat heuristics:
///   1. Low HP → play block/skill cards first.
///   2. Play highest-cost affordable attack.
///   3. Play any remaining affordable card.
///   4. End turn when nothing is playable.
///
/// Map: pick first available node.
/// Cards: pick in early game, skip in late game.
/// Rest: rest if HP &lt; 60%, otherwise upgrade.
/// </summary>
public sealed class SimpleStrategy : IDecisionMaker
{
    public Task<GameAction> DecideAsync(JsonObject snapshot, GamePhase phase)
    {
        var action = phase switch
        {
            GamePhase.Combat => DecideCombat(snapshot),
            GamePhase.MapSelection => new GameAction { Type = ActionType.SelectMapNode, TargetIndex = 0, Reason = "First available node" },
            GamePhase.CardReward => DecideCardReward(snapshot),
            GamePhase.EventChoice => EventChoiceScorer.PickBest(snapshot),
            GamePhase.Shop => DecideShop(snapshot),
            GamePhase.RestSite => DecideRest(snapshot),
            GamePhase.RewardScreen => RewardScorer.PickBest(snapshot),
            GamePhase.RelicSelection => RelicScorer.PickBest(snapshot),
            GamePhase.PostCombatTransition => new GameAction { Type = ActionType.Proceed, Reason = "Advance post-combat screen" },
            GamePhase.TreasureRoom => new GameAction { Type = ActionType.HandleTreasureRoom, Reason = "Open chest and collect" },
            GamePhase.Unknown => new GameAction { Type = ActionType.AdvanceOverlay, Reason = "Unrecognized overlay" },
            _ => new GameAction { Type = ActionType.Wait, Reason = "Idle phase" },
        };
        return Task.FromResult(action);
    }

    private static GameAction DecideCombat(JsonObject snapshot)
    {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null)
            return new GameAction { Type = ActionType.EndTurn, Reason = "No combat state" };

        // Win is processing but play phase still active — don't spam playable skills.
        if (!HasAliveEnemy(combat))
            return new GameAction { Type = ActionType.EndTurn, Reason = "No living enemies — end turn" };

        var hand = combat["hand"]?.AsArray();
        if (hand == null || hand.Count == 0)
            return new GameAction { Type = ActionType.EndTurn, Reason = "Empty hand" };

        var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        bool lowHp = hp < maxHp * 0.4;

        // Low HP: prioritize skills (likely block)
        if (lowHp)
        {
            for (int i = 0; i < hand.Count; i++)
            {
                var card = hand[i]!.AsObject();
                var cost = card["cost"]?.GetValue<int>() ?? 99;
                var type = card["cardType"]?.GetValue<string>() ?? "";
                if (type.Contains("Skill") && cost <= energy)
                    return new GameAction
                    {
                        Type = ActionType.PlayCard,
                        TargetIndex = i,
                        SecondaryIndex = 0,
                        Reason = $"Low HP — play skill [{card["name"]}]",
                    };
            }
        }

        // Best affordable attack
        int bestIdx = -1, bestCost = -1;
        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i]!.AsObject();
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            var type = card["cardType"]?.GetValue<string>() ?? "";
            var targetType = card["targetType"]?.GetValue<string>() ?? "";
            if (!type.Contains("Attack") || cost > energy) continue;
            if (targetType is "AnyAlly" or "AnyPlayer" or "Self") continue;

            if (cost > bestCost)
            {
                bestIdx = i;
                bestCost = cost;
            }
        }
        if (bestIdx >= 0)
            return new GameAction
            {
                Type = ActionType.PlayCard, TargetIndex = bestIdx, SecondaryIndex = 0,
                Reason = $"Play attack [{hand[bestIdx]!["name"]}]"
            };

        // Any affordable card
        for (int i = 0; i < hand.Count; i++)
        {
            var card = hand[i]!.AsObject();
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            if (cost > energy) continue;

            var targetType = card["targetType"]?.GetValue<string>() ?? "";
            return new GameAction
            {
                Type = ActionType.PlayCard,
                TargetIndex = i,
                SecondaryIndex = targetType.Contains("Enemy") ? 0 : -1,
                Reason = $"Play [{card["name"]}]",
            };
        }

        return new GameAction { Type = ActionType.EndTurn, Reason = "No playable cards" };
    }

    /// <summary>
    /// True if snapshot lists at least one enemy with <c>isAlive: true</c>.
    /// Empty/missing enemy list is treated as no targets (end turn).
    /// </summary>
    private static bool HasAliveEnemy(JsonObject combat)
    {
        var enemies = combat["enemies"]?.AsArray();
        // Snapshot omitted enemy list — don't assume combat is over.
        if (enemies == null)
            return true;
        if (enemies.Count == 0)
            return false;

        foreach (var node in enemies)
        {
            if (node is not JsonObject o) continue;
            if (o["isAlive"]?.GetValue<bool>() == true)
                return true;
        }

        return false;
    }

    private static GameAction DecideCardReward(JsonObject snapshot)
    {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        if (floor < 15)
            return new GameAction { Type = ActionType.PickCardReward, TargetIndex = 0, Reason = "Early game — pick first card" };

        return new GameAction { Type = ActionType.SkipCardReward, Reason = "Late game — keep deck lean" };
    }

    private static GameAction DecideShop(JsonObject snapshot)
    {
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;

        if (gold > 200 && deckSize > 10)
            return new GameAction { Type = ActionType.RemoveCardAtShop, TargetIndex = 0, Reason = "Thin deck" };

        return new GameAction { Type = ActionType.LeaveShop, Reason = "Not enough gold" };
    }

    private static GameAction DecideRest(JsonObject snapshot)
    {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;

        if (hp < maxHp * 0.6)
            return new GameAction { Type = ActionType.Rest, Reason = $"HP {hp}/{maxHp} — resting" };

        return new GameAction { Type = ActionType.UpgradeCard, TargetIndex = 0, Reason = "HP healthy — upgrade" };
    }
}
