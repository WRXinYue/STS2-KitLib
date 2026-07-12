using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Godot;
using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.Screens;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Patches;

[HarmonyPatch]
public static class ModPanelSubmenuStackPatch {
    internal static readonly ConditionalWeakTable<NSubmenuStack, ModPanelSubmenu> Submenus = new();

    static System.Collections.Generic.IEnumerable<MethodBase> TargetMethods() {
        yield return AccessTools.Method(
            typeof(NMainMenuSubmenuStack),
            nameof(NMainMenuSubmenuStack.GetSubmenuType),
            [typeof(Type)]);
        yield return AccessTools.Method(
            typeof(NRunSubmenuStack),
            nameof(NRunSubmenuStack.GetSubmenuType),
            [typeof(Type)]);
    }

    [HarmonyPrefix]
    public static bool Prefix(NSubmenuStack __instance, Type type, ref NSubmenu __result) {
        if (type != typeof(ModPanelSubmenu))
            return true;

        __result = Submenus.GetValue(__instance, CreateSubmenu);
        return false;
    }

    internal static ModPanelSubmenu CreateSubmenu(NSubmenuStack stack) {
        var submenu = new ModPanelSubmenu {
            Visible = false,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            FocusMode = Control.FocusModeEnum.None,
        };
        stack.AddChildSafely(submenu);
        return submenu;
    }
}
