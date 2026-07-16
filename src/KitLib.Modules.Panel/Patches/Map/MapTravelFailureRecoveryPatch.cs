using System;
using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Patches;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches.Map;

[HarmonyPatch(typeof(RunManager), nameof(RunManager.EnterMapPointInternal))]
internal static class MapTravelFailureRecoveryPatch {
    [HarmonyFinalizer]
    static Exception? Finalizer(Exception? __exception) {
        if (__exception == null || !KitLibState.IsActive)
            return __exception;

        Callable.From(DevPanelInputRecovery.Recover).CallDeferred();
        return __exception;
    }
}
