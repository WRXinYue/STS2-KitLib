using System.Collections.Generic;
using System.Linq;
using KitLib.Host;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Which run peers participate in prepare/ACK for host commands (2–N players).</summary>
internal static class MpCheatParticipants {
    /// <summary>
    /// Remote peers the host waits on for add-card prepare ACKs.
    /// Uses run roster ∩ lobby connected set when available; otherwise all non-host run players.
    /// </summary>
    public static HashSet<ulong> GetAckRequiredPeerNetIds() {
        var run = RunManager.Instance;
        var hostNetId = run?.NetService?.NetId ?? 0;
        var state = run?.DebugOnlyGetState();
        if (state == null || hostNetId == 0) return [];

        var remotePlayers = state.Players
            .Select(p => p.NetId)
            .Where(id => id != hostNetId)
            .ToHashSet();

        if (KitLibSyncBotOps.IsEnabled?.Invoke() == true)
            return remotePlayers;

        var connected =
#if STS2_STABLE_PROFILE
            run.RunLobby?.ConnectedPlayerIds;
#else
            run.RunLobby?.PlayerIds?.ToHashSet();
#endif
        if (connected == null || connected.Count == 0)
            return remotePlayers;

        remotePlayers.IntersectWith(connected);
        return remotePlayers;
    }

    public static int RemotePeerCount => GetAckRequiredPeerNetIds().Count;
}
