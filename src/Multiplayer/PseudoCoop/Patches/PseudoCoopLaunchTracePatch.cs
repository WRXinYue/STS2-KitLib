using DevMode.Multiplayer.LanTest;
using HarmonyLib;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.Multiplayer.PseudoCoop.Patches;

/// <summary>Milestone logs to pinpoint embark crashes.</summary>
[HarmonyPatch(typeof(NSceneContainer), nameof(NSceneContainer.SetCurrentScene))]
internal static class PseudoCoopSetSceneTracePatch {
    [HarmonyPrefix]
    static void Prefix() => MainFile.Logger.Info("[PseudoCoop] SetCurrentScene(NRun) starting…");

    [HarmonyPostfix]
    static void Postfix() => MainFile.Logger.Info("[PseudoCoop] SetCurrentScene(NRun) complete.");
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class PseudoCoopLaunchTracePatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix() {
        DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch();
        MainFile.Logger.Info("[PseudoCoop] RunManager.Launch postfixes done.");
    }
}

[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class PseudoCoopNRunReadyTracePatch {
    [HarmonyPrefix]
    static void Prefix() => MainFile.Logger.Info("[PseudoCoop] NRun._Ready starting…");

    [HarmonyPostfix]
    static void Postfix() {
        DualInstanceTestBootstrap.TryAutoLanPresetsOnLaunch();
        MainFile.Logger.Info("[PseudoCoop] NRun._Ready complete.");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class PseudoCoopEnterActTracePatch {
    [HarmonyPrefix]
    static void Prefix(int currentActIndex) =>
        MainFile.Logger.Info($"[PseudoCoop] EnterAct({currentActIndex}) starting…");

    [HarmonyPostfix]
    static void Postfix(int currentActIndex) {
        MainFile.Logger.Info($"[PseudoCoop] EnterAct({currentActIndex}) complete.");
        if (currentActIndex == 0)
            PseudoCoopDeferredInit.TryScheduleAfterEnterAct0();
    }
}
