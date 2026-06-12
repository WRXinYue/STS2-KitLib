using Godot;
using HarmonyLib;
using KitLib.Feedback;

namespace KitLib.Patches;

/// <summary>STS2 normal quit goes through <see cref="SceneTree.Quit"/>; hook it before tree teardown.</summary>
[HarmonyPatch(typeof(SceneTree), nameof(SceneTree.Quit))]
internal static class CrashRecoveryQuitPatch {
    static void Prefix() => CrashRecoveryStore.MarkSessionCleanExit();
}
