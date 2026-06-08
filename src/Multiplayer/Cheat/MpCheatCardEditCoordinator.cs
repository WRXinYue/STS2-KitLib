using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Presets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Host-authoritative edit-card: prepare/ACK on connected peers, then apply template (2–N players).</summary>
internal static class MpCheatCardEditCoordinator {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, PendingEdit> PendingByCommandId = new();
    private static readonly HashSet<ulong> ExecutedCommandIds = new();
    private static readonly Dictionary<ulong, TaskCompletionSource<string>> ClientEditCompletions = new();
    private static ulong _nextCommandId;
    private static ulong _nextClientRequestId;

    private const int BaseAckTimeoutMs = 8000;
    private const int ClientRequestTimeoutMs = 25000;
    private const int AckTimeoutPerPeerMs = 1500;
    private const int MaxAckTimeoutMs = 20000;
    private const int MaxConcurrentPending = 64;
    private const int MaxExecutedIdHistory = 256;

    private sealed class PendingEdit {
        public required ulong CommandId { get; init; }
        public required MpCheatEditCardPayload Payload { get; init; }
        public required CardEditTemplate Template { get; init; }
        public required HashSet<ulong> AwaitingPeers { get; init; }
        public required int RequiredAckCount { get; init; }
        public required RunState State { get; init; }
        public required Player TargetPlayer { get; init; }
        public required CardModel Card { get; init; }
        public required CardTarget Target { get; init; }
        public Dictionary<ulong, MpCheatAddCardAckMessage> Acks { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    public static async Task<string> TryHostEditCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        CardEditTemplate template) {
        var (_, message) = await TryHostEditCardCoreAsync(state, targetPlayer, card, target, template);
        return message;
    }

    public static async Task<string> TryClientRequestEditCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        CardEditTemplate template) {
        if (MpCheatSession.IsHost)
            return await TryHostEditCardAsync(state, targetPlayer, card, target, template);

        if (!MpCheatSession.CanUseMultiplayerCheats)
            return I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");

        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (localNetId == 0 || targetPlayer.NetId != localNetId)
            return I18N.T(
                "mpcheat.cardEdit.clientSelfOnly",
                "In multiplayer you can only edit cards on your own character.");

        if (!CardActions.TryBuildEditPayload(targetPlayer, card, target, template, out var payload, out var error))
            return FormatError(error);

        var clientRequestId = Interlocked.Increment(ref _nextClientRequestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate) {
            ClientEditCompletions[clientRequestId] = completion;
        }

        MpCheatNetBus.ClientSendEditCardRequest(new MpCheatEditCardClientRequestMessage {
            ClientRequestId = clientRequestId,
            RequesterNetId = localNetId,
            Payload = payload,
        });
        MainFile.Logger.Info(
            $"[MpCheat] EditCard client request id={clientRequestId} card={payload.CardId} target={localNetId} pile={payload.Target} idx={payload.PileIndex}.");

        try {
            using var cts = new CancellationTokenSource(ClientRequestTimeoutMs);
            return await completion.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            return I18N.T(
                "mpcheat.cardEdit.clientRequestTimeout",
                "Host did not respond to edit-card request in time.");
        }
        finally {
            lock (Gate) {
                ClientEditCompletions.Remove(clientRequestId);
            }
        }
    }

    public static void OnClientEditCardRequestReceived(MpCheatEditCardClientRequestMessage request, ulong senderId) {
        if (!MpCheatSession.IsHost) return;
        TaskHelper.RunSafely(HandleClientEditCardRequestAsync(request, senderId));
    }

    public static void OnClientEditCardResultReceived(MpCheatAddCardClientResultMessage result) {
        if (MpCheatSession.IsHost) return;
        TaskCompletionSource<string>? completion;
        lock (Gate) {
            ClientEditCompletions.TryGetValue(result.ClientRequestId, out completion);
        }

        if (completion == null) {
            MainFile.Logger.Debug(
                $"[MpCheat] EditCard client result id={result.ClientRequestId} ignored (no pending UI).");
            return;
        }

        MainFile.Logger.Info(
            $"[MpCheat] EditCard client result id={result.ClientRequestId} ok={result.Success}.");
        completion.TrySetResult(result.Message);
    }

    private static async Task HandleClientEditCardRequestAsync(
        MpCheatEditCardClientRequestMessage request,
        ulong senderId) {
        void Reply(bool success, string message) =>
            MpCheatNetBus.HostSendEditCardRequestResult(senderId, new MpCheatAddCardClientResultMessage {
                ClientRequestId = request.ClientRequestId,
                Success = success,
                Message = message,
            });

        if (!MpCheatSession.CanEditMultiplayerCheats) {
            Reply(false, I18N.T("mpcheat.cardEdit.hostOnly", "Only the host can edit cards in multiplayer."));
            return;
        }

        if (request.RequesterNetId != senderId) {
            Reply(false, FormatError("request sender mismatch"));
            return;
        }

        if (request.Payload.TargetPlayerNetId != senderId) {
            Reply(false, I18N.T(
                "mpcheat.cardEdit.clientSelfOnly",
                "In multiplayer you can only edit cards on your own character."));
            return;
        }

        var resolved = TryResolveForExecute(request.Payload);
        if (resolved == null) {
            Reply(false, FormatError("invalid edit-card request"));
            return;
        }

        var (state, player, card, target, template) = resolved.Value;
        MainFile.Logger.Info(
            $"[MpCheat] EditCard client request from {senderId} card={request.Payload.CardId}.");

        var (success, message) = await TryHostEditCardCoreAsync(state, player, card, target, template);
        Reply(success, message);
    }

    private static async Task<(bool Success, string Message)> TryHostEditCardCoreAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        CardTarget target,
        CardEditTemplate template) {
        if (!MpCheatSession.CanEditMultiplayerCheats)
            return (false, I18N.T("mpcheat.cardEdit.hostOnly", "Only the host can edit cards in multiplayer."));

        if (!CardActions.TryBuildEditPayload(targetPlayer, card, target, template, out var payload, out var localError))
            return (false, FormatError(localError));

        var cardId = payload.CardId;
        var commandId = Interlocked.Increment(ref _nextCommandId);
        var awaitingPeers = MpCheatParticipants.GetAckRequiredPeerNetIds();

        PendingEdit pending;
        lock (Gate) {
            PruneIfOverCapacity();
            pending = new PendingEdit {
                CommandId = commandId,
                Payload = payload,
                Template = template,
                AwaitingPeers = awaitingPeers,
                RequiredAckCount = awaitingPeers.Count,
                State = state,
                TargetPlayer = targetPlayer,
                Card = card,
                Target = target,
            };
            PendingByCommandId[commandId] = pending;
        }

        MainFile.Logger.Info(
            $"[MpCheat] EditCard host start id={commandId} card={cardId} target={targetPlayer.NetId} pile={payload.Target} idx={payload.PileIndex} ackPeers={awaitingPeers.Count}.");

        BroadcastCommand(MpCheatCommandKind.EditCardPrepare, commandId, payload);

        if (awaitingPeers.Count == 0)
            return (true, FinishHostExecute(pending, cardId));

        var timeoutMs = ComputeAckTimeoutMs(awaitingPeers.Count);
        using var cts = new CancellationTokenSource(timeoutMs);
        try {
            var ok = await pending.Completion.Task.WaitAsync(cts.Token);
            if (!ok) {
                var fail = pending.Acks.Values.FirstOrDefault(a => !a.Success);
                RemovePending(commandId);
                return (false, fail != null ? FormatPeerError(fail) : FormatAckTimeout(pending));
            }

            return (true, FinishHostExecute(pending, cardId));
        }
        catch (OperationCanceledException) {
            RemovePending(commandId);
            return (false, FormatAckTimeout(pending));
        }
    }

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.EditCard == null) return;
        if (MpCheatSession.IsHost) return;

        MainFile.Logger.Info(
            $"[MpCheat] EditCard prepare id={message.CommandId} card={message.EditCard.CardId} target={message.EditCard.TargetPlayerNetId}");
        var (ok, error) = TryResolveAndValidate(message.EditCard);
        var peerNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.SendAddCardAck(new MpCheatAddCardAckMessage {
            CommandId = message.CommandId,
            PeerNetId = peerNetId,
            Success = ok,
            Error = error,
        });
        MainFile.Logger.Info(
            $"[MpCheat] EditCard ack sent id={message.CommandId} peer={peerNetId} ok={ok} err={error ?? ""}");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.EditCard == null) return;
        if (MpCheatSession.IsHost) return;

        lock (Gate) {
            if (!TrackExecuted(message.CommandId)) {
                MainFile.Logger.Debug($"[MpCheat] EditCard execute id={message.CommandId} skipped (duplicate).");
                return;
            }
        }

        MainFile.Logger.Info(
            $"[MpCheat] EditCard execute id={message.CommandId} card={message.EditCard.CardId} target={message.EditCard.TargetPlayerNetId}");
        var resolved = TryResolveForExecute(message.EditCard);
        if (resolved == null) {
            MainFile.Logger.Warn($"[MpCheat] EditCard execute skipped: {message.EditCard.CardId}");
            return;
        }

        var (_, _, card, _, template) = resolved.Value;
        CardEditActions.ApplyTemplate(card, template);
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

    private static string FinishHostExecute(PendingEdit pending, string cardId) {
        var commandId = pending.CommandId;
        BroadcastCommand(MpCheatCommandKind.EditCardExecute, commandId, pending.Payload);
        CardEditActions.ApplyTemplate(pending.Card, pending.Template);
        RemovePending(commandId);
        var targetLabel = MpCheatPlayerLabels.FormatLogLabel(pending.TargetPlayer);
        MainFile.Logger.Info(
            $"[MpCheat] EditCard command {commandId} executed card={cardId} target={targetLabel}.");
        var acked = pending.Acks.Count(a => a.Value.Success);
        if (pending.RequiredAckCount > 0) {
            return string.Format(
                I18N.T("mpcheat.cardEdit.successWithAcksFor", "Edited {0} for {1} ({2}/{3} players confirmed)."),
                cardId,
                targetLabel,
                acked,
                pending.RequiredAckCount);
        }
        return string.Format(
            I18N.T("mpcheat.cardEdit.successFor", "Edited {0} for {1}."),
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
        MainFile.Logger.Warn($"[MpCheat] Dropped oldest pending edit-card command {oldest} (capacity).");
    }

    private static bool TrackExecuted(ulong commandId) {
        if (ExecutedCommandIds.Count >= MaxExecutedIdHistory)
            ExecutedCommandIds.Clear();
        return ExecutedCommandIds.Add(commandId);
    }

    private static (bool Ok, string? Error) TryResolveAndValidate(MpCheatEditCardPayload payload) {
        var resolved = TryResolveForExecute(payload);
        if (resolved == null)
            return (false, "invalid edit-card payload");
        var (state, player, card, target, template) = resolved.Value;
        return CardActions.TryValidateEdit(state, player, card, target, template, out var err)
            ? (true, null)
            : (false, err);
    }

    private static (RunState State, Player Player, CardModel Card, CardTarget Target, CardEditTemplate Template)?
        TryResolveForExecute(MpCheatEditCardPayload payload) {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return null;

        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) return null;

        var card = CardActions.ResolveCardFromEditPayload(player, payload);
        if (card == null) return null;

        var template = MpCheatNetJson.DeserializeEditTemplate(payload.TemplateJson);
        if (template == null || !template.HasAnyPatch()) return null;

        return (state, player, card, (CardTarget)payload.Target, template);
    }

    private static void BroadcastCommand(MpCheatCommandKind kind, ulong commandId, MpCheatEditCardPayload payload) {
        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.BroadcastCommand(new MpCheatCommandMessage {
            Kind = kind,
            IssuedByNetId = netId,
            CommandId = commandId,
            EditCard = payload,
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
            foreach (var tcs in ClientEditCompletions.Values)
                tcs.TrySetResult(I18N.T("mpcheat.cardEdit.cancelled", "Edit card cancelled (run ended)."));
            ClientEditCompletions.Clear();
        }
    }

    private static string FormatError(string error) =>
        string.Format(I18N.T("mpcheat.cardEdit.failedDetail", "Edit card failed: {0}"), error);

    private static string FormatPeerError(MpCheatAddCardAckMessage ack) {
        var err = string.IsNullOrEmpty(ack.Error) ? "validation failed" : ack.Error;
        return string.Format(
            I18N.T("mpcheat.cardEdit.peerFailed", "Player {0} rejected edit card: {1}"),
            ack.PeerNetId,
            err);
    }

    private static string FormatAckTimeout(PendingEdit pending) {
        var got = pending.Acks.Count;
        var need = pending.RequiredAckCount;
        if (need > 0) {
            return string.Format(
                I18N.T("mpcheat.cardEdit.timeoutDetail", "Edit card timed out ({0}/{1} players confirmed)."),
                got,
                need);
        }
        return I18N.T("mpcheat.cardEdit.timeout", "Edit card timed out waiting for other players.");
    }
}
