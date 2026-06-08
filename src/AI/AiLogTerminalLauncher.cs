using System;
using System.Diagnostics;
using System.IO;

namespace KitLib.AI;

/// <summary>Opens a system terminal tailing AI decision lines from the session log.</summary>
internal static class AiLogTerminalLauncher {
    const string FilterPattern = "\\[(AutoPlay|AiHost|MpAi|LanLocal|Companion)\\]";

    public static bool TryOpen(out string? error) {
        error = null;
        var path = ResolveLogPath();
        if (path == null) {
            error = I18N.T("ai.terminal.noLog", "No session log file found yet.");
            return false;
        }

        var psCommand = BuildPowerShellCommand(path);
        var pwsh = ResolvePowerShell7Path();

        if (pwsh != null) {
            if (TryStartProcess(pwsh, $"-NoLogo -NoExit -Command \"{psCommand}\""))
                return true;
            if (TryStartProcess("wt.exe", $"-w 0 \"{pwsh}\" -NoLogo -NoExit -Command \"{psCommand}\""))
                return true;
        }

        if (TryStartProcess("wt.exe", $"-w 0 powershell -NoLogo -NoExit -Command \"{psCommand}\""))
            return true;
        if (TryStartProcess("powershell.exe", $"-NoLogo -NoExit -Command \"{psCommand}\""))
            return true;

        error = I18N.T("ai.terminal.launchFailed", "Could not start a system terminal.");
        return false;
    }

    static string? ResolveLogPath() {
        var session = GameLogFileHydrator.CurrentSessionLogPath;
        if (!string.IsNullOrEmpty(session) && File.Exists(session))
            return session;

        var godot = Path.Combine(GameLogFileHydrator.LogsDirectory, "godot.log");
        if (File.Exists(godot))
            return godot;

        return null;
    }

    static string BuildPowerShellCommand(string logPath) {
        var escaped = logPath.Replace("'", "''");
        return $"Get-Content -LiteralPath '{escaped}' -Wait -Tail 40 | Where-Object {{ $_ -match '{FilterPattern}' }}";
    }

    static string? ResolvePowerShell7Path() {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var candidates = new[] {
            Path.Combine(programFiles, "PowerShell", "7", "pwsh.exe"),
            Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? "", "PowerShell", "7", "pwsh.exe"),
        };

        foreach (var candidate in candidates) {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate))
                return candidate;
        }

        return FindOnPath("pwsh.exe");
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

    static bool TryStartProcess(string fileName, string arguments) {
        try {
            Process.Start(new ProcessStartInfo {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = true,
            });
            return true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[AiHost] Terminal launch failed ({fileName}): {ex.Message}");
            return false;
        }
    }
}
