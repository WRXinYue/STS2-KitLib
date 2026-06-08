using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Host-authoritative remove-card: prepare/ACK on connected peers, then execute (2–N players).</summary>
internal static class MpCheatCardRemoveCoordinator {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, PendingRemove> PendingByCommandId = new();
    private static readonly HashSet<ulong> ExecutedCommandIds = new();
    private static readonly Dictionary<ulong, TaskCompletionSource<string>> ClientRemoveCompletions = new();
    private static ulong _nextCommandId;
    private static ulong _nextClientRequestId;

    private const int BaseAckTimeoutMs = 8000;
    private const int ClientRequestTimeoutMs = 25000;
    private const int AckTimeoutPerPeerMs = 1500;
    private const int MaxAckTimeoutMs = 20000;
    private const int MaxConcurrentPending = 64;
    private const int MaxExecutedIdHistory = 256;

    private sealed class PendingRemove {
        public required ulong CommandId { get; init; }
        public required MpCheatRemoveCardPayload Payload { get; init; }
        public required HashSet<ulong> AwaitingPeers { get; init; }
        public required int RequiredAckCount { get; init; }
        public required RunState State { get; init; }
        public required Player TargetPlayer { get; init; }
        public required CardModel Card { get; init; }
        public required CardTarget Target { get; init; }
        public required bool RemoveFromRunState { get; init; }
        public Dictionary<ulong, MpCheatAddCardAckMessage> Acks { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static async Task<string> TryHostRemoveCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        bool removeFromRunState) {
        var (_, message) = await TryHostRemoveCardCoreAsync(state, targetPlayer, card, target, removeFromRunState);
        return message;
    }

    public static async Task<string> TryClientRequestRemoveCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        bool removeFromRunState) {
        if (MpCheatSession.IsHost)
            return await TryHostRemoveCardAsync(state, targetPlayer, card, target, removeFromRunState);

        if (!MpCheatSession.CanUseMultiplayerCheats)
            return I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");

        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (localNetId == 0 || targetPlayer.NetId != localNetId)
            return I18N.T(
                "mpcheat.cardRemove.clientSelfOnly",
                "In multiplayer you can only remove cards from your own character.");

        if (!CardActions.TryBuildRemovePayload(targetPlayer, card, target, removeFromRunState, out var payload, out var error))
            return FormatError(error);

        var clientRequestId = Interlocked.Increment(ref _nextClientRequestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate) {
            ClientRemoveCompletions[clientRequestId] = completion;
        }

        MpCheatNetBus.ClientSendRemoveCardRequest(new MpCheatRemoveCardClientRequestMessage {
            ClientRequestId = clientRequestId,
            RequesterNetId = localNetId,
            Payload = payload,
        });
        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard client request id={clientRequestId} card={payload.CardId} target={localNetId} pile={payload.Target} idx={payload.PileIndex}.");

        try {
            using var cts = new CancellationTokenSource(ClientRequestTimeoutMs);
            return await completion.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            return I18N.T(
                "mpcheat.cardRemove.clientRequestTimeout",
                "Host did not respond to remove-card request in time.");
        }
        finally {
            lock (Gate) {
                ClientRemoveCompletions.Remove(clientRequestId);
            }
        }
    }

    public static void OnClientRemoveCardRequestReceived(MpCheatRemoveCardClientRequestMessage request, ulong senderId) {
        if (!MpCheatSession.IsHost) return;
        TaskHelper.RunSafely(HandleClientRemoveCardRequestAsync(request, senderId));
    }

    public static void OnClientRemoveCardResultReceived(MpCheatAddCardClientResultMessage result) {
        if (MpCheatSession.IsHost) return;
        TaskCompletionSource<string>? completion;
        lock (Gate) {
            ClientRemoveCompletions.TryGetValue(result.ClientRequestId, out completion);
        }

        if (completion == null) {
            MainFile.Logger.Debug(
                $"[MpCheat] RemoveCard client result id={result.ClientRequestId} ignored (no pending UI).");
            return;
        }

        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard client result id={result.ClientRequestId} ok={result.Success}.");
        completion.TrySetResult(result.Message);
    }

    private static async Task HandleClientRemoveCardRequestAsync(
        MpCheatRemoveCardClientRequestMessage request,
        ulong senderId) {
        void Reply(bool success, string message) =>
            MpCheatNetBus.HostSendRemoveCardRequestResult(senderId, new MpCheatAddCardClientResultMessage {
                ClientRequestId = request.ClientRequestId,
                Success = success,
                Message = message,
            });

        if (!MpCheatSession.CanEditMultiplayerCheats) {
            Reply(false, I18N.T("mpcheat.cardRemove.hostOnly", "Only the host can remove cards in multiplayer."));
            return;
        }

        if (request.RequesterNetId != senderId) {
            Reply(false, FormatError("request sender mismatch"));
            return;
        }

        if (request.Payload.TargetPlayerNetId != senderId) {
            Reply(false, I18N.T(
                "mpcheat.cardRemove.clientSelfOnly",
                "In multiplayer you can only remove cards from your own character."));
            return;
        }

        var resolved = TryResolveForExecute(request.Payload);
        if (resolved == null) {
            Reply(false, FormatError("invalid remove-card request"));
            return;
        }

        var (state, player, card, target, removeFromRunState) = resolved.Value;
        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard client request from {senderId} card={request.Payload.CardId}.");

        var (success, message) = await TryHostRemoveCardCoreAsync(state, player, card, target, removeFromRunState);
        Reply(success, message);
    }

    private static async Task<(bool Success, string Message)> TryHostRemoveCardCoreAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        bool removeFromRunState) {
        if (!MpCheatSession.CanEditMultiplayerCheats)
            return (false, I18N.T("mpcheat.cardRemove.hostOnly", "Only the host can remove cards in multiplayer."));

        if (!CardActions.TryBuildRemovePayload(targetPlayer, card, target, removeFromRunState, out var payload, out var localError))
            return (false, FormatError(localError));

        var cardId = payload.CardId;
        var commandId = Interlocked.Increment(ref _nextCommandId);
        var awaitingPeers = MpCheatParticipants.GetAckRequiredPeerNetIds();

        PendingRemove pending;
        lock (Gate) {
            PruneIfOverCapacity();
            pending = new PendingRemove {
                CommandId = commandId,
                Payload = payload,
                AwaitingPeers = awaitingPeers,
                RequiredAckCount = awaitingPeers.Count,
                State = state,
                TargetPlayer = targetPlayer,
                Card = card,
                Target = target,
                RemoveFromRunState = removeFromRunState,
            };
            PendingByCommandId[commandId] = pending;
        }

        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard host start id={commandId} card={cardId} target={targetPlayer.NetId} pile={payload.Target} idx={payload.PileIndex} ackPeers={awaitingPeers.Count}.");

        BroadcastCommand(MpCheatCommandKind.RemoveCardPrepare, commandId, payload);

        if (awaitingPeers.Count == 0)
            return (true, await FinishHostExecute(pending, cardId));

        var timeoutMs = ComputeAckTimeoutMs(awaitingPeers.Count);
        using var cts = new CancellationTokenSource(timeoutMs);
        try {
            var ok = await pending.Completion.Task.WaitAsync(cts.Token);
            if (!ok) {
                var fail = pending.Acks.Values.FirstOrDefault(a => !a.Success);
                RemovePending(commandId);
                return (false, fail != null ? FormatPeerError(fail) : FormatAckTimeout(pending));
            }

            return (true, await FinishHostExecute(pending, cardId));
        }
        catch (OperationCanceledException) {
            RemovePending(commandId);
            return (false, FormatAckTimeout(pending));
        }
    }

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.RemoveCard == null) return;
        if (MpCheatSession.IsHost) return;

        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard prepare id={message.CommandId} card={message.RemoveCard.CardId} target={message.RemoveCard.TargetPlayerNetId}");
        var (ok, error) = TryResolveAndValidate(message.RemoveCard);
        var peerNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.SendAddCardAck(new MpCheatAddCardAckMessage {
            CommandId = message.CommandId,
            PeerNetId = peerNetId,
            Success = ok,
            Error = error,
        });
        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard ack sent id={message.CommandId} peer={peerNetId} ok={ok} err={error ?? ""}");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.RemoveCard == null) return;
        if (MpCheatSession.IsHost) return;

        lock (Gate) {
            if (!TrackExecuted(message.CommandId)) {
                MainFile.Logger.Debug($"[MpCheat] RemoveCard execute id={message.CommandId} skipped (duplicate).");
                return;
            }
        }

        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard execute id={message.CommandId} card={message.RemoveCard.CardId} target={message.RemoveCard.TargetPlayerNetId}");
        var resolved = TryResolveForExecute(message.RemoveCard);
        if (resolved == null) {
            MainFile.Logger.Warn($"[MpCheat] RemoveCard execute skipped: {message.RemoveCard.CardId}");
            return;
        }

        var (state, player, card, target, removeFromRunState) = resolved.Value;
        TaskHelper.RunSafely(CardActions.ExecuteRemoveFromMpSync(state, player, card, target, removeFromRunState));
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

    private static async Task<string> FinishHostExecute(PendingRemove pending, string cardId) {
        var commandId = pending.CommandId;
        BroadcastCommand(MpCheatCommandKind.RemoveCardExecute, commandId, pending.Payload);
        await CardActions.ExecuteRemoveFromMpSync(
            pending.State,
            pending.TargetPlayer,
            pending.Card,
            pending.Target,
            pending.RemoveFromRunState);
        RemovePending(commandId);
        var targetLabel = MpCheatPlayerLabels.FormatLogLabel(pending.TargetPlayer);
        MainFile.Logger.Info(
            $"[MpCheat] RemoveCard command {commandId} executed card={cardId} target={targetLabel}.");
        var acked = pending.Acks.Count(a => a.Value.Success);
        if (pending.RequiredAckCount > 0) {
            return string.Format(
                I18N.T("mpcheat.cardRemove.successWithAcksFor", "Removed {0} from {1} ({2}/{3} players confirmed)."),
                cardId,
                targetLabel,
                acked,
                pending.RequiredAckCount);
        }
        return string.Format(
            I18N.T("mpcheat.cardRemove.successFor", "Removed {0} from {1}."),
            cardId,
            targetLabel);
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
        MainFile.Logger.Warn($"[MpCheat] Dropped oldest pending remove-card command {oldest} (capacity).");
    }

    private static bool TrackExecuted(ulong commandId) {
        if (ExecutedCommandIds.Count >= MaxExecutedIdHistory)
            ExecutedCommandIds.Clear();
        return ExecutedCommandIds.Add(commandId);
    }

    private static (bool Ok, string? Error) TryResolveAndValidate(MpCheatRemoveCardPayload payload) {
        var resolved = TryResolveForExecute(payload);
        if (resolved == null)
            return (false, "invalid remove-card payload");
        var (state, player, card, target, removeFromRunState) = resolved.Value;
        return CardActions.TryValidateRemove(state, player, card, target, removeFromRunState, out var err)
            ? (true, null)
            : (false, err);
    }

    private static (RunState State, Player Player, CardModel Card, CardTarget Target, bool RemoveFromRunState)?
        TryResolveForExecute(MpCheatRemoveCardPayload payload) {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return null;

        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) return null;

        var card = CardActions.ResolveCardFromRemovePayload(player, payload);
        if (card == null) return null;

        return (state, player, card, (CardTarget)payload.Target, payload.RemoveFromRunState);
    }

    private static void BroadcastCommand(MpCheatCommandKind kind, ulong commandId, MpCheatRemoveCardPayload payload) {
        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.BroadcastCommand(new MpCheatCommandMessage {
            Kind = kind,
            IssuedByNetId = netId,
            CommandId = commandId,
            RemoveCard = payload,
        });
    }

    private static void RemovePending(ulong commandId) {
        lock (Gate) {
            PendingByCommandId.Remove(commandId);
        }
    }

    internal static void Reset() {
        lock (Gate) {
            foreach (var pending in PendingByCommandId.Values)
                pending.Completion.TrySetResult(false);
            PendingByCommandId.Clear();
            ExecutedCommandIds.Clear();
            foreach (var tcs in ClientRemoveCompletions.Values)
                tcs.TrySetResult(I18N.T("mpcheat.cardRemove.cancelled", "Remove card cancelled (run ended)."));
            ClientRemoveCompletions.Clear();
        }
    }

    private static string FormatError(string error) =>
        string.Format(I18N.T("mpcheat.cardRemove.failedDetail", "Remove card failed: {0}"), error);

    private static string FormatPeerError(MpCheatAddCardAckMessage ack) {
        var err = string.IsNullOrEmpty(ack.Error) ? "validation failed" : ack.Error;
        return string.Format(
            I18N.T("mpcheat.cardRemove.peerFailed", "Player {0} rejected remove card: {1}"),
            ack.PeerNetId,
            err);
    }

    private static string FormatAckTimeout(PendingRemove pending) {
        var got = pending.Acks.Count;
        var need = pending.RequiredAckCount;
        if (need > 0) {
            return string.Format(
                I18N.T("mpcheat.cardRemove.timeoutDetail", "Remove card timed out ({0}/{1} players confirmed)."),
                got,
                need);
        }
        return I18N.T("mpcheat.cardRemove.timeout", "Remove card timed out waiting for other players.");
    }
}
