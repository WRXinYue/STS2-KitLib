using System.Diagnostics;
using KitLib.Settings;

namespace KitLib.ModPanel.Diagnostics;

/// <summary>Structured ModPanel timing logs when <see cref="KitLibSettings.ModPanelDiagnosticMode"/> is on.</summary>
internal static class ModPanelPerf {
    public const string Prefix = "[ModPanelPerf]";

    public static bool IsEnabled => SettingsStore.Current.ModPanelDiagnosticMode;

    public static Stopwatch Start() => Stopwatch.StartNew();

    public static void Log(string phase, Stopwatch sw, string? detail = null) {
        if (!IsEnabled)
            return;
        var msg = $"{Prefix} {phase} elapsedMs={sw.ElapsedMilliseconds}";
        if (!string.IsNullOrWhiteSpace(detail))
            msg += $" {detail}";
        MainFile.Logger.Info(msg);
    }
}
