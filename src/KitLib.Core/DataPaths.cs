using System;
using System.IO;
using Godot;
using KitLib.Host;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib;

/// <summary>
/// Resolves writable user-data paths under
/// <c>user://steam/{userId}/mod_data/KitLib/</c>.
/// </summary>
public static class DataPaths {
    private const string ModDataSubdir = "mod_data/KitLib";

    private static string? _baseDir;

    /// <summary>Absolute filesystem path to the KitLib user-data root.</summary>
    public static string BaseDir => ResolvedBaseDir();

    /// <summary>Pin paths during mod init; later phases must use pinned paths only.</summary>
    public static void EnsurePinnedOnMainThread() {
        if (_baseDir == null)
            _baseDir = ResolveBaseDir();
        KitLibHost.PinModDataDir(_baseDir);
    }

    internal static bool TryGetPinnedBaseDir(out string path) {
        if (_baseDir != null) {
            path = _baseDir;
            return true;
        }
        if (!string.IsNullOrEmpty(KitLibHost.ModDataDir)) {
            path = KitLibHost.ModDataDir;
            _baseDir = path;
            return true;
        }
        path = "";
        return false;
    }

    internal static string GetPinnedBaseDir() {
        if (TryGetPinnedBaseDir(out var path))
            return path;
        throw new InvalidOperationException("DataPaths not pinned — call EnsurePinnedOnMainThread during mod init.");
    }

    public static string SettingsFile => Path.Combine(ResolvedBaseDir(), "settings.json");
    public static string SnapshotsDir => Path.Combine(ResolvedBaseDir(), "snapshots");
    public static string PresetsDir => Path.Combine(ResolvedBaseDir(), "presets");
    public static string ScriptsDir => Path.Combine(ResolvedBaseDir(), "scripts");
    public static string FingerprintFile => Path.Combine(ResolvedBaseDir(), "last_mod_fingerprint.json");
    public static string ProfileBackupsDir => Path.Combine(ResolvedBaseDir(), "profile_backups");

    private static string ResolvedBaseDir() {
        if (TryGetPinnedBaseDir(out var path))
            return path;
        return _baseDir ??= ResolveBaseDir();
    }

    private static string ResolveBaseDir() {
        if (!KitLibBootstrapGate.CanResolveGodotDataPaths && TryGetPinnedBaseDir(out var pinned))
            return pinned;

        var kitLibPath = UserDataPathProvider.GetAccountScopedBasePath(ModDataSubdir);
        var kitLibDir = ProjectSettings.GlobalizePath(kitLibPath);
        try {
            Directory.CreateDirectory(kitLibDir);
        }
        catch (Exception) {
        }
        KitLibHost.PinModDataDir(kitLibDir);
        return kitLibDir;
    }
}
