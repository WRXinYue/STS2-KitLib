using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Settings;

namespace KitLib;

/// <summary>
/// Toggles the game's built-in <see cref="FastModeType.Instant"/> mode to skip card animations.
/// The game normally blocks Instant mode on non-editor builds — a Harmony patch in
/// <see cref="Patches.InstantModeUnlockPatch"/> removes that restriction.
/// </summary>
internal static class SkipAnimControl {
    private static FastModeType _savedMode = FastModeType.Normal;
    private static bool _skipping;

    public static bool IsSkipping => _skipping;

    /// <summary>Toggle skip on/off.</summary>
    public static void Toggle() {
        if (_skipping)
            Disable();
        else
            Enable();
    }

    /// <summary>Get the display label for the current state.</summary>
    public static string GetLabel() => _skipping ? I18N.T("skipanim.on", "On") : I18N.T("skipanim.off", "Off");

    /// <summary>Restore original fast mode (called on run end / detach).</summary>
    public static void Reset() {
        if (_skipping)
            Disable();
    }

    private static void Enable() {
        _savedMode = SaveManager.Instance.PrefsSave.FastMode;
        SaveManager.Instance.PrefsSave.FastMode = FastModeType.Instant;
        _skipping = true;
        MainFile.Logger.Info("SkipAnimControl: Card animation skip enabled (Instant mode)");
    }

    private static void Disable() {
        SaveManager.Instance.PrefsSave.FastMode = _savedMode;
        _skipping = false;
        MainFile.Logger.Info($"SkipAnimControl: Card animation skip disabled (restored to {_savedMode})");
    }
}
