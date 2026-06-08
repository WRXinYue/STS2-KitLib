using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.AI.Knowledge;

public sealed record MonsterMoveProfile(
    string MoveId,
    IReadOnlyList<IntentType> IntentTypes,
    IReadOnlyList<MonsterMoveEffect> Effects) {
    public MonsterMoveProfile(string moveId, IReadOnlyList<IntentType> intentTypes)
        : this(moveId, intentTypes, Array.Empty<MonsterMoveEffect>()) { }
}
