using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.AutoPlay.Strategies;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>Host-only: rule-based combat for simulated remote players (human plays local).</summary>
internal static class MpAiTeammateHost {
    static bool _tickRunning;
    static readonly Dictionary<ulong, (ModelId CardId, int Streak)> _enqueueStreak = [];
    static readonly Dictionary<ulong, GameLoop> _loops = [];

    const int MaxPlayEnqueueStreak = 3;
    public static bool IsEnabled =>
        SettingsStore.Current.MpAiTeammateEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.InMultiplayerRun;

    public static void OnRunEnded() {
        AiHostContext.Clear();
        _tickRunning = false;
        AiDecisionGate.Reset();
        _loops.Clear();
        _enqueueStreak.Clear();
        PseudoCoopActionQueue.ClearInFlightAll();
        CompanionDecisionHost.OnRunEnded();
        LanLocalDecisionHost.OnRunEnded();
        AiDecisionLog.Clear();
    }

    internal static void NotifyCardQueued(ulong netId, ModelId cardId) {
        if (_enqueueStreak.TryGetValue(netId, out var entry) && entry.CardId == cardId)
            _enqueueStreak[netId] = (cardId, entry.Streak + 1);
        else
            _enqueueStreak[netId] = (cardId, 1);
    }

    internal static void NotifyCombatActionFinished(ulong netId) {
        ResetPlayStreak(netId);
    }

    static void ResetPlayStreak(ulong netId) => _enqueueStreak.Remove(netId);

    static bool HasExcessiveEnqueueStreak(ulong netId) =>
        _enqueueStreak.TryGetValue(netId, out var entry) && entry.Streak >= MaxPlayEnqueueStreak;

    public static void OnSessionDisabled() {
        AiHostContext.Clear();
        _tickRunning = false;

        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;

        foreach (var peer in SimulatedPeerRegistry.GetHostDrivenCombatPeers()) {
            if (peer.Creature.IsDead) continue;
            PseudoCoopActionQueue.ClearStaleInFlight(peer.NetId);
            MpAiTeammateCombatActions.ForceSignalEndTurnForHostDrivenPeer(peer);
        }

        MainFile.Logger.Info("[MpAiTeammate] Host AI disabled — flushed stale in-flight and signaled end turn for host-driven peers.");
    }

    public static void Poll(double delta, ref double accum) {
        if (!IsEnabled) return;

        accum += delta;
        if (accum < 0.4) return;
        accum = 0;

        if (_tickRunning) return;
        if (!AiDecisionGate.TryEnter()) return;

        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) {
            AiDecisionGate.Exit();
            return;
        }

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) {
            AiDecisionGate.Exit();
            return;
        }

        SimulatedPeerRegistry.Refresh();

        foreach (var player in SimulatedPeerRegistry.GetMpAiTeammateTargets()) {
            if (LanAiOwnership.IsLocalPlayer(player)) continue;
            if (player.Creature.IsDead) continue;
            PseudoCoopActionQueue.ClearStaleInFlight(player.NetId);
            if (cm.IsPlayerReadyToEndTurn(player)) continue;
            if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) continue;
            if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) continue;

            if (HasExcessiveEnqueueStreak(player.NetId)) {
                MainFile.Logger.Warn(
                    $"[MpAiTeammate] Repeated play enqueue without progress netId={player.NetId}; ending turn.");
                ResetPlayStreak(player.NetId);
                MpAiTeammateCombatActions.SignalEndTurn(player);
                continue;
            }

            if (!HasPlayableCard(player)) {
                MpAiTeammateCombatActions.SignalEndTurn(player);
                continue;
            }

            _tickRunning = true;
            TaskHelper.RunSafely(RunCombatDecisionAsync(player));
            return;
        }

        AiDecisionGate.Exit();
    }

    static bool HasPlayableCard(Player player) {
        var hand = player.PlayerCombatState?.Hand?.Cards;
        return hand != null && hand.Any(c => c.CanPlay(out _, out _));
    }

    static async Task RunCombatDecisionAsync(Player player) {
        try {
            AiHostContext.ActiveNetId = player.NetId;
            var loop = GetOrCreateLoop(player.NetId);
            await loop.OnDecisionPointAsync(GamePhase.Combat);
        }
        catch (System.Exception ex) {
            MainFile.Logger.Warn($"[MpAiTeammate] Decision failed netId={player.NetId}: {ex.Message}");
        }
        finally {
            AiHostContext.Clear();
            _tickRunning = false;
            AiDecisionGate.Exit();
        }
    }

    static GameLoop GetOrCreateLoop(ulong netId) {
        if (_loops.TryGetValue(netId, out var existing))
            return existing;

        var runState = RunManager.Instance?.DebugOnlyGetState();
        var player = runState?.Players.FirstOrDefault(p => p.NetId == netId);
        var strategy = StrategyResolver.Resolve(netId, player);

        var loop = new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            strategy,
            msg => AiDecisionLog.Record("MpAi", $"netId={netId} {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
        _loops[netId] = loop;
        return loop;
    }
}
