using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Combat;

public static class LethalChecker {
    public static bool CanLethal(JsonObject snapshot, out int targetIndex) {
        targetIndex = -1;
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return false;

        foreach (var t in EnemyTargetPriority.OrderByAttackerKillPriority(enemies)) {
            if (enemies[t] is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            if (LethalExclusions.ShouldSkip(enemy)) continue;

            var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
            var block = enemy["block"]?.GetValue<int>() ?? 0;
            var damageNeeded = hp + block;
            if (damageNeeded <= 0) continue;

            if (LethalDamageSolver.MaxSingleTargetDamage(hand, energy, t, enemies) >= damageNeeded) {
                targetIndex = t;
                return true;
            }
        }

        return false;
    }

    public static int EstimateMaxDamage(JsonArray hand, int energy, int targetIndex, JsonArray? enemies = null) =>
        LethalDamageSolver.MaxSingleTargetDamage(hand, energy, targetIndex, enemies);

    public static bool CanLethalAfterTransform(JsonObject snapshot, out int targetIndex, out int transformIndex) {
        targetIndex = -1;
        transformIndex = -1;
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return false;

        transformIndex = CombatTransformSimulator.FindHandAttackTransformIndex(hand);
        if (transformIndex < 0) return false;

        var skill = hand[transformIndex]?.AsObject();
        if (skill == null) return false;
        if (CombatTransformSimulator.CountTransformableAttacks(hand) == 0) return false;

        var projected = CombatTransformSimulator.ProjectHandAfterTransform(hand, skill);
        var skillCost = skill["cost"]?.GetValue<int>() ?? 0;
        var energyAfter = Math.Max(0, energy - skillCost);

        foreach (var t in EnemyTargetPriority.OrderByAttackerKillPriority(enemies)) {
            if (enemies[t] is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;
            if (LethalExclusions.ShouldSkip(enemy)) continue;

            var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
            var block = enemy["block"]?.GetValue<int>() ?? 0;
            var damageNeeded = hp + block;
            if (damageNeeded <= 0) continue;

            if (LethalDamageSolver.MaxSingleTargetDamage(projected, energyAfter, t, enemies) >= damageNeeded) {
                targetIndex = t;
                return true;
            }
        }

        return false;
    }

    static bool IsAttack(JsonObject card) => CombatCardStats.IsAttackCard(card);
}
