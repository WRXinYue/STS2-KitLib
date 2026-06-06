using System;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

/// <summary>Beam expansion ordering — same line rank as leaf comparison, plus hard prune sentinels.</summary>
internal static class CombatActionHeuristic {
    public static int QuickScore(
        CombatState state,
        SimCombatAction action,
        JsonObject? rootSnapshot = null) {
        int score = QuickScoreCore(state, action);
        return SimMoveScoring.WithModifiers(state, action, score, rootSnapshot);
    }

    static int QuickScoreCore(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return ScoreEndTurn(state);

        if (action.Kind == SimActionKind.UsePotion)
            return ScorePotionUse(state, action);

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return int.MinValue;

        var card = state.Hand[action.HandIndex];
        if (!CombatCardCost.CanAfford(card, state))
            return int.MinValue;

        if (DeckPollutionEvaluator.IsHandJunk(card)) {
            var emergency = DeckPollutionEvaluator.EmergencyJunkPlayScore(state, card, action.HandIndex);
            if (emergency > int.MinValue + 1)
                return emergency;
            return int.MinValue;
        }

        var junkRelief = DeckPollutionEvaluator.JunkReliefScore(state, card, action.HandIndex);
        if (junkRelief > 0)
            return Math.Max(junkRelief, CombatSetupEvaluator.RankPlayAction(state, action));

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile)) {
            if (CombatTransformSimulator.EstimateTurnDamageDelta(
                    state.ToHandJson(), card.ToJson(), state.Energy) <= 0)
                return int.MinValue;

            var after = CombatSimulator.Apply(state, action);
            if (BlockDefensePolicy.NeedsBlock(state) && !SimLethalChecker.CanSecureKillThisTurn(after))
                return int.MinValue;
        }

        if (card.IsAttack && card.Damage > 0 && ShouldPruneIllusionAttack(state, action))
            return int.MinValue;

        return CombatSetupEvaluator.RankPlayAction(state, action);
    }

    public static bool ShouldPrune(
        CombatState state,
        SimCombatAction action,
        JsonObject? rootSnapshot = null) {
        if (PreservesLethalPotential(state, action))
            return false;
        return QuickScore(state, action, rootSnapshot) <= int.MinValue + 1;
    }

    static bool PreservesLethalPotential(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.UsePotion) {
            var afterPotion = CombatSimulator.Apply(state, action);
            return afterPotion.AliveEnemyCount == 0
                || SimLethalChecker.CanLethal(afterPotion, out _);
        }

        if (action.Kind != SimActionKind.PlayCard
            || action.HandIndex < 0
            || action.HandIndex >= state.Hand.Count)
            return false;

        if (action.EnemyIndex >= 0
            && SimLethalChecker.CanKillEnemyThisAction(state, action.HandIndex, action.EnemyIndex))
            return true;

        var card = state.Hand[action.HandIndex];
        if (card.IsAoe && card.Damage > 0 && AoeDamageEstimator.CanAoeLethalAll(state))
            return true;

        var after = CombatSimulator.Apply(state, action);
        if (after.AliveEnemyCount == 0)
            return true;

        return SimLethalChecker.CanLethal(after, out _);
    }

    static int ScorePotionUse(CombatState state, SimCombatAction action) {
        if (action.PotionSlot < 0)
            return int.MinValue;

        var potion = state.Potions.FirstOrDefault(p => p.Slot == action.PotionSlot);
        if (potion == null)
            return int.MinValue;

        if (!PotionCombatEffectData.TryGetProfile(potion.Id, out var profile) || !profile.Simulatable)
            return int.MinValue;

        var ctx = PotionUseScoring.FromState(state, potion.Id);
        return PotionUseScoring.ScoreSimProfile(state, profile, action.EnemyIndex, ctx);
    }

    static int ScoreEndTurn(CombatState state) {
        if (ThreatModel.IsFatalIfUnblocked(state))
            return int.MinValue;

        if (BlockDefensePolicy.ShouldPrioritizeBlock(state))
            return int.MinValue + 1;

        if (DeckPollutionEvaluator.HasAffordableJunkRelief(state))
            return int.MinValue + 2;
        if (DeckPollutionEvaluator.HasAffordableEmergencyJunkClear(state))
            return int.MinValue + 3;

        return CombatSetupEvaluator.PackLineScore(CombatSetupEvaluator.EvaluateLine(state));
    }

    static bool ShouldPruneIllusionAttack(CombatState state, SimCombatAction action) {
        if (action.EnemyIndex < 0)
            return false;

        var target = ResolveTarget(state, action.EnemyIndex);
        return target != null && !ThreatModel.IsViableAttackTarget(state, target);
    }

    static CombatEnemy? ResolveTarget(CombatState state, int enemyIndex) {
        if (enemyIndex < 0) return null;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            if (enemy.Index == enemyIndex)
                return enemy;
        }

        return null;
    }
}
