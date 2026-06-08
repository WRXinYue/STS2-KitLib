using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using KitLib.CombatStats;
using KitLib.Interop;
using KitLib.Modding;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib.Feedback;

/// <summary>
/// Collects filtered log, Harmony patch dump, framework bridge snapshot, and mod list,
/// then packages them into a ZIP under <c>user://devmode-reports/</c>.
/// All heavy work runs on a background thread; the returned path is the ZIP file path.
/// </summary>
internal static class FeedbackReportBuilder {
    private const string ReportsDir = "devmode-reports";
    /// <summary>Maximum bytes read from the tail of a game log file (512 KB).</summary>
    private const int LogTailBytes = 512 * 1024;

    public readonly record struct BuildRequest(
        string Title,
        string Description,
        /// <summary>Absolute path of the game log file to attach, or null to skip.</summary>
        string? LogFilePath,
        /// <summary>When true, replaces the user data dir path with &lt;user-data&gt; in all text.</summary>
        bool PrivacyMode);

    /// <summary>
    /// Scans <c>user://logs/</c> for game log files, sorted newest first.
    /// Returns (display name, absolute path) pairs. Safe to call on any thread.
    /// </summary>
    public static IReadOnlyList<(string DisplayName, string AbsPath)> ScanLogFiles()
        => GameLogFileHydrator.ScanLogFiles()
            .Select(f => (f.DisplayName, f.AbsPath))
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

        WriteEntry(archive, "report.txt", BuildReportHeader(req, userDataDir), req, userDataDir);
        WriteEntry(archive, "mods.txt", BuildModList(), req, userDataDir);
        WriteEntry(archive, "logs-filtered.txt", BuildFilteredLog(), req, userDataDir);
        WriteEntry(archive, "harmony-patches.txt", BuildHarmonyDump(), req, userDataDir);
        WriteEntry(archive, "framework-bridge.txt", BuildFrameworkBridge(), req, userDataDir);
        WriteEntry(archive, "combat-stats.json", BuildCombatStatsJson(), req, userDataDir);

        if (req.LogFilePath != null && File.Exists(req.LogFilePath)) {
            var logName = Path.GetFileName(req.LogFilePath);
            WriteEntry(archive, $"game-logs/{logName}", ReadLogTail(req.LogFilePath), req, userDataDir);
        }

        return zipPath;
    }

    // ── Section builders ─────────────────────────────────────────────────

    private static string BuildReportHeader(BuildRequest req, string userDataDir) {
        var sb = new StringBuilder();
        sb.AppendLine("=== DevMode Feedback Report ===");
        sb.AppendLine($"Generated : {DateTime.Now:O}");
        sb.AppendLine($"KitLib   : {MainFile.ModID}");
        sb.AppendLine($"OS        : {OS.GetName()} {OS.GetVersion()}");
        // Always redact path in header regardless of privacy mode — it's shown to user in the UI anyway
        sb.AppendLine($"User data : {(req.PrivacyMode ? "<user-data>" : userDataDir)}");
        sb.AppendLine($"Log file  : {(req.LogFilePath != null ? Path.GetFileName(req.LogFilePath) : "(none)")}");
        sb.AppendLine($"Privacy   : {(req.PrivacyMode ? "on (paths redacted)" : "off")}");
        sb.AppendLine();
        sb.AppendLine("--- Title ---");
        sb.AppendLine(string.IsNullOrWhiteSpace(req.Title) ? "(no title)" : req.Title);
        sb.AppendLine();
        sb.AppendLine("--- Description / Steps to reproduce ---");
        sb.AppendLine(string.IsNullOrWhiteSpace(req.Description) ? "(no description)" : req.Description);
        return sb.ToString();
    }

    private static string BuildModList() {
        var mods = ModRuntime.Catalog.GetSnapshot();
        if (mods.Count == 0)
            return "(no mods loaded)";

        var sb = new StringBuilder();
        sb.AppendLine("id | name | version");
        sb.AppendLine(new string('-', 60));
        foreach (var m in mods)
            sb.AppendLine($"{m.Id} | {m.DisplayName} | {m.Version}");
        return sb.ToString();
    }

    private static string BuildFilteredLog() {
        var entries = LogCollector.GetSnapshot();
        var sb = new StringBuilder();
        sb.AppendLine($"Log snapshot — {entries.Count} total entries, suppressed entries excluded");
        sb.AppendLine($"Captured at: {DateTime.Now:O}");
        sb.AppendLine(new string('-', 72));
        sb.AppendLine();

        int suppressed = 0;
        foreach (var e in entries) {
            if (LogSuppressor.IsSuppressed(e.Text)) { suppressed++; continue; }
            var levelTag = e.Level switch {
                LogLevel.Warn  => "WARN ",
                LogLevel.Error => "ERROR",
                _ => "INFO "
            };
            sb.AppendLine($"[{e.Time:HH:mm:ss}] [{levelTag}] {e.Text}");
        }

        sb.AppendLine();
        sb.AppendLine($"--- {entries.Count - suppressed} entries shown, {suppressed} suppressed ---");
        return sb.ToString();
    }

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

    private static string BuildFrameworkBridge() {
        try {
            var s = FrameworkBridge.CaptureSnapshot();
            var sb = new StringBuilder();
            sb.AppendLine("=== Framework Bridge Snapshot ===");
            sb.AppendLine($"Generated: {DateTime.Now:O}");
            sb.AppendLine();
            sb.AppendLine("-- RitsuLib --");
            sb.AppendLine($"  Display name      : {s.RitsuDisplayName}");
            sb.AppendLine($"  Manifest version  : {s.RitsuManifestVersion}");
            sb.AppendLine($"  Assembly version  : {s.RitsuLibAssemblyVersion}");
            sb.AppendLine($"  Framework mod id  : {s.RitsuLibFrameworkModId}");
            sb.AppendLine($"  Settings root key : {s.RitsuSettingsRootKey}");
            sb.AppendLine($"  Settings file     : {s.RitsuSettingsFileName}");
            sb.AppendLine($"  Initialized       : {s.RitsuLibInitialized}");
            sb.AppendLine($"  Active            : {s.RitsuLibActive}");
            sb.AppendLine($"  Mod settings pages: {s.RitsuLibModSettingsPageCount} ({s.RitsuLibDistinctOwningModCount} mods, {s.RitsuLibTotalSectionCount} sections)");
            sb.AppendLine();
            sb.AppendLine("  Page inventory (owning mod | page id | sections | sort | parent | title):");
            foreach (var line in s.RitsuLibPagesInventoryLines.Split('\n'))
                sb.AppendLine("    " + line);
            sb.AppendLine();
            sb.AppendLine("-- Harmony (process-wide) --");
            sb.AppendLine($"  Patched methods   : {s.HarmonyStats.PatchedMethodCount}");
            sb.AppendLine($"  Total operations  : {s.HarmonyStats.TotalPatchOperations}");
            sb.AppendLine($"  Prefixes          : {s.HarmonyStats.PrefixCount}");
            sb.AppendLine($"  Postfixes         : {s.HarmonyStats.PostfixCount}");
            sb.AppendLine($"  Transpilers       : {s.HarmonyStats.TranspilerCount}");
            sb.AppendLine($"  Finalizers        : {s.HarmonyStats.FinalizerCount}");
            return sb.ToString();
        }
        catch (Exception ex) {
            return $"(error capturing bridge snapshot: {ex.Message})";
        }
    }

    /// <summary>
    /// Reads the tail of a game log file (last <see cref="LogTailBytes"/> bytes),
    /// starting from a clean line boundary. Uses <see cref="FileShare.ReadWrite"/>
    /// so the game's open handle doesn't block us.
    /// </summary>
    private static string ReadLogTail(string path) {
        try {
            using var fs = new FileStream(path, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            var truncated = fs.Length > LogTailBytes;
            if (truncated)
                fs.Seek(-LogTailBytes, SeekOrigin.End);

            using var reader = new StreamReader(fs, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            if (truncated)
                reader.ReadLine(); // skip possibly-incomplete first line

            var content = reader.ReadToEnd();
            var sb = new StringBuilder();
            if (truncated)
                sb.AppendLine($"(showing last {LogTailBytes / 1024} KB of file — {fs.Length / 1024} KB total)");
            sb.Append(content);
            return sb.ToString();
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
        // Normalize to forward slashes for matching, then replace both variants
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
