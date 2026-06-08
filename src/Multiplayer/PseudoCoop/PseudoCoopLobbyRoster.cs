using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using KitLib.Multiplayer.SyncBot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Registers simulated peers in <see cref="RunLobby"/> so combat sync does not skip them.</summary>
internal static class PseudoCoopLobbyRoster {
    static readonly FieldInfo ConnectedIdsField =
        AccessTools.Field(typeof(RunLobby), "_connectedPlayerIds")!;

    internal static void RegisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;
        if (!ids.Add(netId)) return;
        MainFile.Logger.Info($"[PseudoCoop] RunLobby connected roster +{netId} (now {ids.Count}).");
    }

    internal static void UnregisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;
        if (!ids.Remove(netId)) return;
        MainFile.Logger.Info($"[PseudoCoop] RunLobby connected roster -{netId}.");
    }

    internal static void OnRunEnded() {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;

        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        foreach (var netId in ids.Where(id => id != hostNetId && id >= MpCheatSyncBot.PhantomPlayerNetId).ToList())
            UnregisterSimulatedPeer(netId);
    }
}
