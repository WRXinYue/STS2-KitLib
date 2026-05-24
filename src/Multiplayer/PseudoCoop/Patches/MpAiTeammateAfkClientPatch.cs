using DevMode.Multiplayer.Cheat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop.Patches;

[HarmonyPatch(typeof(ActionQueueSynchronizer), nameof(ActionQueueSynchronizer.RequestEnqueue))]
internal static class MpAiTeammateAfkRequestEnqueuePatch {
    [HarmonyPrefix]
    static bool Prefix(GameAction action) {
        if (!MpAiTeammateAfkClient.IsEnabled) return true;
        if (RunManager.Instance?.NetService?.Type != NetGameType.Client) return true;

        var owner = ResolveOwner(action);
        if (!MpAiTeammateAfkClient.ShouldBlockLocalCombatInput(owner)) return true;

        MainFile.Logger.Debug(
            $"[MpAiTeammate] Blocked local RequestEnqueue for netId={owner?.NetId} (AFK client).");
        return false;
    }

    static Player? ResolveOwner(GameAction action) {
        var player = Traverse.Create(action).Field<Player>("_player").Value;
        if (player != null) return player;
        return Traverse.Create(action).Property<Player>("Player").Value;
    }
}
