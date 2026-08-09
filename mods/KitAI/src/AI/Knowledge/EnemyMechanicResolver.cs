using System;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Knowledge;

public static class EnemyMechanicResolver {
    public static EnemyMechanicFlags ResolveFlags(JsonObject? enemy) {
        if (enemy == null) return EnemyMechanicFlags.None;

        var flags = EnemyMechanicFlags.None;
        var monsterId = enemy["monsterId"]?.GetValue<string>();
        if (MonsterMechanicIndex.TryGet(monsterId, out var profile))
            flags = profile.Flags;

        if (enemy["isMinion"]?.GetValue<bool>() == true)
            flags |= EnemyMechanicFlags.IsSecondaryEnemy;

        if (HasPowerToken(enemy, "ILLUSION"))
            flags |= EnemyMechanicFlags.HasIllusionRevive;

        if (HasPowerToken(enemy, "MINION"))
            flags |= EnemyMechanicFlags.IsSecondaryEnemy;

        return flags;
    }

    public static int ResolveNonDamageThreat(JsonObject? enemy) {
        if (enemy == null) return 0;

        if (enemy["nonDamageThreat"]?.GetValue<int>() is int cached)
            return cached;

        return NonDamageThreatFromProfile(enemy);
    }

    static int NonDamageThreatFromProfile(JsonObject enemy) {
        var monsterId = enemy["monsterId"]?.GetValue<string>();
        var moveId = enemy["nextMoveId"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(monsterId) || string.IsNullOrWhiteSpace(moveId))
            return 0;

        return MoveEffectPressure.FromMove(monsterId, moveId);
    }

    public static bool IsIllusionMinion(JsonObject? enemy) =>
        ResolveFlags(enemy).HasFlag(EnemyMechanicFlags.HasIllusionRevive);

    public static bool IsDisposableMinion(JsonObject? enemy) {
        var flags = ResolveFlags(enemy);
        if (!flags.HasFlag(EnemyMechanicFlags.IsSecondaryEnemy))
            return false;
        if (flags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
            return false;
        return true;
    }

    static bool HasPowerToken(JsonObject enemy, string token) {
        var powers = enemy["powers"]?.AsArray();
        if (powers == null) return false;

        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (id.Contains(token, StringComparison.Ordinal))
                return true;
        }

        return false;
    }
}
