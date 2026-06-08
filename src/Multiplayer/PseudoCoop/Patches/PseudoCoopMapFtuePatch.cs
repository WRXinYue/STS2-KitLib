using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Skips first-time map tutorial overlay during pseudo-coop (clicks still looked "dead" while waiting for phantom vote).</summary>
[HarmonyPatch(typeof(NMapScreen), "InitMapPrompt")]
internal static class PseudoCoopMapFtuePatch {
    static bool Prefix() {
        if (!KitLibState.IsActive) return true;
        if (!SettingsStore.Current.SyncBotSpawnPhantomPlayer) return true;
        MainFile.Logger.Debug("[PseudoCoop] Skipping map_select_ftue during pseudo-coop.");
        return false;
    }
}
