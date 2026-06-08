using HarmonyLib;

namespace KitLib;

/// <summary>Applies Harmony patches from the shared KitLib.Features assembly once per process.</summary>
public static class KitLibFeaturesPatches {
    static bool _applied;

    public static void EnsureApplied() {
        if (_applied) return;
        _applied = true;
        var harmony = new Harmony("KitLib.Features");
        harmony.PatchAll(typeof(KitLibFeaturesPatches).Assembly);
        MainFile.Logger.Info("KitLib.Features patches applied.");
    }
}
