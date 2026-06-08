using KitLib.Progress;
using HarmonyLib;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Patches;

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.InitProfileId))]
internal static class ModChangeGuardInitProfilePatch {
    static void Postfix() => ModChangeGuard.TryRun(ModChangeTriggerReason.Startup);
}

[HarmonyPatch(typeof(SaveManager), nameof(SaveManager.SaveProgressFile))]
internal static class ModChangeGuardSaveProgressPatch {
    static void Prefix() {
        if (!ModChangeGuard.CompletedForSession)
            ModChangeGuard.TryRun(ModChangeTriggerReason.SafetyNet);
    }
}
