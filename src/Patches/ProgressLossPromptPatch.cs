using KitLib.Progress;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace KitLib.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProgressData))]
internal static class ProgressLossDetectPatch {
    static void Postfix() => ModCharacterProgressLossDetector.DetectAfterProgressLoad();
}

[HarmonyPatch(typeof(ProgressSaveManager), nameof(ProgressSaveManager.LoadProgress))]
internal static class ProgressLossLoadProgressPatch {
    static void Postfix() => ModCharacterProgressLossDetector.DetectAfterProgressLoad();
}
