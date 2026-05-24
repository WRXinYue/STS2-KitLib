using System;
using System.IO;
using System.Text;
using MegaCrit.Sts2.Core.Logging;

namespace DevMode;

/// <summary>
/// Mirrors this process's <see cref="Log.LogCallback"/> stream to
/// <c>mod_data/DevMode/instances/{pid}/session.log</c> so dual-instance runs
/// do not share a single on-disk log file with Godot's rotated <c>godot.log</c>.
/// </summary>
internal static class InstanceLogWriter {
    private static readonly object WriteLock = new();
    private static StreamWriter? _writer;

    public static string InstanceDirectory { get; private set; } = "";

    public static string SessionLogPath { get; private set; } = "";

    public static bool IsActive => _writer != null;

    public static string DisplayName
        => IsActive ? $"instances/{DevModeInstance.ProcessId}/session.log" : "";

    public static void Initialize() {
        try {
            InstanceDirectory = Path.Combine(
                DataPaths.BaseDir, "instances", DevModeInstance.ProcessId.ToString());
            Directory.CreateDirectory(InstanceDirectory);
            SessionLogPath = Path.Combine(InstanceDirectory, "session.log");

            _writer = new StreamWriter(
                new FileStream(SessionLogPath, FileMode.Create, System.IO.FileAccess.Write, FileShare.Read),
                new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)) {
                AutoFlush = true
            };
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[DevMode] Instance log writer failed: {ex.Message}");
            _writer = null;
        }
    }

    public static void Append(LogLevel level, string text) {
        if (_writer == null)
            return;

        lock (WriteLock) {
            try {
                _writer.WriteLine(text);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[DevMode] Instance log write failed: {ex.Message}");
            }
        }
    }

    public static void Shutdown() {
        lock (WriteLock) {
            try {
                _writer?.Flush();
                _writer?.Dispose();
            }
            catch {
                // Best-effort shutdown only.
            }
            finally {
                _writer = null;
            }
        }
    }
}
