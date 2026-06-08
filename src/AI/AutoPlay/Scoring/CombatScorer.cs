using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;

namespace KitLib.AI.AutoPlay.Scoring;

/// <summary>Minimal combat fallback when beam search returns no path. Not used for primary decisions.</summary>
public static class CombatScorer {
    const int EndTurnBaseScore = -10;

    public static GameAction? PickBestCombatMove(JsonObject snapshot) {
        var ranked = ScoreLegalMovesDetailed(snapshot).ToList();
        return ranked.FirstOrDefault()?.Action;
    }

    public static IEnumerable<CombatMoveScore> ScoreLegalMovesDetailed(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null) {
            yield return BuildEndTurn("No combat", EndTurnBaseScore);
            yield break;
        }

        if (!HasAliveEnemy(combat)) {
            yield return BuildEndTurn("No enemies", EndTurnBaseScore);
            yield break;
        }

        var hand = combat["hand"]?.AsArray();
        var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);
        var fatal = IntentCalculator.IsFatalIfUnblocked(snapshot);
        var simState = CombatState.FromSnapshot(snapshot);
        var netIncoming = IntentCalculator.NetDamageAfterBlock(snapshot);

        if (hand != null) {
            for (var i = 0; i < hand.Count; i++) {
                var card = hand[i]?.AsObject();
                if (card == null) continue;
                if (card["canPlay"]?.GetValue<bool>() == false) continue;

                var cost = card["cost"]?.GetValue<int>() ?? 99;
                if (cost > energy) continue;

                var targetType = card["targetType"]?.GetValue<string>() ?? "";
                var profile = CombatCardStats.ResolveProfile(card);
                if (CombatTargetTypes.IsAnyEnemy(targetType)
                    || CombatTargetTypes.AppliesDirectedDebuff(profile)) {
                    foreach (var combatIndex in EnemyIndexResolver.ViableCombatIndices(combat["enemies"]?.AsArray())) {
                        yield return ScoreCard(simState, i, combatIndex, card);
                    }
                }
                else {
                    yield return ScoreCard(simState, i, -1, card);
                }
            }
        }

        yield return BuildEndTurn("End turn", ScoreEndTurn(needsBlock, fatal, netIncoming));
    }

    public static IEnumerable<(GameAction Action, int Score)> ScoreLegalMoves(JsonObject snapshot) {
        foreach (var scored in ScoreLegalMovesDetailed(snapshot))
            yield return (scored.Action, scored.Score);
    }

    static CombatMoveScore ScoreCard(
        CombatState simState,
        int handIndex,
        int targetIndex,
        JsonObject card) {
        var cardId = card["id"]?.GetValue<string>() ?? "";
        var cardName = card["name"]?.GetValue<string>() ?? cardId;
        var isJunk = CombatJunkCard.IsJunkId(cardId, card["rarity"]?.GetValue<string>())
            || string.Equals(card["cardType"]?.GetValue<string>(), "Status", StringComparison.OrdinalIgnoreCase);

        var move = PlayCard(handIndex, targetIndex, card);
        var builder = new ScoreBuilder();

        if (isJunk) {
            builder.Add("junk", -200);
        }
        else {
            var action = new SimCombatAction(SimActionKind.PlayCard, handIndex, targetIndex);
            builder.Add("line", CombatSetupEvaluator.RankPlayAction(simState, action));
        }

        return builder.Build(move with {
            Reason = $"{cardName} fallback={builder.Total}",
        });
    }

    static int ScoreEndTurn(bool needsBlock, bool fatal, int netIncoming) {
        var score = EndTurnBaseScore;
        if (fatal)
            return int.MinValue;
        if (needsBlock && netIncoming > 0)
            score -= 20 + netIncoming;
        return score;
    }

    static CombatMoveScore BuildEndTurn(string reason, int score) {
        var action = new GameAction { Type = ActionType.EndTurn, Reason = reason };
        return new CombatMoveScore(action with { Reason = $"EndTurn fallback={score}" }, score, [$"base:{score}"]);
    }

    static bool HasAliveEnemy(JsonObject combat) {
        var enemies = combat["enemies"]?.AsArray();
        if (enemies == null || enemies.Count == 0) return false;
        return enemies.Any(e => e?["isAlive"]?.GetValue<bool>() != false);
    }

    static GameAction PlayCard(int handIndex, int targetIndex, JsonObject card) => new() {
        Type = ActionType.PlayCard,
        TargetIndex = handIndex,
        SecondaryIndex = targetIndex,
        Reason = card["name"]?.GetValue<string>() ?? card["id"]?.GetValue<string>() ?? "?",
    };
}
