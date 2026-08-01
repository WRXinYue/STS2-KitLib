using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Rooms;
using MegaCrit.Sts2.Core.Nodes.Screens.TreasureRoomRelic;

namespace KitLib.Patches;

/// <summary>Re-apply player layout when a mod-test teleport finishes loading the target room.</summary>
[HarmonyPatch(typeof(NRestSiteRoom), "_Ready")]
internal static class MpUiDebugRestSiteReadyPatch {
    static void Prefix() =>
        MpUiDebugPlayerService.ApplyPendingScenario(MpUiDebugScenario.RestSiteFourSame);
}

[HarmonyPatch(typeof(NTreasureRoomRelicCollection), nameof(NTreasureRoomRelicCollection.Initialize))]
internal static class MpUiDebugTreasureRelicInitPatch {
    static void Prefix() =>
        MpUiDebugPlayerService.ApplyPendingScenario(MpUiDebugScenario.RelicSoloHand);
}
