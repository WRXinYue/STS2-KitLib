using System;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Native STS2 INetMessage bus for MpCheat (no RitsuLib Sidecar).</summary>
internal static class MpCheatNetBus {
    private static object? _registeredService;
    private static long _hostRevision;
    private static MpCheatConfig? _pendingHostConfig;
    private static string? _pendingHostReason;

    public static bool IsReady => _registeredService != null;

    public static void TryRegisterHandlers() {
        if (!MpCheatSession.LocalOptIn) return;

        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;
        if (_registeredService == (object)netService) {
            TryFlushPendingHostPublish();
            return;
        }

        netService.RegisterMessageHandler<ZzzMpCheatConfigNetMessage>(OnConfigReceived);
        netService.RegisterMessageHandler<ZzzMpCheatCommandNetMessage>(OnCommandReceived);
        netService.RegisterMessageHandler<ZzzMpCheatAddCardAckNetMessage>(OnAddCardAckReceived);
        _registeredService = netService;
        MainFile.Logger.Info("[MpCheat] NetMessage handlers registered.");
        TryFlushPendingHostPublish();
    }

    public static void Reset() {
        _registeredService = null;
        _hostRevision = 0;
        _pendingHostConfig = null;
        _pendingHostReason = null;
    }

    public static void HostPublishConfig(MpCheatConfig config, string reason) {
        if (!MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null) return;

        _hostRevision++;
        MpCheatState.ApplySnapshot(config, _hostRevision, reason);

        if (!CanSendNetMessages(netService)) {
            _pendingHostConfig = config;
            _pendingHostReason = reason;
            MainFile.Logger.Debug($"[MpCheat] Config publish deferred ({reason}); net not connected.");
            return;
        }

        _pendingHostConfig = null;
        _pendingHostReason = null;
        netService.SendMessage(new ZzzMpCheatConfigNetMessage {
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
        if (netService == null || !CanSendNetMessages(netService)) return;

        netService.SendMessage(ZzzMpCheatCommandNetMessage.FromDto(message));
        MainFile.Logger.Info($"[MpCheat] Command broadcast kind={message.Kind} id={message.CommandId}.");
    }

    public static void SendAddCardAck(MpCheatAddCardAckMessage ack) {
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        netService.SendMessage(new ZzzMpCheatAddCardAckNetMessage {
            CommandId = ack.CommandId,
            PeerNetId = ack.PeerNetId,
            Success = ack.Success,
            Error = ack.Error ?? "",
        });
    }

    private static void TryFlushPendingHostPublish() {
        if (_pendingHostConfig == null || _pendingHostReason == null) return;
        if (!MpCheatSession.IsHost) return;
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        var config = _pendingHostConfig;
        var reason = _pendingHostReason;
        _pendingHostConfig = null;
        _pendingHostReason = null;
        _hostRevision--;
        HostPublishConfig(config, reason);
    }

    private static bool CanSendNetMessages(INetGameService netService) =>
        netService switch {
            NetHostGameService host => host.IsConnected,
            NetClientGameService client => client.IsConnected,
            _ => false,
        };

    private static void OnConfigReceived(ZzzMpCheatConfigNetMessage msg, ulong senderId) {
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

    private static void OnCommandReceived(ZzzMpCheatCommandNetMessage msg, ulong senderId) {
        try {
            MpCheatCommandExecutor.Execute(msg.ToDto());
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[MpCheat] OnCommandReceived failed: {ex.Message}");
        }
    }

    private static void OnAddCardAckReceived(ZzzMpCheatAddCardAckNetMessage msg, ulong senderId) {
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
