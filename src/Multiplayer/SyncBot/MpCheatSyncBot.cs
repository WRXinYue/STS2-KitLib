using System.Collections.Generic;
using KitLib.Companion;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot;

/// <summary>Host-only dev helper: simulate remote MpCheat ACKs and default player choices in-process.</summary>
internal static class MpCheatSyncBot {
    internal const ulong PhantomPlayerNetId = 1001;

    public static bool IsEnabled =>
        SettingsStore.Current.SyncBotEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.CanUseMultiplayerCheats;

    public static void RefreshSimulatedPeers() => SimulatedPeerRegistry.Refresh();

    public static bool IsSimulatedPeer(ulong netId) => SimulatedPeerRegistry.IsSimulatedPeer(netId);

    public static bool ShouldSimulatePlayer(Player player) {
        if (player == null) return false;
        if (!IsEnabled && !MpAiTeammateHost.IsEnabled) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        return player.NetId != hostNetId && SimulatedPeerRegistry.IsHostDrivenPeer(player.NetId);
    }

    public static void OnRunEnded() {
        PseudoCoopLobbyRoster.OnRunEnded();
        SimulatedPeerRegistry.OnRunEnded();
        MpAiTeammateHost.OnRunEnded();
        CompanionBridge.OnRunEnded();
    }

    public static NetPlayerChoiceResult DefaultIndexChoice() => new() {
        type = PlayerChoiceType.Index,
        indexes = [0],
    };

    public static void InjectPrepareAcks(MpCheatCommandMessage message) {
        if (!IsEnabled || !IsPrepareKind(message.Kind)) return;

        RefreshSimulatedPeers();
        var ackPeers = SimulatedPeerRegistry.GetAckPeerNetIds();
        foreach (var peerId in ackPeers) {
            var ack = new MpCheatAddCardAckMessage {
                CommandId = message.CommandId,
                PeerNetId = peerId,
                Success = true,
            };
            if (!MpCheatCardAddCoordinator.TryHandleAck(ack)
                && !MpCheatCardRemoveCoordinator.TryHandleAck(ack)
                && !MpCheatCardEditCoordinator.TryHandleAck(ack))
                MpCheatItemSyncCore.TryHandleAck(ack);
        }

        if (ackPeers.Count > 0)
            MainFile.Logger.Debug(
                $"[SyncBot] Injected {ackPeers.Count} ACK(s) for command {message.CommandId} kind={message.Kind}.");
    }

    static bool IsPrepareKind(MpCheatCommandKind kind) => kind switch {
        MpCheatCommandKind.AddCardPrepare => true,
        MpCheatCommandKind.RemoveCardPrepare => true,
        MpCheatCommandKind.EditCardPrepare => true,
        MpCheatCommandKind.AddRelicPrepare => true,
        MpCheatCommandKind.RemoveRelicPrepare => true,
        MpCheatCommandKind.AddPotionPrepare => true,
        MpCheatCommandKind.RemovePotionPrepare => true,
        MpCheatCommandKind.AddMonsterPrepare => true,
        MpCheatCommandKind.AddEncounterPrepare => true,
        MpCheatCommandKind.KillEnemyPrepare => true,
        MpCheatCommandKind.AddPowerPrepare => true,
        MpCheatCommandKind.RemovePowerPrepare => true,
        MpCheatCommandKind.ClearPowersPrepare => true,
        _ => false,
    };
}
