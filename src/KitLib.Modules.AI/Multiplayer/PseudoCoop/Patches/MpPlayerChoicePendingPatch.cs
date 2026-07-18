using HarmonyLib;
using KitLib.Multiplayer.SyncBot;
using KitLib.Singleplayer.Companion;
using MegaCrit.Sts2.Core.Entities.Multiplayer;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.GameActions;
using MegaCrit.Sts2.Core.GameActions.Multiplayer;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

[HarmonyPatch(typeof(ActionQueueSet), nameof(ActionQueueSet.PauseActionForPlayerChoice))]
internal static class MpPlayerChoicePendingPatch {
    [HarmonyPostfix]
    static void Postfix(GameAction action, PlayerChoiceOptions options) {
        if (action?.OwnerId is not ulong ownerId || ownerId == 0) return;

        var rm = RunManager.Instance;
        var state = rm?.DebugOnlyGetState();
        var player = state?.GetPlayer(ownerId);
        if (player == null
            || (!MpCheatSyncBot.ShouldSimulatePlayer(player)
                && !SpvCompanionChoiceRouting.ShouldAutoChoose(player)))
            return;

        if (!TryGetLastReservedChoiceId(state!, player, out var choiceId)) return;

        MpPendingPlayerChoice.Register(ownerId, choiceId, options);
    }

    static bool TryGetLastReservedChoiceId(RunState state, Player player, out uint choiceId) {
        choiceId = 0;
        var sync = RunManager.Instance?.PlayerChoiceSynchronizer;
        if (sync == null) return false;

        int slot = state.GetPlayerSlotIndex(player);
        if (slot < 0 || slot >= sync.ChoiceIds.Count) return false;

        uint nextId = sync.ChoiceIds[slot];
        if (nextId == 0) return false;

        choiceId = nextId - 1;
        return true;
    }
}
