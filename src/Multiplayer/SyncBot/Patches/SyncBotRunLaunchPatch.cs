using System.Linq;
using DevMode.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Unlocks;

namespace DevMode.Multiplayer.SyncBot.Patches;

/// <summary>Runs after run launch: optional phantom player, then refresh simulated peer set.</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class SyncBotRunLaunchPatch {
    [HarmonyPostfix]
    [HarmonyPriority(500)]
    static void Postfix(RunState __result) {
        TrySpawnPhantomPlayer(__result);
        MpCheatSyncBot.RefreshSimulatedPeers();
    }

    static void TrySpawnPhantomPlayer(RunState state) {
        if (!SettingsStore.Current.SyncBotSpawnPhantomPlayer) return;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Host) return;
        if (state.Players.Count != 1) return;
        if (state.Players.Any(p => p.NetId == MpCheatSyncBot.PhantomPlayerNetId)) return;

        try {
            var host = state.Players[0];
            var unlock = host.UnlockState
                ?? new UnlockState(SaveManager.Instance.Progress);
            var phantom = Player.CreateForNewRun(host.Character, unlock, MpCheatSyncBot.PhantomPlayerNetId);
            state.AddPlayerDebug(phantom, -1);
            MainFile.Logger.Info(
                $"[SyncBot] Phantom player spawned netId={MpCheatSyncBot.PhantomPlayerNetId} character={host.Character.Id.Entry}.");
        }
        catch (System.Exception ex) {
            MainFile.Logger.Warn($"[SyncBot] Phantom player spawn failed: {ex.Message}");
        }
    }
}
