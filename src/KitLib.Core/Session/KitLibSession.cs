namespace KitLib;

/// <summary>Public session paths for KitLib log instances.</summary>
public static class KitLibSession {
    public static int ProcessId => KitLibInstance.ProcessId;

    public static string InstancesRoot => Path.Combine(DataPaths.BaseDir, "instances");

    /// <summary>Absolute path to <c>instances/{pid}/session.log</c> when <paramref name="pid"/> is set.</summary>
    public static string? TryGetSessionLogPath(int? pid) {
        if (pid is not int id || id <= 0)
            return null;

        var path = Path.Combine(InstancesRoot, id.ToString(), "session.log");
        return path;
    }

    /// <summary>Absolute path to the shared in-game / kitlog filter profile for this process.</summary>
    public static string GetFilterProfilePath(int? pid = null)
        => Path.Combine(InstancesRoot, (pid ?? ProcessId).ToString(), "log-viewer-filter.json");
}
