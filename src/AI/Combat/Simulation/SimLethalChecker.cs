using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Combat;

namespace KitLib.AI.Combat.Simulation;

public static class SimLethalChecker {
    const int SecureKillSearchDepth = 8;

    public static bool CanLethal(CombatState state, out int targetIndex) {
        targetIndex = -1;

        foreach (var enemy in OrderLethalTargets(state)) {
            if (!enemy.IsAlive) continue;
            if (LethalExclusions.ShouldSkip(enemy)) continue;
            if (LethalDamageSolver.MaxSingleTargetDamage(state, enemy.Index) < enemy.EffectiveHp) continue;
            targetIndex = enemy.Index;
            return true;
        }

        return false;
    }

    public static bool CanLethalAfterTransform(CombatState state, out int targetIndex, out int transformHandIndex) {
        targetIndex = -1;
        transformHandIndex = -1;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatTransformSimulator.IsHandAttackTransform(card.Profile)) continue;
            if (!CombatCardCost.CanAfford(card, state)) continue;

            var projected = CombatSimulator.Apply(state, new SimCombatAction(SimActionKind.PlayCard, i, -1));
            if (!CanLethal(projected, out targetIndex)) continue;
            transformHandIndex = i;
            return true;
        }

        return false;
    }

    public static int EstimateMaxDamage(CombatState state) {
        int best = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            if (LethalExclusions.ShouldSkip(enemy)) continue;
            best = Math.Max(best, LethalDamageSolver.MaxSingleTargetDamage(state, enemy.Index));
        }

        return best;
    }

    public static bool CanKillEnemyThisAction(CombatState state, int handIndex, int enemyIndex) {
        if (handIndex < 0 || handIndex >= state.Hand.Count) return false;

        var card = state.Hand[handIndex];
        if (!card.IsAttack || card.Damage <= 0) return false;

        var target = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == enemyIndex);
        if (target == null) return false;

        if (card.IsAoe) {
            int dmg = CombatDamageCalc.OutgoingDamage(card, state);
            return AoeDamageEstimator.EstimateAoeKills(state, dmg) > 0
                && state.Enemies.Where(e => e.IsAlive && e.IntentDamage > 0)
                    .All(e => dmg * (e.Vulnerable > 0 ? 1.5f : 1f) >= e.EffectiveHp);
        }

        int outgoing = CombatDamageCalc.OutgoingDamage(card, state, target.Vulnerable);
        return outgoing >= target.EffectiveHp;
    }

    public static bool CanClearIncomingThisTurn(CombatState state) {
        if (ThreatModel.IncomingDamage(state) == 0)
            return true;

        return SearchSecureKill(state, SecureKillSearchDepth, requireZeroIncoming: true);
    }

    public static bool CanSecureKillThisTurn(CombatState state) {
        if (ThreatModel.NetDamageAfterBlock(state) <= BlockDefensePolicy.SafeChipNetMax)
            return true;

        if (ThreatModel.IncomingDamage(state) == 0)
            return ThreatModel.NetDamageAfterBlock(state) <= BlockDefensePolicy.SafeChipNetMax;

        if (SearchSecureKill(state, SecureKillSearchDepth, requireZeroIncoming: true))
            return true;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatTransformSimulator.IsHandAttackTransform(card.Profile))
                continue;
            if (!CombatCardCost.CanAfford(card, state))
                continue;

            var projected = CombatSimulator.Apply(
                state, new SimCombatAction(SimActionKind.PlayCard, i, -1));
            if (SearchSecureKill(projected, SecureKillSearchDepth - 1, requireZeroIncoming: true))
                return true;
        }

        return false;
    }

    static bool SearchSecureKill(CombatState state, int depth, bool requireZeroIncoming) {
        if (state.AliveEnemyCount == 0)
            return true;

        if (requireZeroIncoming && ThreatModel.IncomingDamage(state) == 0)
            return ThreatModel.NetDamageAfterBlock(state) <= BlockDefensePolicy.SafeChipNetMax;

        if (depth <= 0)
            return false;

        if (AoeDamageEstimator.CanAoeLethalAll(state)) {
            var aoe = AoeDamageEstimator.FindBestAoeLethalAction(state);
            if (aoe != null) {
                var afterAoe = CombatSimulator.Apply(state, aoe);
                if (SearchSecureKill(afterAoe, depth - 1, requireZeroIncoming))
                    return true;
            }
        }

        foreach (var action in EnumerateKillActions(state)) {
            var next = CombatSimulator.Apply(state, action);
            if (SearchSecureKill(next, depth - 1, requireZeroIncoming))
                return true;
        }

        return false;
    }

    static IEnumerable<SimCombatAction> EnumerateKillActions(CombatState state) {
        var threats = state.Enemies
            .Where(e => e.IsAlive && e.IntentDamage > 0)
            .OrderByDescending(e => e.IntentDamage)
            .ThenBy(e => e.EffectiveHp)
            .ToList();

        if (threats.Count == 0) {
            foreach (var enemy in state.Enemies.Where(e => e.IsAlive && ThreatModel.IsViableAttackTarget(state, e))) {
                for (int i = 0; i < state.Hand.Count; i++) {
                    var card = state.Hand[i];
                    if (!CombatCardCost.CanAfford(card, state) || !card.IsAttack) continue;
                    yield return new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                }
            }

            yield break;
        }

        foreach (var threat in threats) {
            for (int i = 0; i < state.Hand.Count; i++) {
                var card = state.Hand[i];
                if (!CombatCardCost.CanAfford(card, state) || !card.IsAttack || card.Damage <= 0) continue;

                if (card.IsAoe) {
                    yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
                    continue;
                }

                yield return new SimCombatAction(SimActionKind.PlayCard, i, threat.Index);
            }
        }
    }

    static IEnumerable<CombatEnemy> OrderLethalTargets(CombatState state) {
        var attackers = state.Enemies
            .Where(e => e.IsAlive && e.EffectiveIncoming > 0 && !LethalExclusions.ShouldSkip(e))
            .OrderByDescending(e => e.EffectiveIncoming)
            .ThenBy(e => e.EffectiveHp);

        if (attackers.Any())
            return attackers.Concat(state.Enemies
                .Where(e => e.IsAlive && e.EffectiveIncoming <= 0 && !LethalExclusions.ShouldSkip(e))
                .OrderByDescending(e => !e.IsMinion)
                .ThenBy(e => e.EffectiveHp));

        return state.Enemies
            .Where(e => e.IsAlive && !LethalExclusions.ShouldSkip(e))
            .OrderByDescending(e => !e.IsMinion)
            .ThenBy(e => e.EffectiveHp);
    }
}
