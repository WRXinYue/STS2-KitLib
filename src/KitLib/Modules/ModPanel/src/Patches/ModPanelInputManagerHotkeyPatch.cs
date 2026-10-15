using Godot;
using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Hotkeys;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Patches;

/// <summary>
/// Dispatches mod-panel open hotkey when DevMode (<see cref="KitLibModuleIds.Panel" />) is not loaded.
/// </summary>
#if STS2_STABLE_PROFILE
[HarmonyPatch(typeof(NInputManager), "ProcessShortcutKeyInput")]
internal static class ModPanelInputManagerHotkeyPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        return !ModPanelInputManagerHotkeyPatchHelper.TryHandleModPanelHotkey(__instance, inputEvent);
    }
}
#else
[HarmonyPatch(typeof(NInputManager), "ProcessHotkeyInput")]
internal static class ModPanelInputManagerProcessHotkeyInputPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        return !ModPanelInputManagerHotkeyPatchHelper.TryHandleModPanelHotkey(__instance, inputEvent);
    }
}

[HarmonyPatch(typeof(NInputManager), "ProcessFkbInput")]
internal static class ModPanelInputManagerProcessFkbInputPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        return !ModPanelInputManagerHotkeyPatchHelper.TryHandleModPanelHotkey(__instance, inputEvent);
    }
}
#endif

file static class ModPanelInputManagerHotkeyPatchHelper {
    internal static bool TryHandleModPanelHotkey(NInputManager instance, InputEvent inputEvent) {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Panel))
            return false;

        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
            return false;

        var viewport = instance.GetViewport();
        if (viewport == null)
            return false;

        return ModPanelHotkeys.TryHandle(key, viewport);
    }
}
