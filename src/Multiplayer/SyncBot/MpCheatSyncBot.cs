using System.Collections.Generic;
using System.Linq;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Entities.Models;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.SyncBot;

/// <summary>Host-only dev helper: simulate remote MpCheat ACKs and default player choices in-process.</summary>
internal static class MpCheatSyncBot {
    internal const ulong PhantomPlayerNetId = 1001;

    static HashSet<ulong> _simulatedPeerNetIds = [];

    public static bool IsEnabled =>
        SettingsStore.Current.SyncBotEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.CanUseMultiplayerCheats;

    public static void RefreshSimulatedPeers() {
        _simulatedPeerNetIds.Clear();
        if (!IsEnabled) return;

        var run = RunManager.Instance;
        var hostNetId = run?.NetService?.NetId ?? 0;
        var state = run?.DebugOnlyGetState();
        if (state == null || hostNetId == 0) return;

        foreach (var id in state.Players.Select(p => p.NetId).Where(id => id != hostNetId))
            _simulatedPeerNetIds.Add(id);
    }

    public static bool IsSimulatedPeer(ulong netId) =>
        IsEnabled && _simulatedPeerNetIds.Contains(netId);

    public static bool ShouldSimulatePlayer(Player player) {
        if (!IsEnabled || player == null) return false;
        var hostNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        return player.NetId != hostNetId && IsSimulatedPeer(player.NetId);
    }

    public static void OnRunEnded() => _simulatedPeerNetIds.Clear();

    public static NetPlayerChoiceResult DefaultIndexChoice() => new() {
        type = PlayerChoiceType.Index,
        indexes = [0],
    };

    public static void InjectPrepareAcks(MpCheatCommandMessage message) {
        if (!IsEnabled || !IsPrepareKind(message.Kind)) return;

        RefreshSimulatedPeers();
        foreach (var peerId in _simulatedPeerNetIds) {
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

        if (_simulatedPeerNetIds.Count > 0)
            MainFile.Logger.Debug(
                $"[SyncBot] Injected {_simulatedPeerNetIds.Count} ACK(s) for command {message.CommandId} kind={message.Kind}.");
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
