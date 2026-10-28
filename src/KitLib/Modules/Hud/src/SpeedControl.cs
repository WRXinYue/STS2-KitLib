using Godot;

namespace KitLib;

/// <summary>
/// Controls game animation speed via <see cref="Engine.TimeScale"/>.
/// Cycles through preset multipliers: 1× → 2× → 3× → 5× → 1×.
/// Restores 1× on run end.
/// </summary>
internal static class SpeedControl {
    private static readonly float[] Speeds = { 1f, 2f, 3f, 5f };

    private static readonly string[] Labels = { "1×", "2×", "3×", "5×" };

    private static int _index;

    /// <summary>Cycle to the next speed preset and apply it.</summary>
    public static void CycleSpeed() {
        _index = (_index + 1) % Speeds.Length;
        Apply();
        MainFile.Logger.Info($"SpeedControl: Game speed set to {Labels[_index]}");
    }

    /// <summary>Get the display label for the current speed.</summary>
    public static string GetLabel() => Labels[_index];

    /// <summary>Reset to 1× (called on run end / detach).</summary>
    public static void Reset() {
        _index = 0;
        Apply();
    }

    private static void Apply() {
        Engine.TimeScale = Speeds[_index];
        KitLibState.GameplayModifiers.GameSpeed = Speeds[_index];
    }
}
