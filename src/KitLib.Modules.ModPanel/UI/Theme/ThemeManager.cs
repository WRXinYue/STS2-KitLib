using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Settings;

namespace KitLib.UI;

/// <summary>
/// Central manager for the active visual theme.
/// Call <see cref="SetDarkMode"/>, <see cref="SetDarkTheme"/>, or <see cref="SetLightTheme"/>
/// to change the theme; all UI components subscribed to <see cref="OnThemeChanged"/> will update.
/// </summary>
internal static class ThemeManager {
    // ── Available theme names per mode ──────────────────────────────────
    public static IReadOnlyList<string> DarkThemes { get; } = new[] { ThemeNames.Dark, ThemeNames.Oled };
    public static IReadOnlyList<string> LightThemes { get; } = new[] { ThemeNames.Light, ThemeNames.Warm };

    /// <summary>Fired whenever the active theme changes.</summary>
    public static event Action? OnThemeChanged;

    /// <summary>The currently active <see cref="ThemeDefinition"/>, derived from persisted settings.</summary>
    public static ThemeDefinition Current {
        get {
            var s = SettingsStore.Current;
            var name = s.DarkMode ? s.DarkThemeName : s.LightThemeName;
            return ThemeDefinition.FromName(name);
        }
    }

    public static bool IsDarkMode => SettingsStore.Current.DarkMode;

    // ── Mutation helpers ─────────────────────────────────────────────────

    /// <summary>Switches between dark and light mode.</summary>
    public static void SetDarkMode(bool dark) {
        SettingsStore.Current.DarkMode = dark;
        SettingsStore.Save();
        OnThemeChanged?.Invoke();
    }

    /// <summary>Sets the active dark theme by name (must be in <see cref="DarkThemes"/>).</summary>
    public static void SetDarkTheme(string name) {
        SettingsStore.Current.DarkThemeName = name;
        SettingsStore.Save();
        if (SettingsStore.Current.DarkMode)
            OnThemeChanged?.Invoke();
    }

    /// <summary>Sets the active light theme by name (must be in <see cref="LightThemes"/>).</summary>
    public static void SetLightTheme(string name) {
        SettingsStore.Current.LightThemeName = name;
        SettingsStore.Save();
        if (!SettingsStore.Current.DarkMode)
            OnThemeChanged?.Invoke();
    }

    /// <summary>Cycles to the next available dark theme.</summary>
    public static string CycleDarkTheme() {
        var themes = DarkThemes;
        var current = SettingsStore.Current.DarkThemeName;
        int idx = themes.ToList().IndexOf(current);
        var next = themes[(idx + 1) % themes.Count];
        SetDarkTheme(next);
        return next;
    }

    /// <summary>Cycles to the next available light theme.</summary>
    public static string CycleLightTheme() {
        var themes = LightThemes;
        var current = SettingsStore.Current.LightThemeName;
        int idx = themes.ToList().IndexOf(current);
        var next = themes[(idx + 1) % themes.Count];
        SetLightTheme(next);
        return next;
    }
}
