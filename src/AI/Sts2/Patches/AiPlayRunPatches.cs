using KitLib.AI.AutoPlay;
using HarmonyLib;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.AI.Sts2.Patches;

[HarmonyPatch(typeof(RunManager))]
internal static class AiPlayRunPatches {
    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.Launch))]
    static void OnRunLaunched() => AiPlayModule.Instance.OnRunStarted();

    [HarmonyPostfix]
    [HarmonyPatch(nameof(RunManager.OnEnded))]
    static void OnRunEnded() => AiPlayModule.Instance.OnRunEnded();
}
