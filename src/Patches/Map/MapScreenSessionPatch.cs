using KitLib.AI.AutoPlay;
using KitLib.AI.Planning;
using KitLib.Map;
using KitLib.Settings;
using HarmonyLib;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches.Map;

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Open))]
public static class MapScreenOpenPatch {
    public static void Postfix(NMapScreen __instance) {
        if (!KitLibState.IsActive) return;
        MapScreenUnlock.OnOpened(__instance);
        MapAiPathOverlayHelper.TryApplyAiPathOverlay(__instance);
    }
}

[HarmonyPatch(typeof(NMapScreen), nameof(NMapScreen.Close))]
public static class MapScreenClosePatch {
    public static void Postfix(NMapScreen __instance) {
        if (!KitLibState.IsActive) return;
        MapScreenUnlock.OnClosed(__instance);
        MapPathOverlay.Clear(__instance);
        MapPathPlanner.ClearCache();
    }
}

static class MapAiPathOverlayHelper {
    internal static void TryApplyAiPathOverlay(NMapScreen screen) {
        if (!AiPlayModule.Instance.IsRunning || !SettingsStore.Current.AutoPlayEnabled)
            return;

        var rm = RunManager.Instance;
        var state = rm?.DebugOnlyGetState();
        var player = LocalContext.GetMe(state);
        if (state == null || player == null) return;

        var plan = MapPathPlanner.Plan(state, player, forceRefresh: true);
        if (plan == null || plan.Edges.Count == 0) return;

        MapPathOverlay.Apply(screen, plan.Edges);
    }
}
