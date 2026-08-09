using System;
using KitLib;
using KitLib.Host;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

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
            if (ShouldAutoPublishHostConfig())
                MpCheatSync.TryPublishInitialHostConfig("handlers_ready");
            return;
        }

        netService.RegisterMessageHandler<ZzzMpCheatEnvelopeNetMessage>(OnEnvelopeReceived);
        _registeredService = netService;
        KitLog.Info("MpCheat", $"NetMessage handlers registered (envelope).");
        TryFlushPendingHostPublish();
        if (ShouldAutoPublishHostConfig())
            MpCheatSync.TryPublishInitialHostConfig("handlers_ready");
    }

    static bool ShouldAutoPublishHostConfig() =>
        !KitLibState.PseudoCoopDeferHeavyUi && !KitLibState.PseudoCoopDeferMpCheatPublish;

    public static void Reset() {
        _registeredService = null;
        _hostRevision = 0;
        _pendingHostConfig = null;
        _pendingHostReason = null;
        MpCheatCardAddCoordinator.Reset();
        MpCheatCardRemoveCoordinator.Reset();
        MpCheatCardEditCoordinator.Reset();
        MpCheatItemSyncCore.Reset();
        MpCheatConfigCoordinator.Reset();
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
            KitLog.Debug("MpCheat", $"Config publish deferred ({reason}); net not connected.");
            return;
        }

        _pendingHostConfig = null;
        _pendingHostReason = null;
        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromConfig((ulong)_hostRevision, MpCheatNetJson.SerializeConfig(config)));
        MpCheatRunSavedData.TryWrite(config);
        KitLog.Info("MpCheat", $"Config broadcast rev={_hostRevision} ({reason}).");
    }

    public static void BroadcastCommand(MpCheatCommandMessage message) {
        if (!MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromCommand(message));
        KitLog.Info("MpCheat", $"Command broadcast kind={message.Kind} id={message.CommandId}.");
        KitLibSyncBotOps.InjectPrepareAcks?.Invoke(message);
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
        KitLog.Debug("MpCheat", $"AddCard request sent to host id={request.ClientRequestId}.");
    }

    /// <summary>Host → client: add-card request outcome.</summary>
    public static void HostSendAddCardRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromAddCardRequestResult(result), peerNetId);
    }

    public static void ClientSendRemoveCardRequest(MpCheatRemoveCardClientRequestMessage request) {
        if (MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromRemoveCardRequest(request));
        KitLog.Debug("MpCheat", $"RemoveCard request sent to host id={request.ClientRequestId}.");
    }

    public static void HostSendRemoveCardRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromRemoveCardRequestResult(result), peerNetId);
    }

    public static void ClientSendEditCardRequest(MpCheatEditCardClientRequestMessage request) {
        if (MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromEditCardRequest(request));
        KitLog.Debug("MpCheat", $"EditCard request sent to host id={request.ClientRequestId}.");
    }

    public static void HostSendEditCardRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromEditCardRequestResult(result), peerNetId);
    }

    public static void ClientSendItemRequest(MpCheatItemClientRequestMessage request) {
        if (MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromItemRequest(request));
        KitLog.Debug("MpCheat", $"Item request sent to host id={request.ClientRequestId} kind={request.Payload.Kind}.");
    }

    public static void HostSendItemRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromItemRequestResult(result), peerNetId);
    }

    public static void ClientSendConfigRequest(MpCheatConfigClientRequestMessage request) {
        if (MpCheatSession.IsHost) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService == null || !CanSendNetMessages(netService)) return;

        SendEnvelope(netService, ZzzMpCheatEnvelopeNetMessage.FromConfigRequest(request));
        KitLog.Debug("MpCheat", $"Config request sent to host id={request.ClientRequestId}.");
    }

    public static void HostSendConfigRequestResult(ulong peerNetId, MpCheatAddCardClientResultMessage result) {
        if (!MpCheatSession.IsHost || peerNetId == 0) return;
        TryRegisterHandlers();
        var netService = RunManager.Instance?.NetService;
        if (netService is not NetHostGameService host || !host.IsConnected) return;

        host.SendMessage(ZzzMpCheatEnvelopeNetMessage.FromConfigRequestResult(result), peerNetId);
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
                    if (!MpCheatCardAddCoordinator.TryHandleAck(msg.Ack)
                        && !MpCheatCardRemoveCoordinator.TryHandleAck(msg.Ack)
                        && !MpCheatCardEditCoordinator.TryHandleAck(msg.Ack))
                        MpCheatItemSyncCore.TryHandleAck(msg.Ack);
                    break;
                case MpCheatWireChannel.AddCardRequest:
                    MpCheatCardAddCoordinator.OnClientAddCardRequestReceived(msg.AddCardRequest, senderId);
                    break;
                case MpCheatWireChannel.AddCardRequestResult:
                    MpCheatCardAddCoordinator.OnClientAddCardResultReceived(msg.AddCardRequestResult);
                    break;
                case MpCheatWireChannel.RemoveCardRequest:
                    MpCheatCardRemoveCoordinator.OnClientRemoveCardRequestReceived(msg.RemoveCardRequest, senderId);
                    break;
                case MpCheatWireChannel.RemoveCardRequestResult:
                    MpCheatCardRemoveCoordinator.OnClientRemoveCardResultReceived(msg.AddCardRequestResult);
                    break;
                case MpCheatWireChannel.EditCardRequest:
                    MpCheatCardEditCoordinator.OnClientEditCardRequestReceived(msg.EditCardRequest, senderId);
                    break;
                case MpCheatWireChannel.EditCardRequestResult:
                    MpCheatCardEditCoordinator.OnClientEditCardResultReceived(msg.AddCardRequestResult);
                    break;
                case MpCheatWireChannel.ItemRequest:
                    MpCheatItemSyncCore.OnClientItemRequestReceived(msg.ItemRequest, senderId);
                    break;
                case MpCheatWireChannel.ItemRequestResult:
                    MpCheatItemSyncCore.OnClientItemResultReceived(msg.AddCardRequestResult);
                    break;
                case MpCheatWireChannel.ConfigRequest:
                    MpCheatConfigCoordinator.OnClientConfigRequestReceived(msg.ConfigRequest, senderId);
                    break;
                case MpCheatWireChannel.ConfigRequestResult:
                    MpCheatConfigCoordinator.OnClientConfigResultReceived(msg.AddCardRequestResult);
                    break;
                default:
                    KitLog.Warn("MpCheat", $"Unknown envelope channel: {msg.Channel}");
                    break;
            }
        }
        catch (Exception ex) {
            KitLog.Warn("MpCheat", $"OnEnvelopeReceived failed ({msg.Channel}): {ex.Message}");
        }
    }

    private static void OnConfigBody(ulong revision, string configJson) {
        if ((long)revision <= MpCheatState.Revision) return;
        var config = MpCheatNetJson.DeserializeConfig(configJson);
        if (config == null) {
            KitLog.Warn("MpCheat", $"Config message had empty/invalid JSON.");
            return;
        }
        MpCheatState.ApplySnapshot(config, (long)revision, "net_config");
    }
}
