using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

public static class CombatSearch {
    const int TimeBudgetMs = 80;
    const int MaxDepth = 2;

    public static GameAction? PickBestMove(JsonObject snapshot) {
        if (LethalChecker.CanLethal(snapshot, out var lethalTarget)) {
            var lethalMove = FindLethalMove(snapshot, lethalTarget);
            if (lethalMove != null)
                return lethalMove with { Reason = $"Lethal on enemy {lethalTarget}" };
        }

        var sw = Stopwatch.StartNew();
        var rootMoves = CombatScorer.ScoreLegalMoves(snapshot).ToList();
        if (rootMoves.Count == 0)
            return CombatScorer.PickBestCombatMove(snapshot);

        (GameAction Action, int Score) best = rootMoves[0];

        foreach (var (action, score) in rootMoves) {
            if (action.Type != ActionType.PlayCard) {
                if (score > best.Score) best = (action, score);
                continue;
            }

            if (sw.ElapsedMilliseconds >= TimeBudgetMs) break;

            var simulated = SimulateAfterPlay(snapshot, action);
            var followUps = CombatScorer.ScoreLegalMoves(simulated).ToList();
            var leafScore = score + (followUps.Count > 0 ? followUps.Max(x => x.Score) / 2 : 0);
            leafScore += EvaluateLeaf(simulated);

            if (leafScore > best.Score)
                best = (action, leafScore);
        }

        if (sw.ElapsedMilliseconds < TimeBudgetMs && MaxDepth >= 2)
            best = RefineWithSecondPlay(snapshot, best, sw);

        return best.Action;
    }

    static (GameAction Action, int Score) RefineWithSecondPlay(
        JsonObject snapshot,
        (GameAction Action, int Score) current,
        Stopwatch sw) {
        if (current.Action.Type != ActionType.PlayCard)
            return current;

        var afterFirst = SimulateAfterPlay(snapshot, current.Action);
        foreach (var (action, score) in CombatScorer.ScoreLegalMoves(afterFirst)) {
            if (sw.ElapsedMilliseconds >= TimeBudgetMs) break;
            if (action.Type != ActionType.PlayCard) continue;

            var afterSecond = SimulateAfterPlay(afterFirst, action);
            var total = current.Score + score + EvaluateLeaf(afterSecond);
            if (total > current.Score)
                current = (current.Action, total);
        }

        return current;
    }

    static int EvaluateLeaf(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var netDamage = IntentCalculator.NetDamageAfterBlock(snapshot);
        var statusDamage = IntentCalculator.EstimateStatusDamage(snapshot);
        var score = hp - netDamage * 2 - statusDamage * 2;

        var combat = snapshot["combat"]?.AsObject();
        var enemies = combat?["enemies"]?.AsArray();
        var aliveCount = 0;
        if (enemies != null) {
            foreach (var node in enemies) {
                if (node is not JsonObject e) continue;
                if (e["isAlive"]?.GetValue<bool>() == false) continue;
                aliveCount++;
                score -= e["currentHp"]?.GetValue<int>() ?? 0;
            }
        }

        score -= aliveCount * 5;
        return score;
    }

    static JsonObject SimulateAfterPlay(JsonObject snapshot, GameAction play) {
        var clone = snapshot.DeepClone()?.AsObject() ?? new JsonObject();
        var combat = clone["combat"]?.AsObject();
        if (combat == null) return clone;

        var hand = combat["hand"]?.AsArray();
        var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
        var idx = play.TargetIndex;

        if (hand != null && idx >= 0 && idx < hand.Count) {
            var card = hand[idx]?.AsObject();
            var cost = card?["cost"]?.GetValue<int>() ?? 0;
            combat["currentEnergy"] = Math.Max(0, energy - cost);
            hand.RemoveAt(idx);

            if (card != null) {
                var cardType = card["cardType"]?.GetValue<string>() ?? "";
                var damage = card["damage"]?.GetValue<int>() ?? 0;
                var isAttack = cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase) || damage > 0;
                var tags = CardCatalog.ResolveTags(
                    card["id"]?.GetValue<string>(),
                    cardType,
                    card["keywords"]?.AsArray());
                var isAoe = tags.Contains(AiTag.Aoe)
                    || card["targetType"]?.GetValue<string>() is "AllEnemy";

                if (isAttack && damage > 0) {
                    ApplyDamageToEnemies(combat, damage, play.SecondaryIndex, isAoe);
                }

                if (cardType.Contains("Skill", StringComparison.OrdinalIgnoreCase)) {
                    var blockGain = card["block"]?.GetValue<int>() ?? 5;
                    combat["playerBlock"] = (combat["playerBlock"]?.GetValue<int>() ?? 0) + blockGain;
                }
            }
        }

        return clone;
    }

    static void ApplyDamageToEnemies(JsonObject combat, int damage, int targetIndex, bool isAoe) {
        var enemies = combat["enemies"]?.AsArray();
        if (enemies == null) return;

        if (isAoe) {
            for (var i = 0; i < enemies.Count; i++)
                ApplyDamageToEnemy(enemies[i]?.AsObject(), damage);
            return;
        }

        if (targetIndex >= 0 && targetIndex < enemies.Count)
            ApplyDamageToEnemy(enemies[targetIndex]?.AsObject(), damage);
    }

    static void ApplyDamageToEnemy(JsonObject? enemy, int damage) {
        if (enemy == null) return;
        if (enemy["isAlive"]?.GetValue<bool>() == false) return;

        var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
        var block = enemy["block"]?.GetValue<int>() ?? 0;
        var remaining = Math.Max(0, damage - block);
        enemy["block"] = Math.Max(0, block - damage);
        enemy["currentHp"] = Math.Max(0, hp - remaining);
        if (hp - remaining <= 0)
            enemy["isAlive"] = false;
    }

    static GameAction? FindLethalMove(JsonObject snapshot, int targetIndex) {
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        if (hand == null) return null;

        for (int i = 0; i < hand.Count; i++) {
            var card = hand[i]?.AsObject();
            if (card == null) continue;
            if (card["canPlay"]?.GetValue<bool>() == false) continue;

            var type = card["cardType"]?.GetValue<string>() ?? "";
            if (!type.Contains("Attack", StringComparison.OrdinalIgnoreCase)
                && (card["damage"]?.GetValue<int>() ?? 0) <= 0)
                continue;

            var cost = card["cost"]?.GetValue<int>() ?? 99;
            var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
            if (cost > energy) continue;

            return new GameAction {
                Type = ActionType.PlayCard,
                TargetIndex = i,
                SecondaryIndex = targetIndex,
                Reason = "Lethal attack",
            };
        }

        return null;
    }
}
