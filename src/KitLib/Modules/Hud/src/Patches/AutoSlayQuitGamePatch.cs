using HarmonyLib;
using KitLib.UI;
using MegaCrit.Sts2.Core.AutoSlay;

namespace KitLib.Patches;

[HarmonyPatch(typeof(AutoSlayer), "QuitGame")]
internal static class AutoSlayQuitGamePatch {
    static bool Prefix() => !AutoSlayRunner.SuppressQuit;
}
