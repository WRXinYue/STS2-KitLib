namespace KitLib.Logging;

/// <summary>Named-pipe log stream shared by the game process and <c>kitlog attach</c>.</summary>
public static class LogStreamContract {
    public const int Version = 1;
    public const int MaxFrameBytes = 65536;
    public const int MaxHistoryEntries = 2000;

    /// <summary>Cross-platform pipe name passed to <see cref="System.IO.Pipes.NamedPipeClientStream"/>.</summary>
    public static string PipeName(int processId) => $"KitLib-log-{processId}";
}
