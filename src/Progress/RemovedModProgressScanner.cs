using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using KitLib.Modding;

namespace KitLib.Progress;

internal static class RemovedModProgressScanner {
    public static void WarnIfResidue(
        ModSetFingerprintData? previous,
        IReadOnlyList<KitLibModInfo> currentMods,
        int profileId,
        string? backupDir) {
        if (previous?.Mods == null || previous.Mods.Count == 0)
            return;

        var currentIds = new HashSet<string>(
            currentMods.Select(m => m.Id),
            StringComparer.OrdinalIgnoreCase);

        var removed = previous.Mods
            .Where(m => !string.IsNullOrEmpty(m.Id) && !currentIds.Contains(m.Id))
            .Select(m => m.Id)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (removed.Count == 0)
            return;

        var progressPath = ProfileProgressBackupService.ResolveProfileScopedPath(
            profileId, "saves/progress.save");
        if (!File.Exists(progressPath))
            return;

        string progressText;
        try {
            progressText = File.ReadAllText(progressPath);
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Could not read progress for residue scan: {ex.Message}");
            return;
        }

        var hits = new List<string>();
        foreach (var modId in removed) {
            if (ContainsModResidue(progressText, modId))
                hits.Add(modId);
        }

        if (hits.Count == 0)
            return;

        var backupNote = string.IsNullOrEmpty(backupDir)
            ? "no backup created"
            : $"backup at {backupDir}";

        MainFile.Logger.Warn(
            $"[ModChangeGuard] Mod set changed and progress.save still contains data from unloaded mod(s): " +
            $"{string.Join(", ", hits)}. {backupNote}. " +
            "Vanilla save filtering may remove this progress on the next write.");
    }

    private static bool ContainsModResidue(string progressText, string modId) {
        foreach (var token in BuildSearchTokens(modId)) {
            if (progressText.Contains(token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static IEnumerable<string> BuildSearchTokens(string modId) {
        if (string.IsNullOrEmpty(modId))
            yield break;

        yield return modId;

        var upper = modId.ToUpperInvariant();
        if (!string.Equals(upper, modId, StringComparison.Ordinal))
            yield return upper;

        var underscored = modId.Replace('-', '_');
        yield return underscored;

        var underscoredUpper = underscored.ToUpperInvariant();
        if (!string.Equals(underscoredUpper, underscored, StringComparison.Ordinal))
            yield return underscoredUpper;

        yield return modId + ".";
        yield return modId + "_";
        yield return upper + "_";
    }
}
