using System;
using System.Diagnostics;
using System.IO;
using KitLib.Host;

namespace KitLib;

/// <summary>Launches the optional <c>kitlog</c> CLI in a system terminal.</summary>
public static class KitLogTerminalLauncher {
    const string SessionTailArgsTemplate = "attach --pid {0} --follow --sync-viewer --tail 0";
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
        var workDir = Path.GetDirectoryName(executable) ?? "";

        if (OperatingSystem.IsWindows()
            && TryBuildWindowsTerminalStartInfo(executable, args, workDir, out var terminalInfo))
            return terminalInfo;

        // Standard .NET pattern: ShellExecute the console app; Windows allocates a new console.
        return new ProcessStartInfo {
            FileName = executable,
            Arguments = args,
            UseShellExecute = true,
            WorkingDirectory = workDir,
        };
    }

    static bool TryBuildWindowsTerminalStartInfo(
        string executable,
        string args,
        string workDir,
        out ProcessStartInfo startInfo) {
        startInfo = null!;
        var wt = ResolveWindowsTerminalExecutable();
        if (wt == null)
            return false;

        startInfo = new ProcessStartInfo {
            FileName = wt,
            Arguments = FormattableString.Invariant(
                $"-d \"{workDir}\" --title \"KitLog\" -- \"{executable}\" {args}"),
            UseShellExecute = true,
        };
        return true;
    }

    static string? ResolveWindowsTerminalExecutable() {
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (!string.IsNullOrEmpty(localAppData)) {
            var storeAlias = Path.Combine(localAppData, "Microsoft", "WindowsApps", "wt.exe");
            if (File.Exists(storeAlias))
                return storeAlias;
        }

        return FindOnPath("wt.exe");
    }

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
