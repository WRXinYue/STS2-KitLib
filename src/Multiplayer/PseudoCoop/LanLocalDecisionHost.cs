using System;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.AutoPlay;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.Multiplayer.Cheat;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop;

/// <summary>
/// LAN dev: automates the local player's non-combat phases on this instance only.
/// Host hand-plays local combat; remote combat is handled by <see cref="MpAiTeammateHost"/>.
/// </summary>
internal static class LanLocalDecisionHost {
    static bool _tickRunning;
    static GameLoop? _loop;

    static readonly GamePhase[] NonCombatPhases = [
        GamePhase.MapSelection,
        GamePhase.EventChoice,
        GamePhase.RelicSelection,
        GamePhase.CardReward,
        GamePhase.RewardScreen,
        GamePhase.PostCombatTransition,
    ];

    public static bool IsEnabled =>
        MpCheatSession.InMultiplayerRun
        && (MpAiTeammateAfkClient.IsEnabled
            || (MpCheatSession.IsHost
                && SettingsStore.Current.MpAiTeammateEnabled
                && SettingsStore.Current.MpAiTeammateDriveLiveEnet));

    public static void OnRunEnded() {
        _tickRunning = false;
        _loop = null;
        AiDecisionGate.Reset();
    }

    public static void Poll(double delta, ref double accum) {
        if (!IsEnabled) return;

        accum += delta;
        if (accum < 0.6) return;
        accum = 0;

        if (_tickRunning) return;
        if (AiDecisionGate.IsCombatInProgress) return;
        if (!AiDecisionGate.TryEnter()) return;

        var phase = AiPlayServices.StateProvider.CurrentPhase;
        if (phase is GamePhase.None or GamePhase.Combat or GamePhase.GameOver or GamePhase.Victory) {
            AiDecisionGate.Exit();
            return;
        }

        if (Array.IndexOf(NonCombatPhases, phase) < 0) {
            AiDecisionGate.Exit();
            return;
        }

        if (!LanAiOwnership.TryGetLocalPlayer(out var local)) {
            AiDecisionGate.Exit();
            return;
        }

        _tickRunning = true;
        TaskHelper.RunSafely(RunDecisionAsync(local, phase));
    }

    static async Task RunDecisionAsync(Player local, GamePhase phase) {
        try {
            AiHostContext.ActiveNetId = local.NetId;
            _loop ??= CreateLoop(local);
            await _loop.OnDecisionPointAsync(phase);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[LanLocal] Decision failed netId={local.NetId}: {ex.Message}");
        }
        finally {
            AiHostContext.Clear();
            _tickRunning = false;
            AiDecisionGate.Exit();
        }
    }

    static GameLoop CreateLoop(Player local) {
        return new GameLoop(
            AiPlayServices.StateProvider,
            AiPlayServices.ActionExecutor,
            StrategyResolver.Resolve(local),
            msg => AiDecisionLog.Record("LanLocal", msg)) {
            ActionDelayMs = SettingsStore.Current.AutoPlayDelayMs,
        };
    }
}
