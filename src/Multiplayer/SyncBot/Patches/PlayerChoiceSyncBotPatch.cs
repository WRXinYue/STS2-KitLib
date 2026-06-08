using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot.Patches;

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice))]
internal static class PlayerChoiceSyncBotPatch {
    [HarmonyPrefix]
    static void Prefix(Player player, uint choiceId) {
        if (!MpCheatSyncBot.ShouldSimulatePlayer(player)) return;

        var sync = RunManager.Instance?.PlayerChoiceSynchronizer;
        if (sync == null) return;

        var choice = SettingsStore.Current.MpAiTeammateEnabled
            ? MpChoiceBot.Decide(player)
            : MpCheatSyncBot.DefaultIndexChoice();

        sync.ReceiveReplayChoice(player, choiceId, choice);
        MainFile.Logger.Debug(
            $"[SyncBot] Pre-filled choice id={choiceId} for player {player.NetId}.");
    }
}
