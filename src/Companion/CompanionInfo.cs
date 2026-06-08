using MegaCrit.Sts2.Core.Models;

namespace KitLib.Companion;

public sealed record CompanionInfo(
    ulong NetId,
    ModelId CharacterId,
    bool IsAiDriven,
    bool IsAlive);
