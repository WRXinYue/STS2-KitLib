using HarmonyLib;
using KitLib.Replay.Commands;
using MegaCrit.Sts2.Core.Nodes.Screens.CardSelection;

namespace KitLib.Replay.Patches.Replay;

using KitLib.Replay;

[HarmonyPatch(typeof(NCardRewardSelectionScreen), "_Ready")]
public static class CardRewardReplayPatch {
    [HarmonyPostfix]
    public static void Postfix(NCardRewardSelectionScreen __instance) {
        if (!ReplayEngine.IsActive)
            return;

        ReplayState.CardRewardSelectionScreen = __instance;
    }
}
