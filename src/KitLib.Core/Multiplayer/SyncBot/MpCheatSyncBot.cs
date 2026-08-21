using System;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.Play;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot;

/// <summary>Host-only: inject remote MpCheat ACKs in-process when SyncBot is on.</summary>
internal static class MpCheatSyncBot {
    internal const ulong PhantomPlayerNetId = 1001;

    public static bool IsEnabled =>
        AiSessionSettings.SyncBotEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.CanUseMultiplayerCheats;

    public static void RefreshSimulatedPeers() => HostDrivenPeers.Refresh();

    public static bool IsSimulatedPeer(ulong netId) => HostDrivenPeers.IsSimulatedPeer(netId);

    public static bool ShouldSimulatePlayer(Player player) {
        if (player == null) return false;
        if (!IsEnabled && !AiSessionSettings.MpAiTeammateEnabled) return false;
        if (!MpCheatSession.IsHost) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        return player.NetId != hostNetId && HostDrivenPeers.IsHostDrivenPeer(player.NetId);
    }

    public static NetPlayerChoiceResult DefaultIndexChoice() {
        var result = new NetPlayerChoiceResult { indexes = [0] };
        var typeField = typeof(NetPlayerChoiceResult).GetField("type");
        if (typeField != null) {
            var index = Enum.GetValues(typeField.FieldType).GetValue(0);
            if (index != null)
                typeField.SetValue(result, index);
        }
        return result;
    }

    public static void InjectPrepareAcks(MpCheatCommandMessage message) {
        if (!IsEnabled || !IsPrepareKind(message.Kind)) return;

        RefreshSimulatedPeers();
        var ackPeers = HostDrivenPeers.GetAckPeerNetIds();
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
            KitLog.Debug("SyncBot", $"Injected {ackPeers.Count} ACK(s) for command {message.CommandId} kind={message.Kind}.");
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
