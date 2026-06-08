using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat;

/// <summary>Context-aware target bias replacing fixed minion +/-30.</summary>
public static class MinionEngagementPolicy {
    public const int StandardPrimaryBonus = 35;
    public const int StandardMinionPenalty = -30;
    public const int IllusionMinionPenalty = -60;
    public const int HighDebuffMinionBonus = 15;
    public const int LowDebuffMinionPenalty = -10;

    public static int TargetBias(JsonArray? enemies, int targetIndex) {
        if (enemies == null || targetIndex < 0 || targetIndex >= enemies.Count)
            return 0;

        var target = enemies[targetIndex]?.AsObject();
        if (!EnemyTargetPriority.IsAlive(target))
            return 0;

        var flags = EnemyMechanicResolver.ResolveFlags(target);
        var nonDamage = EnemyMechanicResolver.ResolveNonDamageThreat(target);

        if (flags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
            return IllusionMinionPenalty;

        if (flags.HasFlag(EnemyMechanicFlags.PeerSummon))
            return 0;

        if (flags.HasFlag(EnemyMechanicFlags.CanBossPhaseRespawn))
            return 10;

        bool hasDisposableMinion = enemies.Any(e =>
            EnemyTargetPriority.IsAlive(e?.AsObject())
            && EnemyMechanicResolver.IsDisposableMinion(e?.AsObject()));

        if (!hasDisposableMinion && !EnemyTargetPriority.HasAliveMinion(enemies))
            return 0;

        if (EnemyTargetPriority.IsMinion(target)) {
            if (nonDamage >= EnemyThreatWeights.DebuffStrong)
                return HighDebuffMinionBonus;
            if (nonDamage >= EnemyThreatWeights.Debuff)
                return LowDebuffMinionPenalty;
            return StandardMinionPenalty;
        }

        if (HasAliveSummonerContext(enemies, targetIndex))
            return StandardPrimaryBonus + SummonUrgencyBonus(target);

        return hasDisposableMinion ? StandardPrimaryBonus : 0;
    }

    static int SummonUrgencyBonus(JsonObject? primary) {
        if (primary == null) return 0;

        var tags = primary["intentTags"]?.AsArray();
        if (tags == null) return 0;

        return tags.Any(t => string.Equals(t?.GetValue<string>(), "Summon", StringComparison.OrdinalIgnoreCase))
            ? 10
            : 0;
    }

    static bool HasAliveSummonerContext(JsonArray enemies, int primaryIndex) {
        var primary = enemies[primaryIndex]?.AsObject();
        if (primary == null) return false;
        if (EnemyTargetPriority.IsMinion(primary)) return false;

        var primaryId = primary["monsterId"]?.GetValue<string>();
        MonsterMechanicIndex.TryGet(primaryId, out var primaryProfile);

        for (int i = 0; i < enemies.Count; i++) {
            if (i == primaryIndex) continue;
            var ally = enemies[i]?.AsObject();
            if (!EnemyTargetPriority.IsAlive(ally)) continue;

            if (EnemyTargetPriority.IsMinion(ally)) {
                var summoner = enemies[i]?["summonerIndex"]?.GetValue<int>() ?? -1;
                if (summoner == primaryIndex)
                    return true;
                continue;
            }

            var allyId = ally?["monsterId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(allyId) || string.IsNullOrWhiteSpace(primaryId))
                continue;

            if (primaryProfile.Flags.HasFlag(EnemyMechanicFlags.CanSummonAllies)
                && primaryProfile.SpawnedMonsterIds.Any(id =>
                    string.Equals(id, allyId, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        if (primaryProfile.Flags.HasFlag(EnemyMechanicFlags.CanSummonAllies)
            && EnemyTargetPriority.HasAliveMinion(enemies))
            return true;

        return false;
    }

    public static bool ShouldWipeMinionsOnPrimaryKill(JsonObject? killedEnemy) {
        if (killedEnemy == null) return false;
        if (EnemyTargetPriority.IsMinion(killedEnemy)) return false;

        var flags = EnemyMechanicResolver.ResolveFlags(killedEnemy);
        if (flags.HasFlag(EnemyMechanicFlags.PeerSummon))
            return false;

        return true;
    }

    public static bool ShouldSimulateMinionWipe(Simulation.CombatEnemy killed, Simulation.CombatEnemy[] enemies) {
        if (killed.IsMinion) return false;
        if (killed.MechanicFlags.HasFlag(EnemyMechanicFlags.PeerSummon))
            return false;

        return enemies.Any(e => e.IsAlive && e.IsMinion && !e.MechanicFlags.HasFlag(EnemyMechanicFlags.HasIllusionRevive));
    }
}
