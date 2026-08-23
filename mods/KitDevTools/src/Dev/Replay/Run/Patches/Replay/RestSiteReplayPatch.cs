using HarmonyLib;
using KitLib.Replay.Utils;
using MegaCrit.Sts2.Core.Multiplayer.Game;

namespace KitLib.Replay.Patches.Replay;

using KitLib.Replay;

[HarmonyPatch(typeof(RestSiteSynchronizer), nameof(RestSiteSynchronizer.BeginRestSite))]
public static class RestSiteReplayPatch {
    [HarmonyPostfix]
    public static void Postfix(RestSiteSynchronizer __instance) {
        RngCheckpointLogger.Log("RestSite (BeginRestSite)");

        if (!ReplayEngine.IsActive)
            return;

        ReplayState.ActiveRestSiteSynchronizer = __instance;
        ReplayDispatcher.DispatchNow();
    }
}
