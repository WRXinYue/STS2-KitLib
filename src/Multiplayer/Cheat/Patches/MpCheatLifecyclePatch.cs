using KitLib;
using KitLib.Multiplayer.Cheat;
using KitLib.Multiplayer.SyncBot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.Cheat.Patches;

[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class MpCheatNRunReadyPatch {
    static void Postfix() {
        if (KitLibState.PseudoCoopLaunchPending || KitLibState.PseudoCoopDeferHeavyUi) return;
        if (!MpCheatSession.LocalOptIn) return;
        MpCheatSync.OnRunStarted();
        MpCheatSync.TryPublishInitialHostConfig("nrun_ready");
    }
}

[HarmonyPatch(typeof(RunManager))]
internal static class MpCheatRunLifecyclePatch {
    // MpCheat arms on NRun._Ready only — Launch postfixes run during a fragile embark window.

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.OnEnded))]
    static void OnEnded() {
        MpCheatSync.OnRunEnded();
        MpCheatSyncBot.OnRunEnded();
        KitLibState.OnRunEnded();
    }
}
