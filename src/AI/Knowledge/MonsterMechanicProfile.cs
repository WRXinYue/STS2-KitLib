using System.Collections.Generic;

namespace DevMode.AI.Knowledge;

public sealed record MonsterMechanicProfile(
    string MonsterId,
    EnemyMechanicFlags Flags,
    IReadOnlyList<MonsterMoveProfile> Moves,
    IReadOnlyList<string> SpawnedMonsterIds);
