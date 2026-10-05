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
    private static bool _fileHydrated;

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
    }

    /// <summary>
    /// One-time backfill: hydrates godot.log history once. Live callbacks are the live source afterwards.
    /// </summary>
    public static void RefreshFileSnapshot() {
        lock (_lock) {
            if (_fileHydrated)
                return;
            _fileHydrated = true;
        }

        var parsed = GameLogFileHydrator.ReadLogEntries();
        if (parsed.Count == 0) {
            GameLogFileHydrator.InvalidateSessionLogPathCache();
            parsed = GameLogFileHydrator.ReadLogEntries();
        }

        lock (_lock) {
            _fileEntries = parsed;
            _dirty = true;
        }
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

    /// <summary>Syncs whether the log viewer browser overlay is currently on screen (and visible).</summary>
    public static void SyncLogViewerOpen(NGlobalUi globalUi) {
        _logViewerOpen = globalUi.GetNodeOrNull<Control>(LogViewerRootName) is { Visible: true };
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
        var merged = new List<Entry>(fileEntries.Count + liveEntries.Length);

        // Keep the tail of backfilled file history that fits alongside live entries.
        int fileBudget = MaxMergedEntries - liveEntries.Length;
        int fileStart = Math.Max(0, fileEntries.Count - Math.Max(fileBudget, 0));
        for (int i = fileStart; i < fileEntries.Count; i++)
            merged.Add(fileEntries[i]);

        merged.AddRange(liveEntries);

        if (merged.Count > MaxMergedEntries)
            merged.RemoveRange(0, merged.Count - MaxMergedEntries);

        return merged;
    }
}
