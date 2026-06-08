using System;
using System.IO;
using KitLib.Host;

namespace KitLib;

/// <summary>
/// Identifies this game process for dual-instance (same machine) testing.
/// </summary>
public static class KitLibInstance {
    public const string SessionBoundaryPrefix = "── KitLib log capture started ──";
    private const string LegacySessionBoundaryPrefix = "── DevMode log capture started ──";

    public static int ProcessId { get; } = Environment.ProcessId;

    public static string SessionBoundaryMarker { get; } =
        $"{SessionBoundaryPrefix} [pid={ProcessId}]";

    /// <summary>Per-process overlay positions while multiple instances are active.</summary>
    public static class SessionOverlay {
        public static float? MpOverlayPosX { get; set; }
        public static float? MpOverlayPosY { get; set; }
        public static float? MonsterIntentOverlayPosX { get; set; }
        public static float? MonsterIntentOverlayPosY { get; set; }
    }

    /// <summary>LAN client AFK — per-process only (dual-instance shares settings.json).</summary>
    internal static class SessionLan {
        public static bool MpAiTeammateAfkClient { get; set; }
    }

    internal static string LogViewerSubtitle {
        get {
            var file = KitLibUserOps.CurrentSessionLogFileName?.Invoke();
            return file != null
                ? I18N.T("log.instance.subtitle", "This window · PID {0} · {1}", ProcessId, file)
                : I18N.T("log.instance.subtitleLive", "This window · PID {0} · live capture", ProcessId);
        }
    }

    public static bool ContainsSessionBoundary(string text)
        => (text.Contains(SessionBoundaryPrefix, StringComparison.Ordinal)
            || text.Contains(LegacySessionBoundaryPrefix, StringComparison.Ordinal))
           && text.Contains($"[pid={ProcessId}]", StringComparison.Ordinal);
}
