using KitLib.Abstractions.Host;
namespace KitLib.Abstractions.Host;

/// <summary>
/// Cheat runtime hooks. Panel UI is owned by KitDevTools; this surface is for patches and APIs.
/// </summary>
public static class KitLibCheatApi {
    public static Action<double>? ProcessFrame { get; set; }
    public static Action? EnsureRuntimeStatModifiers { get; set; }
    public static Action? ClearRunState { get; set; }
    public static Action<bool>? SetMultiplayerCheatOptIn { get; set; }
    public static Func<bool>? CanUseMultiplayerCheats { get; set; }
    public static Action? ResetSkipAnim { get; set; }
    public static Func<bool>? IsSkipAnimSkipping { get; set; }
    public static Func<bool>? IsMpHooksDisabledInMultiplayer { get; set; }
}
