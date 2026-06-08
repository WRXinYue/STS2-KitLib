using System.Linq;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Multiplayer.SyncBot;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot.Patches;

[HarmonyPatch(typeof(NRun), nameof(NRun._Process))]
internal static class CombatSyncBotPatch {
    static double _accum;

    [HarmonyPostfix]
    static void Postfix(double delta) {
        if (!SettingsStore.Current.SyncBotAutoEndTurn || !MpCheatSession.IsHost) return;
        if (!SettingsStore.Current.SyncBotEnabled && !SettingsStore.Current.MpAiTeammateEnabled)
            return;

        _accum += delta;
        if (_accum < 0.5) return;
        _accum = 0;

        var cm = CombatManager.Instance;
        if (cm == null || !Sts2CombatCompat.IsCombatPlayPhase(cm)) return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return;

        SimulatedPeerRegistry.Refresh();

        foreach (var player in SimulatedPeerRegistry.GetRemoteCombatAssistTargets()) {
            if (cm.IsPlayerReadyToEndTurn(player)) continue;
            if (player.PlayerCombatState == null || player.Creature.IsDead) continue;

            if (SettingsStore.Current.MpAiTeammateEnabled
                && player.PlayerCombatState.Hand?.Cards.Any(c => c.CanPlay(out _, out _)) == true)
                continue;

            if (SimulatedPeerRegistry.ShouldHostEnqueueCombatAction(player))
                MpAiTeammateCombatActions.SignalEndTurn(player);
            else
                cm.SetReadyToEndTurn(player, canBackOut: false);
        }
    }
}
