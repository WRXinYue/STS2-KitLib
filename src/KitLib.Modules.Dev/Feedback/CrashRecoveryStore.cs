using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Dev;
using KitLib.Host;

namespace KitLib.Feedback;

internal enum CrashReportKind {
    UnhandledException,
    OrphanSession
}

internal sealed class CrashReport {
    public CrashReportKind Kind { get; init; }
    public DateTime UtcTimestamp { get; init; }
    public int ProcessId { get; init; }
    public string DevModeVersion { get; init; } = "";
    public string? ExceptionType { get; init; }
    public string? Message { get; init; }
    public string? StackTrace { get; init; }
    public int? OrphanProcessId { get; init; }
}

/// <summary>
/// Persists session markers and pending crash reports under KitLib mod_data.
/// Thread-safe for calls from the unhandled-exception handler.
/// </summary>
internal static class CrashRecoveryStore {
    private const int MaxStackTraceLines = 24;
    private const string SessionActiveFileName = "session.active";
    private const string SessionCleanFileName = "session.clean";

    private static readonly object FileLock = new();
    private static bool _cleanExitHandled;

    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    internal static void MarkSessionStarted() {
        lock (FileLock) {
            _cleanExitHandled = false;
            try {
                DetectOrphanSessionsLocked();
                TouchSessionMarkerLocked(KitLibInstance.ProcessId);
            }
            catch (Exception ex) {
                KitLog.Warn("CrashRecovery", $"Failed to start session marker: {ex.Message}");
            }
        }
    }

    /// <summary>Refresh the active-session marker without scanning for orphan sessions.</summary>
    internal static void TouchSessionMarker() {
        lock (FileLock) {
            try {
                TouchSessionMarkerLocked(KitLibInstance.ProcessId);
            }
            catch (Exception ex) {
                KitLog.Warn("CrashRecovery", $"Failed to refresh session marker: {ex.Message}");
            }
        }
    }

    internal static void MarkSessionCleanExit() {
        lock (FileLock) {
            if (_cleanExitHandled)
                return;

            try {
                if (!TryGetModDataRoot(out var root)) {
                    KitLog.Warn("CrashRecovery", "Clean exit skipped: mod_data root unavailable.");
                    return;
                }

                int pid = KitLibInstance.ProcessId;
                ClearSessionArtifactsLocked(root, pid, writeCleanMarker: true);
                _cleanExitHandled = true;
                KitLog.Info("CrashRecovery", $"Session marker cleared (PID {pid}).");
            }
            catch (Exception ex) {
                KitLog.Warn("CrashRecovery", $"Failed to clear session marker: {ex.Message}");
            }
        }
    }

    internal static void AcknowledgeOrphanReport(CrashReport report) {
        lock (FileLock) {
            ClearPendingReportLocked();
            if (report.Kind != CrashReportKind.OrphanSession || report.OrphanProcessId is not int orphanPid)
                return;

            if (!TryGetModDataRoot(out var root))
                return;

            ClearSessionArtifactsLocked(root, orphanPid, writeCleanMarker: true);
            KitLog.Info("CrashRecovery", $"Orphan session acknowledged (PID {orphanPid}).");
        }
    }

    internal static void RecordCrash(Exception? exception, CrashReportKind kind = CrashReportKind.UnhandledException) {
        lock (FileLock) {
            try {
                WritePendingReportLocked(BuildReport(exception, kind));
            }
            catch (Exception ex) {
                KitLog.Warn("CrashRecovery", $"Failed to record crash: {ex.Message}");
            }
        }
    }

    internal static CrashReport? TryConsumePendingReport() {
        lock (FileLock) {
            var report = TryReadPendingReportLocked();
            if (report != null)
                ClearPendingReportLocked();
            return report;
        }
    }

    internal static void ClearPendingReport() {
        lock (FileLock)
            ClearPendingReportLocked();
    }

    internal static string FormatPrefillTitle(CrashReport report) =>
        report.Kind switch {
            CrashReportKind.OrphanSession => I18N.T(
                "errorFeedback.prefill.title.orphan",
                "Unexpected game exit"),
            _ => I18N.T(
                "errorFeedback.prefill.title.exception",
                "Unhandled error: {0}",
                report.ExceptionType ?? "Exception")
        };

    internal static string FormatPrefillDescription(CrashReport report) {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine(I18N.T("errorFeedback.prefill.header", "Automatic crash summary:"));
        sb.AppendLine($"  Time (UTC): {report.UtcTimestamp:u}");
        sb.AppendLine($"  PID: {report.ProcessId}");
        sb.AppendLine($"  DevMode: {report.DevModeVersion}");

        if (report.Kind == CrashReportKind.OrphanSession) {
            sb.AppendLine(I18N.T(
                "errorFeedback.prefill.orphanDetail",
                "  Previous session (PID {0}) did not shut down cleanly.",
                report.OrphanProcessId ?? 0));
        }
        else {
            if (!string.IsNullOrWhiteSpace(report.ExceptionType))
                sb.AppendLine($"  Type: {report.ExceptionType}");
            if (!string.IsNullOrWhiteSpace(report.Message))
                sb.AppendLine($"  Message: {report.Message}");
            if (!string.IsNullOrWhiteSpace(report.StackTrace)) {
                sb.AppendLine();
                sb.AppendLine(I18N.T("errorFeedback.prefill.stackTrace", "Stack trace:"));
                sb.AppendLine(report.StackTrace);
            }
        }

        sb.AppendLine();
        sb.AppendLine(I18N.T(
            "errorFeedback.prefill.stepsHint",
            "Steps to reproduce:"));
        sb.AppendLine();
        return sb.ToString();
    }

    internal static string FormatPromptBody(CrashReport report) {
        if (report.Kind == CrashReportKind.OrphanSession) {
            return I18N.T(
                "crashRecovery.startup.bodyOrphan",
                "The game may have exited unexpectedly during your last session (PID {0}). Export a feedback ZIP to share logs with mod authors.",
                report.OrphanProcessId ?? 0);
        }

        var summary = string.IsNullOrWhiteSpace(report.Message)
            ? report.ExceptionType ?? I18N.T("errorFeedback.prompt.unknownError", "Unknown error")
            : report.Message;

        return I18N.T(
            "errorFeedback.prompt.body",
            "An unhandled error occurred:\n\n{0}\n\nExport a feedback ZIP to share logs with mod authors.",
            summary);
    }

    private static void TouchSessionMarkerLocked(int pid) {
        if (!TryGetModDataRoot(out var root))
            return;

        var instanceDir = GetInstanceDir(root, pid);
        Directory.CreateDirectory(instanceDir);
        TryDelete(Path.Combine(instanceDir, SessionCleanFileName));
        File.WriteAllText(
            Path.Combine(instanceDir, SessionActiveFileName),
            $"{DateTime.UtcNow:O}\n{pid}");
    }

    private static void DetectOrphanSessionsLocked() {
        if (!TryGetModDataRoot(out var modDataRoot))
            return;

        var instancesDir = Path.Combine(modDataRoot, "instances");
        if (!Directory.Exists(instancesDir))
            return;

        foreach (var dir in Directory.GetDirectories(instancesDir)) {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out int pid))
                continue;

            if (pid == KitLibInstance.ProcessId)
                continue;

            var activePath = Path.Combine(dir, SessionActiveFileName);
            var cleanPath = Path.Combine(dir, SessionCleanFileName);
            if (!File.Exists(activePath)) {
                TryDelete(cleanPath);
                continue;
            }

            if (File.Exists(cleanPath)) {
                ClearSessionArtifactsLocked(modDataRoot, pid, writeCleanMarker: false);
                continue;
            }

            if (IsProcessAlive(pid))
                continue;

            ClearSessionArtifactsLocked(modDataRoot, pid, writeCleanMarker: false);

            if (TryReadPendingReportLocked() != null)
                continue;

            WritePendingReportLocked(new CrashReport {
                Kind = CrashReportKind.OrphanSession,
                UtcTimestamp = DateTime.UtcNow,
                ProcessId = KitLibInstance.ProcessId,
                DevModeVersion = GetDevModeVersion(),
                OrphanProcessId = pid
            });
            KitLog.Info("CrashRecovery", $"Orphan session detected (PID {pid}); wrote pending crash report.");
        }
    }

    private static void ClearSessionArtifactsLocked(string modDataRoot, int pid, bool writeCleanMarker) {
        var instanceDir = GetInstanceDir(modDataRoot, pid);
        TryDelete(Path.Combine(instanceDir, SessionActiveFileName));
        TryDelete(Path.Combine(modDataRoot, "instances", $"{pid}.lock"));

        if (writeCleanMarker) {
            Directory.CreateDirectory(instanceDir);
            File.WriteAllText(
                Path.Combine(instanceDir, SessionCleanFileName),
                $"{DateTime.UtcNow:O}\n{pid}");
        }
        else {
            TryDelete(Path.Combine(instanceDir, SessionCleanFileName));
        }
    }

    private static string GetInstanceDir(string modDataRoot, int pid) =>
        Path.Combine(modDataRoot, "instances", pid.ToString());

    private static bool TryGetModDataRoot(out string root) {
        if (DevModDataPaths.IsSet) {
            root = DevModDataPaths.Root;
            return true;
        }

        if (!string.IsNullOrEmpty(KitLibHost.ModDataDir)) {
            root = KitLibHost.ModDataDir;
            return true;
        }

        try {
            if (DataPaths.TryGetPinnedBaseDir(out root))
                return true;
        }
        catch {
            // DataPaths may not be pinned yet during very early startup.
        }

        root = "";
        return false;
    }

    private static string? TryGetPendingReportPath() {
        if (!TryGetModDataRoot(out var root))
            return null;
        return Path.Combine(root, "pending-crash-report.json");
    }

    private static void ClearPendingReportLocked() {
        var path = TryGetPendingReportPath();
        if (path != null)
            TryDelete(path);
    }

    private static CrashReport BuildReport(Exception? exception, CrashReportKind kind) {
        var ex = exception;
        if (ex is TargetInvocationException tie && tie.InnerException != null)
            ex = tie.InnerException;

        return new CrashReport {
            Kind = kind,
            UtcTimestamp = DateTime.UtcNow,
            ProcessId = KitLibInstance.ProcessId,
            DevModeVersion = GetDevModeVersion(),
            ExceptionType = ex?.GetType().FullName,
            Message = ex?.Message,
            StackTrace = TruncateStackTrace(ex?.StackTrace)
        };
    }

    private static string? TruncateStackTrace(string? stackTrace) {
        if (string.IsNullOrWhiteSpace(stackTrace))
            return null;

        var lines = stackTrace.Split('\n');
        if (lines.Length <= MaxStackTraceLines)
            return stackTrace.TrimEnd();

        return string.Join('\n', lines.AsSpan(0, MaxStackTraceLines).ToArray()).TrimEnd()
               + $"\n… ({lines.Length - MaxStackTraceLines} more lines)";
    }

    private static CrashReport? TryReadPendingReportLocked() {
        try {
            var path = TryGetPendingReportPath();
            if (path == null || !File.Exists(path))
                return null;
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<CrashReport>(json, JsonOpts);
        }
        catch {
            return null;
        }
    }

    private static void WritePendingReportLocked(CrashReport report) {
        var path = TryGetPendingReportPath();
        if (path == null)
            return;
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(report, JsonOpts));
        File.Move(tmp, path, overwrite: true);
    }

    private static bool TryDelete(string path) {
        try {
            if (!File.Exists(path))
                return false;
            File.Delete(path);
            return true;
        }
        catch {
            return false;
        }
    }

    private static bool IsProcessAlive(int pid) {
        try {
            using var proc = Process.GetProcessById(pid);
            return !proc.HasExited;
        }
        catch {
            return false;
        }
    }

    private static string GetDevModeVersion() {
        try {
            var asm = Assembly.GetExecutingAssembly();
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
                return info;
            return asm.GetName().Version?.ToString() ?? MainFile.ModID;
        }
        catch {
            return MainFile.ModID;
        }
    }
}
