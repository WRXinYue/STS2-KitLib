using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

/// <summary>Fast move ordering for beam expansion — setup before attacks, kills before chip.</summary>
internal static class CombatActionHeuristic {
    public static int QuickScore(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return ScoreEndTurn(state);

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return int.MinValue;

        var card = state.Hand[action.HandIndex];
        if (!card.CanPlay || card.Cost > state.Energy)
            return int.MinValue;

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile))
            return ScoreHandTransform(state, action, card);

        if (AppliesVulnerable(card))
            return ScoreVulnerableSetup(state, action, card);

        if (card.IsAttack && card.Damage > 0) {
            if (ShouldPruneIllusionAttack(state, action))
                return int.MinValue;
            return ScoreAttack(state, action, card);
        }

        if (card.Block > 0)
            return ScoreBlock(state, card);

        if (card.Profile.AppliedWeak > 0)
            return 28 + card.Profile.AppliedWeak * 6;

        if (MechanicCombatBonus.IsSetupSkill(card.Profile))
            return 22;

        return 8;
    }

    public static bool ShouldPrune(CombatState state, SimCombatAction action) =>
        QuickScore(state, action) <= int.MinValue + 1;

    static int ScoreHandTransform(CombatState state, SimCombatAction action, CombatHandCard card) {
        var hand = state.ToHandJson();
        var delta = CombatTransformSimulator.EstimateTurnDamageDelta(hand, card.ToJson(), state.Energy);
        if (delta <= 0) {
            var after = CombatSimulator.Apply(state, action);
            if (!SimLethalChecker.CanLethal(after, out _))
                return int.MinValue;
            return 120;
        }

        return 40 + delta;
    }

    static int ScoreVulnerableSetup(CombatState state, SimCombatAction action, CombatHandCard card) {
        if (action.EnemyIndex >= 0) {
            var value = CombatSetupEvaluator.ComputeVulnerableSetupValue(
                state, action.HandIndex, action.EnemyIndex);
            if (value <= 0) return 5;
            return 90 + value * 4 + card.Damage * 2;
        }

        int best = 0;
        foreach (var enemyIndex in OrderedAttackTargets(state)) {
            var value = CombatSetupEvaluator.ComputeVulnerableSetupValue(
                state, action.HandIndex, enemyIndex);
            if (value > best) best = value;
        }

        if (best <= 0) return 5;
        return 90 + best * 4 + card.Damage * 2;
    }

    static int ScoreAttack(CombatState state, SimCombatAction action, CombatHandCard card) {
        var score = card.Damage * 3;
        var target = ResolveTarget(state, action.EnemyIndex);
        if (target != null) {
            if (target.Vulnerable <= 0) {
                var setupValue = CombatSetupEvaluator.ComputeBestVulnerableDeferValue(
                    BuildSnapshot(state), state.ToHandJson(), state.Energy, EnemyToJson(target));
                score -= setupValue;
            }

            var eff = EffectiveDamage(card.Damage, target);
            if (eff >= target.EffectiveHp)
                score += 220;
            score += Math.Max(0, 60 - target.EffectiveHp);
            score += target.IntentDamage * 3;
            if (target.IsMinion)
                score -= 25;

            score += ThreatEconomy.KillBeforeHitBonus(target, state);
        }

        if (card.IsAoe) {
            int kills = AoeDamageEstimator.EstimateAoeKills(state, card.Damage);
            score += kills * 80;
        }

        if (ThreatModel.IsFatalIfUnblocked(state) && ThreatModel.NetDamageAfterBlock(state) > card.Damage)
            score -= 40;

        return score;
    }

    static int ScoreBlock(CombatState state, CombatHandCard card) {
        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net <= 0) return 5;

        var effective = Math.Min(card.Block, net);
        var score = 25 + effective * 3;
        if (ThreatModel.IsFatalIfUnblocked(state))
            score += 50;
        return score;
    }

    static int ScoreEndTurn(CombatState state) {
        var playable = state.Hand.Count(c => c.CanPlay && c.Cost <= state.Energy);
        if (playable == 0)
            return 50;

        var net = ThreatModel.NetDamageAfterBlock(state);
        if (ThreatModel.IsFatalIfUnblocked(state))
            return int.MinValue;

        if (net > 0 && state.PlayerBlock < net)
            return 5;

        var nextPressure = ThreatModel.ScaledNextTurnPressure(state);
        if (ThreatModel.IncomingDamage(state) == 0 && nextPressure >= 8)
            return 5 - nextPressure / 2;

        var debt = CombatSetupEvaluator.ComputeSetupDebt(state);
        return 15 - playable * 3 - debt;
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

    static IEnumerable<int> OrderedAttackTargets(CombatState state) =>
        state.Enemies
            .Where(e => ThreatModel.IsViableAttackTarget(state, e))
            .OrderByDescending(e => e.IsMinion ? 0 : 1)
            .ThenBy(e => e.EffectiveHp)
            .ThenByDescending(e => e.IntentDamage)
            .ThenByDescending(e => ThreatModel.NextTurnAttackOn(e))
            .Take(4)
            .Select(e => e.Index);

    static int EffectiveDamage(int damage, CombatEnemy target) =>
        (int)Math.Round(damage * (target.Vulnerable > 0 ? 1.5f : 1f));

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0
        || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

    static JsonObject BuildSnapshot(CombatState state) => new() {
        ["currentHp"] = state.PlayerHp,
        ["maxHp"] = state.PlayerMaxHp,
        ["combat"] = new JsonObject {
            ["playerBlock"] = state.PlayerBlock,
            ["currentEnergy"] = state.Energy,
            ["enemies"] = new JsonArray(state.Enemies.Select(EnemyToJson).ToArray()),
        },
    };

    static JsonObject EnemyToJson(CombatEnemy enemy) => new() {
        ["index"] = enemy.Index,
        ["currentHp"] = enemy.CurrentHp,
        ["maxHp"] = enemy.MaxHp,
        ["block"] = enemy.Block,
        ["isAlive"] = enemy.IsAlive,
        ["intentDamage"] = enemy.IntentDamage,
        ["powers"] = enemy.Vulnerable > 0
            ? new JsonArray(new JsonObject { ["id"] = "VULNERABLE", ["amount"] = enemy.Vulnerable })
            : new JsonArray(),
    };
}
