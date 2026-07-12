using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using Godot;
using KitLib.CombatStats;
using KitLib.Interop;

namespace KitLib.Feedback;

/// <summary>
/// Collects Harmony patch dump, combat stats, and game log,
/// then packages them into a ZIP under <c>user://devmode-reports/</c>.
/// All heavy work runs on a background thread; the returned path is the ZIP file path.
/// </summary>
internal static class FeedbackReportBuilder {
    private const string ReportsDir = "devmode-reports";

    public readonly record struct BuildRequest(
        /// <summary>Absolute path of the game log file to attach.</summary>
        string LogFilePath,
        /// <summary>When true, replaces the user data dir path with &lt;user-data&gt; in all text.</summary>
        bool PrivacyMode);

    /// <summary>
    /// Scans <c>user://logs/</c> for game log files, sorted newest first.
    /// Returns (display name, absolute path, is-current-session) triples. Safe to call on any thread.
    /// </summary>
    public static IReadOnlyList<(string DisplayName, string AbsPath, bool IsCurrentSession)> ScanLogFiles()
        => GameLogFileHydrator.ScanLogFiles()
            .Select(f => (f.DisplayName, f.AbsPath, f.IsCurrentSession))
            .ToList();

    /// <summary>
    /// Build the report ZIP synchronously. Run this on a background thread.
    /// Returns the absolute path to the created ZIP file.
    /// </summary>
    public static string Build(BuildRequest req) {
        var userDataDir = OS.GetUserDataDir();
        var reportsPath = Path.Combine(userDataDir, ReportsDir);
        Directory.CreateDirectory(reportsPath);

        var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(reportsPath, $"devmode-report-{ts}.zip");

        using var stream = new FileStream(zipPath, FileMode.CreateNew, System.IO.FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        WriteEntry(archive, "harmony-patches.txt", BuildHarmonyDump(), req, userDataDir);
        WriteEntry(archive, "combat-stats.json", BuildCombatStatsJson(), req, userDataDir);

        if (!File.Exists(req.LogFilePath))
            throw new FileNotFoundException("Game log file not found.", req.LogFilePath);

        var logName = Path.GetFileName(req.LogFilePath);
        WriteEntry(archive, logName, ReadLogFile(req.LogFilePath), req, userDataDir);

        return zipPath;
    }

    // ── Section builders ─────────────────────────────────────────────────

    private static string BuildHarmonyDump() {
        var report = HarmonyPatchReportBuilder.BuildReport(out var error);
        return string.IsNullOrEmpty(error) ? report : $"(error generating report: {error})";
    }

    private static string BuildCombatStatsJson() {
        try {
            if (!KitLibState.IsActive)
                return "{\"note\":\"Dev Mode inactive during report\"}";
            return CombatStatsExport.ToJson(CombatStatsExport.CaptureBundle());
        }
        catch (Exception ex) {
            return $"{{\"error\":\"{ex.Message}\"}}";
        }
    }

    /// <summary>
    /// Reads a game log file in full. Uses <see cref="FileShare.ReadWrite"/>
    /// so the game's open handle doesn't block us.
    /// </summary>
    private static string ReadLogFile(string path) {
        try {
            using var fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            return reader.ReadToEnd();
        }
        catch (Exception ex) {
            return $"(error reading log file: {ex.Message})";
        }
    }

    // ── Privacy ───────────────────────────────────────────────────────────

    /// <summary>
    /// Replaces occurrences of the user data directory path with &lt;user-data&gt;.
    /// Handles both forward-slash and backslash variants.
    /// </summary>
    private static string Redact(string text, string userDataDir) {
        var fwd = userDataDir.Replace('\\', '/');
        var bwd = userDataDir.Replace('/', '\\');
        text = text.Replace(fwd, "<user-data>", StringComparison.OrdinalIgnoreCase);
        if (bwd != fwd)
            text = text.Replace(bwd, "<user-data>", StringComparison.OrdinalIgnoreCase);
        return text;
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void WriteEntry(ZipArchive archive, string name, string content,
        BuildRequest req, string userDataDir) {
        if (req.PrivacyMode)
            content = Redact(content, userDataDir);

        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
