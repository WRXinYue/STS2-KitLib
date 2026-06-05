using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DevMode.AI.Combat.Simulation;

public static class ThreatModel {
    public static int IncomingDamage(CombatState state) =>
        state.Enemies
            .Where(e => e.IsAlive && e.IntentDamage > 0)
            .Sum(e => e.IntentDamage);

    public static int NetDamageAfterBlock(CombatState state) =>
        Math.Max(0, IncomingDamage(state) - state.PlayerBlock);

    public static int EffectiveHp(CombatState state) =>
        Math.Max(1, state.PlayerHp - state.StatusDamage);

    public static bool IsFatalIfUnblocked(CombatState state) =>
        NetDamageAfterBlock(state) >= EffectiveHp(state);

    public static int NextTurnIncoming(CombatState state) =>
        state.Enemies
            .Where(e => e.IsAlive && e.IntentSteps.Length > 1)
            .Sum(e => e.IntentSteps[1].IntentDamage);

    public static int AliveThreatCount(CombatState state) =>
        state.Enemies.Count(e => e.IsAlive && e.IntentDamage > 0);

    public static bool CanEliminateAllThreats(CombatState state, int maxSingleTargetDamage) {
        var threats = state.Enemies
            .Where(e => e.IsAlive && e.IntentDamage > 0)
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

        bool hadAliveMinion = enemies.Any(e => e.IsAlive && e.IsMinion);
        if (!hadAliveMinion) return;

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive || !enemies[i].IsMinion) continue;
            enemies[i] = enemies[i].MarkDead();
        }
    }

    // JsonObject bridge for IntentCalculator
    public static int IncomingDamage(JsonObject snapshot) =>
        IncomingDamage(CombatState.FromSnapshot(snapshot));

    public static int NetDamageAfterBlock(JsonObject snapshot) =>
        NetDamageAfterBlock(CombatState.FromSnapshot(snapshot));
}
