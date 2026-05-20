using DevMode.Multiplayer.Cheat;
using DevMode.Multiplayer.SyncBot;
using DevMode.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.SyncBot.Patches;

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class CombatSyncBotPatch {
    static double _accum;

    [HarmonyPostfix]
    static void Postfix(double delta) {
        if (!SettingsStore.Current.SyncBotEnabled
            || !SettingsStore.Current.SyncBotAutoEndTurn
            || !MpCheatSession.IsHost) return;

        _accum += delta;
        if (_accum < 0.5) return;
        _accum = 0;

        var cm = CombatManager.Instance;
        if (cm is not { IsInProgress: true, IsPlayPhase: true }) return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return;

        foreach (var player in state.Players) {
            if (LocalContext.IsMe(player)) continue;
            if (!MpCheatSyncBot.ShouldSimulatePlayer(player)) continue;
            if (cm.IsPlayerReadyToEndTurn(player)) continue;
            if (player.PlayerCombatState == null || player.Creature.IsDead) continue;

            cm.SetReadyToEndTurn(player, canBackOut: false);
        }
    }
}
