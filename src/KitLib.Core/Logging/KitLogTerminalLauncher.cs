using System;
using System.Diagnostics;
using System.IO;
using KitLib.Host;

namespace KitLib;

/// <summary>Launches the optional <c>kitlog</c> CLI in a system terminal.</summary>
public static class KitLogTerminalLauncher {
    const string SessionTailArgsTemplate = "tail -f --tail 0 --sync-viewer --pid {0}";
    const string AiTailArgsTemplate = "tail -f --tail 40 --filter ai --pid {0}";

    public static bool TryOpenSessionTail(out string? error)
        => TryOpen(SessionTailArgsTemplate, out error);

    public static bool TryOpenAiTail(out string? error)
        => TryOpen(AiTailArgsTemplate, out error);

    static bool TryOpen(string tailArgsTemplate, out string? error) {
        error = null;

        if (TryLaunchKitLog(tailArgsTemplate, out var launchError))
            return true;

        error = launchError ?? BuildFallbackMessage(tailArgsTemplate);
        return false;
    }

    static bool TryLaunchKitLog(string tailArgsTemplate, out string? error) {
        error = null;
        var executable = ResolveKitLogExecutable();
        if (executable == null)
            return false;

        var args = string.Format(tailArgsTemplate, KitLibInstance.ProcessId);

        try {
            Process.Start(BuildStartInfo(executable, args));
            return true;
        }
        catch (Exception ex) {
            KitLog.Warn("KitLog", $"kitlog launch failed ({executable}): {ex.Message}");
            error = I18N.T("ai.terminal.launchFailed", "Could not start kitlog: {0}", ex.Message);
            return false;
        }
    }

    static ProcessStartInfo BuildStartInfo(string executable, string args) {
        if (OperatingSystem.IsWindows()) {
            var command = QuoteWindowsArg(executable);
            if (!string.IsNullOrEmpty(args))
                command += " " + args;

            return new ProcessStartInfo {
                FileName = "cmd.exe",
                Arguments = $"/c start \"KitLog\" cmd /k {command}",
                UseShellExecute = true,
            };
        }

        return new ProcessStartInfo {
            FileName = executable,
            Arguments = args,
            UseShellExecute = true,
        };
    }

    static string QuoteWindowsArg(string value)
        => value.Contains(' ') || value.Contains('"') ? $"\"{value.Replace("\"", "\\\"")}\"" : value;

    static string? ResolveKitLogExecutable() {
        var modDir = Path.GetDirectoryName(typeof(MainFile).Assembly.Location);
        if (!string.IsNullOrEmpty(modDir)) {
            var toolsDir = Path.Combine(modDir, "tools");
            var candidates = new[] {
                Path.Combine(toolsDir, OperatingSystem.IsWindows() ? "kitlog.exe" : "kitlog"),
                Path.Combine(toolsDir, "kitlog.exe"),
                Path.Combine(toolsDir, "kitlog"),
            };

            foreach (var candidate in candidates) {
                if (File.Exists(candidate))
                    return candidate;
            }
        }

        var onPath = FindOnPath(OperatingSystem.IsWindows() ? "kitlog.exe" : "kitlog");
        if (onPath != null)
            return onPath;

        if (OperatingSystem.IsWindows())
            return FindOnPath("kitlog");

        return null;
    }

    static string? FindOnPath(string fileName) {
        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv))
            return null;

        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)) {
            var full = Path.Combine(dir.Trim(), fileName);
            if (File.Exists(full))
                return full;
        }

        return null;
    }

    static string BuildFallbackMessage(string tailArgsTemplate) {
        var sessionPath = KitLibUserOps.CurrentSessionLogPath?.Invoke();
        var command = $"kitlog {string.Format(tailArgsTemplate, KitLibInstance.ProcessId)}";

        if (string.IsNullOrEmpty(sessionPath)) {
            return I18N.T(
                "ai.terminal.kitlogMissingNoLog",
                "kitlog not found. Install KitLog.Cli from the optional tools zip, then run: {0}",
                command);
        }

        return I18N.T(
            "ai.terminal.kitlogMissing",
            "kitlog not found. Install KitLog.Cli from the optional tools zip.\nRun: {0}\nLog: {1}",
            command,
            sessionPath);
    }
}
