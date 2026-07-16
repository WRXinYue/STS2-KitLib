using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Patches;

[HarmonyPatch(typeof(NErrorPopup), "_Ready")]
internal static class ErrorPopupKitLibLogExportPatch {
    [HarmonyPostfix]
    static void Postfix(NErrorPopup __instance) {
        if (!KitLibHost.IsModuleLoaded(KitLibModuleIds.Panel))
            return;

        ErrorPopupKitLibLogExportUI.TryAttach(__instance);
    }
}
