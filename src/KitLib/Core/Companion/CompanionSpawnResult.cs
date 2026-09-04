using MegaCrit.Sts2.Core.Entities.Players;

namespace KitLib.Companion;

public sealed record CompanionSpawnResult(
    bool Ok,
    ulong NetId,
    string? Error,
    Player? Player = null);
