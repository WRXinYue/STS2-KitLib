namespace KitLib.Logging;

/// <summary>Shared session-boundary markers for log files (Core, User, KitLog CLI).</summary>
public static class KitLogMarkers {
    public const string SessionBoundaryPrefix = "── KitLib log capture started ──";
    public const string LegacySessionBoundaryPrefix = "── DevMode log capture started ──";

    /// <summary>True when <paramref name="text"/> contains a KitLib session boundary for any process.</summary>
    public static bool ContainsAnySessionBoundary(string text)
        => text.Contains(SessionBoundaryPrefix, StringComparison.Ordinal)
           || text.Contains(LegacySessionBoundaryPrefix, StringComparison.Ordinal);
}
