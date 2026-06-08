using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Mirrors host map votes to simulated peers after the host vote is recorded.</summary>
[HarmonyPatch(typeof(MapSelectionSynchronizer), nameof(MapSelectionSynchronizer.PlayerVotedForMapCoord))]
internal static class PseudoCoopMapVoteSynchronizerPatch {
    [HarmonyPostfix]
    static void Postfix(Player player, MapLocation source, MapVote? destination) {
        if (!destination.HasValue) return;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;
        if (!LocalContext.IsMe(player)) return;

        var map = NMapScreen.Instance;
        if (map == null) return;

        PseudoCoopMapVoteMirror.MirrorHostVote(map, source, destination.Value);

        // LAN / MP embark may never call NMapScreen.Open; first host map vote is a safe fallback.
        if (KitLibState.PseudoCoopAwaitingMapFinish)
            PseudoCoopDeferredInit.TryScheduleMapFinish();
    }
}

[HarmonyPatch(typeof(ActChangeSynchronizer), nameof(ActChangeSynchronizer.OnPlayerReady))]
internal static class PseudoCoopActReadyPatch {
    [HarmonyPostfix]
    static void Postfix(ActChangeSynchronizer __instance, Player player) {
        if (!PseudoCoopMapVoteMirror.ShouldMirror) return;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;
        if (!LocalContext.IsMe(player)) return;

        foreach (var peer in SimulatedPeerRegistry.GetMapMirrorTargets()) {
            __instance.OnPlayerReady(peer);
            MainFile.Logger.Info($"[PseudoCoop] Auto act-ready vote netId={peer.NetId}.");
        }
    }
}
