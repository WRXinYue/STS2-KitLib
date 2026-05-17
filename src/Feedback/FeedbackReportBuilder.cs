using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using DevMode.Interop;
using DevMode.Modding;
using Godot;
using MegaCrit.Sts2.Core.Logging;

namespace DevMode.Feedback;

/// <summary>
/// Collects filtered log, Harmony patch dump, framework bridge snapshot, and mod list,
/// then packages them into a ZIP under <c>user://devmode-reports/</c>.
/// All heavy work runs on a background thread; the returned path is the ZIP file path.
/// </summary>
internal static class FeedbackReportBuilder {
    private const string ReportsDir = "devmode-reports";
    private const int MaxLogEntries = 2000;

    public readonly record struct BuildRequest(string Title, string Description);

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

        WriteEntry(archive, "report.txt", BuildReportHeader(req));
        WriteEntry(archive, "mods.txt", BuildModList());
        WriteEntry(archive, "logs-filtered.txt", BuildFilteredLog());
        WriteEntry(archive, "harmony-patches.txt", BuildHarmonyDump());
        WriteEntry(archive, "framework-bridge.txt", BuildFrameworkBridge());

        return zipPath;
    }

    // ── Section builders ─────────────────────────────────────────────────

    private static string BuildReportHeader(BuildRequest req) {
        var sb = new StringBuilder();
        sb.AppendLine("=== DevMode Feedback Report ===");
        sb.AppendLine($"Generated : {DateTime.Now:O}");
        sb.AppendLine($"DevMode   : {MainFile.ModID}");
        sb.AppendLine($"OS        : {OS.GetName()} {OS.GetVersion()}");
        sb.AppendLine($"User data : {OS.GetUserDataDir()}");
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

    // ── Helpers ───────────────────────────────────────────────────────────

    private static void WriteEntry(ZipArchive archive, string name, string content) {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }
}
