using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>
/// STS2: prefer primary enemies (summoners) over disposable minions.
/// Target bias delegated to <see cref="MinionEngagementPolicy"/>.
/// </summary>
public static class EnemyTargetPriority {
    public static bool IsMinion(JsonObject? enemy) {
        if (enemy == null) return false;
        if (enemy["isMinion"]?.GetValue<bool>() == true) return true;

        var flags = EnemyMechanicResolver.ResolveFlags(enemy);
        if (flags.HasFlag(EnemyMechanicFlags.IsSecondaryEnemy))
            return true;

        var powers = enemy["powers"]?.AsArray();
        if (powers == null) return false;

        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (id.Contains("MINION")) return true;
        }

        return false;
    }

    public static bool IsAlive(JsonObject? enemy) =>
        enemy != null && enemy["isAlive"]?.GetValue<bool>() != false;

    public static bool HasAliveMinion(JsonArray? enemies) {
        if (enemies == null) return false;
        return enemies.Any(e => IsAlive(e?.AsObject()) && IsMinion(e?.AsObject()));
    }

    public static bool HasAliveNonMinion(JsonArray? enemies) {
        if (enemies == null) return false;
        return enemies.Any(e => IsAlive(e?.AsObject()) && !IsMinion(e?.AsObject()));
    }

    public static int TargetBias(JsonArray? enemies, int targetIndex) =>
        MinionEngagementPolicy.TargetBias(enemies, targetIndex);

    public static IEnumerable<int> OrderByPriority(JsonArray enemies) {
        var alive = Enumerable.Range(0, enemies.Count)
            .Where(i => IsAlive(enemies[i]?.AsObject()))
            .ToList();

        if (!alive.Any(i => MinionEngagementPolicy.TargetBias(enemies, i) < 0))
            return alive.OrderBy(i => enemies[i]?["currentHp"]?.GetValue<int>() ?? int.MaxValue);

        return alive
            .OrderByDescending(i => MinionEngagementPolicy.TargetBias(enemies, i))
            .ThenBy(i => enemies[i]?["currentHp"]?.GetValue<int>() ?? int.MaxValue);
    }

    /// <summary>Attackers first (by intent, then HP), then default priority order.</summary>
    public static IEnumerable<int> OrderByAttackerKillPriority(JsonArray enemies) {
        var alive = Enumerable.Range(0, enemies.Count)
            .Where(i => IsAlive(enemies[i]?.AsObject()))
            .ToList();

        var attackers = alive
            .Where(i => (enemies[i]?["intentDamage"]?.GetValue<int>() ?? 0) > 0)
            .OrderByDescending(i => enemies[i]?["intentDamage"]?.GetValue<int>() ?? 0)
            .ThenBy(i => enemies[i]?["currentHp"]?.GetValue<int>() ?? int.MaxValue)
            .ToList();

        if (attackers.Count > 0) {
            var rest = OrderByPriority(enemies).Where(i => !attackers.Contains(i));
            return attackers.Concat(rest);
        }

        return OrderByPriority(enemies);
    }
}
