using System;
using System.Collections.Generic;
using System.IO;
using Godot;
using KitLib.Host;
using KitLib.Logging;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib;

/// <summary>
/// Captures log entries emitted by the game's logging system into an in-memory ring buffer.
/// Subscribe via <see cref="Log.LogCallback"/> so every Logger instance is covered.
/// Opening the log viewer hydrates from <c>user://logs/godot.log</c>.
/// </summary>
internal static class LogCollector {
    public const int MaxLiveEntries = 2000;
    private const int MaxPostBoundaryFileLines = 1200;

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
    private static DateTime _lastFileSnapshotUtc = DateTime.MinValue;

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
        KitLibHost.IsDualInstanceActive = KitLibProcessScope.IsDualInstanceActive;
        LogStreamPipeServer.Start();
        Log.LogCallback += OnLogReceived;
        MainFile.Logger.Info(KitLibInstance.SessionBoundaryMarker);
        LogViewerFilterSync.PublishDefaults();
        ScheduleKitlogStartupIfEnabled();
    }

    static void ScheduleKitlogStartupIfEnabled() {
        if (!SettingsStore.Current.LaunchKitlogOnStartup)
            return;
        Callable.From(TryLaunchKitlogStartup).CallDeferred();
    }

    static void TryLaunchKitlogStartup() {
        if (!DevViewerLauncher.TryOpenLogs(out var error) && !string.IsNullOrEmpty(error))
            KitLog.Debug("DevViewer", error);
    }

    /// <summary>
    /// Re-reads the current log file and merges it with live callback entries on the next snapshot.
    /// </summary>
    public static void RefreshFileSnapshot() {
        var parsed = GameLogFileHydrator.ReadLogEntries();
        if (parsed.Count == 0) {
            GameLogFileHydrator.InvalidateSessionLogPathCache();
            parsed = GameLogFileHydrator.ReadLogEntries();
        }
        var path = GameLogFileHydrator.GodotLogPath;
        if (path != null) {
            try {
                _lastFileSnapshotUtc = File.GetLastWriteTimeUtc(path);
            }
            catch {
                // ignore mtime probe failures
            }
        }
        lock (_lock) {
            _fileEntries = parsed;
            _dirty = true;
        }
    }

    /// <summary>Re-reads godot.log only when the file changed since the last snapshot.</summary>
    public static void RefreshFileSnapshotIfChanged() {
        var path = GameLogFileHydrator.GodotLogPath;
        if (path == null)
            return;

        DateTime mtime;
        try {
            mtime = File.GetLastWriteTimeUtc(path);
        }
        catch {
            return;
        }

        if (mtime == _lastFileSnapshotUtc)
            return;

        RefreshFileSnapshot();
    }

    private static void OnLogReceived(LogLevel level, string text, int _) {
        text = NormalizeHostScopedCallbackText(text);
        lock (_lock) {
            _liveEntries.Enqueue(new Entry(level, text, DateTime.Now));
            while (_liveEntries.Count > MaxLiveEntries)
                _liveEntries.Dequeue();

            if (level >= LogLevel.Warn && !IsAlertsSuppressed())
                _unseenAlertSeverity = MaxSeverity(_unseenAlertSeverity, level);
        }
        _dirty = true;

        PublishStreamEntry(level, text);
    }

    static void PublishStreamEntry(LogLevel level, string text) {
        var lvl = GameLogLineFormat.LevelToken(level).ToLowerInvariant();
        var fingerprint = $"{lvl}|{text}";
        if (StructuredLogDedupe.TryConsume(fingerprint))
            return;

        var boundary = IsSessionBoundary(new Entry(level, text, DateTime.Now));
        var entry = LogStreamEntry.FromGameCallback(
            lvl,
            text,
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            boundary);
        LogStreamHub.Publish(entry);
    }

    internal static bool TryContainsLiveText(string text) {
        lock (_lock) {
            foreach (var entry in _liveEntries) {
                if (string.Equals(entry.Text, text, StringComparison.Ordinal))
                    return true;
            }
        }

        return false;
    }

    internal static void Inject(string text, LogLevel level) => OnLogReceived(level, text, 0);

    /// <summary>
    /// Legacy KitLog hub lines used <c>[KitLib][scope]</c>; official <see cref="Logger"/> emits <c>[KitLib] [scope]</c>.
    /// </summary>
    static string NormalizeHostScopedCallbackText(string text) {
        const string legacy = "[KitLib][";
        if (text.StartsWith(legacy, StringComparison.Ordinal))
            return "[KitLib] [" + text[legacy.Length..];
        return text;
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
                var postBoundary = new List<Entry>(fileEntries.Count - fileBoundaryIndex);
                for (int i = fileBoundaryIndex; i < fileEntries.Count; i++) {
                    var entry = fileEntries[i];
                    postBoundary.Add(IsSessionBoundary(entry) ? entry : PromoteFileEntryToSession(entry));
                }

                if (postBoundary.Count > MaxPostBoundaryFileLines)
                    postBoundary = postBoundary.GetRange(
                        postBoundary.Count - MaxPostBoundaryFileLines,
                        MaxPostBoundaryFileLines);

                // File tail is chronological; live supplements callback-only lines not yet on disk.
                AppendUnique(merged, postBoundary);
                AppendUnique(merged, liveEntries);
            }
            else {
                for (int i = fileBoundaryIndex; i < fileEntries.Count; i++)
                    merged.Add(PromoteFileEntryToSession(fileEntries[i]));
            }
        }

        TrimToMaxEntries(merged);

        return merged;
    }

    /// <summary>
    /// Caps total size while preserving pre-boundary history and live callback lines.
    /// File-only supplement lines (no wall clock) are dropped first.
    /// </summary>
    private static void TrimToMaxEntries(List<Entry> merged) {
        if (merged.Count <= MaxMergedEntries)
            return;

        int boundaryIdx = FindLastBoundaryIndex(merged);
        if (boundaryIdx < 0) {
            merged.RemoveRange(0, merged.Count - MaxMergedEntries);
            return;
        }

        int tailStart = boundaryIdx + 1;
        int tailBudget = MaxMergedEntries - tailStart;
        if (tailBudget <= 0) {
            merged.RemoveRange(tailStart, merged.Count - tailStart);
            return;
        }

        int excess = merged.Count - tailStart - tailBudget;
        if (excess <= 0)
            return;

        var victims = new List<int>(excess);
        for (int i = tailStart; i < merged.Count && victims.Count < excess; i++) {
            if (!merged[i].HasWallClockTime)
                victims.Add(i);
        }

        for (int i = tailStart; i < merged.Count && victims.Count < excess; i++) {
            if (merged[i].HasWallClockTime)
                victims.Add(i);
        }

        for (int i = victims.Count - 1; i >= 0; i--)
            merged.RemoveAt(victims[i]);
    }

    private static int FindLastBoundaryIndex(List<Entry> entries) {
        for (int i = entries.Count - 1; i >= 0; i--) {
            if (IsSessionBoundary(entries[i]))
                return i;
        }

        return -1;
    }

    private static void AppendUnique(List<Entry> merged, IEnumerable<Entry> entries, List<Entry>? skipFingerprints = null) {
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

    /// <summary>Post-boundary file lines are current-session history and get live formatting in the viewer.</summary>
    private static Entry PromoteFileEntryToSession(Entry entry)
        => entry.IsFromFile ? entry with { IsFromFile = false } : entry;
}
