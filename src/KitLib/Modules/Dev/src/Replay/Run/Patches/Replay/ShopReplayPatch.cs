using HarmonyLib;
using KitLib.Replay.Utils;
using MegaCrit.Sts2.Core.Nodes.Rooms;

namespace KitLib.Replay.Patches.Replay;

using KitLib.Replay;

[HarmonyPatch(typeof(NMerchantRoom))]
public static class ShopReplayPatch {
    [HarmonyPostfix]
    [HarmonyPatch("_Ready")]
    public static void ReadyPostfix(NMerchantRoom __instance) {
        ReplayState.ActiveMerchantRoom = __instance;
        if (ReplayEngine.IsActive)
            ReplayDispatcher.TryDispatch();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(NMerchantRoom.OpenInventory))]
    public static void OpenInventoryPostfix(NMerchantRoom __instance) {
        if (!ReplayEngine.IsActive)
            return;

        ReplayDispatcher.TryDispatch();
    }
}
