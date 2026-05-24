using System;
using System.IO;

namespace DevMode;

/// <summary>
/// Identifies this game process for dual-instance (same machine) testing.
/// </summary>
internal static class DevModeInstance {
    public const string SessionBoundaryPrefix = "── DevMode log capture started ──";

    public static int ProcessId { get; } = Environment.ProcessId;

    public static string SessionBoundaryMarker { get; } =
        $"{SessionBoundaryPrefix} [pid={ProcessId}]";

    /// <summary>Per-process overlay positions while multiple instances are active.</summary>
    internal static class SessionOverlay {
        public static float? MpOverlayPosX { get; set; }
        public static float? MpOverlayPosY { get; set; }
        public static float? MonsterIntentOverlayPosX { get; set; }
        public static float? MonsterIntentOverlayPosY { get; set; }
    }

    internal static string LogViewerSubtitle {
        get {
            var file = GameLogFileHydrator.CurrentSessionLogFileName;
            return file != null
                ? I18N.T("log.instance.subtitle", "This window · PID {0} · {1}", ProcessId, file)
                : I18N.T("log.instance.subtitleLive", "This window · PID {0} · live capture", ProcessId);
        }
    }

    internal static bool ContainsSessionBoundary(string text)
        => text.Contains(SessionBoundaryPrefix, StringComparison.Ordinal)
           && text.Contains($"[pid={ProcessId}]", StringComparison.Ordinal);
}
