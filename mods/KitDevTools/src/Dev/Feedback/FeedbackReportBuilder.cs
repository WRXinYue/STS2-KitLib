using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using Godot;
using KitLib.Combat;
using KitLib.CombatStats;
using KitLib.Interop;
using KitLib.Modding;

namespace KitLib.Feedback;

/// <summary>
/// Collects Harmony dump, combat stats, screenshot, attachments, checkpoints, and game log
/// into a ZIP under <c>user://devmode-reports/</c>.
/// Heavy work runs on a background thread; the returned path is the ZIP file path.
/// </summary>
internal static class FeedbackReportBuilder {
    private const string ReportsDir = "devmode-reports";
    private const int MaxExtraImageBytes = 8 * 1024 * 1024;

    private static readonly JsonSerializerOptions MetaJson = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public readonly record struct NamedBlob(string FileName, byte[] Bytes);

    public readonly record struct BuildRequest(
        string LogFilePath,
        bool PrivacyMode,
        string? Description = null,
        string? Category = null,
        string? Mood = null,
        byte[]? ScreenshotPng = null,
        IReadOnlyList<NamedBlob>? ExtraImages = null);

    public static IReadOnlyList<(string DisplayName, string AbsPath, bool IsCurrentSession)> ScanLogFiles()
        => GameLogFileHydrator.ScanLogFiles()
            .Select(f => (f.DisplayName, f.AbsPath, f.IsCurrentSession))
            .ToList();

    public static string Build(BuildRequest req) {
        var userDataDir = OS.GetUserDataDir();
        var reportsPath = Path.Combine(userDataDir, ReportsDir);
        Directory.CreateDirectory(reportsPath);

        var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(reportsPath, $"devmode-report-{ts}.zip");

        using var stream = new FileStream(zipPath, FileMode.CreateNew, System.IO.FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        var checkpointDir = CombatCheckpointStore.TryGetExportDirectory();
        WriteEntry(archive, "report-meta.json", BuildReportMeta(req, checkpointDir), req, userDataDir);
        WriteEntry(archive, "harmony-patches.txt", BuildHarmonyDump(), req, userDataDir);
        WriteEntry(archive, "combat-stats.json", BuildCombatStatsJson(), req, userDataDir);

        if (req.ScreenshotPng is { Length: > 0 })
            WriteBinary(archive, "screenshot.png", req.ScreenshotPng);

        WriteExtraImages(archive, req.ExtraImages);

        if (checkpointDir != null)
            WriteCheckpointDir(archive, checkpointDir, req, userDataDir);

        if (!File.Exists(req.LogFilePath))
            throw new FileNotFoundException("Game log file not found.", req.LogFilePath);

        var logName = Path.GetFileName(req.LogFilePath);
        WriteEntry(archive, logName, ReadLogFile(req.LogFilePath), req, userDataDir);

        return zipPath;
    }

    internal static NamedBlob? TryReadImageFile(string path) {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            return null;
        var info = new FileInfo(path);
        if (info.Length <= 0 || info.Length > MaxExtraImageBytes)
            return null;
        var name = SanitizeFileName(info.Name);
        return new NamedBlob(name, File.ReadAllBytes(path));
    }

    private static string BuildReportMeta(BuildRequest req, string? checkpointDir) {
        var mods = ModRuntime.Catalog.GetSnapshot()
            .Select(m => new {
                id = m.Id,
                name = m.DisplayName,
                version = m.Version,
            })
            .ToList();

        FrameworkBridge.FrameworkBridgeSnapshot fw;
        try {
            fw = FrameworkBridge.CaptureSnapshot();
        }
        catch (Exception ex) {
            fw = default;
            KitLog.Warn("Feedback", $"Framework snapshot failed: {ex.Message}");
        }

        var extra = req.ExtraImages ?? Array.Empty<NamedBlob>();
        var meta = new {
            exportedAt = DateTimeOffset.Now.ToString("o"),
            description = req.Description ?? "",
            category = string.IsNullOrWhiteSpace(req.Category) ? "other" : req.Category,
            mood = string.IsNullOrWhiteSpace(req.Mood) ? "none" : req.Mood,
            privacyMode = req.PrivacyMode,
            screenshot = req.ScreenshotPng is { Length: > 0 },
            extraImages = extra.Select(b => b.FileName).ToList(),
            combatCheckpoint = checkpointDir != null,
            gameAssembly = typeof(MegaCrit.Sts2.Core.Nodes.NGame).Assembly.GetName().Version?.ToString() ?? "",
            kitLibAssembly = typeof(KitLibState).Assembly.GetName().Version?.ToString() ?? "",
            mods,
            framework = new {
                available = FrameworkBridge.IsAvailable,
                ritsu = fw.RitsuDisplayName,
                ritsuVersion = fw.RitsuManifestVersion,
                initialized = fw.RitsuLibInitialized,
                harmonyPatchedMethods = fw.HarmonyStats.PatchedMethodCount,
            },
        };
        return JsonSerializer.Serialize(meta, MetaJson);
    }

    private static string BuildHarmonyDump() {
        var report = HarmonyPatchReportBuilder.BuildReport(out var error);
        return string.IsNullOrEmpty(error) ? report : $"(error generating report: {error})";
    }

    private static string BuildCombatStatsJson() {
        try {
            if (!KitLib.KitLibState.IsActive)
                return "{\"note\":\"Dev Mode inactive during report\"}";
            return CombatStatsExport.ToJson(CombatStatsExport.CaptureBundle());
        }
        catch (Exception ex) {
            return $"{{\"error\":\"{ex.Message}\"}}";
        }
    }

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

    private static void WriteExtraImages(ZipArchive archive, IReadOnlyList<NamedBlob>? images) {
        if (images == null || images.Count == 0)
            return;

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int i = 0;
        foreach (var blob in images) {
            if (blob.Bytes is not { Length: > 0 } || blob.Bytes.Length > MaxExtraImageBytes)
                continue;
            var name = UniqueName(SanitizeFileName(blob.FileName), used);
            WriteBinary(archive, $"attachments/{name}", blob.Bytes);
            i++;
            if (i >= 8)
                break;
        }
    }

    private static void WriteCheckpointDir(
        ZipArchive archive, string dir, BuildRequest req, string userDataDir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var zipName = "combat-checkpoint/" + rel;
            var ext = Path.GetExtension(file);
            if (req.PrivacyMode && (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase))) {
                WriteEntry(archive, zipName, File.ReadAllText(file), req, userDataDir);
            }
            else {
                WriteBinary(archive, zipName, File.ReadAllBytes(file));
            }
        }
    }

    private static string SanitizeFileName(string name) {
        var file = Path.GetFileName(name);
        if (string.IsNullOrWhiteSpace(file))
            file = "image.png";
        foreach (var c in Path.GetInvalidFileNameChars())
            file = file.Replace(c, '_');
        return file;
    }

    private static string UniqueName(string name, HashSet<string> used) {
        if (used.Add(name))
            return name;
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        int n = 2;
        while (true) {
            var candidate = $"{stem}-{n}{ext}";
            if (used.Add(candidate))
                return candidate;
            n++;
        }
    }

    private static string Redact(string text, string userDataDir) {
        var fwd = userDataDir.Replace('\\', '/');
        var bwd = userDataDir.Replace('/', '\\');
        text = text.Replace(fwd, "<user-data>", StringComparison.OrdinalIgnoreCase);
        if (bwd != fwd)
            text = text.Replace(bwd, "<user-data>", StringComparison.OrdinalIgnoreCase);
        return text;
    }

    private static void WriteEntry(ZipArchive archive, string name, string content,
        BuildRequest req, string userDataDir) {
        if (req.PrivacyMode)
            content = Redact(content, userDataDir);

        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write(content);
    }

    private static void WriteBinary(ZipArchive archive, string name, byte[] bytes) {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }
}
