namespace KitLib.Host;

/// <summary>Cheat module entry points wired at KitLib.Cheat init; invoked from Panel DevPanel.</summary>
public static class KitLibCheatOps {
    public static Action? OpenCards { get; set; }
    public static Action? OpenRelics { get; set; }
    public static Action? OpenEnemies { get; set; }
    public static Action? OpenPowers { get; set; }
    public static Action? OpenPotions { get; set; }
    public static Action? OpenEvents { get; set; }
    public static Action? OpenRooms { get; set; }
    public static Action? OpenConsole { get; set; }
    public static Action? OpenPresets { get; set; }
    public static Action? OpenCardTest { get; set; }
    public static Action<double>? ProcessFrame { get; set; }
    public static Action? EnsureRuntimeStatModifiers { get; set; }
    public static Action? ClearRunState { get; set; }
    public static Action<bool>? SetMultiplayerCheatOptIn { get; set; }
    public static Func<bool>? CanUseMultiplayerCheats { get; set; }
    public static Action? ResetSkipAnim { get; set; }
    public static Func<bool>? IsSkipAnimSkipping { get; set; }
    public static Func<bool>? IsMpHooksDisabledInMultiplayer { get; set; }
}
