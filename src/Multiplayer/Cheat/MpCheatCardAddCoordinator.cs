using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Presets;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat;

/// <summary>Host-authoritative add-card: prepare/ACK on connected peers, then execute (2–N players).</summary>
internal static class MpCheatCardAddCoordinator {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, PendingAdd> PendingByCommandId = new();
    private static readonly HashSet<ulong> ExecutedCommandIds = new();
    private static readonly Dictionary<ulong, TaskCompletionSource<string>> ClientAddCompletions = new();
    private static ulong _nextCommandId;
    private static ulong _nextClientRequestId;

    private const int BaseAckTimeoutMs = 8000;
    private const int ClientRequestTimeoutMs = 25000;
    private const int AckTimeoutPerPeerMs = 1500;
    private const int MaxAckTimeoutMs = 20000;
    private const int MaxConcurrentPending = 64;
    private const int MaxExecutedIdHistory = 256;

    private sealed class PendingAdd {
        public required ulong CommandId { get; init; }
        public required MpCheatAddCardPayload Payload { get; init; }
        public required HashSet<ulong> AwaitingPeers { get; init; }
        public required int RequiredAckCount { get; init; }
        public required RunState State { get; init; }
        public required Player TargetPlayer { get; init; }
        public required CardModel Card { get; init; }
        public required AddCardRequest Request { get; init; }
        public CardPreviewStyle? UpgradePreviewStyle { get; init; }
        public Dictionary<ulong, MpCheatAddCardAckMessage> Acks { get; } = new();
        public TaskCompletionSource<bool> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    /// <summary>Host initiates synced add-card; returns user-facing status text.</summary>
    public static async Task<string> TryHostAddCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        AddCardRequest request,
        CardPreviewStyle? upgradePreviewStyle) {
        var (_, message) = await TryHostAddCardCoreAsync(state, targetPlayer, card, request, upgradePreviewStyle);
        return message;
    }

    /// <summary>Client asks host to run synced add-card (target must be local player).</summary>
    public static async Task<string> TryClientRequestAddCardAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        AddCardRequest request,
        CardPreviewStyle? upgradePreviewStyle) {
        if (MpCheatSession.IsHost)
            return await TryHostAddCardAsync(state, targetPlayer, card, request, upgradePreviewStyle);

        if (!MpCheatSession.CanUseMultiplayerCheats)
            return I18N.T(
                "mpcheat.blocked",
                "Multiplayer cheat inactive: {0}",
                MpCheatSession.LastBlockReason ?? "unknown");

        var localNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        if (localNetId == 0 || targetPlayer.NetId != localNetId)
            return I18N.T(
                "mpcheat.cardAdd.clientSelfOnly",
                "In multiplayer you can only add cards to your own character.");

        if (!TryValidateAdd(state, targetPlayer, card, request, out var localError))
            return FormatError(localError);

        var cardId = ((AbstractModel)card).Id.Entry;
        var clientRequestId = Interlocked.Increment(ref _nextClientRequestId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate) {
            ClientAddCompletions[clientRequestId] = completion;
        }

        MpCheatNetBus.ClientSendAddCardRequest(new MpCheatAddCardClientRequestMessage {
            ClientRequestId = clientRequestId,
            RequesterNetId = localNetId,
            Payload = ToPayload(localNetId, request, upgradePreviewStyle.HasValue, cardId),
        });
        MainFile.Logger.Info(
            $"[MpCheat] AddCard client request id={clientRequestId} card={cardId} target={localNetId}.");

        try {
            using var cts = new CancellationTokenSource(ClientRequestTimeoutMs);
            return await completion.Task.WaitAsync(cts.Token);
        }
        catch (OperationCanceledException) {
            return I18N.T(
                "mpcheat.cardAdd.clientRequestTimeout",
                "Host did not respond to add-card request in time.");
        }
        finally {
            lock (Gate) {
                ClientAddCompletions.Remove(clientRequestId);
            }
        }
    }

    public static void OnClientAddCardRequestReceived(MpCheatAddCardClientRequestMessage request, ulong senderId) {
        if (!MpCheatSession.IsHost) return;
        TaskHelper.RunSafely(HandleClientAddCardRequestAsync(request, senderId));
    }

    public static void OnClientAddCardResultReceived(MpCheatAddCardClientResultMessage result) {
        if (MpCheatSession.IsHost) return;
        TaskCompletionSource<string>? completion;
        lock (Gate) {
            ClientAddCompletions.TryGetValue(result.ClientRequestId, out completion);
        }

        if (completion == null) {
            MainFile.Logger.Debug(
                $"[MpCheat] AddCard client result id={result.ClientRequestId} ignored (no pending UI).");
            return;
        }

        MainFile.Logger.Info(
            $"[MpCheat] AddCard client result id={result.ClientRequestId} ok={result.Success}.");
        completion.TrySetResult(result.Message);
    }

    private static async Task HandleClientAddCardRequestAsync(
        MpCheatAddCardClientRequestMessage request,
        ulong senderId) {
        void Reply(bool success, string message) =>
            MpCheatNetBus.HostSendAddCardRequestResult(senderId, new MpCheatAddCardClientResultMessage {
                ClientRequestId = request.ClientRequestId,
                Success = success,
                Message = message,
            });

        if (!MpCheatSession.CanEditMultiplayerCheats) {
            Reply(false, I18N.T("mpcheat.cardAdd.hostOnly", "Only the host can add cards in multiplayer."));
            return;
        }

        if (request.RequesterNetId != senderId) {
            Reply(false, FormatError("request sender mismatch"));
            return;
        }

        if (request.Payload.TargetPlayerNetId != senderId) {
            Reply(false, I18N.T(
                "mpcheat.cardAdd.clientSelfOnly",
                "In multiplayer you can only add cards to your own character."));
            return;
        }

        var resolved = TryResolveForExecute(request.Payload);
        if (resolved == null) {
            Reply(false, FormatError("invalid add-card request"));
            return;
        }

        var (state, player, card, addRequest, style) = resolved.Value;
        MainFile.Logger.Info(
            $"[MpCheat] AddCard client request from {senderId} card={request.Payload.CardId}.");

        var (success, message) = await TryHostAddCardCoreAsync(state, player, card, addRequest, style);
        Reply(success, message);
    }

    private static async Task<(bool Success, string Message)> TryHostAddCardCoreAsync(
        RunState state,
        Player targetPlayer,
        CardModel card,
        AddCardRequest request,
        CardPreviewStyle? upgradePreviewStyle) {
        if (!MpCheatSession.CanEditMultiplayerCheats)
            return (false, I18N.T("mpcheat.cardAdd.hostOnly", "Only the host can add cards in multiplayer."));

        if (!TryValidateAdd(state, targetPlayer, card, request, out var localError))
            return (false, FormatError(localError));

        var cardId = ((AbstractModel)card).Id.Entry;
        var payload = ToPayload(targetPlayer.NetId, request, upgradePreviewStyle.HasValue, cardId);
        var commandId = Interlocked.Increment(ref _nextCommandId);
        var awaitingPeers = MpCheatParticipants.GetAckRequiredPeerNetIds();

        PendingAdd pending;
        lock (Gate) {
            PruneIfOverCapacity();
            pending = new PendingAdd {
                CommandId = commandId,
                Payload = payload,
                AwaitingPeers = awaitingPeers,
                RequiredAckCount = awaitingPeers.Count,
                State = state,
                TargetPlayer = targetPlayer,
                Card = card,
                Request = request,
                UpgradePreviewStyle = upgradePreviewStyle,
            };
            PendingByCommandId[commandId] = pending;
        }

        MainFile.Logger.Info(
            $"[MpCheat] AddCard host start id={commandId} card={cardId} target={targetPlayer.NetId} ackPeers={awaitingPeers.Count}.");

        BroadcastCommand(MpCheatCommandKind.AddCardPrepare, commandId, payload);

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
        if (!MpCheatSession.CanUseMultiplayerCheats || message.AddCard == null) return;
        if (MpCheatSession.IsHost) return;

        MainFile.Logger.Info(
            $"[MpCheat] AddCard prepare id={message.CommandId} card={message.AddCard.CardId} target={message.AddCard.TargetPlayerNetId}");
        var (ok, error) = TryResolveAndValidate(message.AddCard);
        var peerNetId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.SendAddCardAck(new MpCheatAddCardAckMessage {
            CommandId = message.CommandId,
            PeerNetId = peerNetId,
            Success = ok,
            Error = error,
        });
        MainFile.Logger.Info(
            $"[MpCheat] AddCard ack sent id={message.CommandId} peer={peerNetId} ok={ok} err={error ?? ""}");
    }

    public static void OnExecuteReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.AddCard == null) return;
        if (MpCheatSession.IsHost) return;

        lock (Gate) {
            if (!TrackExecuted(message.CommandId)) {
                MainFile.Logger.Debug($"[MpCheat] AddCard execute id={message.CommandId} skipped (duplicate).");
                return;
            }
        }

        MainFile.Logger.Info(
            $"[MpCheat] AddCard execute id={message.CommandId} card={message.AddCard.CardId} target={message.AddCard.TargetPlayerNetId}");
        var resolved = TryResolveForExecute(message.AddCard);
        if (resolved == null) {
            MainFile.Logger.Warn($"[MpCheat] AddCard execute skipped: {message.AddCard.CardId}");
            return;
        }

        var (state, player, card, request, style) = resolved.Value;
        TaskHelper.RunSafely(ExecuteOnAllPeers(state, player, card, request, style));
    }

    /// <returns>True if this ACK matched a pending add-card command.</returns>
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

    private static async Task<string> FinishHostExecute(PendingAdd pending, string cardId) {
        var commandId = pending.CommandId;
        BroadcastCommand(MpCheatCommandKind.AddCardExecute, commandId, pending.Payload);
        await ExecuteOnAllPeers(
            pending.State,
            pending.TargetPlayer,
            pending.Card,
            pending.Request,
            pending.UpgradePreviewStyle);
        RemovePending(commandId);
        var targetLabel = MpCheatPlayerLabels.FormatLogLabel(pending.TargetPlayer);
        MainFile.Logger.Info(
            $"[MpCheat] AddCard command {commandId} executed card={cardId} target={targetLabel}.");
        var acked = pending.Acks.Count(a => a.Value.Success);
        if (pending.RequiredAckCount > 0) {
            return string.Format(
                I18N.T("mpcheat.cardAdd.successWithAcksFor", "Added {0} for {1} ({2}/{3} players confirmed)."),
                cardId,
                targetLabel,
                acked,
                pending.RequiredAckCount);
        }
        return string.Format(
            I18N.T("mpcheat.cardAdd.successFor", "Added {0} for {1}."),
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
        MainFile.Logger.Warn($"[MpCheat] Dropped oldest pending add-card command {oldest} (capacity).");
    }

    private static bool TrackExecuted(ulong commandId) {
        if (ExecutedCommandIds.Count >= MaxExecutedIdHistory)
            ExecutedCommandIds.Clear();
        return ExecutedCommandIds.Add(commandId);
    }

    private static async Task ExecuteOnAllPeers(
        RunState state,
        Player player,
        CardModel card,
        AddCardRequest request,
        CardPreviewStyle? upgradePreviewStyle) {
        await CardActions.ExecuteAddFromMpSync(state, player, card, request, upgradePreviewStyle);
    }

    private static bool TryValidateAdd(RunState state, Player player, CardModel card, AddCardRequest request,
        out string error) =>
        CardActions.TryValidateAdd(state, player, card, request, out error);

    private static (bool Ok, string? Error) TryResolveAndValidate(MpCheatAddCardPayload payload) {
        var resolved = TryResolveForExecute(payload);
        if (resolved == null)
            return (false, "invalid add-card payload");
        var (state, player, card, request, _) = resolved.Value;
        return CardActions.TryValidateAdd(state, player, card, request, out var err)
            ? (true, null)
            : (false, err);
    }

    private static (RunState State, Player Player, CardModel Card, AddCardRequest Request, CardPreviewStyle? Style)?
        TryResolveForExecute(MpCheatAddCardPayload payload) {
        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return null;

        var player = CardActions.FindPlayerByNetId(payload.TargetPlayerNetId);
        if (player == null) return null;

        var card = CardActions.FindCardById(payload.CardId);
        if (card == null) return null;

        var request = new AddCardRequest {
            Target = (CardTarget)payload.Target,
            Duration = (EffectDuration)payload.Duration,
            UpgradeLevelsToApply = payload.UpgradeLevels,
            StagedTemplate = ResolveStagedTemplateFromPayload(payload),
        };
        CardPreviewStyle? style = payload.UseUpgradePreviewStyle
            ? CardPreviewStyle.HorizontalLayout
            : null;
        return (state, player, card, request, style);
    }

    private static MpCheatAddCardPayload ToPayload(
        ulong targetNetId,
        AddCardRequest request,
        bool usePreviewStyle,
        string cardId) {
        var template = CardActions.ResolveStagedTemplate(request);
        var templateJson = template?.HasAnyPatch() == true
            ? MpCheatNetJson.SerializeEditTemplate(template)
            : "";
        return new MpCheatAddCardPayload {
            CardId = cardId,
            TargetPlayerNetId = targetNetId,
            Target = (int)request.Target,
            Duration = (int)request.Duration,
            UpgradeLevels = request.UpgradeLevelsToApply,
            CustomBaseCost = template?.BaseCost,
            TemplateJson = templateJson,
            UseUpgradePreviewStyle = usePreviewStyle,
        };
    }

    private static CardEditTemplate? ResolveStagedTemplateFromPayload(MpCheatAddCardPayload payload) {
        var template = MpCheatNetJson.DeserializeEditTemplate(payload.TemplateJson);
        if (template?.HasAnyPatch() == true)
            return template;
        if (payload.CustomBaseCost.HasValue)
            return new CardEditTemplate { BaseCost = payload.CustomBaseCost };
        return null;
    }

    private static void BroadcastCommand(MpCheatCommandKind kind, ulong commandId, MpCheatAddCardPayload payload) {
        var netId = RunManager.Instance?.NetService?.NetId ?? 0;
        MpCheatNetBus.BroadcastCommand(new MpCheatCommandMessage {
            Kind = kind,
            IssuedByNetId = netId,
            CommandId = commandId,
            AddCard = payload,
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
            foreach (var tcs in ClientAddCompletions.Values)
                tcs.TrySetResult(I18N.T("mpcheat.cardAdd.cancelled", "Add card cancelled (run ended)."));
            ClientAddCompletions.Clear();
        }
    }

    private static string FormatError(string error) =>
        string.Format(I18N.T("mpcheat.cardAdd.failedDetail", "Add card failed: {0}"), error);

    private static string FormatPeerError(MpCheatAddCardAckMessage ack) {
        var err = string.IsNullOrEmpty(ack.Error) ? "validation failed" : ack.Error;
        return string.Format(
            I18N.T("mpcheat.cardAdd.peerFailed", "Player {0} rejected add card: {1}"),
            ack.PeerNetId,
            err);
    }

    private static string FormatAckTimeout(PendingAdd pending) {
        var got = pending.Acks.Count;
        var need = pending.RequiredAckCount;
        if (need > 0) {
            return string.Format(
                I18N.T("mpcheat.cardAdd.timeoutDetail", "Add card timed out ({0}/{1} players confirmed)."),
                got,
                need);
        }
        return I18N.T("mpcheat.cardAdd.timeout", "Add card timed out waiting for other players.");
    }
}
