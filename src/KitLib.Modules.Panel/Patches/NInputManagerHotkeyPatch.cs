using Godot;
using HarmonyLib;
using KitLib.Hotkeys;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Patches;

/// <summary>
/// Hooks official shortcut dispatch before keycode-only matching
/// (<see cref="NInputManager.ProcessShortcutKeyInput"/>).
/// </summary>
[HarmonyPatch(typeof(NInputManager), "ProcessShortcutKeyInput")]
internal static class NInputManagerProcessShortcutKeyInputPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        if (inputEvent is not InputEventKey { Pressed: true, Echo: false })
            return true;

        var viewport = __instance.GetViewport();
        if (viewport == null)
            return true;

        return !KitLibHotkeyInput.TryHandleAll(inputEvent, viewport);
    }
}
