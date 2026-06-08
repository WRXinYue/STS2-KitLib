using System;
using System.Collections.Generic;
using KitLib.Modding;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.Progress;

internal static class ModChangeGuard {
    internal static bool CompletedForSession { get; private set; }

    internal static bool ModSetChangedThisSession { get; private set; }

    internal static string? LastSessionBackupDir { get; private set; }

    public static void TryRun(ModChangeTriggerReason reason) {
        if (CompletedForSession)
            return;

        try {
            var settings = SettingsStore.Current;
            var mods = ModRuntime.Catalog.GetSnapshot();
            var hash = ModSetFingerprintStore.ComputeHash(mods);
            var stored = ModSetFingerprintStore.Load();

            if (stored != null && string.Equals(stored.Hash, hash, StringComparison.Ordinal)) {
                ModSetChangedThisSession = false;
                CompletedForSession = true;
                return;
            }

            ModSetChangedThisSession = true;

            var autoBackup = settings.AutoBackupProgressOnModChange;
            var warnResidue = settings.WarnOnRemovedModProgressResidue;

            if (!autoBackup && !warnResidue) {
                ModSetFingerprintStore.Save(mods, hash);
                CompletedForSession = true;
                return;
            }

            int profileId = SaveManager.Instance.CurrentProfileId;
            string? backupDir = null;

            if (autoBackup) {
                backupDir = ProfileProgressBackupService.BackupActiveProfile(
                    profileId, reason, hash, mods);
                if (!string.IsNullOrEmpty(backupDir))
                    LastSessionBackupDir = backupDir;
            }

            if (warnResidue)
                RemovedModProgressScanner.WarnIfResidue(stored, mods, profileId, backupDir);

            ModSetFingerprintStore.Save(mods, hash);
            CompletedForSession = true;
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[ModChangeGuard] Guard run failed ({reason}): {ex.Message}");
        }
    }
}
