using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI;

/// <summary>Scoped player for MpAiTeammateHost decisions (not the local human).</summary>
internal static class AiHostContext {
    public static ulong? ActiveNetId { get; set; }

    public static bool TryGetControlledPlayer(RunState state, out Player player) {
        player = null!;
        if (ActiveNetId is not ulong netId || netId == 0)
            return false;

        player = state.Players.FirstOrDefault(p => p.NetId == netId);
        return player != null;
    }

    public static void Clear() => ActiveNetId = null;
}
