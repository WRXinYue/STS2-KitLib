using System;
using KitLib.Host;

namespace KitLib;

/// <summary>Deprecated shim; opens the browser dev viewer instead of <c>kitlog</c>.</summary>
[Obsolete("Use DevViewerLauncher — logs are shown in the browser dev viewer at http://127.0.0.1:9878/#/logs")]
public static class KitLogTerminalLauncher {
    public static bool TryOpenSessionTail(out string? error)
        => DevViewerLauncher.TryOpenLogs(out error);

    public static bool TryOpenAiTail(out string? error)
        => DevViewerLauncher.TryOpenAiLogs(out error);
}
