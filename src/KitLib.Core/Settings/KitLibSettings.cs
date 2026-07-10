using System;
using System.Collections.Generic;
using KitLib.Hooks;

namespace KitLib.Settings;

/// <summary>
/// Persistent user preferences for DevMode appearance. Serialized to settings.json.
/// </summary>
public sealed partial class KitLibSettings {
    public bool DarkMode { get; set; } = true;
    public string DarkThemeName { get; set; } = ThemeNames.Dark;
    public string LightThemeName { get; set; } = ThemeNames.Light;

    /// <summary>User accent color for DevMode and ModPanel UI (#RRGGBB).</summary>
    public string AccentColorHex { get; set; } = "#EA9104";

    /// <summary>User-defined hook rules (trigger + conditions + actions).</summary>
    public List<HookEntry> Hooks { get; set; } = [];

    /// <summary>Per-rail-section tab order. Keys: <see cref="RailTabPreferences.PrimaryKey"/> / <see cref="RailTabPreferences.UtilityKey"/>.</summary>
    public Dictionary<string, List<string>> RailTabOrder { get; set; } = new(StringComparer.Ordinal);

    /// <summary>Tab ids hidden from the in-game rail (settings tab cannot be hidden).</summary>
    public HashSet<string> RailHiddenTabIds { get; set; } =
        new(DefaultHiddenRailTabIds, StringComparer.Ordinal);

    /// <summary>
    /// Dev overlay level for normal (non-test) runs: Disabled, DevPanel, or Cheat.
    /// </summary>
    public string NormalRunMode { get; set; } = "DevPanel";

    /// <summary>Opt in to synchronized multiplayer cheat sessions (requires DevMode on all peers).</summary>
    public bool MultiplayerCheatOptIn { get; set; }

    /// <summary>Solo AutoPlay strategy: <c>Strong</c> (default) or <c>Simple</c>.</summary>
    public string AutoPlayStrategy { get; set; } = "Strong";

    /// <summary>AI托管操作间隔（毫秒）。</summary>
    public int AutoPlayDelayMs { get; set; } = 800;

    /// <summary>Log combat score breakdown (top alternatives) to AutoPlay terminal.</summary>
    public bool AiCombatVerboseLog { get; set; } = true;

    /// <summary>Show in-game AI hosting HUD overlay during solo AutoPlay.</summary>
    public bool AiHudEnabled { get; set; } = true;

    /// <summary>Weight for Spire Codex community priors (0 = off, 1 = default).</summary>
    public float CodexPriorWeight { get; set; } = 1f;

    /// <summary>Auto ready-to-end-turn for non-local players when SyncBot is on.</summary>
    public bool SyncBotAutoEndTurn { get; set; } = true;

    /// <summary>Include cards hidden from the official library (<c>ShouldShowInCardLibrary</c> false) in DevMode browsers.</summary>
    public bool ShowHiddenCards { get; set; }

    /// <summary>Whether the draggable top-right multiplayer combat score panel is shown.</summary>
    public bool CombatStatsMpOverlayEnabled { get; set; } = true;

    /// <summary>Whether the draggable enemy intent prediction panel is shown during combat.</summary>
    public bool CombatStatsMonsterIntentOverlayEnabled { get; set; }

    /// <summary>Saved free position for the multiplayer score overlay (null = default top-right).</summary>
    public float? CombatStatsMpOverlayPosX { get; set; }

    public float? CombatStatsMpOverlayPosY { get; set; }

    /// <summary>Saved free position for the enemy intent overlay (null = default top-left).</summary>
    public float? CombatStatsMonsterIntentOverlayPosX { get; set; }

    public float? CombatStatsMonsterIntentOverlayPosY { get; set; }

    /// <summary>
    /// <see langword="false"/> = blink peek tab until first rail hover;
    /// <see langword="null"/> / <see langword="true"/> = intro dismissed.
    /// </summary>
    public bool? RailIntroDismissed { get; set; }

    /// <summary>Back up active profile progress when the loaded mod set fingerprint changes.</summary>
    public bool AutoBackupProgressOnModChange { get; set; } = true;

    /// <summary>Warn when progress.save still contains data from mods removed since last launch.</summary>
    public bool WarnOnRemovedModProgressResidue { get; set; } = true;

    /// <summary>Prompt to restore from backup when mod character progress is missing after load.</summary>
    public bool PromptOnModCharacterProgressLoss { get; set; } = true;

    /// <summary>Whether the mod panel sidebar scope blurb is expanded.</summary>
    public bool ModPanelScopeStripExpanded { get; set; } = true;

    /// <summary>Show fixed top-right Dev performance overlay (toggle via hotkey).</summary>
    public bool PerfHudEnabled { get; set; }

    /// <summary>Append structured perf events to instances/{pid}/perf-trace.log.</summary>
    public bool PerfHudTraceToFile { get; set; }

    /// <summary>Log ModPanel open/refresh timings and verbose sidebar diagnostics.</summary>
    public bool ModPanelDiagnosticMode { get; set; }

    /// <summary>Launch optional kitlog CLI in a system terminal when KitLib.User starts (session tail, --sync-viewer).</summary>
    public bool LaunchKitlogOnStartup { get; set; }

    /// <summary>Minimal | Standard | Full | Custom — see <see cref="SatelliteModulesEnabled"/>.</summary>
    public string SatelliteLoadProfile { get; set; } = SatelliteModuleLoadProfileNames.Standard;

    /// <summary>Per-module enable flags for toggleable satellites (User/ModPanel are always on).</summary>
    public Dictionary<string, bool> SatelliteModulesEnabled { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Enable in-game keyboard shortcuts for the DevMode sidebar shell.</summary>
    public bool HotkeysEnabled { get; set; } = true;

    public static readonly string[] DefaultHiddenRailTabIds = {
        "devmode.scripts",
    };
}

public static class ThemeNames {
    public const string Dark = "Dark";
    public const string Oled = "OLED";
    public const string Light = "Light";
    public const string Warm = "Warm";
}
