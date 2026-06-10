using System;
using System.IO;
using System.Text;
using KitLib.Settings;

namespace KitLib.DevPerf;

internal static class DevPerfTraceWriter {
    static readonly object WriteLock = new();
    static string? _tracePath;

    static string TracePath {
        get {
            if (_tracePath != null)
                return _tracePath;

            var dir = InstanceLogWriter.IsActive
                ? InstanceLogWriter.InstanceDirectory
                : Path.Combine(DataPaths.BaseDir, "instances", KitLibInstance.ProcessId.ToString());

            Directory.CreateDirectory(dir);
            _tracePath = Path.Combine(dir, "perf-trace.log");
            return _tracePath;
        }
    }

    internal static void TryAppendTransition(string name, long elapsedMs) {
        if (!SettingsStore.Current.PerfHudTraceToFile)
            return;

        AppendLine($"transition,{Escape(name)},{elapsedMs}");
    }

    internal static void TryAppendFrameSpike(double elapsedMs) {
        if (!SettingsStore.Current.PerfHudTraceToFile)
            return;

        AppendLine($"frame_spike,,{(long)Math.Round(elapsedMs)}");
    }

    static void AppendLine(string payload) {
        try {
            var line = $"{DateTime.UtcNow:O},{payload}{Environment.NewLine}";
            lock (WriteLock) {
                File.AppendAllText(TracePath, line, Encoding.UTF8);
            }
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[Perf] trace write failed: {ex.Message}");
        }
    }

    static string Escape(string value) =>
        value.Contains(',', StringComparison.Ordinal) ? $"\"{value.Replace("\"", "\"\"")}\"" : value;
}
