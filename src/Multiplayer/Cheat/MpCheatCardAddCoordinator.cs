using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat;

/// <summary>Host-authoritative add-card: prepare/ACK on all peers, then execute together.</summary>
internal static class MpCheatCardAddCoordinator {
    private static readonly object Gate = new();
    private static ulong _nextCommandId;
    private static PendingAdd? _pending;

    private const int AckTimeoutMs = 8000;

    private sealed class PendingAdd {
        public required ulong CommandId { get; init; }
        public required MpCheatAddCardPayload Payload { get; init; }
        public required HashSet<ulong> AwaitingPeers { get; init; }
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
        var remotes = GetRemotePlayerNetIds();

        PendingAdd pending;
        lock (Gate) {
            _pending?.Completion.TrySetResult(false);
            pending = new PendingAdd {
                CommandId = commandId,
                Payload = payload,
                AwaitingPeers = remotes.ToHashSet(),
            };
            _pending = pending;
        }

        BroadcastCommand(MpCheatCommandKind.AddCardPrepare, commandId, payload);

        if (remotes.Count == 0) {
            BroadcastCommand(MpCheatCommandKind.AddCardExecute, commandId, payload);
            await ExecuteOnAllPeers(state, targetPlayer, card, request, upgradePreviewStyle);
            ClearPending(commandId);
            MainFile.Logger.Info($"[MpCheat] AddCard command {commandId} executed (solo host).");
            return I18N.T("mpcheat.cardAdd.success", "Card added (all players).");
        }

        using var cts = new CancellationTokenSource(AckTimeoutMs);
        try {
            var ok = await pending.Completion.Task.WaitAsync(cts.Token);
            if (!ok) {
                var fail = pending.Acks.Values.FirstOrDefault(a => !a.Success);
                ClearPending(commandId);
                return fail != null
                    ? FormatPeerError(fail)
                    : I18N.T("mpcheat.cardAdd.failed", "Add card failed.");
            }

            BroadcastCommand(MpCheatCommandKind.AddCardExecute, commandId, payload);
            await ExecuteOnAllPeers(state, targetPlayer, card, request, upgradePreviewStyle);
            ClearPending(commandId);
            MainFile.Logger.Info($"[MpCheat] AddCard command {commandId} executed for {cardId}.");
            return I18N.T("mpcheat.cardAdd.success", "Card added (all players).");
        }
        catch (OperationCanceledException) {
            ClearPending(commandId);
            return I18N.T("mpcheat.cardAdd.timeout", "Add card timed out waiting for other players.");
        }
    }

    public static void OnPrepareReceived(MpCheatCommandMessage message) {
        if (!MpCheatSession.CanUseMultiplayerCheats || message.AddCard == null) return;
        if (MpCheatSession.IsHost) return;

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

        PendingAdd? pending;
        lock (Gate) {
            pending = _pending;
            if (pending == null || pending.CommandId != ack.CommandId) return;
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

    private static List<ulong> GetRemotePlayerNetIds() {
        var local = RunManager.Instance?.NetService?.NetId ?? 0;
        return RunManager.Instance?.DebugOnlyGetState()?.Players
            .Where(p => p.NetId != local)
            .Select(p => p.NetId)
            .ToList() ?? [];
    }

    private static void ClearPending(ulong commandId) {
        lock (Gate) {
            if (_pending?.CommandId == commandId)
                _pending = null;
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
}
