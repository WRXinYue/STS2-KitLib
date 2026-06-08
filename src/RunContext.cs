using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib;

internal static class RunContext {
    private static RunState? _pendingState;
    private static ulong _pendingPlayerNetId;

    public static bool TryGetRunAndPlayer(out RunState state, out Player player) {
        state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) {
            player = null;
            return false;
        }
        player = LocalContext.GetMe((IEnumerable<Player>)state.Players)
            ?? state.Players.FirstOrDefault();
        return player != null;
    }

    public static void Begin(RunState state, Player player) {
        _pendingState = state;
        _pendingPlayerNetId = player.NetId;
    }

    public static bool TryResolvePending(out RunState state, out Player player) {
        state = _pendingState ?? RunManager.Instance?.DebugOnlyGetState();
        if (state == null) {
            player = null;
            return false;
        }
        player = ((IEnumerable<Player>)state.Players)
                    .FirstOrDefault(p => p.NetId == _pendingPlayerNetId)
                ?? LocalContext.GetMe((IEnumerable<Player>)state.Players)
                ?? state.Players.FirstOrDefault();
        return player != null;
    }

    public static void Clear() {
        _pendingState = null;
        _pendingPlayerNetId = 0;
    }
}
