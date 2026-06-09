namespace KitLib.Logging;

/// <summary>Session boundary markers aligned with <c>KitLib.Abstractions.Logging.KitLogMarkers</c>.</summary>
public static class KitLogMarkers {
    public const string SessionBoundaryPrefix = "── KitLib log capture started ──";
    public const string LegacySessionBoundaryPrefix = "── DevMode log capture started ──";

    public static bool ContainsAnySessionBoundary(string text)
        => text.Contains(SessionBoundaryPrefix, StringComparison.Ordinal)
           || text.Contains(LegacySessionBoundaryPrefix, StringComparison.Ordinal);
}
