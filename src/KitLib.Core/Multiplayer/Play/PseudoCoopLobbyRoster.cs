using System.Reflection;
using KitLib.Multiplayer.SyncBot;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;
using MegaCrit.Sts2.Core.Runs;

#if STS2_STABLE_PROFILE
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
#else
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer;
#endif

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Registers simulated peers in <see cref="RunLobby"/> so combat sync does not skip them.</summary>
/// <remarks>
/// 0.110.0 replaced the private <c>_connectedPlayerIds</c> set of raw ids with a public
/// <c>Players</c> list of <c>RunLobbyPlayer</c>, off which <c>PlayerIds</c> is projected.
/// That is a public, mutable list, so the 0.110.1 path needs no reflection at all — which
/// also removes the failure mode where the renamed private field left
/// <c>ConnectedIdsField</c> null and every call threw <see cref="System.NullReferenceException"/>
/// out of <c>CompanionSpawnService.TrySpawnMultiplayerHost</c>, aborting the spawn after the
/// companion had already been added to the run.
/// </remarks>
internal static class PseudoCoopLobbyRoster {
#if STS2_STABLE_PROFILE
    static readonly FieldInfo ConnectedIdsField =
        AccessTools.Field(typeof(RunLobby), "_connectedPlayerIds")!;

    internal static void RegisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;
        if (!ids.Add(netId)) return;
        KitLog.Info("PseudoCoop", $"RunLobby connected roster +{netId} (now {ids.Count}).");
    }

    internal static void UnregisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;
        if (!ids.Remove(netId)) return;
        KitLog.Info("PseudoCoop", $"RunLobby connected roster -{netId}.");
    }

    internal static void OnRunEnded() {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (ConnectedIdsField.GetValue(lobby) is not HashSet<ulong> ids) return;

        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        foreach (var netId in ids.Where(id => id != hostNetId && id >= MpCheatSyncBot.PhantomPlayerNetId).ToList())
            UnregisterSimulatedPeer(netId);
    }
#else
    internal static void RegisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (lobby.Players.Any(p => p.id == netId)) return;

        // A phantom peer reports the local build, so the lobby's own mod/version diffing
        // sees a peer identical to the host rather than one with an empty handshake.
        lobby.Players.Add(CreateSimulatedLobbyPlayer(netId));
        KitLog.Info("PseudoCoop", $"RunLobby connected roster +{netId} (now {lobby.Players.Count}).");
    }

    internal static void UnregisterSimulatedPeer(ulong netId) {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;
        if (lobby.Players.RemoveAll(p => p.id == netId) == 0) return;
        KitLog.Info("PseudoCoop", $"RunLobby connected roster -{netId}.");
    }

    internal static void OnRunEnded() {
        var lobby = RunManager.Instance?.RunLobby;
        if (lobby == null) return;

        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        var phantoms = lobby.Players
            .Select(p => p.id)
            .Where(id => id != hostNetId && id >= MpCheatSyncBot.PhantomPlayerNetId)
            .ToList();

        foreach (var netId in phantoms)
            UnregisterSimulatedPeer(netId);
    }

    static RunLobbyPlayer CreateSimulatedLobbyPlayer(ulong netId) {
        object boxed = new RunLobbyPlayer { id = netId };
        var versionField = typeof(RunLobbyPlayer).GetField("versionInfo");
        if (versionField != null) {
            var localDefault = versionField.FieldType.GetMethod("LocalDefault", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (localDefault != null)
                versionField.SetValue(boxed, localDefault.Invoke(null, null));
        }
        return (RunLobbyPlayer)boxed;
    }
#endif
}
