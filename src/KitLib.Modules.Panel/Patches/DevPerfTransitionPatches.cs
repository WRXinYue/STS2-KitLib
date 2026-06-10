using System.Collections.Concurrent;
using System.Diagnostics;
using HarmonyLib;
using KitLib.DevPerf;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches;

internal static class DevPerfTransitionTiming {
    static readonly ConcurrentDictionary<string, Stopwatch> Active = new(StringComparer.Ordinal);

    internal static void Begin(string key) {
        var sw = Stopwatch.StartNew();
        Active[key] = sw;
    }

    internal static void End(string key, string logName) {
        if (!Active.TryRemove(key, out var sw))
            return;

        sw.Stop();
        DevPerfEventLog.LogTransition(logName, sw.ElapsedMilliseconds);
    }

    internal static void Cancel(string key) => Active.TryRemove(key, out _);
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.Launch))]
internal static class DevPerfLaunchPatch {
    const string Key = "Launch";

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.Begin(Key);
    }

    [HarmonyPostfix]
    static void Postfix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.End(Key, Key);
    }
}

[HarmonyPatch(typeof(NRun), "_Ready")]
internal static class DevPerfNRunReadyPatch {
    const string Key = "NRun._Ready";

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.Begin(Key);
    }

    [HarmonyPostfix]
    static void Postfix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.End(Key, Key);
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapPointInternal))]
internal static class DevPerfEnterMapPointPatch {
    const string Key = "EnterMapPoint";

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.Begin(Key);
    }

    [HarmonyPostfix]
    static void Postfix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.End(Key, Key);
    }
}

[HarmonyPatch(typeof(SaveSlotManager), nameof(SaveSlotManager.SaveSnapshotToFiles))]
internal static class DevPerfDevSnapshotSavePatch {
    const string Key = "SaveRun.dev";

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive) return;
        DevPerfTransitionTiming.Begin(Key);
    }

    [HarmonyPostfix]
    static void Postfix(bool __result) {
        if (!KitLibState.IsActive) return;
        if (!__result) {
            DevPerfTransitionTiming.Cancel(Key);
            return;
        }

        DevPerfTransitionTiming.End(Key, Key);
    }

    [HarmonyFinalizer]
    static Exception? Finalizer(Exception __exception) {
        if (!KitLibState.IsActive) return __exception;
        DevPerfTransitionTiming.Cancel(Key);
        return __exception;
    }
}
