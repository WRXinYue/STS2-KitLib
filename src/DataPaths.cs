using System;
using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib;

/// <summary>
/// Resolves writable user-data paths under
/// <c>user://steam/{userId}/mod_data/KitLib/</c>.
/// Migrates legacy <c>mod_data/DevMode/</c> on first access.
/// </summary>
public static class DataPaths {
    private const string LegacyModDataSubdir = "mod_data/DevMode";
    private const string ModDataSubdir = "mod_data/KitLib";

    private static string? _baseDir;

    /// <summary>Absolute filesystem path to the KitLib user-data root.</summary>
    public static string BaseDir => _baseDir ??= ResolveBaseDir();

    public static string SettingsFile => Path.Combine(BaseDir, "settings.json");
    public static string SnapshotsDir => Path.Combine(BaseDir, "snapshots");
    public static string PresetsDir   => Path.Combine(BaseDir, "presets");
    public static string ScriptsDir   => Path.Combine(BaseDir, "scripts");
    public static string FingerprintFile => Path.Combine(BaseDir, "last_mod_fingerprint.json");
    public static string ProfileBackupsDir => Path.Combine(BaseDir, "profile_backups");

    private static string ResolveBaseDir() {
        var kitLibPath = UserDataPathProvider.GetAccountScopedBasePath(ModDataSubdir);
        var kitLibDir = ProjectSettings.GlobalizePath(kitLibPath);
        TryMigrateLegacyDataDir(kitLibDir);
        return kitLibDir;
    }

    private static void TryMigrateLegacyDataDir(string kitLibDir) {
        if (Directory.Exists(kitLibDir) && Directory.EnumerateFileSystemEntries(kitLibDir).GetEnumerator().MoveNext())
            return;

        var legacyPath = UserDataPathProvider.GetAccountScopedBasePath(LegacyModDataSubdir);
        var legacyDir = ProjectSettings.GlobalizePath(legacyPath);
        if (!Directory.Exists(legacyDir))
            return;

        try {
            Directory.CreateDirectory(Path.GetDirectoryName(kitLibDir)!);
            if (Directory.Exists(kitLibDir))
                Directory.Delete(kitLibDir, recursive: true);
            Directory.Move(legacyDir, kitLibDir);
            MainFile.Logger.Info($"[KitLib] Migrated user data from {LegacyModDataSubdir} to {ModDataSubdir}.");
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] Legacy data migration failed: {ex.Message}");
        }
    }
}
