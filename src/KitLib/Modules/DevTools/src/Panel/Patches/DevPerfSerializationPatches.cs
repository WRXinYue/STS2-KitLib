using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using HarmonyLib;
using KitLib.DevPerf;
using MegaCrit.Sts2.Core.Multiplayer.Serialization;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace KitLib.Patches;

[HarmonyPatch(typeof(PacketWriter), "ResizeBufferIfNecessary")]
internal static class DevPerfPacketWriterResizePatch {
    [ThreadStatic] static int _prevCapacity;

    [HarmonyPrefix]
    static void Prefix(PacketWriter __instance) {
        if (!KitLibState.IsActive)
            return;

        _prevCapacity = __instance.Buffer?.Length ?? 0;
    }

    [HarmonyPostfix]
    static void Postfix(PacketWriter __instance) {
        if (!KitLibState.IsActive)
            return;

        var nextCapacity = __instance.Buffer?.Length ?? 0;
        if (nextCapacity <= _prevCapacity)
            return;

        DevPerfEventLog.LogDetail(
            $"PacketWriter grow {_prevCapacity}->{nextCapacity} pos={__instance.BytePosition}");
    }
}

[HarmonyPatch(typeof(RunManager), nameof(RunManager.ToSave))]
internal static class DevPerfRunToSavePatch {
    const string Key = "Save.ToSerializable";

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive)
            return;
        DevPerfTransitionTiming.Begin(Key);
    }

    [HarmonyPostfix]
    static void Postfix() {
        if (!KitLibState.IsActive)
            return;
        DevPerfTransitionTiming.End(Key, Key);
    }

    [HarmonyFinalizer]
    static Exception? Finalizer(Exception __exception) {
        if (!KitLibState.IsActive)
            return __exception;
        DevPerfTransitionTiming.Cancel(Key);
        return __exception;
    }
}

[HarmonyPatch(typeof(RunSaveManager), nameof(RunSaveManager.SaveRun))]
internal static class DevPerfGameSaveRunPatch {
    const string Key = "SaveRun";
    [ThreadStatic] static Stopwatch? _sw;

    [HarmonyPrefix]
    static void Prefix() {
        if (!KitLibState.IsActive)
            return;
        _sw = Stopwatch.StartNew();
    }

    [HarmonyPostfix]
    static void Postfix(Task __result) {
        if (!KitLibState.IsActive || _sw == null)
            return;

        var sw = _sw;
        _sw = null;
        __result.ContinueWith(
            _ => DevPerfEventLog.LogTransition(Key, sw.ElapsedMilliseconds),
            CancellationToken.None,
            TaskContinuationOptions.None,
            TaskScheduler.Default);
    }

    [HarmonyFinalizer]
    static Exception? Finalizer(Exception __exception) {
        if (!KitLibState.IsActive)
            return __exception;
        _sw = null;
        return __exception;
    }
}
