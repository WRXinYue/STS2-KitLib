using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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
/// Persists session markers and pending crash reports under <see cref="DataPaths.BaseDir"/>.
/// Thread-safe for calls from the unhandled-exception handler.
/// </summary>
internal static class CrashRecoveryStore {
    private const int MaxStackTraceLines = 24;
    private static readonly object FileLock = new();

    private static readonly JsonSerializerOptions JsonOpts = new() {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string PendingReportPath => Path.Combine(DataPaths.BaseDir, "pending-crash-report.json");

    private static string SessionActivePath =>
        Path.Combine(DataPaths.BaseDir, "instances", KitLibInstance.ProcessId.ToString(), "session.active");

    internal static void MarkSessionStarted() {
        lock (FileLock) {
            try {
                DetectOrphanSessionsLocked();
                var dir = Path.GetDirectoryName(SessionActivePath);
                if (!string.IsNullOrEmpty(dir))
                    Directory.CreateDirectory(dir);
                File.WriteAllText(SessionActivePath, $"{DateTime.UtcNow:O}\n{KitLibInstance.ProcessId}");
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib CrashRecovery] Failed to mark session started: {ex.Message}");
            }
        }
    }

    internal static void MarkSessionCleanExit() {
        lock (FileLock) {
            TryDelete(SessionActivePath);
        }
    }

    internal static void RecordCrash(Exception? exception, CrashReportKind kind = CrashReportKind.UnhandledException) {
        lock (FileLock) {
            try {
                var report = BuildReport(exception, kind);
                WritePendingReportLocked(report);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLib CrashRecovery] Failed to record crash: {ex.Message}");
            }
        }
    }

    internal static CrashReport? TryConsumePendingReport() {
        lock (FileLock) {
            var report = TryReadPendingReportLocked();
            if (report != null)
                TryDelete(PendingReportPath);
            return report;
        }
    }

    internal static void ClearPendingReport() {
        lock (FileLock)
            TryDelete(PendingReportPath);
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

    private static void DetectOrphanSessionsLocked() {
        var instancesDir = Path.Combine(DataPaths.BaseDir, "instances");
        if (!Directory.Exists(instancesDir))
            return;

        foreach (var dir in Directory.GetDirectories(instancesDir)) {
            var marker = Path.Combine(dir, "session.active");
            if (!File.Exists(marker))
                continue;

            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out int pid))
                continue;

            if (pid == KitLibInstance.ProcessId)
                continue;

            if (IsProcessAlive(pid))
                continue;

            TryDelete(marker);

            if (TryReadPendingReportLocked() != null)
                continue;

            WritePendingReportLocked(new CrashReport {
                Kind = CrashReportKind.OrphanSession,
                UtcTimestamp = DateTime.UtcNow,
                ProcessId = KitLibInstance.ProcessId,
                DevModeVersion = GetDevModeVersion(),
                OrphanProcessId = pid
            });
        }
    }

    private static CrashReport BuildReport(Exception? exception, CrashReportKind kind) {
        var ex = exception;
        if (ex is System.Reflection.TargetInvocationException tie && tie.InnerException != null)
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
            if (!File.Exists(PendingReportPath))
                return null;
            var json = File.ReadAllText(PendingReportPath);
            return JsonSerializer.Deserialize<CrashReport>(json, JsonOpts);
        }
        catch {
            return null;
        }
    }

    private static void WritePendingReportLocked(CrashReport report) {
        var dir = Path.GetDirectoryName(PendingReportPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        var tmp = PendingReportPath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(report, JsonOpts));
        File.Move(tmp, PendingReportPath, overwrite: true);
    }

    private static void TryDelete(string path) {
        try {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch {
            // Best-effort cleanup only.
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
