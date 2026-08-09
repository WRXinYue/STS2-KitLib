using System.Collections.Generic;

namespace KitLib.AI.Knowledge;

public sealed record MonsterMechanicProfile(
    string MonsterId,
    EnemyMechanicFlags Flags,
    IReadOnlyList<MonsterMoveProfile> Moves,
    IReadOnlyList<string> SpawnedMonsterIds);
