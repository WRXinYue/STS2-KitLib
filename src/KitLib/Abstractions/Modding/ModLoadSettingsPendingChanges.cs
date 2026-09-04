namespace KitLib.Abstractions.Modding;

/// <summary>Pure pending-restart check aligned with <c>NModdingScreen.OnModEnabledOrDisabled</c>.</summary>
public static class ModLoadSettingsPendingChanges {
    public static bool EntryHasPendingRestart(ModEntryLoadStatus runtimeStatus, bool settingsEnabled) {
        if (runtimeStatus == ModEntryLoadStatus.Disabled && settingsEnabled)
            return true;
        if (runtimeStatus == ModEntryLoadStatus.Loaded && !settingsEnabled)
            return true;
        return false;
    }

    public static bool AnyPendingRestart(IEnumerable<(ModEntryLoadStatus RuntimeStatus, bool SettingsEnabled)> mods) {
        foreach (var (runtime, enabled) in mods) {
            if (EntryHasPendingRestart(runtime, enabled))
                return true;
        }
        return false;
    }
}
