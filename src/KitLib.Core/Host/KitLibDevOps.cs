namespace KitLib.Host;

public static class KitLibDevOps {
    public static Action? OpenHooks { get; set; }
    public static Action? OpenEnemyIntent { get; set; }
    public static Action? OpenLogExport { get; set; }

    /// <summary>Open the local dev viewer log tab (<c>http://127.0.0.1:9878/#/logs</c>).</summary>
    public static Func<string?, bool>? TryOpenDevViewerLogs { get; set; }
}
