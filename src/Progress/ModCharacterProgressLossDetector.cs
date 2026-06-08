using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Progress;

internal sealed record ModCharacterProgressLossResult(
    ProfileBackupSummary Backup,
    IReadOnlyList<string> LostCharacterNames);

internal static class ModCharacterProgressLossDetector {
    internal static ModCharacterProgressLossResult? Pending { get; private set; }

    internal static void DetectAfterProgressLoad() {
        Pending = TryDetect();
        if (Pending != null) {
            var names = string.Join(", ", Pending.LostCharacterNames);
            if (SettingsStore.Current.PromptOnModCharacterProgressLoss) {
                MainFile.Logger.Info(
                    $"[ProgressGuard] Mod character progress loss detected ({Pending.LostCharacterNames.Count}): {names}. " +
                    $"Backup: {Pending.Backup.DirectoryName}.");
            }
            else {
                MainFile.Logger.Info(
                    $"[ProgressGuard] Mod character progress loss detected ({Pending.LostCharacterNames.Count}): {names}. " +
                    "Startup prompt disabled in settings.");
            }
            return;
        }

        MainFile.Logger.Info(
            $"[ProgressGuard] Startup loss scan: no restorable mod character loss " +
            $"(prompt={(SettingsStore.Current.PromptOnModCharacterProgressLoss ? "on" : "off")}).");
    }

    internal static void ClearPending() => Pending = null;

    private static ModCharacterProgressLossResult? TryDetect() {
        try {
            int profileId = SaveManager.Instance.CurrentProfileId;
            var loaded = ResolveLoadedStats(profileId);
            int backupsScanned = 0;

            foreach (var backup in EnumerateCandidateBackups(profileId)) {
                if (!backup.HasProgressSave)
                    continue;

                backupsScanned++;
                var result = TryFindLossInBackup(backup, loaded);
                if (result != null)
                    return result;
            }

            LogScanSummary(profileId, backupsScanned, loaded);
            LogNoLoss(profileId);
            return null;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ProgressGuard] Mod character loss detection failed: {ex.Message}");
            return null;
        }
    }

    private static IReadOnlyDictionary<ModelId, CharacterStats> ResolveLoadedStats(int profileId) {
        var diskStats = TryLoadProgressStatsFromDisk(profileId);
        if (diskStats != null)
            return diskStats;

        return SaveManager.Instance.Progress.CharacterStats;
    }

    private static Dictionary<ModelId, CharacterStats>? TryLoadProgressStatsFromDisk(int profileId) {
        try {
            var progressPath = ProfileProgressBackupService.ResolveProfileScopedPath(
                profileId, "saves/progress.save");
            if (!File.Exists(progressPath))
                return null;

            return TryLoadStatsFromFile(progressPath);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ProgressGuard] Could not read active progress.save: {ex.Message}");
            return null;
        }
    }

    private static void LogScanSummary(
        int profileId,
        int backupsScanned,
        IReadOnlyDictionary<ModelId, CharacterStats> loaded) {
        if (backupsScanned == 0) {
            MainFile.Logger.Info(
                $"[ProgressGuard] Startup loss scan: no backups found for profile {profileId}.");
            return;
        }

        foreach (var backup in ProfileProgressBackupService.ListRecentBackups(profileId, maxCount: 3)) {
            var backupStats = TryLoadBackupStats(backup.BackupDirectory);
            if (backupStats == null)
                continue;

            foreach (var (id, backupStat) in backupStats) {
                if (!ModCharacterCatalog.IsModCharacterId(id))
                    continue;
                if (!CharacterProgressActivity.HasActivity(backupStat))
                    continue;

                loaded.TryGetValue(id, out var loadedStat);
                var loadedSummary = loadedStat == null
                    ? "missing"
                    : $"A{loadedStat.MaxAscension} {loadedStat.TotalWins}W/{loadedStat.TotalLosses}L";
                var backupSummary =
                    $"A{backupStat.MaxAscension} {backupStat.TotalWins}W/{backupStat.TotalLosses}L";

                MainFile.Logger.Info(
                    $"[ProgressGuard] Startup loss scan: backup {backup.DirectoryName} has mod char " +
                    $"{ModCharacterCatalog.ResolveCharacterName(id)} ({backupSummary}); loaded={loadedSummary}.");
            }
        }
    }

    private static ModCharacterProgressLossResult? TryFindLossInBackup(
        ProfileBackupSummary backup,
        IReadOnlyDictionary<ModelId, CharacterStats> loaded) {
        var backupStats = TryLoadBackupStats(backup.BackupDirectory);
        if (backupStats == null)
            return null;

        var lostNames = new List<string>();

            foreach (var (id, backupStat) in backupStats) {
                if (!ModCharacterCatalog.IsModCharacterId(id))
                    continue;
                if (!CharacterProgressActivity.IsMissingOrDegraded(loaded, id, backupStat))
                    continue;

                lostNames.Add(ModCharacterCatalog.ResolveCharacterName(id));
            }

            if (lostNames.Count == 0)
                return null;

            MainFile.Logger.Info(
                $"[ProgressGuard] Recoverable mod character loss found in backup {backup.DirectoryName}.");
            return new ModCharacterProgressLossResult(backup, lostNames);
    }

    private static IEnumerable<ProfileBackupSummary> EnumerateCandidateBackups(int profileId) {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var sessionDir = ModChangeGuard.LastSessionBackupDir;
        if (!string.IsNullOrEmpty(sessionDir)
            && File.Exists(Path.Combine(sessionDir, "progress.save"))
            && TryParseBackupSummary(Path.GetFileName(sessionDir), sessionDir, profileId, out var sessionBackup)) {
            seen.Add(sessionDir);
            yield return sessionBackup;
        }

        foreach (var backup in ProfileProgressBackupService.ListRecentBackups(profileId, maxCount: 10)) {
            if (seen.Add(backup.BackupDirectory))
                yield return backup;
        }
    }

    private static void LogNoLoss(int profileId) {
        if (!ModChangeGuard.ModSetChangedThisSession)
            return;

        MainFile.Logger.Info(
            $"[ProgressGuard] Mod set changed this session but no restorable mod character loss " +
            $"(scanned recent backups for profile {profileId}; latest backup may already be filtered).");
    }

    private static bool TryParseBackupSummary(
        string name,
        string dir,
        int profileId,
        out ProfileBackupSummary summary) {
        summary = default;
        var suffix = $"_profile{profileId}";
        if (!name.EndsWith(suffix, StringComparison.Ordinal))
            return false;

        var stampPart = name.AsSpan(0, name.Length - suffix.Length);
        if (!DateTimeOffset.TryParseExact(
                stampPart,
                "yyyyMMdd_HHmmss",
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AssumeUniversal,
                out var timestamp)) {
            return false;
        }

        summary = new ProfileBackupSummary(name, dir, timestamp, true);
        return true;
    }

    private static Dictionary<ModelId, CharacterStats>? TryLoadBackupStats(string backupDir) {
        var progressPath = Path.Combine(backupDir, "progress.save");
        if (!File.Exists(progressPath))
            return null;

        try {
            return TryLoadStatsFromFile(progressPath);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ProgressGuard] Could not read backup stats: {ex.Message}");
            return null;
        }
    }

    private static Dictionary<ModelId, CharacterStats>? TryLoadStatsFromFile(string progressPath) {
        var json = File.ReadAllText(progressPath);
        var result = SaveManager.FromJson<SerializableProgress>(json);
        if (!result.Success || result.SaveData?.CharStats == null)
            return null;

        var statsById = new Dictionary<ModelId, CharacterStats>();
        foreach (var stat in result.SaveData.CharStats) {
            if (stat.Id is not { } id || id == ModelId.none)
                continue;
            if (statsById.TryGetValue(id, out var existing))
                MergeCharacterStats(existing, stat);
            else
                statsById[id] = stat;
        }

        return statsById;
    }

    private static void MergeCharacterStats(CharacterStats existing, CharacterStats incoming) {
        existing.TotalWins = Math.Max(existing.TotalWins, incoming.TotalWins);
        existing.TotalLosses = Math.Max(existing.TotalLosses, incoming.TotalLosses);
        existing.Playtime = Math.Max(existing.Playtime, incoming.Playtime);
        existing.MaxAscension = Math.Max(existing.MaxAscension, incoming.MaxAscension);
        existing.BestWinStreak = Math.Max(existing.BestWinStreak, incoming.BestWinStreak);
        existing.CurrentWinStreak = Math.Max(existing.CurrentWinStreak, incoming.CurrentWinStreak);
        existing.PreferredAscension = Math.Max(existing.PreferredAscension, incoming.PreferredAscension);
    }
}

internal static class ModCharacterCatalog {
    private static readonly Lazy<HashSet<ModelId>> VanillaCharacterIds = new(() => new HashSet<ModelId> {
        ModelDb.GetId<Ironclad>(),
        ModelDb.GetId<Silent>(),
        ModelDb.GetId<Defect>(),
        ModelDb.GetId<Regent>(),
        ModelDb.GetId<Necrobinder>(),
        ModelDb.GetId<RandomCharacter>(),
        ModelDb.GetId<Deprived>(),
        ModelDb.GetId<DeprecatedCharacter>(),
    });

    public static IEnumerable<CharacterModel> EnumerateModCharacters() =>
        ModelDb.AllAbstractModelSubtypes
            .Where(t => t.IsSubclassOf(typeof(CharacterModel)) && !t.IsAbstract)
            .Select(t => ModelDb.GetByIdOrNull<CharacterModel>(ModelDb.GetId(t)))
            .Where(c => c is not null)
            .Select(c => c!)
            .Where(c => c is not RandomCharacter and not DeprecatedCharacter and not Deprived)
            .Where(c => IsModCharacterId(c.Id))
            .OrderBy(GetDisplayName, StringComparer.OrdinalIgnoreCase);

    public static bool IsModCharacterId(ModelId id) {
        if (id == ModelId.none)
            return false;

        return !VanillaCharacterIds.Value.Contains(id);
    }

    public static string GetDisplayName(CharacterModel character) {
        try {
            return character.Title.GetFormattedText();
        }
        catch {
            return character.Id.Entry;
        }
    }

    public static string ResolveCharacterName(ModelId? id) {
        if (id is not { } modelId || modelId == ModelId.none)
            return "?";

        var character = ModelDb.GetByIdOrNull<CharacterModel>(modelId);
        if (character != null)
            return GetDisplayName(character);

        if (!string.IsNullOrEmpty(modelId.Entry))
            return modelId.Entry.Replace('_', ' ');
        return modelId.ToString();
    }
}
