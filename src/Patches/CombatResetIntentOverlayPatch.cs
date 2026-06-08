using KitLib.UI;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace KitLib.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class CombatResetIntentOverlayPatch {
    [HarmonyPrefix]
    static void Prefix() => MonsterIntentOverlayUI.Hide();
}
