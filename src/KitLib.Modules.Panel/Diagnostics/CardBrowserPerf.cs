using System.Diagnostics;
using KitLib.DevPerf;

namespace KitLib.UI.Diagnostics;

/// <summary>Structured card browser timing logs; enabled by default for hitch diagnosis.</summary>
internal static class CardBrowserPerf {
    public const string Prefix = "[CardBrowserPerf]";

    public static bool IsEnabled => true;

    public static Stopwatch Start() => Stopwatch.StartNew();

    public static void Log(string phase, Stopwatch sw, string? detail = null) {
        if (!IsEnabled)
            return;

        var msg = $"{Prefix} {phase} elapsedMs={sw.ElapsedMilliseconds}";
        if (!string.IsNullOrWhiteSpace(detail))
            msg += $" {detail}";
        MainFile.Logger.Info(msg);
        DevPerfEventLog.LogDetail(msg);
    }
}
