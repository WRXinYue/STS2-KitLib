using HarmonyLib;
using KitLib.Host;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Multiplayer.PseudoCoop.Patches;

/// <summary>Pseudo-coop embark milestones and dual-instance preset hooks.</summary>
[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class PseudoCoopLaunchTracePatch {
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void Postfix() {
        KitLibPseudoCoopOps.RunDualInstanceLanPresets?.Invoke();
        if (KitLibState.IsPseudoCoopSession)
            KitLog.Info("PseudoCoop", "RunManager.Launch postfixes done.");
    }
}

[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class PseudoCoopNRunReadyTracePatch {
    [HarmonyPrefix]
    static void Prefix() {
        if (KitLibState.IsPseudoCoopSession)
            KitLog.Info("PseudoCoop", "NRun._Ready starting…");
    }

    [HarmonyPostfix]
    static void Postfix() {
        KitLibPseudoCoopOps.RunDualInstanceLanPresets?.Invoke();
        if (KitLibState.IsPseudoCoopSession)
            KitLog.Info("PseudoCoop", "NRun._Ready complete.");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterAct))]
internal static class PseudoCoopEnterActTracePatch {
    [HarmonyPrefix]
    static void Prefix(int currentActIndex) {
        if (KitLibState.IsPseudoCoopSession)
            KitLog.Info("PseudoCoop", $"EnterAct({currentActIndex}) starting…");
    }

    [HarmonyPostfix]
    static void Postfix(int currentActIndex) {
        if (currentActIndex == 0)
            PseudoCoopDeferredInit.TryScheduleAfterEnterAct0();

        if (KitLibState.IsPseudoCoopSession)
            KitLog.Info("PseudoCoop", $"EnterAct({currentActIndex}) complete.");
    }
}
