using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

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

    public const int SlotClearThreshold = 35;

    public static GameAction? TryProactiveSlotClear(JsonObject snapshot) {
        var potions = snapshot["potions"]?.AsArray();
        if (potions == null || potions.Count == 0) return null;

        if (IntentCalculator.TotalIncomingDamage(snapshot) > 0)
            return null;
        if (IntentCalculator.NeedsBlock(snapshot))
            return null;

        var potionCount = potions.Count;
        var beltFull = snapshot["hasOpenPotionSlots"]?.GetValue<bool>() == false;
        if (!beltFull && potionCount < 3)
            return null;

        SyncCombatTurn(snapshot);
        if (_usedThisTurn >= 1)
            return null;

        var plan = DeckPlanInferer.Infer(snapshot);
        var state = CombatState.FromSnapshot(snapshot);
        var candidates = new List<(int Slot, int Score, string Label, JsonObject Potion)>();

        for (int i = 0; i < potions.Count; i++) {
            if (potions[i] is not JsonObject potion) continue;
            if (!TryReadPotionSlot(potion, i, out var slot, out var id)) continue;

            var ctx = PotionUseScoring.FromSnapshot(snapshot, id, plan);
            if (PotionUseScoring.ScoreSlotClearBonus(ctx) <= 0)
                continue;

            int score;
            if (PotionCombatEffectData.IsSimulatable(id)) {
                if (!PotionCombatEffectData.TryGetProfile(id, out var profile))
                    continue;
                if (PotionUseScoring.IsAttackDebuffLowValue(state, profile))
                    continue;
                if (PotionUseScoring.IsEnergyPotionLowValue(state, profile))
                    continue;

                int enemyIndex = NeedsEnemyTarget(profile.TargetType)
                    ? CombatSetupEvaluator.PrimaryAttackTargetIndex(state)
                    : -1;
                score = PotionUseScoring.ScoreSimProfile(state, profile, enemyIndex, ctx);
            } else {
                score = ScoreCombatUse(potion, snapshot, plan);
            }

            if (score >= SlotClearThreshold)
                candidates.Add((slot, score, ShortId(id), potion));
        }

        if (candidates.Count == 0)
            return null;

        candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
        var best = candidates[0];
        if (!PotionExistsAtSlot(potions, best.Slot, best.Potion))
            return null;

        AiDecisionLog.Record("AutoPlay",
            $"potion slot-clear [{best.Label}:+{best.Score}] alts [{string.Join("] [", candidates.Take(3).Select(c => $"{c.Label}:+{c.Score}"))}]");
        return BuildUseAction(snapshot, (best.Slot, best.Score, PotionCategory.Utility, best.Label, best.Potion));
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
            if (PotionCombatEffectData.IsSimulatable(id))
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
        var ctx = PotionUseScoring.FromSnapshot(snapshot, id, plan);
        int score = PotionUseScoring.ScoreCategory(ctx, profile, category);

        var usage = potion["usage"]?.GetValue<string>() ?? profile.Usage;
        if (!IsCombatUsable(usage))
            score = 0;

        return score;
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
        var state = CombatState.FromSnapshot(snapshot);
        if (state.AliveEnemyCount == 0)
            return 0;

        int best = 0;
        foreach (var action in LegalActionGenerator.Generate(state, snapshot)) {
            if (action.Kind == SimActionKind.EndTurn)
                continue;
            best = Math.Max(best, CombatActionHeuristic.QuickScore(state, action, snapshot));
        }

        return best;
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
