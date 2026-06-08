using HarmonyLib;
using System.Linq;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Simulated peers must ready end-turn and enemy-turn phases (no ENet client to enqueue actions).</summary>
[HarmonyPatch(typeof(EndPlayerTurnAction), "ExecuteAction")]
internal static class PseudoCoopEndPlayerTurnPatch {
    [HarmonyPostfix]
    static void Postfix(EndPlayerTurnAction __instance) {
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;

        var host = Traverse.Create(__instance).Field<Player>("_player").Value;
        if (host == null || !LocalContext.IsMe(host)) return;

        PseudoCoopCombatReady.EnsureHostDrivenPeersEndTurn();
        PseudoCoopCombatReady.ReadyPhantomPeersToEndTurn();
    }
}

[HarmonyPatch(typeof(ReadyToBeginEnemyTurnAction), "ExecuteAction")]
internal static class PseudoCoopReadyEnemyTurnPatch {
    [HarmonyPostfix]
    static void Postfix() {
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;
        PseudoCoopCombatReady.ReadySimulatedPeersToBeginEnemyTurn();
    }
}

internal static class PseudoCoopCombatReady {
    internal static void EnsureHostDrivenPeersEndTurn() {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;

        foreach (var peer in SimulatedPeerRegistry.GetHostDrivenCombatPeers()) {
            if (peer.Creature.IsDead) continue;
            if (HasPlayableCard(peer)) continue;
            if (PseudoCoopActionQueue.HasPendingCombatActions(peer.NetId)) continue;
            MpAiTeammateCombatActions.SignalEndTurnForHostDrivenPeer(peer);
        }
    }

    static bool HasPlayableCard(Player player) {
        var hand = player.PlayerCombatState?.Hand?.Cards;
        return hand != null && hand.Any(c => c.CanPlay(out _, out _));
    }

    /// <summary>Phantom/offline peers only; host-driven live ENet uses EnsureHostDrivenPeersEndTurn.</summary>
    internal static void ReadyPhantomPeersToEndTurn() {
        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;

        foreach (var peer in SimulatedPeerRegistry.GetRemoteCombatAssistTargets()) {
            if (peer.Creature.IsDead) continue;
            if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(peer)) continue;
            if (cm.IsPlayerReadyToEndTurn(peer)) continue;

            cm.SetReadyToEndTurn(peer, canBackOut: false);
            MainFile.Logger.Info($"[PseudoCoop] Auto ready-to-end-turn netId={peer.NetId}.");
        }
    }

    internal static void ReadySimulatedPeersToBeginEnemyTurn() {
        var cm = CombatManager.Instance;
        if (cm is not { IsInProgress: true }) return;

        if (RunManager.Instance?.ActionQueueSynchronizer == null) return;

        foreach (var peer in SimulatedPeerRegistry.GetRemoteCombatAssistTargets()) {
            if (peer.Creature.IsDead) continue;
            if (Sts2CombatCompat.IsPlayerReadyToBeginEnemyTurn(cm, peer)) continue;
            if (PseudoCoopActionQueue.HasQueuedReadyToBeginEnemyTurn(peer.NetId)) continue;

            // Live ENet AFK client enqueues after host Ready + phase-1 checkpoint (see #11).
            if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(peer)) continue;

            cm.SetReadyToBeginEnemyTurn(peer);
            MainFile.Logger.Info($"[PseudoCoop] Auto ready-to-begin-enemy-turn netId={peer.NetId}.");
        }
    }
}
