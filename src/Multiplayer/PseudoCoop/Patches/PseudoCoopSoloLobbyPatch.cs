using System.Linq;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Allows host-only lobby to begin when phantom player will be spawned at launch.</summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.IsAboutToBeginGame))]
internal static class PseudoCoopSoloLobbyPatch {
    [HarmonyPostfix]
    static void Postfix(StartRunLobby __instance, ref bool __result) {
        if (__result) return;
        if (!SettingsStore.Current.SyncBotSpawnPhantomPlayer) return;
        if (__instance.NetService.Type != NetGameType.Host) return;
        if (__instance.Players.Count != 1) return;
        if (!__instance.Players.All(p => p.isReady)) return;
        __result = true;
    }
}
