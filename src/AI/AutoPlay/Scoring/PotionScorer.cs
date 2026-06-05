using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>Scores held potions in combat; uses only when better than playing cards (except emergencies).</summary>
public static class PotionScorer {
    public const int UseThreshold = 25;
    const int CombatBaselineMargin = 8;

    static int _turnNumber = -1;
    static int _usedThisTurn;

    public static void SyncCombatTurn(JsonObject snapshot) {
        var turn = snapshot["combat"]?.AsObject()?["turnNumber"]?.GetValue<int>() ?? 0;
        if (turn != _turnNumber) {
            _turnNumber = turn;
            _usedThisTurn = 0;
        }
    }

    public static void NotifyPotionUsed() => _usedThisTurn++;

    public static void ResetTurnTracking() {
        _turnNumber = -1;
        _usedThisTurn = 0;
    }

    public static GameAction? TryEmergencyPotion(JsonObject snapshot) {
        var potions = snapshot["potions"]?.AsArray();
        if (potions == null || potions.Count == 0) return null;

        SyncCombatTurn(snapshot);
        if (_usedThisTurn >= 1) return null;

        var plan = DeckPlanInferer.Infer(snapshot);
        var candidates = new List<(int Slot, int Score, PotionCategory Category, string Label, JsonObject Potion)>();

        for (int i = 0; i < potions.Count; i++) {
            if (potions[i] is not JsonObject potion) continue;
            if (!TryReadPotionSlot(potion, i, out var slot, out var id)) continue;

            var profile = PotionMechanicIndex.GetOrDefault(id);
            var category = ParseCategory(potion, profile);
            if (category != PotionCategory.Heal && category != PotionCategory.Block)
                continue;
            if (!IsEmergency(category, snapshot))
                continue;

            var score = ScoreCombatUse(potion, snapshot, plan);
            if (score > 0)
                candidates.Add((slot, score, category, ShortId(id), potion));
        }

        if (candidates.Count == 0) return null;

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = candidates[0];
        if (best.Score < UseThreshold)
            return null;

        if (!PotionExistsAtSlot(potions, best.Slot, best.Potion))
            return null;

        LogCandidates(snapshot, candidates, best);
        return BuildUseAction(snapshot, best);
    }

    /// <summary>Fallback for potions not modeled in beam simulation.</summary>
    public static GameAction? TryFallbackPotion(JsonObject snapshot) {
        var potions = snapshot["potions"]?.AsArray();
        if (potions == null || potions.Count == 0) return null;

        SyncCombatTurn(snapshot);

        var plan = DeckPlanInferer.Infer(snapshot);
        var candidates = new List<(int Slot, int Score, PotionCategory Category, string Label, JsonObject Potion)>();

        for (int i = 0; i < potions.Count; i++) {
            if (potions[i] is not JsonObject potion) continue;
            if (!TryReadPotionSlot(potion, i, out var slot, out var id)) continue;
            if (PotionCombatEffectData.IsSimulatable(id))
                continue;

            var score = ScoreCombatUse(potion, snapshot, plan);
            if (score > 0) {
                var potionProfile = PotionMechanicIndex.GetOrDefault(id);
                candidates.Add((slot, score, ParseCategory(potion, potionProfile), ShortId(id), potion));
            }
        }

        if (candidates.Count == 0) return null;

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = candidates[0];
        if (best.Score < UseThreshold)
            return null;

        if (_usedThisTurn >= 1)
            return null;

        var baseline = BestCombatMoveScore(snapshot);
        if (best.Score < baseline + CombatBaselineMargin)
            return null;

        if (!PotionExistsAtSlot(potions, best.Slot, best.Potion))
            return null;

        LogCandidates(snapshot, candidates, best);
        return BuildUseAction(snapshot, best);
    }

    [Obsolete("Use TryEmergencyPotion, beam planner, or TryFallbackPotion.")]
    public static GameAction? TryUsePotion(JsonObject snapshot) {
        var emergency = TryEmergencyPotion(snapshot);
        if (emergency != null) return emergency;
        return TryFallbackPotion(snapshot);
    }

    static GameAction BuildUseAction(
        JsonObject snapshot,
        (int Slot, int Score, PotionCategory Category, string Label, JsonObject Potion) best) {
        var profile = PotionMechanicIndex.GetOrDefault(best.Potion["id"]?.GetValue<string>());
        var targetType = best.Potion["targetType"]?.GetValue<string>() ?? profile.TargetType;
        int enemyTarget = NeedsEnemyTarget(targetType)
            ? CombatSetupEvaluator.PrimaryAttackTargetIndex(CombatState.FromSnapshot(snapshot))
            : -1;

        return new GameAction {
            Type = ActionType.UsePotion,
            TargetIndex = best.Slot,
            SecondaryIndex = enemyTarget,
            Reason = $"Combat potion [{best.Label}] score={best.Score}",
        };
    }

    public static int ScoreCombatUse(JsonObject potion, JsonObject snapshot, DeckPlan? plan = null) {
        plan ??= DeckPlanInferer.Infer(snapshot);

        var id = potion["id"]?.GetValue<string>() ?? "";
        var profile = PotionMechanicIndex.GetOrDefault(id);
        var category = ParseCategory(potion, profile);
        var retain = potion["retainScore"]?.GetValue<int>() ?? PotionTierCatalog.GetRetainScore(id);

        var hpRatio = IntentCalculator.HpRatio(snapshot);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);
        var fatal = IntentCalculator.IsFatalIfUnblocked(snapshot);
        var enemies = IntentCalculator.AliveEnemyCount(snapshot);
        var combat = snapshot["combat"]?.AsObject();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var maxEnergy = combat?["maxEnergy"]?.GetValue<int>() ?? 3;
        var hand = combat?["hand"]?.AsArray();
        var canLethal = LethalChecker.CanLethal(snapshot, out _);
        var maxOffense = hand != null ? LethalChecker.EstimateMaxDamage(hand, energy, 0) : 0;
        var minPlayCost = MinPlayableCost(hand, energy);

        int score = 0;

        switch (category) {
            case PotionCategory.Heal:
                if (hpRatio < 0.35f) score += 40;
                else if (hpRatio < 0.55f && fatal) score += 30;
                else if (fatal) score += 20;
                break;

            case PotionCategory.Block:
                if (fatal) score += 45;
                else if (needsBlock && incoming >= 20) score += 30;
                else if (needsBlock) score += 20;
                score += profile.EstimatedBlock;
                break;

            case PotionCategory.DamageSingle:
            case PotionCategory.DamageAoE:
                if (canLethal) score += 15;
                if (enemies >= 2 && category == PotionCategory.DamageAoE) score += 25;
                if (!canLethal && maxOffense > 0) {
                    var gap = EstimateLethalGap(snapshot);
                    if (gap > 0 && gap <= profile.EstimatedDamage + 8) score += 30;
                }
                score += profile.EstimatedDamage;
                break;

            case PotionCategory.Energy:
                if (energy >= maxEnergy) {
                    score = 0;
                    break;
                }
                if (!canLethal && minPlayCost > energy && minPlayCost <= energy + 2)
                    score += 35;
                else if (canLethal) score += 10;
                break;

            case PotionCategory.Draw:
                if (needsBlock && energy <= 1) score += 15;
                if (enemies >= 2) score += 10;
                break;

            case PotionCategory.Buff:
                score += (int)Math.Round(plan.GetWeight(AiTag.Attack) * 12f);
                if (enemies >= 2) score += 8;
                if (energy >= maxEnergy && !needsBlock && !fatal)
                    score -= 40;
                else if (energy >= maxEnergy - 1 && maxOffense > 0 && !needsBlock)
                    score -= 25;
                break;

            case PotionCategory.Debuff:
                score += (int)Math.Round(plan.GetWeight(AiTag.Attack) * 8f);
                if (enemies >= 2) score += 10;
                if (incoming >= 10) score += 12;
                if (!needsBlock && incoming < 8 && maxOffense > 0 && energy >= 2)
                    score -= 22;
                break;

            case PotionCategory.Random:
                score += 18;
                if (canLethal || needsBlock) score += 8;
                if (energy >= maxEnergy && maxOffense > 0 && !needsBlock)
                    score -= 15;
                break;

            case PotionCategory.Utility:
                score += 5;
                break;
        }

        score += SynergyBonus(category, plan);
        score -= WastePenalty(retain, hpRatio, fatal, needsBlock, snapshot);

        var usage = potion["usage"]?.GetValue<string>() ?? profile.Usage;
        if (!IsCombatUsable(usage))
            score = 0;

        return Math.Max(0, score);
    }

    static bool IsEmergency(PotionCategory category, JsonObject snapshot) {
        var fatal = IntentCalculator.IsFatalIfUnblocked(snapshot);
        var hpRatio = IntentCalculator.HpRatio(snapshot);

        return category switch {
            PotionCategory.Heal => fatal || hpRatio < 0.35f,
            PotionCategory.Block => fatal,
            _ => false,
        };
    }

    static int BestCombatMoveScore(JsonObject snapshot) {
        var ranked = CombatScorer.ScoreLegalMovesDetailed(snapshot)
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();
        return ranked?.Score ?? 0;
    }

    static bool TryReadPotionSlot(JsonObject potion, int arrayIndex, out int slot, out string id) {
        slot = potion["slot"]?.GetValue<int>() ?? arrayIndex;
        id = potion["id"]?.GetValue<string>() ?? "";
        return !string.IsNullOrWhiteSpace(id);
    }

    static bool PotionExistsAtSlot(JsonArray potions, int slot, JsonObject expected) {
        var expectedId = expected["id"]?.GetValue<string>() ?? "";
        foreach (var node in potions) {
            if (node is not JsonObject potion) continue;
            var potionSlot = potion["slot"]?.GetValue<int>() ?? -1;
            if (potionSlot != slot) continue;
            var id = potion["id"]?.GetValue<string>() ?? "";
            return !string.IsNullOrWhiteSpace(id)
                && string.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    static bool NeedsEnemyTarget(string? targetType) {
        if (string.IsNullOrEmpty(targetType)) return false;
        return targetType.Contains("Enemy", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsCombatUsable(string usage) {
        if (string.IsNullOrEmpty(usage)) return true;
        if (usage.Contains("Any", StringComparison.OrdinalIgnoreCase)) return true;
        if (usage.Contains("Combat", StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    static PotionCategory ParseCategory(JsonObject potion, PotionMechanicProfile profile) {
        var raw = potion["category"]?.GetValue<string>();
        if (!string.IsNullOrEmpty(raw) && Enum.TryParse<PotionCategory>(raw, out var parsed))
            return parsed;
        return profile.Category;
    }

    static int SynergyBonus(PotionCategory category, DeckPlan plan) => category switch {
        PotionCategory.Debuff when plan.GetWeight(AiTag.Attack) > 0.9f => 8,
        PotionCategory.Buff when plan.GetWeight(AiTag.Scaling) > 0.8f => 10,
        PotionCategory.Random when plan.GetWeight(AiTag.Draw) > 0.7f => 6,
        _ => 0,
    };

    static int WastePenalty(int retain, float hpRatio, bool fatal, bool needsBlock, JsonObject snapshot) {
        if (fatal || needsBlock) return 0;

        var room = (snapshot["roomType"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var isBigFight = room.Contains("ELITE") || room.Contains("BOSS");

        if (!isBigFight && hpRatio > 0.65f)
            return retain + 15;

        if (!isBigFight && hpRatio > 0.45f)
            return retain / 2;

        return 0;
    }

    static int EstimateLethalGap(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return 0;

        var maxDamage = LethalChecker.EstimateMaxDamage(hand, energy, 0);
        var minHp = int.MaxValue;
        foreach (var node in enemies) {
            if (node is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            var hp = (enemy["currentHp"]?.GetValue<int>() ?? 0) + (enemy["block"]?.GetValue<int>() ?? 0);
            if (hp < minHp) minHp = hp;
        }

        if (minHp == int.MaxValue) return 0;
        return Math.Max(0, minHp - maxDamage);
    }

    static int MinPlayableCost(JsonArray? hand, int energy) {
        if (hand == null) return 99;
        var min = 99;
        foreach (var node in hand) {
            if (node is not JsonObject card) continue;
            if (card["canPlay"]?.GetValue<bool>() != true) continue;
            var cost = card["cost"]?.GetValue<int>() ?? 99;
            if (cost < min) min = cost;
        }
        return min == 99 ? energy + 1 : min;
    }

    static void LogCandidates(
        JsonObject snapshot,
        List<(int Slot, int Score, PotionCategory Category, string Label, JsonObject Potion)> candidates,
        (int Slot, int Score, PotionCategory Category, string Label, JsonObject Potion) best) {
        var top = candidates.Take(3)
            .Select(c => $"{c.Label}:+{c.Score}")
            .ToArray();
        var baseline = BestCombatMoveScore(snapshot);
        AiDecisionLog.Record("AutoPlay",
            $"potion pick [{best.Label}:+{best.Score}] card={baseline} alts [{string.Join("] [", top)}]");
    }

    static string ShortId(string id) {
        if (string.IsNullOrEmpty(id)) return "?";
        var s = id;
        if (s.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase))
            s = s["POTION.".Length..];
        return s;
    }
}
