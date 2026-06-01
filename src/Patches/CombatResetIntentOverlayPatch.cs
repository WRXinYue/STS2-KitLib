using DevMode.UI;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;

namespace DevMode.Patches;

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.Reset))]
internal static class CombatResetIntentOverlayPatch {
    [HarmonyPrefix]
    static void Prefix() => MonsterIntentOverlayUI.Hide();
}
