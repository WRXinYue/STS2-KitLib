namespace KitLib.Replay;

/// <summary>
/// Spectator lock during replay: view (hover / pile inspect) stays allowed;
/// playing cards, traveling the map, and cheat edits do not.
/// </summary>
internal static class ReplayInputGuard {
    static int _automated;

    public static bool IsLocked => CombatReplayPlayback.IsActive;

    public static bool BlocksCheats => IsLocked;

    public static bool IsAutomated => _automated > 0;

    public static void BeginAutomated() => _automated++;

    public static void EndAutomated() {
        if (_automated > 0)
            _automated--;
    }
}
