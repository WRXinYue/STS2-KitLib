using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.SyncBot.Patches;

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice))]
internal static class PlayerChoiceSyncBotPatch {
    [HarmonyPrefix]
    static void Prefix(Player player, uint choiceId) {
        if (!MpCheatSyncBot.ShouldSimulatePlayer(player)) return;

        var sync = RunManager.Instance?.PlayerChoiceSynchronizer;
        if (sync == null) return;

        sync.ReceiveReplayChoice(player, choiceId, MpCheatSyncBot.DefaultIndexChoice());
        MainFile.Logger.Debug(
            $"[SyncBot] Pre-filled choice id={choiceId} for player {player.NetId} (index 0).");
    }
}
