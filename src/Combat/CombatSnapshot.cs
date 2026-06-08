using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Multiplayer;

namespace KitLib.Combat;

internal readonly struct CombatSnapshot {
    internal int Round { get; init; }
    internal CombatSide CurrentSide { get; init; }
    internal NetFullCombatState State { get; init; }
}
