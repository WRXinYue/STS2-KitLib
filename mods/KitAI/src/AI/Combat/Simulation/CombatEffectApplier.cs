using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatEffectApplier {
    public static void ApplyAoeDamage(List<CombatEnemy> enemies, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
        }
    }

    public static void ApplySingleDamage(List<CombatEnemy> enemies, int targetIndex, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != targetIndex && i != targetIndex) continue;
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
            return;
        }
    }

    public static void ApplyDebuff(
        List<CombatEnemy> enemies,
        int enemyIndex,
        bool isAoe,
        string token,
        int amount) {
        if (isAoe) {
            for (int i = 0; i < enemies.Count; i++) {
                if (!enemies[i].IsAlive) continue;
                enemies[i] = token == "VULNERABLE"
                    ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                    : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            }
            return;
        }

        if (enemyIndex < 0) return;
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != enemyIndex && i != enemyIndex) continue;
            enemies[i] = token == "VULNERABLE"
                ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            return;
        }
    }

    public static List<PlayerCombatModifier> AddModifier(
        IReadOnlyList<PlayerCombatModifier> modifiers,
        PlayerCombatModifier modifier) {
        var list = modifiers.ToList();
        list.Add(modifier);
        return list;
    }

    public static void ApplyEnemyStrengthDelta(List<CombatEnemy> enemies, int delta) {
        if (delta == 0) return;
        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            enemies[i] = enemies[i].AddStrength(delta);
        }
    }

    static CombatEnemy ApplyDamageToEnemy(CombatEnemy enemy, int damage) {
        var remaining = Math.Max(0, damage - enemy.Block);
        var newBlock = Math.Max(0, enemy.Block - damage);
        var newHp = Math.Max(0, enemy.CurrentHp - remaining);
        return enemy.WithHp(newHp, newBlock, newHp > 0);
    }
}
