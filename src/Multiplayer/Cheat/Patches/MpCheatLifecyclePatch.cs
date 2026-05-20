using DevMode.Multiplayer.Cheat;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.Cheat.Patches;

[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class MpCheatNRunReadyPatch {
    static void Postfix() => MpCheatNetBus.TryRegisterHandlers();
}

[HarmonyPatch(typeof(RunManager))]
internal static class MpCheatRunLifecyclePatch {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.Launch))]
    static void OnLaunch() => MpCheatSync.OnRunStarted();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.OnEnded))]
    static void OnEnded() {
        MpCheatSync.OnRunEnded();
        DevModeState.OnRunEnded();
    }
}
