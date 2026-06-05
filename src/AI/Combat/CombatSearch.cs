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
        var mustBlock = IntentCalculator.IsFatalIfUnblocked(snapshot);

        if (!mustBlock && LethalChecker.CanLethalAfterTransform(snapshot, out var transformTarget, out var transformIndex)) {
            var transformFirst = new GameAction {
                Type = ActionType.PlayCard,
                TargetIndex = transformIndex,
                SecondaryIndex = -1,
                Reason = $"Lethal setup: transform → enemy {transformTarget}",
            };
            LogSimplePick(snapshot, transformFirst, "lethal-transform");
            return transformFirst;
        }

        if (!mustBlock && LethalChecker.CanLethal(snapshot, out var lethalTarget)) {
            var lethalMove = FindLethalMove(snapshot, lethalTarget);
            if (lethalMove != null) {
                var lethal = lethalMove with { Reason = $"Lethal on enemy {lethalTarget}" };
                LogSimplePick(snapshot, lethal, "lethal");
                return lethal;
            }
        }

        var rootMoves = CombatScorer.ScoreLegalMovesDetailed(snapshot).ToList();
        if (rootMoves.Count == 0) {
            var fallback = CombatScorer.PickBestCombatMove(snapshot);
            if (fallback != null)
                LogSimplePick(snapshot, fallback, "fallback");
            return fallback;
        }

        var sw = Stopwatch.StartNew();
        var best = rootMoves.OrderByDescending(x => x.Score).First();
        var bestLeaf = best.Score;

        foreach (var scored in rootMoves) {
            if (scored.Action.Type != ActionType.PlayCard) {
                if (scored.Score > bestLeaf) {
                    bestLeaf = scored.Score;
                    best = scored;
                }
                continue;
            }

            if (sw.ElapsedMilliseconds >= TimeBudgetMs) break;

            var simulated = SimulateAfterPlay(snapshot, scored.Action);
            var followUps = CombatScorer.ScoreLegalMovesDetailed(simulated).ToList();
            var leafScore = scored.Score + (followUps.Count > 0 ? followUps.Max(x => x.Score) / 2 : 0);
            leafScore += EvaluateLeaf(simulated);

            if (leafScore > bestLeaf) {
                bestLeaf = leafScore;
                best = scored with { Score = leafScore };
            }
        }

        if (sw.ElapsedMilliseconds < TimeBudgetMs && MaxDepth >= 2)
            best = RefineWithSecondPlay(snapshot, best, rootMoves, sw);

        var picked = best.Action with {
            Reason = best.Action.Reason ?? CombatMoveScore.FormatMoveLabel(best.Action),
        };

        var ranked = rootMoves
            .OrderByDescending(x => x.Score)
            .ToList();
        CombatDecisionLog.LogPick(snapshot, picked, ranked, $"search={best.Score}");

        return picked;
    }

    static void LogSimplePick(JsonObject snapshot, GameAction action, string note) {
        if (!CombatDecisionLog.VerboseEnabled) return;
        AiDecisionLog.Record("AutoPlay", $"Combat pick {CombatMoveScore.FormatMoveLabel(action)} ({note})");
    }

    static CombatMoveScore RefineWithSecondPlay(
        JsonObject snapshot,
        CombatMoveScore current,
        List<CombatMoveScore> rootMoves,
        Stopwatch sw) {
        if (current.Action.Type != ActionType.PlayCard)
            return current;

        var best = current;
        var afterFirst = SimulateAfterPlay(snapshot, current.Action);
        foreach (var scored in CombatScorer.ScoreLegalMovesDetailed(afterFirst)) {
            if (sw.ElapsedMilliseconds >= TimeBudgetMs) break;
            if (scored.Action.Type != ActionType.PlayCard) continue;

            var afterSecond = SimulateAfterPlay(afterFirst, scored.Action);
            var total = current.Score + scored.Score + EvaluateLeaf(afterSecond);
            if (total > best.Score)
                best = current with { Score = total };
        }

        return best;
    }

    static int EvaluateLeaf(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var netDamage = IntentCalculator.NetDamageAfterBlock(snapshot);
        var statusDamage = IntentCalculator.EstimateStatusDamage(snapshot);
        var score = hp - netDamage * 3 - statusDamage * 2;

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

            if (card != null) {
                var profile = CombatCardStats.ResolveProfile(card);

                if (CombatTransformSimulator.IsHandAttackTransform(profile)) {
                    CombatTransformSimulator.ApplyHandAttackTransform(hand, idx);
                }
                else {
                    hand.RemoveAt(idx);
                    ApplyPlayedCardEffects(combat, card, profile, play);
                }
            }
            else {
                hand.RemoveAt(idx);
            }
        }

        return clone;
    }

    static void ApplyPlayedCardEffects(JsonObject combat, JsonObject card, CardMechanicProfile profile, GameAction play) {
        var damage = CombatCardStats.ResolveDamage(card);
        var isAttack = CombatCardStats.IsAttackCard(card);
        var tags = CardCatalog.ResolveTags(
            card["id"]?.GetValue<string>(),
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        var isAoe = tags.Contains(AiTag.Aoe)
            || card["targetType"]?.GetValue<string>() is "AllEnemy";

        if (isAttack && damage > 0)
            ApplyDamageToEnemies(combat, damage, play.SecondaryIndex, isAoe);

        if (profile.AppliedVulnerable > 0) {
            var enemies = combat["enemies"]?.AsArray();
            var target = ResolveSimTarget(enemies, play.SecondaryIndex, isAoe);
            if (target != null)
                CombatPowerReader.ApplyPower(target, "VULNERABLE", profile.AppliedVulnerable);
        }

        if (profile.AppliedWeak > 0) {
            var enemies = combat["enemies"]?.AsArray();
            var target = ResolveSimTarget(enemies, play.SecondaryIndex, isAoe);
            if (target != null)
                CombatPowerReader.ApplyPower(target, "WEAK", profile.AppliedWeak);
        }

        if (CombatCardStats.IsSkillCard(card)) {
            var blockGain = CombatCardStats.ResolveBlock(card);
            if (blockGain > 0)
                combat["playerBlock"] = (combat["playerBlock"]?.GetValue<int>() ?? 0) + blockGain;
            else if (!MechanicCombatBonus.IsSetupSkill(profile))
                combat["playerBlock"] = (combat["playerBlock"]?.GetValue<int>() ?? 0) + 5;
        }
    }

    static JsonObject? ResolveSimTarget(JsonArray? enemies, int targetIndex, bool isAoe) {
        if (enemies == null || enemies.Count == 0) return null;
        if (isAoe) return enemies[0]?.AsObject();
        if (targetIndex >= 0 && targetIndex < enemies.Count)
            return enemies[targetIndex]?.AsObject();
        return enemies[0]?.AsObject();
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

        var scaled = (int)Math.Round(damage * CombatPowerReader.AttackDamageMultiplier(enemy));
        var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
        var block = enemy["block"]?.GetValue<int>() ?? 0;
        var remaining = Math.Max(0, scaled - block);
        enemy["block"] = Math.Max(0, block - scaled);
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
            if (!CombatCardStats.IsAttackCard(card)) continue;

            var cost = card["cost"]?.GetValue<int>() ?? 99;
            var energy = combat!["currentEnergy"]?.GetValue<int>() ?? 0;
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
