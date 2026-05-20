using System;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Native STS2 INetMessage bus for MpCheat (single envelope, no RitsuLib Sidecar).</summary>
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

        netService.RegisterMessageHandler<ZzzMpCheatEnvelopeNetMessage>(OnEnvelopeReceived);
        _registeredService = netService;
        MainFile.Logger.Info("[MpCheat] NetMessage handlers registered (envelope).");
        TryFlushPendingHostPublish();
    }

    public static void Reset() {
        _registeredService = null;
        _hostRevision = 0;
        _pendingHostConfig = null;
        _pendingHostReason = null;
        MpCheatCardAddCoordinator.Reset();
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
        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromConfig((ulong)_hostRevision, MpCheatNetJson.SerializeConfig(config)));
        MpCheatRunSavedData.TryWrite(config);
        MainFile.Logger.Info($"[MpCheat] Config broadcast rev={_hostRevision} ({reason}).");
    }

    public static void BroadcastCommand(MpCheatCommandMessage message) {
        if (!MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromCommand(message));
        MainFile.Logger.Info($"[MpCheat] Command broadcast kind={message.Kind} id={message.CommandId}.");
    }

    public static void SendAddCardAck(MpCheatAddCardAckMessage ack) {
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromAck(ack));
    }

    /// <summary>Client → host: request synced add-card.</summary>
    public static void ClientSendAddCardRequest(MpCheatAddCardClientRequestMessage request) {
        if (MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromAddCardRequest(request));
        MainFile.Logger.Debug(
            $"[MpCheat] AddCard request sent to host id={request.ClientRequestId}.");
    }

    /// <summary>Host → client: add-card request outcome.</summary>
    public static void HostSendAddCardRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromAddCardRequestResult(result), peerNetId);
    }

    private static void SendEnvelope(INetGameService netService, ZzzMpCheatEnvelopeNetMessage envelope) {
        netService.SendMessage(envelope);
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

    private static void OnEnvelopeReceived(ZzzMpCheatEnvelopeNetMessage msg, ulong senderId) {
        try {
            switch (msg.Channel) {
                case MpCheatWireChannel.Config:
                    OnConfigBody(msg.ConfigRevision, msg.ConfigJson);
                    break;
                case MpCheatWireChannel.Command:
                    MpCheatCommandExecutor.Execute(msg.Command);
                    break;
                case MpCheatWireChannel.AddCardAck:
                    MpCheatCardAddCoordinator.OnAckReceived(msg.Ack);
                    break;
                case MpCheatWireChannel.AddCardRequest:
                    MpCheatCardAddCoordinator.OnClientAddCardRequestReceived(msg.AddCardRequest, senderId);
                    break;
                case MpCheatWireChannel.AddCardRequestResult:
                    MpCheatCardAddCoordinator.OnClientAddCardResultReceived(msg.AddCardRequestResult);
                    break;
                default:
                    MainFile.Logger.Warn($"[MpCheat] Unknown envelope channel: {msg.Channel}");
                    break;
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[MpCheat] OnEnvelopeReceived failed ({msg.Channel}): {ex.Message}");
        }
    }

    private static void OnConfigBody(ulong revision, string configJson) {
        if ((long)revision <= MpCheatState.Revision) return;
        var config = MpCheatNetJson.DeserializeConfig(configJson);
        if (config == null) {
            MainFile.Logger.Warn("[MpCheat] Config message had empty/invalid JSON.");
            return;
        }
        MpCheatState.ApplySnapshot(config, (long)revision, "net_config");
    }
}
