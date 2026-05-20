using System;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Native STS2 INetMessage bus for MpCheat (no RitsuLib Sidecar).</summary>
internal static class MpCheatNetBus {
    private static object? _registeredService;
    private static long _hostRevision;

    public static bool IsReady => _registeredService != null;

    public static void TryRegisterHandlers() {
        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;
        if (_registeredService == (object)netService) return;

        netService.RegisterMessageHandler<MpCheatConfigNetMessage>(OnConfigReceived);
        netService.RegisterMessageHandler<MpCheatCommandNetMessage>(OnCommandReceived);
        netService.RegisterMessageHandler<MpCheatAddCardAckNetMessage>(OnAddCardAckReceived);
        _registeredService = netService;
        MainFile.Logger.Info("[MpCheat] NetMessage handlers registered.");
    }

    public static void Reset() {
        _registeredService = null;
        _hostRevision = 0;
    }

    public static void HostPublishConfig(MpCheatConfig config, string reason) {
        if (!MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;

        _hostRevision++;
        MpCheatState.ApplySnapshot(config, _hostRevision, reason);

        netService.SendMessage(new MpCheatConfigNetMessage {
            Revision = (ulong)_hostRevision,
            ConfigJson = MpCheatNetJson.SerializeConfig(config),
        });
        MpCheatRunSavedData.TryWrite(config);
        MainFile.Logger.Info($"[MpCheat] Config broadcast rev={_hostRevision} ({reason}).");
    }

    public static void BroadcastCommand(MpCheatCommandMessage message) {
        if (!MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;

        netService.SendMessage(MpCheatCommandNetMessage.FromDto(message));
        MainFile.Logger.Info($"[MpCheat] Command broadcast kind={message.Kind} id={message.CommandId}.");
    }

    public static void SendAddCardAck(MpCheatAddCardAckMessage ack) {
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;

        netService.SendMessage(new MpCheatAddCardAckNetMessage {
            CommandId = ack.CommandId,
            PeerNetId = ack.PeerNetId,
            Success = ack.Success,
            Error = ack.Error ?? "",
        });
    }

    private static void OnConfigReceived(MpCheatConfigNetMessage msg, ulong senderId) {
        try {
            if ((long)msg.Revision <= MpCheatState.Revision) return;
            var config = MpCheatNetJson.DeserializeConfig(msg.ConfigJson);
            if (config == null) {
                MainFile.Logger.Warn("[MpCheat] Config message had empty/invalid JSON.");
                return;
            }
            MpCheatState.ApplySnapshot(config, (long)msg.Revision, "net_config");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[MpCheat] OnConfigReceived failed: {ex.Message}");
        }
    }

    private static void OnCommandReceived(MpCheatCommandNetMessage msg, ulong senderId) {
        try {
            MpCheatCommandExecutor.Execute(msg.ToDto());
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[MpCheat] OnCommandReceived failed: {ex.Message}");
        }
    }

    private static void OnAddCardAckReceived(MpCheatAddCardAckNetMessage msg, ulong senderId) {
        try {
            MpCheatCardAddCoordinator.OnAckReceived(new MpCheatAddCardAckMessage {
                CommandId = msg.CommandId,
                PeerNetId = msg.PeerNetId,
                Success = msg.Success,
                Error = msg.Error,
            });
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[MpCheat] OnAddCardAckReceived failed: {ex.Message}");
        }
    }
}
