using KitLib.Host;

namespace KitLib;

/// <summary>Opens the embedded dev viewer in the system browser (replaces deprecated <c>kitlog</c>).</summary>
public static class DevViewerLauncher {
    public static bool TryOpenLogs(out string? error, string? query = null) {
        error = null;
        if (KitLibDevOps.TryOpenDevViewerLogs?.Invoke(query) == true)
            return true;

        error = I18N.T(
            "devViewer.launchFailed",
            "Could not open the dev viewer. Load KitLib.Dev and try again.");
        return false;
    }

    /// <summary>
    /// Startup path: prefer reusing a still-open browser tab that reconnects to the local viewer.
    /// </summary>
    public static bool TryOpenLogsOnStartup(out string? error, string? query = null) {
        error = null;
        if (KitLibDevOps.TryScheduleDevViewerLogsOnStartup?.Invoke(query) == true)
            return true;
        return TryOpenLogs(out error, query);
    }

    public static bool TryOpenAiLogs(out string? error)
        => TryOpenLogs(out error, "preset=ai");
}
