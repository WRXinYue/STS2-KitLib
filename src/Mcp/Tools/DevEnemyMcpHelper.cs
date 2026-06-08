using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Mcp.Tools;

internal static class DevEnemyMcpHelper {
    public static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };

    public static JsonArray SerializeMonsters(IEnumerable<MonsterModel> monsters, string? prefix = null) {
        var arr = new JsonArray();
        foreach (var monster in monsters) {
            var id = ((AbstractModel)monster).Id.Entry;
            if (!string.IsNullOrEmpty(prefix)
                && !id.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                continue;

            var entry = new JsonObject { ["monsterId"] = id };
            try {
                var title = monster.Title?.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                    entry["name"] = title;
            }
            catch { }
            arr.Add(entry);
        }
        return arr;
    }

    public static JsonArray SerializeEnemies(IReadOnlyList<Creature> enemies) {
        var arr = new JsonArray();
        for (var i = 0; i < enemies.Count; i++) {
            var enemy = enemies[i];
            var entry = new JsonObject {
                ["index"] = i,
                ["hp"] = enemy.CurrentHp,
                ["maxHp"] = enemy.MaxHp,
                ["block"] = enemy.Block,
                ["isDead"] = enemy.IsDead,
                ["powerCount"] = enemy.Powers.Count,
            };
            try {
                entry["monsterId"] = enemy.ModelId.Entry;
            }
            catch {
                entry["monsterId"] = "unknown";
            }
            try {
                var title = enemy.Monster?.Title?.GetFormattedText();
                if (!string.IsNullOrWhiteSpace(title))
                    entry["name"] = title;
            }
            catch { }
            arr.Add(entry);
        }
        return arr;
    }

    public static bool TryRequireCombat(out JsonObject? error) {
        error = null;
        if (CombatEnemyActions.GetCombatState() == null) {
            error = Fail("Not in combat.");
            return false;
        }
        return true;
    }

    public static MonsterModel? FindMonster(string monsterId) {
        if (string.IsNullOrWhiteSpace(monsterId))
            return null;
        return EnemyActions.GetAllMonsters().FirstOrDefault(m =>
            string.Equals(((AbstractModel)m).Id.Entry, monsterId.Trim(), System.StringComparison.OrdinalIgnoreCase));
    }
}
