using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace DevMode.AI.Combat;

/// <summary>
/// STS2: prefer primary enemies (summoners) over minions — killing the owner ends the threat.
/// </summary>
public static class EnemyTargetPriority {
    public static bool IsMinion(JsonObject? enemy) {
        if (enemy == null) return false;
        if (enemy["isMinion"]?.GetValue<bool>() == true) return true;

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

    public static int TargetBias(JsonArray? enemies, int targetIndex) {
        if (!HasAliveMinion(enemies) || targetIndex < 0) return 0;
        var target = enemies![targetIndex]?.AsObject();
        if (!IsAlive(target)) return 0;
        return IsMinion(target) ? -30 : 35;
    }

    public static IEnumerable<int> OrderByPriority(JsonArray enemies) {
        var alive = Enumerable.Range(0, enemies.Count)
            .Where(i => IsAlive(enemies[i]?.AsObject()))
            .ToList();
        if (!HasAliveMinion(enemies)) return alive;

        return alive
            .OrderByDescending(i => IsMinion(enemies[i]?.AsObject()) ? 0 : 1)
            .ThenBy(i => enemies[i]?["currentHp"]?.GetValue<int>() ?? int.MaxValue);
    }
}
