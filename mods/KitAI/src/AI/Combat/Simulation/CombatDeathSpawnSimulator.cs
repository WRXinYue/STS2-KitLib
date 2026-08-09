using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Spawns encounter minions when a primary with <see cref="EnemyMechanicFlags.SpawnsOnDeath"/> dies.</summary>
internal static class CombatDeathSpawnSimulator {
    public static void TrySpawnOnDeath(IList<CombatEnemy> enemies, CombatEnemy killed) {
        if (!killed.IsAlive || killed.IsMinion)
            return;
        if (!MonsterMechanicIndex.TryGet(killed.MonsterId, out var profile))
            return;
        if (!profile.Flags.HasFlag(EnemyMechanicFlags.SpawnsOnDeath))
            return;
        if (profile.SpawnedMonsterIds.Count == 0)
            return;

        IReadOnlyList<CombatEnemy> roster = enemies.ToList();
        var state = StubState(roster);
        int spawnCount = MonsterProbeOverrides.GetDeathSpawnCount(killed.MonsterId);

        foreach (var spawnId in profile.SpawnedMonsterIds) {
            if (roster.Any(e => e.IsAlive
                    && string.Equals(e.MonsterId, spawnId, StringComparison.OrdinalIgnoreCase)))
                continue;

            for (int i = 0; i < spawnCount; i++) {
                int newIndex = CombatSummonFactory.NextEnemyIndex(roster);
                var spawned = CombatSummonFactory.CreateSummonedEnemy(
                    spawnId,
                    newIndex,
                    killed.Index,
                    roster,
                    state);
                enemies.Add(spawned);
                roster = enemies.ToList();
            }
        }
    }

    static CombatState StubState(IReadOnlyList<CombatEnemy> enemies) =>
        new(
            1, 1, 0, 0, 3, 0, 1,
            Array.Empty<CombatHandCard>(),
            Array.Empty<CombatPileCard>(),
            Array.Empty<CombatPileCard>(),
            Array.Empty<CombatPileCard>(),
            Array.Empty<PlayerCombatModifier>(),
            enemies,
            Array.Empty<string>(),
            Array.Empty<CombatPotionSlot>());
}
