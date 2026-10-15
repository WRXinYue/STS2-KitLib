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
using MegaCrit.Sts2.Core.Logging;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Saves.Managers;

namespace KitLib.Feedback;

/// <summary>
/// Collects Harmony dump, combat stats, screenshot, attachments, checkpoints, and game log
/// into a ZIP under <c>user://devmode-reports/</c>.
/// Heavy work runs on a background thread; the returned path is the ZIP file path.
/// </summary>
internal static class FeedbackReportBuilder {
    private const string ReportsDir = "devmode-reports";
    private const int MaxExtraImageBytes = 8 * 1024 * 1024;

    internal static string ReportsDirectory => Path.Combine(OS.GetUserDataDir(), ReportsDir);

    private static readonly JsonSerializerOptions MetaJson = new() {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public readonly record struct NamedBlob(string FileName, byte[] Bytes);

    public readonly record struct BuildRequest(
        string LogFilePath,
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
        var reportsPath = ReportsDirectory;
        Directory.CreateDirectory(reportsPath);

        var ts = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(reportsPath, $"devmode-report-{ts}.zip");

        using var stream = new FileStream(zipPath, FileMode.CreateNew, System.IO.FileAccess.Write);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: false);

        var checkpointDir = CombatCheckpointStore.TryGetExportDirectory();
        WriteEntry(archive, "report-meta.json", BuildReportMeta(req, checkpointDir));
        WriteEntry(archive, "harmony-patches.txt", BuildHarmonyDump());
        WriteEntry(archive, "combat-stats.json", BuildCombatStatsJson());

        if (req.ScreenshotPng is { Length: > 0 })
            WriteBinary(archive, "screenshot.png", req.ScreenshotPng);

        WriteExtraImages(archive, req.ExtraImages);

        if (checkpointDir != null)
            WriteCheckpointDir(archive, checkpointDir);

        WriteOfficialGameFiles(archive);

        if (!File.Exists(req.LogFilePath))
            throw new FileNotFoundException("Game log file not found.", req.LogFilePath);

        var logName = Path.GetFileName(req.LogFilePath);
        WriteEntry(archive, logName, ReadLogFile(req.LogFilePath));

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

    /// <summary>
    /// Flush official <c>replays/latest.mcr</c> while still on the game thread.
    /// Dev / pseudo-coop runs skip the write via <c>DevCombatReplaySkipPatch</c>.
    /// </summary>
    internal static void FlushOfficialReplay() {
        try {
            var rm = RunManager.Instance;
            if (rm is { IsInProgress: true } && rm.CombatReplayWriter.IsRecordingReplay)
                rm.WriteReplay(stopRecording: false);
        }
        catch (Exception ex) {
            KitLog.Warn("Feedback", $"Official replay flush failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Same save set as <c>GetLogsConsoleCmd.ZipFeedbackLogs</c>: current profile, settings,
    /// progress, run, prefs, <c>latest.mcr</c>, newest history, recent <c>.corrupt</c>, and
    /// <c>release_info.json</c>. JSON is passed through <see cref="LogSanitizer"/>.
    /// </summary>
    private static void WriteOfficialGameFiles(ZipArchive archive) {
        try {
            var accountBase = ProjectSettings.GlobalizePath(UserDataPathProvider.GetAccountScopedBasePath(""));
            if (string.IsNullOrEmpty(accountBase))
                return;

            int profileId;
            try {
                profileId = SaveManager.Instance.CurrentProfileId;
            }
            catch (Exception) {
                return;
            }

            var relatives = new List<string> {
#if STS2_STABLE_PROFILE
                ProfileSaveManager.ProfilePath,
#else
                ProfileSaveManager.GetProfileSavePath(),
#endif
                "settings.save",
                ProgressSaveManager.GetProgressPathForProfile(profileId),
                RunSaveManager.GetRunSavePath(profileId, "current_run.save"),
                RunSaveManager.GetRunSavePath(profileId, "current_run_mp.save"),
                PrefsSaveManager.GetPrefsPath(profileId),
                Path.Combine(UserDataPathProvider.GetProfileDir(profileId), "replays/latest.mcr"),
            };

            var historyDir = Path.Combine(accountBase, RunHistorySaveManager.GetHistoryPath(profileId));
            if (Directory.Exists(historyDir)) {
                var newest = Directory.EnumerateFiles(historyDir, "*", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTime)
                    .FirstOrDefault();
                if (newest != null)
                    relatives.Add(Path.GetRelativePath(accountBase, newest));
            }

            if (Directory.Exists(accountBase)) {
                foreach (var file in Directory.EnumerateFiles(accountBase, "*", SearchOption.AllDirectories)) {
                    if (file.EndsWith(".corrupt", StringComparison.Ordinal)
                        && DateTimeOffset.Now - File.GetLastWriteTime(file) < TimeSpan.FromDays(1))
                        relatives.Add(Path.GetRelativePath(accountBase, file));
                }
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in relatives) {
                if (string.IsNullOrWhiteSpace(rel) || !seen.Add(rel))
                    continue;
                var abs = Path.Combine(accountBase, rel);
                if (!File.Exists(abs))
                    continue;
                WriteDiskFile(archive, abs, "saves/" + rel.Replace('\\', '/'));
            }

            var releaseInfo = Path.Combine(OS.GetExecutablePath().GetBaseDir(), "release_info.json");
            if (File.Exists(releaseInfo))
                WriteDiskFile(archive, releaseInfo, "release_info.json");
        }
        catch (Exception ex) {
            KitLog.Warn("Feedback", $"Official save pack failed: {ex.Message}");
        }
    }

    private static void WriteDiskFile(ZipArchive archive, string absPath, string zipName) {
        zipName = zipName.Replace('\\', '/');
        try {
            if (Path.GetExtension(absPath).Equals(".json", StringComparison.OrdinalIgnoreCase)) {
                WriteEntry(archive, zipName, File.ReadAllText(absPath));
                return;
            }

            using var src = new FileStream(absPath, FileMode.Open, System.IO.FileAccess.Read, FileShare.ReadWrite);
            var entry = archive.CreateEntry(zipName, CompressionLevel.Optimal);
            using var dest = entry.Open();
            src.CopyTo(dest);
        }
        catch (Exception ex) {
            KitLog.Warn("Feedback", $"Could not pack '{zipName}': {ex.Message}");
        }
    }

    private static void WriteCheckpointDir(ZipArchive archive, string dir) {
        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)) {
            var rel = Path.GetRelativePath(dir, file).Replace('\\', '/');
            var zipName = "combat-checkpoint/" + rel;
            var ext = Path.GetExtension(file);
            if (ext.Equals(".json", StringComparison.OrdinalIgnoreCase)
                || ext.Equals(".txt", StringComparison.OrdinalIgnoreCase)) {
                WriteEntry(archive, zipName, File.ReadAllText(file));
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

    private static void WriteEntry(ZipArchive archive, string name, string content) {
        content = LogSanitizer.Sanitize(content);

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
