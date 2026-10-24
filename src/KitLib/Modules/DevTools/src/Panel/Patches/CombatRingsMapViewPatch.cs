using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Patches;

// The band switches between combat and run totals when the map opens, so repaint on the transition.
// Refresh is deferred and no-ops when the HUD is not attached.
[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class CombatRingsMapOpenPatch {
    public static void Postfix() => CombatRingsHud.Refresh();
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
public static class CombatRingsMapClosePatch {
    public static void Postfix() => CombatRingsHud.Refresh();
}
