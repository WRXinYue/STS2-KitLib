using System;
using System.Linq;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.SyncBot;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Companion;

internal static class CompanionNetIdAllocator {
    internal const int MaxCoopPlayers = 4;
    internal const ulong FirstCompanionNetId = MpCheatSyncBot.PhantomPlayerNetId;

    internal static bool TryAllocate(RunState state, ulong? preferredNetId, out ulong netId, out string? error) {
        netId = 0;
        error = null;

        if (state.Players.Count >= MaxCoopPlayers) {
            error = "Run is at maximum player count (4).";
            return false;
        }

        if (preferredNetId is ulong preferred) {
            if (preferred == 0) {
                error = "Preferred netId must be non-zero.";
                return false;
            }

            if (state.Players.Any(p => p.NetId == preferred)) {
                error = $"NetId {preferred} is already in use.";
                return false;
            }

            netId = preferred;
            return true;
        }

        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        for (ulong candidate = FirstCompanionNetId; candidate < FirstCompanionNetId + MaxCoopPlayers; candidate++) {
            if (candidate == hostNetId) continue;
            if (state.Players.All(p => p.NetId != candidate)) {
                netId = candidate;
                return true;
            }
        }

        error = "No companion netId slot available.";
        return false;
    }
}
