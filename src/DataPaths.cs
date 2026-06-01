using System.IO;
using Godot;
using MegaCrit.Sts2.Core.Saves;

namespace DevMode;

/// <summary>
/// Resolves all writable user-data paths for DevMode to
/// <c>user://steam/{userId}/mod_data/DevMode/</c>
/// (e.g. <c>~/Library/Application Support/SlayTheSpire2/steam/{userId}/mod_data/DevMode/</c>).
/// All paths are lazily resolved after Godot and platform services are initialized.
/// </summary>
internal static class DataPaths {
    private static string? _baseDir;

    /// <summary>
    /// Absolute filesystem path to the DevMode user-data root directory.
    /// </summary>
    public static string BaseDir => _baseDir ??= ResolveBaseDir();

    public static string SettingsFile => Path.Combine(BaseDir, "settings.json");
    public static string SnapshotsDir => Path.Combine(BaseDir, "snapshots");
    public static string PresetsDir   => Path.Combine(BaseDir, "presets");
    public static string ScriptsDir   => Path.Combine(BaseDir, "scripts");
    public static string FingerprintFile => Path.Combine(BaseDir, "last_mod_fingerprint.json");
    public static string ProfileBackupsDir => Path.Combine(BaseDir, "profile_backups");

    private static string ResolveBaseDir() {
        var godotPath = UserDataPathProvider.GetAccountScopedBasePath("mod_data/DevMode");
        return ProjectSettings.GlobalizePath(godotPath);
    }
}
