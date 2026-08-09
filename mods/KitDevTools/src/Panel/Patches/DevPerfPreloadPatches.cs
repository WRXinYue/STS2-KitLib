using System.Diagnostics;
using HarmonyLib;
using KitLib.DevPerf;
using MegaCrit.Sts2.Core.Assets;

namespace KitLib.Patches;

[HarmonyPatch(typeof(AssetLoadingSession), "FinalizeLoading")]
internal static class DevPerfPreloadFinalizePatch {
    [HarmonyPostfix]
    static void Postfix(AssetLoadingSession __instance) {
        if (!KitLibState.IsActive || !__instance.IsCompleted)
            return;

        var tr = Traverse.Create(__instance);
        var name = tr.Field("_name").GetValue<string>();
        var stopwatch = tr.Field("_stopwatch").GetValue<Stopwatch>();
        var totalLoaded = tr.Field("_totalLoaded").GetValue<int>();
        if (stopwatch == null || string.IsNullOrWhiteSpace(name))
            return;

        var label = $"Preload.{SanitizeLabel(name)}";
        var elapsedMs = stopwatch.ElapsedMilliseconds;
        DevPerfEventLog.LogTransition(label, elapsedMs, totalLoaded);
    }

    static string SanitizeLabel(string name) {
        var trimmed = name.Trim();
        if (trimmed.Length == 0)
            return "unknown";
        return trimmed.Replace(" ", "_");
    }
}
