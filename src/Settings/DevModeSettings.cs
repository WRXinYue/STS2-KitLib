using System;
using System.Collections.Generic;
using DevMode.Hooks;

namespace DevMode.Settings;

/// <summary>
/// Persistent user preferences for DevMode appearance. Serialized to settings.json.
/// </summary>
public sealed class DevModeSettings {
    public bool DarkMode { get; set; } = true;
    public string DarkThemeName { get; set; } = ThemeNames.Dark;
    public string LightThemeName { get; set; } = ThemeNames.Light;

    /// <summary>Key = browser overlay <c>rootName</c> (e.g. <c>DevModeConsole</c>); value = last panel width in px.</summary>
    public Dictionary<string, int> BrowserPanelWidths { get; set; } = new(StringComparer.Ordinal);

    /// <summary>User-defined hook rules (trigger + conditions + actions).</summary>
    public List<HookEntry> Hooks { get; set; } = [];

    /// <summary>Per-rail-section tab order. Keys: <see cref="RailTabPreferences.PrimaryKey"/> / <see cref="RailTabPreferences.UtilityKey"/>.</summary>
    public Dictionary<string, List<string>> RailTabOrder { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Tab ids hidden from the in-game rail (settings tab cannot be hidden).</summary>
    public HashSet<string> RailHiddenTabIds { get; set; } =
        new(RailTabPreferences.DefaultHiddenTabIds, StringComparer.Ordinal);

    /// <summary>Whether <see cref="RailTabPreferences.DefaultHiddenTabIds"/> have been merged into saved settings.</summary>
    public int RailLayoutDefaultsVersion { get; set; } = 1;
}

public static class ThemeNames {
    public const string Dark = "Dark";
    public const string Oled = "OLED";
    public const string Light = "Light";
    public const string Warm = "Warm";
}
