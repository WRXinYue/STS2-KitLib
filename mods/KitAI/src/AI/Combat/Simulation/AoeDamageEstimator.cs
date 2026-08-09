using System;
using System.Collections.Generic;
using System.Linq;

namespace KitLib.AI.Combat.Simulation;

public static class AoeDamageEstimator {
    public static bool CanAoeLethalAll(CombatState state) {
        var aoeDamage = MaxAoeDamage(state);
        if (aoeDamage <= 0) return false;

        return state.Enemies
            .Where(e => e.IsAlive)
            .All(e => aoeDamage * VulnMultiplier(e) >= e.EffectiveHp);
    }

    public static int MaxAoeDamage(CombatState state) {
        int best = 0;
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (!card.IsAttack || !card.IsAoe) continue;
            best = Math.Max(best, CombatDamageCalc.OutgoingDamage(card, state));
        }
        return best;
    }

    public static int EstimateAoeKills(CombatState state, int aoeDamage) {
        if (aoeDamage <= 0) return 0;
        int kills = 0;
        foreach (var enemy in state.Enemies) {
            if (!enemy.IsAlive) continue;
            if (aoeDamage * VulnMultiplier(enemy) >= enemy.EffectiveHp)
                kills++;
        }
        return kills;
    }

    public static SimCombatAction? FindBestAoeLethalAction(CombatState state) {
        SimCombatAction? best = null;
        int bestKills = 0;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (!card.IsAttack || !card.IsAoe) continue;
            if (card.Damage <= 0) continue;

            int kills = EstimateAoeKills(state, CombatDamageCalc.OutgoingDamage(card, state));
            if (kills <= bestKills) continue;
            bestKills = kills;
            best = new SimCombatAction(SimActionKind.PlayCard, i, -1);
        }

        return bestKills > 0 ? best : null;
    }

    static float VulnMultiplier(CombatEnemy enemy) =>
        enemy.Vulnerable > 0 ? 1.5f : 1f;
}
