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
[HarmonyPatch(typeof(NInputManager), "ProcessShortcutKeyInput")]
internal static class ModPanelInputManagerHotkeyPatch {
    [HarmonyPrefix]
    static bool Prefix(NInputManager __instance, InputEvent inputEvent) {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.Panel))
            return true;

        if (inputEvent is not InputEventKey { Pressed: true, Echo: false } key)
            return true;

        var viewport = __instance.GetViewport();
        if (viewport == null)
            return true;

        return !ModPanelHotkeys.TryHandle(key, viewport);
    }
}
