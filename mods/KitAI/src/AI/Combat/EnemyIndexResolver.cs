using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace KitLib.AI.Combat;

/// <summary>Maps snapshot enemy array slots ↔ combat <c>index</c> field (0-based slot in <c>CombatState.Enemies</c>, stable when others die).</summary>
internal static class EnemyIndexResolver {
    public static int CombatIndex(JsonObject enemy, int arraySlot) =>
        enemy["index"]?.GetValue<int>() ?? arraySlot;

    public static JsonObject? FindByCombatIndex(JsonArray? enemies, int combatIndex) {
        if (enemies == null || combatIndex < 0)
            return null;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject enemy)
                continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false)
                continue;
            if (CombatIndex(enemy, i) == combatIndex)
                return enemy;
        }

        if (combatIndex < enemies.Count)
            return enemies[combatIndex]?.AsObject();

        return null;
    }

    public static int ArraySlot(JsonArray? enemies, int combatIndex) {
        if (enemies == null || combatIndex < 0)
            return -1;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject enemy)
                continue;
            if (CombatIndex(enemy, i) == combatIndex)
                return i;
        }

        return combatIndex < enemies.Count ? combatIndex : -1;
    }

    public static IEnumerable<int> ViableCombatIndices(JsonArray? enemies) {
        if (enemies == null)
            yield break;

        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i] is not JsonObject enemy)
                continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false)
                continue;
            yield return CombatIndex(enemy, i);
        }
    }
}
