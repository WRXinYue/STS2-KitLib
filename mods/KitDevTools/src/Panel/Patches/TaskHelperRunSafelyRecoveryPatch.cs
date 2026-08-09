using System;
using System.Threading.Tasks;
using Godot;
using HarmonyLib;
using KitLib;
using MegaCrit.Sts2.Core.Helpers;

namespace KitLib.Patches;

/// <summary>Observe fire-and-forget tasks; Finalizer on async LogTaskExceptions does not always run.</summary>
[HarmonyPatch(typeof(TaskHelper), nameof(TaskHelper.RunSafely))]
internal static class TaskHelperRunSafelyRecoveryPatch {
    [HarmonyPostfix]
    static void Postfix(Task __result) {
        if (!KitLibState.IsActive || __result == null)
            return;

        _ = __result.ContinueWith(
            t => {
                if (!t.IsFaulted)
                    return;
                var ex = t.Exception?.GetBaseException();
                if (ex is null or OperationCanceledException)
                    return;
                Callable.From(DevPanelInputRecovery.Recover).CallDeferred();
            },
            TaskContinuationOptions.OnlyOnFaulted);
    }
}
