using MegaCrit.Sts2.Core.Nodes.Screens.Map;

namespace KitLib.Map;

/// <summary>
/// Map debug-jump cheat: full unlock on <see cref="NMapScreen"/> while
/// <see cref="KitLibState.MapCheats.MapDebugJumpEnabled"/> is on.
/// </summary>
internal static class MapScreenUnlock {
    public static bool IsActive =>
        KitLibState.CheatsInRun && KitLibState.MapCheats.MapDebugJumpEnabled;

    /// <summary>Enable jump cheat when entering the map room from the Dev panel room teleport.</summary>
    public static void EnableFromDevPanel() {
        if (!KitLibState.CheatsInRun) return;
        KitLibState.MapCheats.MapDebugJumpEnabled = true;
    }

    public static void OnOpened(NMapScreen screen) {
        if (IsActive)
            ApplyUnlock(screen);
        else
            ClearVisuals(screen);
    }

    public static void OnClosed(NMapScreen screen) {
        ClearVisuals(screen);
    }

    public static void ApplyUnlock(NMapScreen? screen = null) {
        if (!IsActive) return;
        screen ??= NMapScreen.Instance;
        if (screen == null) return;
        MapScreenReflection.RecalculateTravelability(screen);
    }

    public static void ClearVisuals(NMapScreen screen) {
        screen.SetDebugTravelEnabled(false);
        screen.RefreshAllPointVisuals();
    }
}
