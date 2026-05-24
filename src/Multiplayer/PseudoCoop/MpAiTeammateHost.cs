using System.Linq;
using System.Threading.Tasks;
using DevMode.AI;
using DevMode.AI.AutoPlay.Strategies;
using DevMode.AI.Core;
using DevMode.AI.Core.Schema;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop;

/// <summary>Host-only: rule-based combat for simulated remote players (human plays local).</summary>
internal static class MpAiTeammateHost {
    static GameLoop? _loop;
    static bool _tickRunning;

    public static bool IsEnabled =>
        SettingsStore.Current.MpAiTeammateEnabled
        && MpCheatSession.IsHost
        && MpCheatSession.InMultiplayerRun;

    public static void OnRunEnded() {
        AiHostContext.Clear();
        _tickRunning = false;
        _loop = null;
        PseudoCoopActionQueue.ClearInFlightAll();
    }

    public static void Poll(double delta, ref double accum) {
        if (!IsEnabled) return;

        accum += delta;
        if (accum < 0.4) return;
        accum = 0;

        if (_tickRunning) return;

        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return;

        SimulatedPeerRegistry.Refresh();

        foreach (var player in SimulatedPeerRegistry.GetMpAiTeammateTargets()) {
            if (player.Creature.IsDead) continue;
            if (cm.IsPlayerReadyToEndTurn(player)) continue;
            if (PseudoCoopActionQueue.HasQueuedEndTurn(player.NetId)) continue;
            if (PseudoCoopActionQueue.HasPendingCombatActions(player.NetId)) continue;

            if (!HasPlayableCard(player)) {
                MpAiTeammateCombatActions.SignalEndTurn(player);
                continue;
            }

            if (!ShouldActForPlayer(player, cm)) continue;

            _tickRunning = true;
            TaskHelper.RunSafely(RunCombatDecisionAsync(player));
            return;
        }
    }

    static bool HasPlayableCard(Player player) {
        var hand = player.PlayerCombatState?.Hand?.Cards;
        return hand != null && hand.Any(c => c.CanPlay(out _, out _));
    }

    static bool ShouldActForPlayer(Player player, CombatManager cm) => HasPlayableCard(player);

    static async Task RunCombatDecisionAsync(Player player) {
        try {
            EnsureLoop();
            AiHostContext.ActiveNetId = player.NetId;
            await _loop!.OnDecisionPointAsync(GamePhase.Combat);
        }
        catch (System.Exception ex) {
            MainFile.Logger.Warn($"[MpAiTeammate] Decision failed netId={player.NetId}: {ex.Message}");
        }
        finally {
            AiHostContext.Clear();
            _tickRunning = false;
        }
    }

    static void EnsureLoop() {
        _loop ??= new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            new SimpleStrategy(),
            msg => MainFile.Logger.Info($"[MpAiTeammate] {msg}")) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
    }
}
