using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class ThreatModel {
    public static int IncomingDamage(CombatState state) =>
        state.Enemies
            .Where(e => e.IsAlive && e.EffectiveIncoming > 0)
            .Sum(e => e.EffectiveIncoming);

    public static int NetDamageAfterBlock(CombatState state) =>
        Math.Max(0, IncomingDamage(state) - state.PlayerBlock);

    public static int EffectiveHp(CombatState state) =>
        Math.Max(1, state.PlayerHp - state.StatusDamage);

    public static bool IsFatalIfUnblocked(CombatState state) =>
        NetDamageAfterBlock(state) >= EffectiveHp(state);

    public static int NextTurnIncoming(CombatState state) {
        double total = 0;
        foreach (var enemy in state.Enemies.Where(e => e.IsAlive && e.IntentSteps.Length > 1)) {
            var step = enemy.IntentSteps[1];
            var damage = step.IntentDamage + step.NonDamageThreat;
            if (step.IsUncertain)
                damage = (int)Math.Round(damage * EnemyThreatWeights.NextTurnUncertainMultiplier);
            total += damage;
        }

        return (int)Math.Round(total);
    }

    public static int AliveThreatCount(CombatState state) =>
        state.Enemies.Count(e => e.IsAlive && e.EffectiveIncoming > 0);

    public static bool CanEliminateAllThreats(CombatState state, int maxSingleTargetDamage) {
        var threats = state.Enemies
            .Where(e => e.IsAlive && e.EffectiveIncoming > 0)
            .ToList();
        if (threats.Count == 0) return true;

        foreach (var threat in threats) {
            if (threat.EffectiveHp > maxSingleTargetDamage)
                return false;
        }

        return NetDamageAfterBlock(state) <= 8;
    }

    public static void OnPrimaryEnemyKilled(IList<CombatEnemy> enemies, int killedIndex) {
        if (killedIndex < 0 || killedIndex >= enemies.Count) return;
        if (enemies[killedIndex].IsMinion) return;
        if (!MinionEngagementPolicy.ShouldSimulateMinionWipe(
                enemies[killedIndex], enemies.ToArray()))
            return;

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive || !enemies[i].IsMinion) continue;
            if (enemies[i].MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
                continue;
            enemies[i] = enemies[i].MarkDead();
        }
    }

    public static int NonDamageThreatFromJson(JsonObject? enemy) =>
        EnemyMechanicResolver.ResolveNonDamageThreat(enemy);

    // JsonObject bridge for IntentCalculator
    public static int IncomingDamage(JsonObject snapshot) =>
        IncomingDamage(CombatState.FromSnapshot(snapshot));

    public static int NetDamageAfterBlock(JsonObject snapshot) =>
        NetDamageAfterBlock(CombatState.FromSnapshot(snapshot));
}
