using KitLib.Presets;

namespace KitLib.Cheat;

/// <summary>Preset restart payload consumed by <see cref="Patches.RunStartPatch"/>.</summary>
public static class CheatRestartState {
    public static LoadoutPreset? PendingRestartPreset { get; set; }

    public static PresetContents PendingRestartScope { get; set; }

    public static void ClearPresetRestart() {
        PendingRestartPreset = null;
        PendingRestartScope = PresetContents.None;
    }
}
