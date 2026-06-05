using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>Scores legal combat moves from a JSON snapshot (vanilla heuristics + mod modifiers).</summary>
public static class CombatScorer {
    const int EndTurnBaseScore = -10;

    public static GameAction? PickBestCombatMove(JsonObject snapshot) {
        var best = ScoreLegalMoves(snapshot)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        return best.Action;
    }

    public static IEnumerable<(GameAction Action, int Score)> ScoreLegalMoves(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null) {
            yield return (EndTurn("No combat"), EndTurnBaseScore);
            yield break;
        }

        if (!HasAliveEnemy(combat)) {
            yield return (EndTurn("No enemies"), EndTurnBaseScore);
            yield break;
        }

        var hand = combat["hand"]?.AsArray();
        var energy = combat["currentEnergy"]?.GetValue<int>() ?? 0;
        var hpRatio = IntentCalculator.HpRatio(snapshot);
        var lowHp = hpRatio < 0.4f;
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);
        var canLethal = LethalChecker.CanLethal(snapshot, out _);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var netIncoming = IntentCalculator.NetDamageAfterBlock(snapshot);
        var enemies = combat["enemies"]?.AsArray();
        var multiEnemy = IntentCalculator.AliveEnemyCount(snapshot) >= 2;

        if (hand != null) {
            for (var i = 0; i < hand.Count; i++) {
                var card = hand[i]?.AsObject();
                if (card == null) continue;
                if (card["canPlay"]?.GetValue<bool>() == false) continue;

                var cost = card["cost"]?.GetValue<int>() ?? 99;
                if (cost > energy) continue;

                var cardType = card["cardType"]?.GetValue<string>() ?? "";
                var targetType = card["targetType"]?.GetValue<string>() ?? "";
                var isAttack = cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase)
                    || (card["damage"]?.GetValue<int>() ?? 0) > 0;
                var isSkill = cardType.Contains("Skill", StringComparison.OrdinalIgnoreCase);
                var hasBlock = (card["block"]?.GetValue<int>() ?? 0) > 0;

                if (targetType is "AnyEnemy" or "AllEnemy") {
                    var targetCount = enemies?.Count ?? 1;
                    for (var t = 0; t < Math.Max(targetCount, 1); t++) {
                        var move = PlayCard(i, t, card);
                        var score = ScoreCard(snapshot, move, card, isAttack, isSkill, hasBlock,
                            lowHp, hpRatio, needsBlock, canLethal, incoming, netIncoming, multiEnemy, enemies, t);
                        score = AiMoveModifierHub.ApplyModifiers(snapshot, move, score);
                        yield return (move, score);
                    }
                }
                else {
                    var move = PlayCard(i, -1, card);
                    var score = ScoreCard(snapshot, move, card, isAttack, isSkill, hasBlock,
                        lowHp, hpRatio, needsBlock, canLethal, incoming, netIncoming, multiEnemy, enemies, -1);
                    score = AiMoveModifierHub.ApplyModifiers(snapshot, move, score);
                    yield return (move, score);
                }
            }
        }

        var end = EndTurn("End turn");
        var endScore = AiMoveModifierHub.ApplyModifiers(snapshot, end, EndTurnBaseScore);
        if (needsBlock && incoming > 0)
            endScore -= 15;
        yield return (end, endScore);
    }

    static int ScoreCard(
        JsonObject snapshot,
        GameAction move,
        JsonObject card,
        bool isAttack,
        bool isSkill,
        bool hasBlock,
        bool lowHp,
        float hpRatio,
        bool needsBlock,
        bool canLethal,
        int incoming,
        int netIncoming,
        bool multiEnemy,
        JsonArray? enemies,
        int targetIndex) {
        var cost = card["cost"]?.GetValue<int>() ?? 1;
        var score = 0;
        var blockValue = card["block"]?.GetValue<int>() ?? 0;
        var damageValue = card["damage"]?.GetValue<int>() ?? 0;
        var cardId = card["id"]?.GetValue<string>() ?? "";

        var tags = CardCatalog.ResolveTags(
            cardId,
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        var isAoe = tags.Contains(AiTag.Aoe) || card["targetType"]?.GetValue<string>() is "AllEnemy";

        if (needsBlock && (isSkill || hasBlock) && !canLethal) {
            var blockNeeded = Math.Max(0, netIncoming);
            if (blockNeeded > 0) {
                var effectiveBlock = Math.Min(blockValue, blockNeeded);
                score += 20 + effectiveBlock;
                if (incoming >= 15) score += 10;
                if (blockValue > blockNeeded + 5)
                    score -= (blockValue - blockNeeded) * 2;
            }
        }
        else if ((isSkill || hasBlock) && (!needsBlock || canLethal)) {
            score -= 40;
        }

        if (lowHp && isSkill && needsBlock && !canLethal)
            score += 15;

        if (IsSelfDamageCard(cardId) && hpRatio < 0.65f)
            score -= 30;

        if (isAttack) {
            score += 20 + cost * 5 + damageValue;
            score += TargetEnemyBonus(enemies, targetIndex);

            if (canLethal)
                score += 25;
            else if (needsBlock && incoming > damageValue)
                score -= 5;
        }
        else if (isSkill)
            score += 15 + cost * 2 + (needsBlock ? blockValue / 2 : 0);
        else
            score += 10;

        if (isAoe && multiEnemy)
            score += 12;
        if (multiEnemy && isAttack)
            score += 15;
        if (multiEnemy && hasBlock && !needsBlock)
            score -= 20;

        score -= Math.Max(0, cost - 1) * 2;
        return score;
    }

    static bool IsSelfDamageCard(string cardId) {
        var upper = cardId.ToUpperInvariant();
        return upper.Contains("HEMOKINESIS") || upper.Contains("BLOODLETTING")
            || upper.Contains("OFFERING");
    }

    static int TargetEnemyBonus(JsonArray? enemies, int targetIndex) {
        if (enemies == null || enemies.Count == 0)
            return 0;

        JsonObject? target = null;
        if (targetIndex >= 0 && targetIndex < enemies.Count)
            target = enemies[targetIndex]?.AsObject();

        target ??= enemies[0]?.AsObject();
        if (target == null) return 0;

        var hp = target["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = target["maxHp"]?.GetValue<int>() ?? 1;
        var lowEnemy = maxHp > 0 && hp <= maxHp * 0.25;
        return lowEnemy ? 30 : 5;
    }

    static bool HasAliveEnemy(JsonObject combat) {
        var enemies = combat["enemies"]?.AsArray();
        if (enemies == null) return true;
        if (enemies.Count == 0) return false;
        return enemies.Any(e => e?["isAlive"]?.GetValue<bool>() != false);
    }

    static GameAction PlayCard(int handIndex, int targetIndex, JsonObject card) => new() {
        Type = ActionType.PlayCard,
        TargetIndex = handIndex,
        SecondaryIndex = targetIndex,
        Reason = $"Scored play [{card["name"]?.GetValue<string>()}]",
    };

    static GameAction EndTurn(string reason) => new() {
        Type = ActionType.EndTurn,
        Reason = reason,
    };
}
