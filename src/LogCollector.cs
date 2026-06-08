using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib;

/// <summary>
/// Captures log entries emitted by the game's logging system into an in-memory ring buffer.
/// Subscribe via <see cref="Log.LogCallback"/> so every Logger instance is covered.
/// Opening the log viewer also hydrates from this process's mirrored log at
/// <c>mod_data/KitLib/instances/{pid}/session.log</c>, falling back to <c>user://logs/</c>.
/// </summary>
internal static class LogCollector {
    public const int MaxLiveEntries = 2000;
    public const int MaxMergedEntries = 4000;
    internal const string LogViewerRootName = "KitLibLogViewer";

    /// <summary>Legacy prefix; new sessions append <c>[pid=…]</c> via <see cref="KitLibInstance.SessionBoundaryMarker"/>.</summary>
    public const string SessionBoundaryMarker = KitLibInstance.SessionBoundaryPrefix;

    public readonly record struct Entry(
        LogLevel Level,
        string Text,
        DateTime Time,
        bool IsFromFile = false,
        bool HasWallClockTime = true);

    private static readonly Queue<Entry> _liveEntries = new();
    private static List<Entry> _fileEntries = [];
    private static readonly object _lock = new();
    private static volatile bool _dirty;
    private static LogLevel? _unseenAlertSeverity;
    private static bool _logViewerOpen;

    /// <summary>True when new entries have arrived since the last <see cref="MarkClean"/> call.</summary>
    public static bool IsDirty => _dirty;

    /// <summary>Highest unseen Warn/Error since last acknowledge, or null when none.</summary>
    public static LogLevel? UnseenAlertSeverity {
        get {
            lock (_lock)
                return _unseenAlertSeverity;
        }
    }

    private const string LegacySessionBoundaryMarker = "── DevMode log capture started ──";

    public static bool IsSessionBoundary(in Entry entry)
        => KitLibInstance.ContainsSessionBoundary(entry.Text)
           || entry.Text.Contains(SessionBoundaryMarker, StringComparison.Ordinal)
           || entry.Text.Contains(LegacySessionBoundaryMarker, StringComparison.Ordinal);

    public static void Initialize() {
        Log.LogCallback += OnLogReceived;
        MainFile.Logger.Info(KitLibInstance.SessionBoundaryMarker);
    }

    /// <summary>
    /// Re-reads the current session log file and merges it with live callback entries on the next snapshot.
    /// </summary>
    public static void RefreshFileSnapshot() {
        var parsed = GameLogFileHydrator.ReadSessionLogEntries();
        if (parsed.Count == 0) {
            GameLogFileHydrator.InvalidateSessionLogPathCache();
            parsed = GameLogFileHydrator.ReadSessionLogEntries();
        }
        lock (_lock) {
            _fileEntries = parsed;
            _dirty = true;
        }
    }

    private static void OnLogReceived(LogLevel level, string text, int _) {
        InstanceLogWriter.Enqueue(text);
        lock (_lock) {
            _liveEntries.Enqueue(new Entry(level, text, DateTime.Now));
            while (_liveEntries.Count > MaxLiveEntries)
                _liveEntries.Dequeue();

            if (level >= LogLevel.Warn && !IsAlertsSuppressed())
                _unseenAlertSeverity = MaxSeverity(_unseenAlertSeverity, level);
        }
        _dirty = true;
    }

    /// <summary>Clears unseen Warn/Error alert state (e.g. when opening the log viewer).</summary>
    public static void AcknowledgeAlerts() {
        lock (_lock)
            _unseenAlertSeverity = null;
    }

    /// <summary>Syncs whether the log viewer browser overlay is currently on screen.</summary>
    public static void SyncLogViewerOpen(NGlobalUi globalUi) {
        _logViewerOpen = globalUi.GetNodeOrNull<Control>(LogViewerRootName) != null;
    }

    private static bool IsAlertsSuppressed() => _logViewerOpen;

    private static LogLevel MaxSeverity(LogLevel? current, LogLevel incoming)
        => current == null || incoming > current ? incoming : current.Value;

    /// <summary>Returns a merged snapshot of file-hydrated and live entries (thread-safe copy).</summary>
    public static List<Entry> GetSnapshot() {
        List<Entry> fileCopy;
        Entry[] liveCopy;
        lock (_lock) {
            fileCopy = _fileEntries;
            liveCopy = _liveEntries.ToArray();
        }

        return MergeEntries(fileCopy, liveCopy);
    }

    public static void Clear() {
        lock (_lock) {
            _liveEntries.Clear();
            _fileEntries.Clear();
            _unseenAlertSeverity = null;
        }
        _dirty = true;
    }

    public static void MarkClean() => _dirty = false;

    private static List<Entry> MergeEntries(List<Entry> fileEntries, Entry[] liveEntries) {
        int fileBoundaryIndex = FindLastBoundaryIndex(fileEntries);

        var merged = new List<Entry>(fileEntries.Count + liveEntries.Length);

        if (fileBoundaryIndex < 0) {
            AppendUnique(merged, fileEntries);
            AppendUnique(merged, liveEntries, skipFingerprints: merged);
        }
        else {
            for (int i = 0; i < fileBoundaryIndex; i++)
                merged.Add(fileEntries[i]);

            if (liveEntries.Length > 0) {
                AppendUnique(merged, liveEntries);
            }
            else {
                for (int i = fileBoundaryIndex; i < fileEntries.Count; i++)
                    merged.Add(fileEntries[i]);
            }
        }

        TrimToMaxEntries(merged);

        return merged;
    }

    /// <summary>
    /// Caps total size while preserving the full pre-boundary file history; only trims the live tail.
    /// </summary>
    private static void TrimToMaxEntries(List<Entry> merged) {
        if (merged.Count <= MaxMergedEntries)
            return;

        int boundaryIdx = FindLastBoundaryIndex(merged);
        if (boundaryIdx < 0) {
            merged.RemoveRange(0, merged.Count - MaxMergedEntries);
            return;
        }

        int preserveThroughBoundary = boundaryIdx + 1;
        int maxLiveEntries = MaxMergedEntries - preserveThroughBoundary;
        if (maxLiveEntries < 0)
            maxLiveEntries = 0;

        int liveStart = preserveThroughBoundary;
        int liveCount = merged.Count - liveStart;
        if (liveCount > maxLiveEntries)
            merged.RemoveRange(liveStart, liveCount - maxLiveEntries);
    }

    private static int FindLastBoundaryIndex(List<Entry> entries) {
        for (int i = entries.Count - 1; i >= 0; i--) {
            if (IsSessionBoundary(entries[i]))
                return i;
        }

        return -1;
    }

    private static void AppendUnique(List<Entry> merged, List<Entry> entries, List<Entry>? skipFingerprints = null) {
        var seen = skipFingerprints != null
            ? BuildFingerprintSet(skipFingerprints)
            : BuildFingerprintSet(merged);

        foreach (var entry in entries) {
            if (seen.Add(Fingerprint(entry)))
                merged.Add(entry);
        }
    }

    private static void AppendUnique(List<Entry> merged, Entry[] entries, List<Entry>? skipFingerprints = null) {
        var seen = skipFingerprints != null
            ? BuildFingerprintSet(skipFingerprints)
            : BuildFingerprintSet(merged);

        foreach (var entry in entries) {
            if (seen.Add(Fingerprint(entry)))
                merged.Add(entry);
        }
    }

    private static HashSet<string> BuildFingerprintSet(List<Entry> entries) {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
            seen.Add(Fingerprint(entry));
        return seen;
    }

    private static string Fingerprint(Entry entry)
        => $"{(int)entry.Level}|{entry.Text.Trim()}";
}
