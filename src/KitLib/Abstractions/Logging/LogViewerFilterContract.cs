namespace KitLib.Abstractions.Logging;

/// <summary>JSON contract shared by the in-game log viewer and <c>kitlog --sync-viewer</c>.</summary>
public static class LogViewerFilterContract {
    public const int Version = 1;
    public const string FileName = "log-viewer-filter.json";
}
