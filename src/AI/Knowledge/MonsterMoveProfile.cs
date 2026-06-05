using System.Collections.Generic;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace DevMode.AI.Knowledge;

public sealed record MonsterMoveProfile(
    string MoveId,
    IReadOnlyList<IntentType> IntentTypes);
