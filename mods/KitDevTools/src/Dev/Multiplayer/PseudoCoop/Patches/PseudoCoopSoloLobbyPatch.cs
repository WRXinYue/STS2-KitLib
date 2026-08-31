using System.Collections;
using System.Linq;
using HarmonyLib;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Multiplayer;
using MegaCrit.Sts2.Core.Multiplayer.Game;
using MegaCrit.Sts2.Core.Multiplayer.Game.Lobby;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Allows a host-only lobby to begin when a phantom player will be spawned at launch.</summary>
[HarmonyPatch(typeof(StartRunLobby), nameof(StartRunLobby.IsAboutToBeginGame))]
internal static class PseudoCoopSoloLobbyPatch {
    [HarmonyPostfix]
    static void Postfix(StartRunLobby __instance, ref bool __result) {
        if (__result)
            return;
        try {
            if (ShouldBeginWithPhantomHost(__instance))
                __result = true;
        }
        catch (Exception ex) {
            // Harmony postfix exceptions abort vanilla embark. A beta-built DLL calling
            // StartRunLobby.Players (List<StartRunLobbyPlayer> on 0.110.1+) JITs as
            // MissingMethodException on 0.107.1 (List<LobbyPlayer>).
            KitLog.Warn("PseudoCoop", $"IsAboutToBeginGame postfix ignored ({ex.GetType().Name}: {ex.Message}).");
        }
    }

    static bool ShouldBeginWithPhantomHost(StartRunLobby lobby) {
        if (!AiSessionSettings.SyncBotSpawnPhantomPlayer)
            return false;
        if (lobby.NetService.Type != NetGameType.Host)
            return false;
        return AllReadySolo(lobby);
    }

    static bool AllReadySolo(StartRunLobby lobby) {
#if STS2_STABLE_PROFILE
        var players = lobby.Players;
        return players.Count == 1 && players.All(p => p.isReady);
#else
        var list = typeof(StartRunLobby).GetProperty(nameof(StartRunLobby.Players))?.GetValue(lobby);
        if (list is not ICollection players || players.Count != 1)
            return false;

        foreach (var player in players) {
            if (player is null || !IsReady(player))
                return false;
        }

        return true;
#endif
    }

#if !STS2_STABLE_PROFILE
    static bool IsReady(object player) {
        var type = player.GetType();
        if (type.GetField("isReady")?.GetValue(player) is bool fieldReady)
            return fieldReady;
        return type.GetProperty("isReady")?.GetValue(player) is bool propReady && propReady;
    }
#endif
}
