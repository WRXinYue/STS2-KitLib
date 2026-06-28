using HarmonyLib;
using KitLib.Host;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat.Patches;

// MpCheat arms only via PseudoCoopDeferredInit.RunLateMpCheatArm() for pseudo-coop sessions.
// Regular multiplayer must not arm MpCheat during NRun._Ready — the Steam relay connection
// is not yet stable at that point, and SendMessage crashes the host.
[HarmonyPatch(typeof(RunManager))]
internal static class MpCheatRunLifecyclePatch {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.OnEnded))]
    static void OnEnded() {
        MpCheatSync.OnRunEnded();
        KitLibSyncBotOps.OnRunEnded?.Invoke();
        KitLibState.OnRunEnded();
    }
}
