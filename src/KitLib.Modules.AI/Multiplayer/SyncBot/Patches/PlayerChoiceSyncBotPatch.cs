using HarmonyLib;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot.Patches;

[HarmonyPatch(typeof(PlayerChoiceSynchronizer), nameof(PlayerChoiceSynchronizer.WaitForRemoteChoice))]
internal static class PlayerChoiceSyncBotPatch {
    [HarmonyPrefix]
    static void Prefix(Player player, uint choiceId) {
        var isSpCompanion = SpvCompanionChoiceRouting.ShouldAutoChoose(player);
        if (!MpCheatSyncBot.ShouldSimulatePlayer(player) && !isSpCompanion) return;

        var sync = RunManager.Instance?.PlayerChoiceSynchronizer;
        if (sync == null) return;

        var choice = MpCheatSyncBot.ShouldSimulatePlayer(player) && !isSpCompanion
            ? (AiSessionSettings.MpAiTeammateEnabled
                ? MpChoiceBot.Decide(player, choiceId)
                : MpCheatSyncBot.DefaultIndexChoice())
            : MpChoiceBot.Decide(player, choiceId);

        sync.ReceiveReplayChoice(player, choiceId, choice);
        KitLog.Debug("SyncBot", $"Pre-filled choice id={choiceId} for player {player.NetId} type={choice.type}.");
    }
}
