using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.AutoPlay.Strategies;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Singleplayer.Companion;

/// <summary>Combat AI for companions in singleplayer via direct play (no action queue).</summary>
internal static class SpvCompanionAiHost {
    static bool _tickRunning;
    static readonly Dictionary<ulong, GameLoop> _loops = [];

    public static bool IsEnabled =>
        SpvCompanionRegistry.HasAny && SpvCompanionRegistry.IsSingleplayerRun();

    public static void OnRunEnded() {
        AiHostContext.Clear();
        _tickRunning = false;
        AiDecisionGate.Reset();
        _loops.Clear();
        SpvCompanionNonCombatHost.OnRunEnded();
        SpvCompanionSession.OnRunEnded();
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

        if (RunManager.Instance?.DebugOnlyGetState() == null) {
            AiDecisionGate.Exit();
            return;
        }

        foreach (var player in SpvCompanionRegistry.GetCombatTargets()) {
            if (player.Creature.IsDead) continue;
            PseudoCoopActionQueue.ClearStaleInFlight(player.NetId);
            if (cm.IsPlayerReadyToEndTurn(player)) continue;
            if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) continue;
            if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) continue;
            if (cm.IsExecutingCardOrPotionEffect(player)) continue;

            if (!HasPlayableCard(player)) {
                if (!HasCardsInHand(player)) {
                    if (SpvCompanionSession.IsAwaitingCombatBootstrap(player.NetId))
                        continue;

                    SpvCompanionCombatActions.SignalEndTurn(player);
                    continue;
                }

                SpvCompanionCombatActions.SignalEndTurn(player);
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

    static bool HasCardsInHand(Player player) {
        var hand = player.PlayerCombatState?.Hand?.Cards;
        return hand is { Count: > 0 };
    }

    static async Task RunCombatDecisionAsync(Player player) {
        try {
            AiHostContext.ActiveNetId = player.NetId;
            using (SpvCompanionCardSelectScope.Enter()) {
                var loop = GetOrCreateLoop(player);
                await loop.OnDecisionPointAsync(GamePhase.Combat);
            }
        }
        catch (System.Exception ex) {
            KitLog.Warn("SpCompanion", $"Decision failed netId={player.NetId}: {ex.Message}");
        }
        finally {
            AiHostContext.Clear();
            _tickRunning = false;
            AiDecisionGate.Exit();
        }
    }

    static GameLoop GetOrCreateLoop(Player player) {
        if (_loops.TryGetValue(player.NetId, out var existing))
            return existing;

        var loop = new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            StrategyResolver.Resolve(player),
            msg => AiDecisionLog.Record("SpCompanion", $"netId={player.NetId} {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
        _loops[player.NetId] = loop;
        return loop;
    }
}
