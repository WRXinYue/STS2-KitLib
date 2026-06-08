using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

internal delegate bool MpCheatItemValidateDelegate(MpCheatItemPayload payload, out string? error);

internal delegate Task MpCheatItemExecuteDelegate(MpCheatItemPayload payload);

/// <summary>Shared prepare/ACK/execute flow for relic, potion, and combat item cheats.</summary>
internal static class MpCheatItemSyncCore {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, PendingItem> PendingByCommandId = new();
    private static readonly HashSet<ulong> ExecutedCommandIds = new();
    private static readonly Dictionary<ulong, TaskCompletionSource<string>> ClientCompletions = new();
    private static ulong _nextCommandId;
    private static ulong _nextClientRequestId;

    private const int BaseAckTimeoutMs = 8000;
    private const int ClientRequestTimeoutMs = 25000;
    private const int AckTimeoutPerPeerMs = 1500;
    private const int MaxAckTimeoutMs = 20000;
    private const int MaxConcurrentPending = 64;
    private const int MaxExecutedIdHistory = 256;

    private sealed class PendingItem {
        public required ulong CommandId { get; init; }
        public required MpCheatItemPayload Payload { get; init; }
        public required MpCheatCommandKind ExecuteKind { get; init; }
        public required HashSet<ulong> AwaitingPeers { get; init; }
        public required int RequiredAckCount { get; init; }
        public required MpCheatItemExecuteDelegate Execute { get; init; }
        public required string LogTag { get; init; }
        public Dictionary<ulong, MpCheatAddCardAckMessage> Acks { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static async Task<string> TryHostAsync(
        MpCheatCommandKind prepareKind,
        MpCheatCommandKind executeKind,
        MpCheatItemPayload payload,
        MpCheatItemValidateDelegate validate,
        MpCheatItemExecuteDelegate execute,
        string logTag,
        Func<MpCheatItemPayload, string> formatSuccess) {
        var (_, message) = await TryHostWithResultAsync(
            prepareKind, executeKind, payload, validate, execute, logTag, formatSuccess);
        return message;
    }

    public static Task<(bool Success, string Message)> TryHostWithResultAsync(
        MpCheatCommandKind prepareKind,
        MpCheatCommandKind executeKind,
        MpCheatItemPayload payload,
        MpCheatItemValidateDelegate validate,
        MpCheatItemExecuteDelegate execute,
        string logTag,
        Func<MpCheatItemPayload, string> formatSuccess) =>
        TryHostCoreAsync(prepareKind, executeKind, payload, validate, execute, logTag, formatSuccess);

    public static async Task<string> TryClientRequestAsync(
        MpCheatItemPayload payload,
        MpCheatItemValidateDelegate validate,
        bool requireSelfTarget,
        string logTag) {
        if (MpCheatSession.IsHost)
            return I18N.T("mpcheat.item.hostDirect", "Use host controls to apply this cheat.");

        if (!MpCheatSession.CanUseMultiplayerCheats)
            return I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");

        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (localNetId == 0)
            return FormatError("no local net id");

        if (requireSelfTarget && payload.TargetPlayerNetId != 0 && payload.TargetPlayerNetId != localNetId)
            return I18N.T(
                "mpcheat.item.clientSelfOnly",
                "In multiplayer you can only change your own character for this action.");

        if (!validate(payload, out var err))
            return FormatError(err ?? "validation failed");

        var clientRequestId = Interlocked.Increment(ref _nextClientRequestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate) {
            ClientCompletions[clientRequestId] = completion;
        }

        MpCheatNetBus.ClientSendItemRequest(new MpCheatItemClientRequestMessage {
            ClientRequestId = clientRequestId,
            RequesterNetId = localNetId,
            Payload = payload,
        });
        MainFile.Logger.Info($"[MpCheat] {logTag} client request id={clientRequestId} kind={payload.Kind} item={payload.ItemId}.");

        try {
            using var cts = new CancellationTokenSource(ClientRequestTimeoutMs);
            return await completion.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            return I18N.T(
                "mpcheat.item.clientRequestTimeout",
                "Host did not respond to item cheat request in time.");
        }
        finally {
            lock (Gate) {
                ClientCompletions.Remove(clientRequestId);
            }
        }
    }

    public static void OnClientItemRequestReceived(MpCheatItemClientRequestMessage request, ulong senderId) {
        if (!MpCheatSession.IsHost) return;
        TaskHelper.RunSafely(HandleClientItemRequestAsync(request, senderId));
    }

    public static void OnClientItemResultReceived(MpCheatAddCardClientResultMessage result) {
        if (MpCheatSession.IsHost) return;
        TaskCompletionSource<string>? completion;
        lock (Gate) {
            ClientCompletions.TryGetValue(result.ClientRequestId, out completion);
        }

        if (completion == null) {
            MainFile.Logger.Debug(
                $"[MpCheat] Item client result id={result.ClientRequestId} ignored (no pending UI).");
            return;
        }

        completion.TrySetResult(result.Message);
    }

    public static void OnPrepareReceived(
        MpCheatCommandMessage message,
        MpCheatItemValidateDelegate validate,
        string logTag) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.Item == null) return;
        if (MpCheatSession.IsHost) return;

        MainFile.Logger.Info(
            $"[MpCheat] {logTag} prepare id={message.CommandId} kind={message.Item.Kind} item={message.Item.ItemId}");
        var ok = validate(message.Item, out var error);
        var peerNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.SendAddCardAck(new MpCheatAddCardAckMessage {
            CommandId = message.CommandId,
            PeerNetId = peerNetId,
            Success = ok,
            Error = error,
        });
    }

    public static void OnExecuteReceived(
        MpCheatCommandMessage message,
        MpCheatItemExecuteDelegate execute,
        string logTag) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.Item == null) return;
        if (MpCheatSession.IsHost) return;

        lock (Gate) {
            if (!TrackExecuted(message.CommandId)) {
                MainFile.Logger.Debug($"[MpCheat] {logTag} execute id={message.CommandId} skipped (duplicate).");
                return;
            }
        }

        MainFile.Logger.Info(
            $"[MpCheat] {logTag} execute id={message.CommandId} kind={message.Item.Kind} item={message.Item.ItemId}");
        TaskHelper.RunSafely(execute(message.Item));
    }

    public static bool TryHandleAck(MpCheatAddCardAckMessage ack) {
        if (!MpCheatSession.IsHost) return false;

        lock (Gate) {
            if (!PendingByCommandId.TryGetValue(ack.CommandId, out var pending)) return false;

            pending.Acks[ack.PeerNetId] = ack;
            if (!ack.Success) {
                pending.Completion.TrySetResult(false);
                return true;
            }

            pending.AwaitingPeers.Remove(ack.PeerNetId);
            if (pending.AwaitingPeers.Count == 0)
                pending.Completion.TrySetResult(true);
            return true;
        }
    }

    internal static void Reset() {
        lock (Gate) {
            foreach (var pending in PendingByCommandId.Values)
                pending.Completion.TrySetResult(false);
            PendingByCommandId.Clear();
            ExecutedCommandIds.Clear();
            foreach (var tcs in ClientCompletions.Values)
                tcs.TrySetResult(I18N.T("mpcheat.item.cancelled", "Item cheat cancelled (run ended)."));
            ClientCompletions.Clear();
        }
    }

    private static async Task HandleClientItemRequestAsync(
        MpCheatItemClientRequestMessage request,
        ulong senderId) {
        void Reply(bool success, string message) =>
            MpCheatNetBus.HostSendItemRequestResult(senderId, new MpCheatAddCardClientResultMessage {
                ClientRequestId = request.ClientRequestId,
                Success = success,
                Message = message,
            });

        if (!MpCheatSession.CanEditMultiplayerCheats) {
            Reply(false, I18N.T("mpcheat.item.hostOnly", "Only the host can apply this cheat in multiplayer."));
            return;
        }

        if (request.RequesterNetId != senderId) {
            Reply(false, FormatError("request sender mismatch"));
            return;
        }

        var payload = request.Payload;
        var requireSelf = payload.Kind is MpCheatItemKind.AddRelic or MpCheatItemKind.RemoveRelic
            or MpCheatItemKind.AddPotion or MpCheatItemKind.RemovePotion
            or MpCheatItemKind.AddPower or MpCheatItemKind.RemovePower or MpCheatItemKind.ClearPowers;
        if (requireSelf && payload.TargetPlayerNetId != 0 && payload.TargetPlayerNetId != senderId) {
            Reply(false, I18N.T(
                "mpcheat.item.clientSelfOnly",
                "In multiplayer you can only change your own character for this action."));
            return;
        }

        var (success, message) = await DispatchHostFromClientRequestAsync(payload);
        Reply(success, message);
    }

    private static Task<(bool Success, string Message)> DispatchHostFromClientRequestAsync(MpCheatItemPayload payload) =>
        payload.Kind switch {
            MpCheatItemKind.AddRelic => MpCheatRelicCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.RemoveRelic => MpCheatRelicCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.AddPotion => MpCheatPotionCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.RemovePotion => MpCheatPotionCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.AddMonster => MpCheatCombatEnemyCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.AddEncounter => MpCheatCombatEnemyCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.KillEnemy => MpCheatCombatEnemyCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.KillAllEnemies => MpCheatCombatEnemyCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.AddPower => MpCheatPowerCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.RemovePower => MpCheatPowerCoordinator.TryHostFromPayloadCoreAsync(payload),
            MpCheatItemKind.ClearPowers => MpCheatPowerCoordinator.TryHostFromPayloadCoreAsync(payload),
            _ => Task.FromResult((false, FormatError("unknown item kind"))),
        };

    private static async Task<(bool Success, string Message)> TryHostCoreAsync(
        MpCheatCommandKind prepareKind,
        MpCheatCommandKind executeKind,
        MpCheatItemPayload payload,
        MpCheatItemValidateDelegate validate,
        MpCheatItemExecuteDelegate execute,
        string logTag,
        Func<MpCheatItemPayload, string> formatSuccess) {
        if (!MpCheatSession.CanEditMultiplayerCheats)
            return (false, I18N.T("mpcheat.item.hostOnly", "Only the host can apply this cheat in multiplayer."));

        if (!validate(payload, out var err))
            return (false, FormatError(err ?? "validation failed"));

        var commandId = Interlocked.Increment(ref _nextCommandId);
        var awaitingPeers = MpCheatParticipants.GetAckRequiredPeerNetIds();

        PendingItem pending;
        lock (Gate) {
            PruneIfOverCapacity();
            pending = new PendingItem {
                CommandId = commandId,
                Payload = payload,
                ExecuteKind = executeKind,
                AwaitingPeers = awaitingPeers,
                RequiredAckCount = awaitingPeers.Count,
                Execute = execute,
                LogTag = logTag,
            };
            PendingByCommandId[commandId] = pending;
        }

        MainFile.Logger.Info(
            $"[MpCheat] {logTag} host start id={commandId} kind={payload.Kind} item={payload.ItemId} ackPeers={awaitingPeers.Count}.");

        BroadcastCommand(prepareKind, commandId, payload);

        if (awaitingPeers.Count == 0)
            return (true, await FinishHostExecute(pending, formatSuccess));

        var timeoutMs = ComputeAckTimeoutMs(awaitingPeers.Count);
        using var cts = new CancellationTokenSource(timeoutMs);
        try {
            var ackOk = await pending.Completion.Task.WaitAsync(cts.Token);
            if (!ackOk) {
                var fail = pending.Acks.Values.FirstOrDefault(a => !a.Success);
                RemovePending(commandId);
                return (false, fail != null ? FormatPeerError(fail) : FormatAckTimeout(pending));
            }

            return (true, await FinishHostExecute(pending, formatSuccess));
        }
        catch (OperationCanceledException) {
            RemovePending(commandId);
            return (false, FormatAckTimeout(pending));
        }
    }

    private static async Task<string> FinishHostExecute(
        PendingItem pending,
        Func<MpCheatItemPayload, string> formatSuccess) {
        var commandId = pending.CommandId;
        BroadcastCommand(pending.ExecuteKind, commandId, pending.Payload);
        await pending.Execute(pending.Payload);
        RemovePending(commandId);
        MainFile.Logger.Info(
            $"[MpCheat] {pending.LogTag} command {commandId} executed kind={pending.Payload.Kind} item={pending.Payload.ItemId}.");
        var acked = pending.Acks.Count(a => a.Value.Success);
        var baseMsg = formatSuccess(pending.Payload);
        if (pending.RequiredAckCount > 0) {
            return string.Format(
                I18N.T("mpcheat.item.successWithAcks", "{0} ({1}/{2} players confirmed)."),
                baseMsg,
                acked,
                pending.RequiredAckCount);
        }
        return baseMsg;
    }

    private static int ComputeAckTimeoutMs(int peerCount) {
        if (peerCount <= 1) return BaseAckTimeoutMs;
        return Math.Min(MaxAckTimeoutMs, BaseAckTimeoutMs + AckTimeoutPerPeerMs * (peerCount - 1));
    }

    private static void PruneIfOverCapacity() {
        if (PendingByCommandId.Count < MaxConcurrentPending) return;
        var oldest = PendingByCommandId.Keys.Min();
        var pending = PendingByCommandId[oldest];
        pending.Completion.TrySetResult(false);
        PendingByCommandId.Remove(oldest);
        MainFile.Logger.Warn($"[MpCheat] Dropped oldest pending item command {oldest} (capacity).");
    }

    private static bool TrackExecuted(ulong commandId) {
        if (ExecutedCommandIds.Count >= MaxExecutedIdHistory)
            ExecutedCommandIds.Clear();
        return ExecutedCommandIds.Add(commandId);
    }

    private static void BroadcastCommand(MpCheatCommandKind kind, ulong commandId, MpCheatItemPayload payload) {
        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.BroadcastCommand(new MpCheatCommandMessage {
            Kind = kind,
            IssuedByNetId = netId,
            CommandId = commandId,
            Item = payload,
        });
    }

    private static void RemovePending(ulong commandId) {
        lock (Gate) {
            PendingByCommandId.Remove(commandId);
        }
    }

    internal static string FormatError(string error) =>
        string.Format(I18N.T("mpcheat.item.failedDetail", "Item cheat failed: {0}"), error);

    private static string FormatPeerError(MpCheatAddCardAckMessage ack) {
        var err = string.IsNullOrEmpty(ack.Error) ? "validation failed" : ack.Error;
        return string.Format(
            I18N.T("mpcheat.item.peerFailed", "Player {0} rejected: {1}"),
            ack.PeerNetId,
            err);
    }

    private static string FormatAckTimeout(PendingItem pending) {
        var got = pending.Acks.Count;
        var need = pending.RequiredAckCount;
        if (need > 0) {
            return string.Format(
                I18N.T("mpcheat.item.timeoutDetail", "Timed out ({0}/{1} players confirmed)."),
                got,
                need);
        }
        return I18N.T("mpcheat.item.timeout", "Timed out waiting for other players.");
    }
}
