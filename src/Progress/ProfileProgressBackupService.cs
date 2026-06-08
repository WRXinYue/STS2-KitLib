using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using KitLib.Modding;
using Godot;
using MegaCrit.Sts2.Core.Runs;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Progress;

internal enum ModChangeTriggerReason {
    Startup,
    SafetyNet,
}

internal readonly record struct ProfileBackupSummary(
    string DirectoryName,
    string BackupDirectory,
    DateTimeOffset UtcTimestamp,
    bool HasProgressSave);

internal enum ProgressRestoreResult {
    Success,
    BlockedRunInProgress,
    MissingProgress,
    IoError,
    LoadFailed,
}

internal sealed class ProfileBackupMeta {
    public DateTimeOffset UtcTimestamp { get; set; }
    public int ProfileId { get; set; }
    public string TriggerReason { get; set; } = "";
    public string FingerprintHash { get; set; } = "";
    public List<string> CopiedFiles { get; set; } = [];
    public List<ModFingerprintEntry> Mods { get; set; } = [];
}

internal static class ProfileProgressBackupService {
    private const int MaxBackupsPerProfile = 10;

    private static readonly (string FileName, string ScopedPath)[] OptionalFiles = [
        ("prefs.save", "saves/prefs.save"),
        ("current_run.save", "saves/current_run.save"),
    ];

    public static string? BackupActiveProfile(
        int profileId,
        ModChangeTriggerReason reason,
        string fingerprintHash,
        IReadOnlyList<KitLibModInfo> mods) {
        try {
            var progressPath = ResolveProfileScopedPath(profileId, "saves/progress.save");
            if (!File.Exists(progressPath)) {
                MainFile.Logger.Info(
                    $"[ModChangeGuard] No progress.save for profile {profileId}; skipping backup.");
                return null;
            }

            var stamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd_HHmmss");
            var backupDir = Path.Combine(DataPaths.ProfileBackupsDir, $"{stamp}_profile{profileId}");
            Directory.CreateDirectory(backupDir);

            var copied = new List<string>();
            CopyIfExists(progressPath, Path.Combine(backupDir, "progress.save"), copied);

            foreach (var (fileName, scopedPath) in OptionalFiles) {
                var source = ResolveProfileScopedPath(profileId, scopedPath);
                CopyIfExists(source, Path.Combine(backupDir, fileName), copied);
            }

            var meta = new ProfileBackupMeta {
                UtcTimestamp = DateTimeOffset.UtcNow,
                ProfileId = profileId,
                TriggerReason = reason.ToString(),
                FingerprintHash = fingerprintHash,
                CopiedFiles = copied,
                Mods = mods.Select(m => new ModFingerprintEntry {
                    Id = m.Id,
                    Version = m.Version,
                    Dependencies = m.Dependencies.Count == 0 ? [] : m.Dependencies.ToList(),
                }).ToList(),
            };

            var metaPath = Path.Combine(backupDir, "backup_meta.json");
            File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            }));

            TrimOldBackups(profileId);

            MainFile.Logger.Info(
                $"[ModChangeGuard] Backed up profile {profileId} progress to {backupDir} ({reason}).");
            return backupDir;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Profile backup failed: {ex.Message}");
            return null;
        }
    }

    public static string ResolveProfileScopedPath(int profileId, string scopedPath) {
        var godotPath = UserDataPathProvider.GetProfileScopedPath(profileId, scopedPath);
        return ProjectSettings.GlobalizePath(godotPath);
    }

    public static IReadOnlyList<ProfileBackupSummary> ListRecentBackups(int profileId, int maxCount = 5) {
        if (maxCount <= 0 || !Directory.Exists(DataPaths.ProfileBackupsDir))
            return Array.Empty<ProfileBackupSummary>();

        var suffix = $"_profile{profileId}";
        var summaries = new List<ProfileBackupSummary>(maxCount);

        foreach (var dir in Directory.GetDirectories(DataPaths.ProfileBackupsDir)) {
            var name = Path.GetFileName(dir);
            if (!name.EndsWith(suffix, StringComparison.Ordinal))
                continue;

            var stampPart = name.AsSpan(0, name.Length - suffix.Length);
            if (!DateTimeOffset.TryParseExact(
                    stampPart,
                    "yyyyMMdd_HHmmss",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.AssumeUniversal,
                    out var timestamp)) {
                continue;
            }

            summaries.Add(new ProfileBackupSummary(
                name,
                dir,
                timestamp,
                File.Exists(Path.Combine(dir, "progress.save"))));
        }

        summaries.Sort((a, b) => b.UtcTimestamp.CompareTo(a.UtcTimestamp));
        if (summaries.Count <= maxCount)
            return summaries;

        return summaries.GetRange(0, maxCount);
    }

    public static string GetBackupDirectory(string directoryName) =>
        Path.Combine(DataPaths.ProfileBackupsDir, directoryName);

    public static ProfileBackupMeta? TryLoadMeta(string backupDir) {
        try {
            var metaPath = Path.Combine(backupDir, "backup_meta.json");
            if (!File.Exists(metaPath))
                return null;

            return JsonSerializer.Deserialize<ProfileBackupMeta>(File.ReadAllText(metaPath), new JsonSerializerOptions {
                PropertyNameCaseInsensitive = true,
            });
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Failed to read backup meta: {ex.Message}");
            return null;
        }
    }

    public static ProgressRestoreResult TryRestoreProgress(string backupDir, int profileId) {
        if (RunManager.Instance?.IsInProgress == true)
            return ProgressRestoreResult.BlockedRunInProgress;

        var sourcePath = Path.Combine(backupDir, "progress.save");
        if (!File.Exists(sourcePath))
            return ProgressRestoreResult.MissingProgress;

        var targetPath = ResolveProfileScopedPath(profileId, "saves/progress.save");
        string? preRestorePath = null;

        try {
            var savesDir = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(savesDir))
                Directory.CreateDirectory(savesDir);

            if (File.Exists(targetPath)) {
                preRestorePath = Path.Combine(
                    savesDir!,
                    $"progress.save.pre_restore_{DateTimeOffset.UtcNow:yyyyMMdd_HHmmss}");
                File.Copy(targetPath, preRestorePath, overwrite: true);
            }

            File.Copy(sourcePath, targetPath, overwrite: true);

            var loadResult = SaveManager.Instance.InitProgressData();
            if (!loadResult.Success) {
                TryRollbackRestore(targetPath, preRestorePath);
                MainFile.Logger.Warn(
                    $"[ModChangeGuard] Restore reload failed: {loadResult.Status}");
                return ProgressRestoreResult.LoadFailed;
            }

            MainFile.Logger.Info($"[ModChangeGuard] Restored progress from {backupDir}.");
            return ProgressRestoreResult.Success;
        }
        catch (Exception ex) {
            TryRollbackRestore(targetPath, preRestorePath);
            MainFile.Logger.Warn($"[ModChangeGuard] Restore failed: {ex.Message}");
            return ProgressRestoreResult.IoError;
        }
    }

    private static void TryRollbackRestore(string targetPath, string? preRestorePath) {
        if (preRestorePath == null || !File.Exists(preRestorePath))
            return;

        try {
            File.Copy(preRestorePath, targetPath, overwrite: true);
            SaveManager.Instance.InitProgressData();
            MainFile.Logger.Info("[ModChangeGuard] Rolled back progress.restore after failed reload.");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Restore rollback failed: {ex.Message}");
        }
    }

    private static void CopyIfExists(string source, string dest, List<string> copied) {
        if (!File.Exists(source))
            return;

        File.Copy(source, dest, overwrite: true);
        copied.Add(Path.GetFileName(dest));
    }

    private static void TrimOldBackups(int profileId) {
        if (!Directory.Exists(DataPaths.ProfileBackupsDir))
            return;

        var suffix = $"_profile{profileId}";
        var dirs = Directory.GetDirectories(DataPaths.ProfileBackupsDir)
            .Where(d => Path.GetFileName(d).EndsWith(suffix, StringComparison.Ordinal))
            .OrderByDescending(d => Path.GetFileName(d), StringComparer.Ordinal)
            .ToList();

        for (var i = MaxBackupsPerProfile; i < dirs.Count; i++) {
            try {
                Directory.Delete(dirs[i], recursive: true);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[ModChangeGuard] Failed to trim old backup '{dirs[i]}': {ex.Message}");
            }
        }
    }
}
