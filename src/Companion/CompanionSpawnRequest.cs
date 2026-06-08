using KitLib.AI.Core;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Unlocks;

namespace KitLib.Companion;

public sealed record CompanionSpawnRequest(
    CharacterModel Character,
    ulong? PreferredNetId = null,
    UnlockState? UnlockState = null,
    IDecisionMaker? Strategy = null,
    bool EnableAiTeammate = true,
    bool MirrorMapVotes = true,
    bool EnableNonCombatAi = false);
