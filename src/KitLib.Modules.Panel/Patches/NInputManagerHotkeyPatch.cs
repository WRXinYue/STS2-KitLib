using Godot;
using HarmonyLib;
using KitLib.Hotkeys;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Patches;

/// <summary>
/// Hooks official shortcut dispatch before keycode-only matching
/// (<see cref="NInputManager.ProcessHotkeyInput"/> / <c>ProcessFkbInput</c>).
/// </summary>
[HarmonyPatch(typeof(NInputManager), "ProcessHotkeyInput")]
internal static class NInputManagerProcessHotkeyInputPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        return !NInputManagerHotkeyPatchHelper.TryHandleKitLibHotkey(__instance, inputEvent);
    }
}

[HarmonyPatch(typeof(NInputManager), "ProcessFkbInput")]
internal static class NInputManagerProcessFkbInputPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        return !NInputManagerHotkeyPatchHelper.TryHandleKitLibHotkey(__instance, inputEvent);
    }
}

file static class NInputManagerHotkeyPatchHelper {
    internal static bool TryHandleKitLibHotkey(NInputManager instance, InputEvent inputEvent) {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false })
            return false;

        var viewport = instance.GetViewport();
        if (viewport == null)
            return false;

        return KitLibHotkeyInput.TryHandleAll(inputEvent, viewport);
    }
}
