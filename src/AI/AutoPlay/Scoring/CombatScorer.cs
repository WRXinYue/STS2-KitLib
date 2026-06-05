using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>Scores legal combat moves from a JSON snapshot (mechanic-aware heuristics + mod modifiers).</summary>
public static class CombatScorer {
    const int EndTurnBaseScore = -10;

    public static GameAction? PickBestCombatMove(JsonObject snapshot) {
        var ranked = ScoreLegalMovesDetailed(snapshot).ToList();
        var best = ranked.FirstOrDefault();
        return best?.Action;
    }

    public static IEnumerable<CombatMoveScore> ScoreLegalMovesDetailed(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        if (combat == null) {
            yield return BuildEndTurn("No combat", EndTurnBaseScore, snapshot);
            yield break;
        }

        if (!HasAliveEnemy(combat)) {
            yield return BuildEndTurn("No enemies", EndTurnBaseScore, snapshot);
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

                var targetType = card["targetType"]?.GetValue<string>() ?? "";
                if (targetType is "AnyEnemy" or "AllEnemy") {
                    var targetCount = enemies?.Count ?? 1;
                    for (var t = 0; t < Math.Max(targetCount, 1); t++) {
                        var move = PlayCard(i, t, card);
                        yield return ScoreCardDetailed(snapshot, move, card, hand, energy,
                            lowHp, hpRatio, needsBlock, canLethal, incoming, netIncoming,
                            multiEnemy, enemies, t);
                    }
                }
                else {
                    var move = PlayCard(i, -1, card);
                    yield return ScoreCardDetailed(snapshot, move, card, hand, energy,
                        lowHp, hpRatio, needsBlock, canLethal, incoming, netIncoming,
                        multiEnemy, enemies, -1);
                }
            }
        }

        yield return BuildEndTurn("End turn", ScoreEndTurn(needsBlock, incoming, snapshot), snapshot);
    }

    public static IEnumerable<(GameAction Action, int Score)> ScoreLegalMoves(JsonObject snapshot) {
        foreach (var scored in ScoreLegalMovesDetailed(snapshot))
            yield return (scored.Action, scored.Score);
    }

    static CombatMoveScore ScoreCardDetailed(
        JsonObject snapshot,
        GameAction move,
        JsonObject card,
        JsonArray? hand,
        int energy,
        bool lowHp,
        float hpRatio,
        bool needsBlock,
        bool canLethal,
        int incoming,
        int netIncoming,
        bool multiEnemy,
        JsonArray? enemies,
        int targetIndex) {
        var profile = CombatCardStats.ResolveProfile(card);
        var cost = card["cost"]?.GetValue<int>() ?? 1;
        var damageValue = CombatCardStats.ResolveDamage(card);
        var blockValue = CombatCardStats.ResolveBlock(card);
        var cardId = card["id"]?.GetValue<string>() ?? "";
        var isAttack = CombatCardStats.IsAttackCard(card);
        var isSkill = CombatCardStats.IsSkillCard(card);
        var hasBlock = blockValue > 0;
        var targetEnemy = ResolveTargetEnemy(enemies, targetIndex);

        var tags = CardCatalog.ResolveTags(
            cardId,
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        var isAoe = tags.Contains(AiTag.Aoe) || card["targetType"]?.GetValue<string>() is "AllEnemy";
        var isBlockCard = hasBlock || tags.Contains(AiTag.Block);
        var blockUrgency = IntentCalculator.BlockUrgency(snapshot);
        var fatalIfUnblocked = IntentCalculator.IsFatalIfUnblocked(snapshot);

        var builder = new ScoreBuilder();

        if (needsBlock && (isSkill || isBlockCard)) {
            var blockNeeded = Math.Max(0, netIncoming);
            if (blockNeeded > 0) {
                var effectiveBlock = Math.Min(blockValue, blockNeeded);
                builder.Add("block", 25 + effectiveBlock * 2);
                if (incoming >= 15) builder.Add("big-hit", 12);
                if (fatalIfUnblocked) builder.Add("fatal", 25);
                if (blockValue > blockNeeded + 5)
                    builder.Add("overblock", -(blockValue - blockNeeded));
            }
        }
        else if ((isSkill || isBlockCard) && !needsBlock && !MechanicCombatBonus.IsSetupSkill(profile)) {
            builder.Add("skill-no-block-need", -CombatScoreWeights.NonSetupSkillPenalty);
        }

        if (lowHp && (isSkill || isBlockCard) && needsBlock)
            builder.Add("low-hp-skill", 20);

        if (IsSelfDamageCard(cardId) && hpRatio < 0.65f)
            builder.Add("self-dmg", -30);

        if (isAttack) {
            builder.Add("attack", 20 + cost * 5 + damageValue);
            builder.Add("target", TargetEnemyBonus(enemies, targetIndex));
            if (needsBlock) {
                builder.Add("attack-unsafe", -(blockUrgency / 2 + Math.Max(0, netIncoming - damageValue) / 2));
                if (canLethal && !fatalIfUnblocked)
                    builder.Add("lethal", 25);
                else if (canLethal)
                    builder.Add("lethal-risky", 8);
            }
            else if (canLethal) {
                builder.Add("lethal", 25);
            }
        }
        else if (isSkill)
            builder.Add("skill", 15 + cost * 2 + (needsBlock ? blockValue : 0));
        else
            builder.Add("other", 10);

        if (isAoe && multiEnemy) builder.Add("aoe", 12);
        if (multiEnemy && isAttack) builder.Add("multi", 15);
        if (multiEnemy && hasBlock && !needsBlock) builder.Add("multi-defend", -20);

        builder.Add("cost", -Math.Max(0, cost - 1) * 2);

        var mechBonus = MechanicCombatBonus.Score(snapshot, card, profile, hand, targetEnemy, energy);
        if (needsBlock && blockUrgency >= 40 && (isAttack || MechanicCombatBonus.IsSetupSkill(profile)))
            mechBonus = mechBonus * 2 / 3;
        builder.Add("mechanic", mechBonus);

        var cardName = card["name"]?.GetValue<string>() ?? cardId;
        var withMods = AiMoveModifierHub.ApplyModifiers(snapshot, move, builder.Total);
        if (withMods != builder.Total)
            builder.Add("mod", withMods - builder.Total);

        return builder.Build(move with {
            Reason = $"{cardName} score={withMods} [{builder.FormatTerms()}]",
        });
    }

    static int ScoreEndTurn(bool needsBlock, int incoming, JsonObject snapshot) {
        var score = EndTurnBaseScore;
        if (needsBlock && incoming > 0)
            score -= 15 + IntentCalculator.BlockUrgency(snapshot) / 4;
        return score;
    }

    static CombatMoveScore BuildEndTurn(string reason, int baseScore, JsonObject snapshot) {
        var action = new GameAction { Type = ActionType.EndTurn, Reason = reason };
        var score = AiMoveModifierHub.ApplyModifiers(snapshot, action, baseScore);
        var terms = new List<string> { $"base:{baseScore}" };
        if (score != baseScore)
            terms.Add($"mod:{score - baseScore:+0;-0;0}");
        return new CombatMoveScore(action with { Reason = $"EndTurn score={score}" }, score, terms);
    }

    static JsonObject? ResolveTargetEnemy(JsonArray? enemies, int targetIndex) {
        if (enemies == null || enemies.Count == 0) return null;
        if (targetIndex >= 0 && targetIndex < enemies.Count)
            return enemies[targetIndex]?.AsObject();
        return enemies[0]?.AsObject();
    }

    static bool IsSelfDamageCard(string cardId) {
        var upper = cardId.ToUpperInvariant();
        return upper.Contains("HEMOKINESIS") || upper.Contains("BLOODLETTING")
            || upper.Contains("OFFERING");
    }

    static int TargetEnemyBonus(JsonArray? enemies, int targetIndex) {
        if (enemies == null || enemies.Count == 0)
            return 0;

        JsonObject? target = ResolveTargetEnemy(enemies, targetIndex);
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
        Reason = card["name"]?.GetValue<string>() ?? card["id"]?.GetValue<string>() ?? "?",
    };
}
