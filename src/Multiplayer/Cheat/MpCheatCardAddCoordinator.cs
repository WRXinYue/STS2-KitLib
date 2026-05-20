using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Host-authoritative add-card: prepare/ACK on connected peers, then execute (2–N players).</summary>
internal static class MpCheatCardAddCoordinator {
    private static readonly object Gate = new();
    private static readonly Dictionary<ulong, PendingAdd> PendingByCommandId = new();
    private static readonly HashSet<ulong> ExecutedCommandIds = new();
    private static ulong _nextCommandId;

    private const int BaseAckTimeoutMs = 8000;
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
        if (!MpCheatSession.CanEditMultiplayerCheats)
            return I18N.T("mpcheat.cardAdd.hostOnly", "Only the host can add cards in multiplayer.");

        if (!TryValidateAdd(state, targetPlayer, card, request, out var localError))
            return FormatError(localError);

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

        if (awaitingPeers.Count == 0) {
            return await FinishHostExecute(pending, cardId);
        }

        var timeoutMs = ComputeAckTimeoutMs(awaitingPeers.Count);
        using var cts = new CancellationTokenSource(timeoutMs);
        try {
            var ok = await pending.Completion.Task.WaitAsync(cts.Token);
            if (!ok) {
                var fail = pending.Acks.Values.FirstOrDefault(a => !a.Success);
                RemovePending(commandId);
                return fail != null
                    ? FormatPeerError(fail)
                    : FormatAckTimeout(pending);
            }

            return await FinishHostExecute(pending, cardId);
        }
        catch (OperationCanceledException) {
            RemovePending(commandId);
            return FormatAckTimeout(pending);
        }
    }

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.AddCard == null) return;
        if (MpCheatSession.IsHost) return;

        MainFile.Logger.Info(
            $"[MpCheat] AddCard prepare id={message.CommandId} card={message.AddCard.CardId} target={message.AddCard.TargetPlayerNetId}");
        var (ok, error) = TryResolveAndValidate(message.AddCard);
        MpCheatNetBus.SendAddCardAck(new MpCheatAddCardAckMessage {
            CommandId = message.CommandId,
            PeerNetId = RunManager.Instance?.NetService?.NetId ?? 0,
            Success = ok,
            Error = error,
        });
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

    public static void OnAckReceived(MpCheatAddCardAckMessage ack) {
        if (!MpCheatSession.IsHost) return;

        lock (Gate) {
            if (!PendingByCommandId.TryGetValue(ack.CommandId, out var pending)) return;

            pending.Acks[ack.PeerNetId] = ack;
            if (!ack.Success) {
                pending.Completion.TrySetResult(false);
                return;
            }

            pending.AwaitingPeers.Remove(ack.PeerNetId);
            if (pending.AwaitingPeers.Count == 0)
                pending.Completion.TrySetResult(true);
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
        MainFile.Logger.Info($"[MpCheat] AddCard command {commandId} executed for {cardId}.");
        var acked = pending.Acks.Count(a => a.Value.Success);
        if (pending.RequiredAckCount > 0) {
            return string.Format(
                I18N.T("mpcheat.cardAdd.successWithAcks", "Card added ({0}/{1} players confirmed)."),
                acked,
                pending.RequiredAckCount);
        }
        return I18N.T("mpcheat.cardAdd.success", "Card added (all players).");
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
            CustomBaseCost = payload.CustomBaseCost,
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
        string cardId) =>
        new() {
            CardId = cardId,
            TargetPlayerNetId = targetNetId,
            Target = (int)request.Target,
            Duration = (int)request.Duration,
            UpgradeLevels = request.UpgradeLevelsToApply,
            CustomBaseCost = request.CustomBaseCost,
            UseUpgradePreviewStyle = usePreviewStyle,
        };

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
