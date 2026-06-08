using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Powers;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.AI.Knowledge;

/// <summary>Locale-independent enemy mechanic discovery from official MonsterModel structure.</summary>
internal static class OfficialMonsterProbe {
    const BindingFlags MemberFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    public static EnemyMechanicFlags ProbeTypeGraph(MonsterModel monster) {
        var blob = CollectTypeTokenBlob(monster.GetType());
        return FlagsFromTokenBlob(blob);
    }

    public static MonsterMechanicProfile BuildProfile(
        MonsterModel monster,
        IReadOnlyList<string> spawnedMonsterIds) {
        var id = monster.Id.Entry ?? monster.GetType().Name;
        var moves = MonsterMoveScanner.ScanMoves(monster);
        var flags = MonsterMoveScanner.FlagsFromMoves(moves);
        flags |= ProbeTypeGraph(monster);
        flags |= MonsterProbeOverrides.GetExtraFlags(id);

        var spawnIds = new HashSet<string>(spawnedMonsterIds, StringComparer.OrdinalIgnoreCase);
        foreach (var extra in MonsterProbeOverrides.GetSpawnedIds(id))
            spawnIds.Add(extra);

        if (flags.HasFlag(EnemyMechanicFlags.CanSummonAllies)
            && !flags.HasFlag(EnemyMechanicFlags.PeerSummon)
            && spawnIds.Count > 0)
            flags |= EnemyMechanicFlags.SpawnsWithMinionPower;

        return new MonsterMechanicProfile(
            id,
            flags,
            moves,
            spawnIds.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList());
    }

    static EnemyMechanicFlags FlagsFromTokenBlob(string blob) {
        if (string.IsNullOrWhiteSpace(blob))
            return EnemyMechanicFlags.None;

        var upper = blob.ToUpperInvariant();
        var flags = EnemyMechanicFlags.None;

        if (upper.Contains("ILLUSIONPOWER", StringComparison.Ordinal))
            flags |= EnemyMechanicFlags.HasIllusionRevive | EnemyMechanicFlags.IsSecondaryEnemy;

        if (upper.Contains("MINIONPOWER", StringComparison.Ordinal))
            flags |= EnemyMechanicFlags.IsSecondaryEnemy;

        if (upper.Contains("ADAPTABLEPOWER", StringComparison.Ordinal)
            || upper.Contains("TESTSUBJECT", StringComparison.Ordinal))
            flags |= EnemyMechanicFlags.CanBossPhaseRespawn;

        if (upper.Contains("TWOTAILED", StringComparison.Ordinal))
            flags |= EnemyMechanicFlags.PeerSummon;

        if (upper.Contains("TOUGHEGG", StringComparison.Ordinal))
            flags |= EnemyMechanicFlags.HatchesInPlace | EnemyMechanicFlags.IsSecondaryEnemy;

        return flags;
    }

    static string CollectTypeTokenBlob(Type type) {
        var tokens = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var t = type; t != null && t != typeof(object); t = t.BaseType) {
            if (!string.IsNullOrWhiteSpace(t.Name))
                tokens.Add(t.Name);
            if (!string.IsNullOrWhiteSpace(t.Namespace))
                tokens.Add(t.Namespace);

            foreach (var method in t.GetMethods(MemberFlags)) {
                if (!string.IsNullOrWhiteSpace(method.Name))
                    tokens.Add(method.Name);
            }
        }

        return string.Join(' ', tokens);
    }

    public static int NonDamageThreatFromIntentTypes(IEnumerable<IntentType> intentTypes) {
        _ = intentTypes;
        return 0;
    }
}
