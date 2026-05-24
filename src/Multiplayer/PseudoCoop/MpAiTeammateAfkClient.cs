using DevMode.AI.AutoPlay;
using DevMode.Multiplayer.Cheat;
using DevMode.Settings;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop;

/// <summary>Client AFK: local player accepts host-enqueued combat actions only.</summary>
internal static class MpAiTeammateAfkClient {
    public static bool IsSessionEnabled =>
        DevModeInstanceRegistry.IsDualInstanceActive()
            ? DevModeInstance.SessionLan.MpAiTeammateAfkClient
            : SettingsStore.Current.MpAiTeammateAfkClient;

    public static bool IsEnabled =>
        IsSessionEnabled
        && MpCheatSession.InMultiplayerRun
        && !MpCheatSession.IsHost;

    public static void SetSessionEnabled(bool enabled) {
        if (enabled
            && !MpCheatSession.IsHost
            && !DevModeInstanceRegistry.IsDualInstanceActive()
            && !SettingsStore.Current.MpAiTeammateDriveLiveEnet) {
            MainFile.Logger.Warn(
                "[MpAiTeammate] AFK client enabled but host DriveLiveEnet is off in shared settings — "
                + "host must apply LAN host preset or desync is likely.");
        }

        if (DevModeInstanceRegistry.IsDualInstanceActive())
            DevModeInstance.SessionLan.MpAiTeammateAfkClient = enabled;
        else {
            SettingsStore.Current.MpAiTeammateAfkClient = enabled;
            SettingsStore.Save();
        }

        if (enabled) AiPlayModule.Instance.StopLoop();
        MainFile.Logger.Info(
            $"[MpAiTeammate] AFK client {(enabled ? "enabled" : "disabled")} pid={DevModeInstance.ProcessId} dual={DevModeInstanceRegistry.IsDualInstanceActive()}");
    }

    public static bool ShouldBlockLocalCombatInput(Player? player) {
        if (!IsEnabled || player == null) return false;
        return LocalContext.IsMe(player);
    }

    /// <summary>
    /// Client AFK backup when phase 1 begins before host-driven enqueue arrives
    /// (e.g. dual EndPlayerTurn → EndTurnPhaseOne without NotPlayPhase on P1 ready).
    /// </summary>
    public static void TrySignalReadyToBeginEnemyTurn() {
        if (!IsEnabled) return;

        var cm = CombatManager.Instance;
        if (cm is not { IsInProgress: true }) return;

        var sync = RunManager.Instance?.ActionQueueSynchronizer;
        if (sync?.CombatState != ActionSynchronizerCombatState.EndTurnPhaseOne) return;

        var me = Sts2CombatCompat.GetLocalPlayer();
        if (me == null || me.Creature.IsDead) return;
        if (Sts2CombatCompat.IsPlayerReadyToBeginEnemyTurn(cm, me)) return;
        if (PseudoCoopActionQueue.HasQueuedReadyToBeginEnemyTurn(me.NetId)) return;

        sync.RequestEnqueue(new ReadyToBeginEnemyTurnAction(me));
        MainFile.Logger.Info($"[MpAiTeammate] AFK client ready-to-begin-enemy-turn netId={me.NetId}.");
    }
}
