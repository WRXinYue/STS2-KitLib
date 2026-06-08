using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace KitLib;

/// <summary>
/// Mirrors this process's <see cref="Log.LogCallback"/> stream to
/// <c>mod_data/KitLib/instances/{pid}/session.log</c> so dual-instance runs
/// do not share a single on-disk log file with Godot's rotated <c>godot.log</c>.
/// Callback enqueues only; <see cref="TryFlush"/> drains on a timer or at shutdown.
/// </summary>
internal static class InstanceLogWriter {
    internal const double FlushIntervalSeconds = 0.25;

    private static readonly object WriteLock = new();
    private static readonly Queue<string> PendingLines = new();
    private static StreamWriter? _writer;

    public static string InstanceDirectory { get; private set; } = "";

    public static string SessionLogPath { get; private set; } = "";

    public static bool IsActive => _writer != null;

    public static string DisplayName
        => IsActive ? $"instances/{KitLibInstance.ProcessId}/session.log" : "";

    public static void Initialize() {
        try {
            InstanceDirectory = Path.Combine(
                DataPaths.BaseDir, "instances", KitLibInstance.ProcessId.ToString());
            Directory.CreateDirectory(InstanceDirectory);
            SessionLogPath = Path.Combine(InstanceDirectory, "session.log");

            _writer = new StreamWriter(
                new FileStream(SessionLogPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) {
                AutoFlush = false
            };
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] Instance log writer failed: {ex.Message}");
            _writer = null;
        }
    }

    public static void Enqueue(string text) {
        if (_writer == null)
            return;

        lock (WriteLock)
            PendingLines.Enqueue(text);
    }

    /// <summary>Drains pending lines to disk with a single flush. Safe on the main thread.</summary>
    public static void TryFlush() {
        if (_writer == null)
            return;

        lock (WriteLock) {
            if (PendingLines.Count == 0)
                return;

            try {
                while (PendingLines.Count > 0)
                    _writer.WriteLine(PendingLines.Dequeue());
                _writer.Flush();
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib] Instance log write failed: {ex.Message}");
            }
        }
    }

    public static void Shutdown() {
        lock (WriteLock) {
            try {
                if (_writer != null) {
                    while (PendingLines.Count > 0)
                        _writer.WriteLine(PendingLines.Dequeue());
                    _writer.Flush();
                    _writer.Dispose();
                }
            }
            catch {
                // Best-effort shutdown only.
            }
            finally {
                PendingLines.Clear();
                _writer = null;
            }
        }
    }
}
