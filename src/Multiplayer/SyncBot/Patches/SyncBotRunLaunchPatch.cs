using KitLib.Multiplayer.PseudoCoop;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.SyncBot.Patches;

// Launch postfix removed: preset is applied in PseudoCoopLobbyHost; phantom spawns on map history.

[HarmonyPatch(typeof(RunState), nameof(RunState.AppendToMapPointHistory))]
internal static class SyncBotPhantomAfterMapPatch {
    [HarmonyPostfix]
    static void Postfix(RunState __instance) => PhantomPlayerSpawner.TrySpawn(__instance);
}
