using System;
using System.IO;
using System.Linq;

namespace KitLib;

/// <summary>
/// Tracks live DevMode processes on this machine via heartbeat lock files under
/// <c>mod_data/KitLib/instances/</c>.
/// </summary>
internal static class KitLibInstanceRegistry {
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(45);
    private static string InstancesDir => Path.Combine(DataPaths.BaseDir, "instances");
    private static string LockPath => Path.Combine(InstancesDir, $"{KitLibInstance.ProcessId}.lock");

    public static void Register() {
        try {
            Directory.CreateDirectory(InstancesDir);
            WriteLock();
            CleanupStaleLocks();
        }
        catch (Exception ex) {
            MainFile.Logger.Warn($"[KitLib] Instance registry register failed: {ex.Message}");
        }
    }

    public static void Heartbeat() {
        try {
            if (!Directory.Exists(InstancesDir))
                Register();
            else
                WriteLock();
        }
        catch {
            // Best-effort heartbeat only.
        }
    }

    public static void Unregister() {
        try {
            if (File.Exists(LockPath))
                File.Delete(LockPath);
        }
        catch {
            // Best-effort cleanup only.
        }
    }

    /// <summary>True when another live DevMode process is on this machine.</summary>
    public static bool IsDualInstanceActive() => ActiveInstanceCount() > 1;

    public static int ActiveInstanceCount() {
        try {
            CleanupStaleLocks();
            if (!Directory.Exists(InstancesDir))
                return File.Exists(LockPath) ? 1 : 0;
            return Directory.EnumerateFiles(InstancesDir, "*.lock").Count();
        }
        catch {
            return 1;
        }
    }

    private static void WriteLock() {
        File.WriteAllText(LockPath, DateTime.UtcNow.ToString("O"));
    }

    private static void CleanupStaleLocks() {
        if (!Directory.Exists(InstancesDir))
            return;

        var cutoff = DateTime.UtcNow - StaleAfter;
        foreach (var path in Directory.EnumerateFiles(InstancesDir, "*.lock")) {
            try {
                var text = File.ReadAllText(path).Trim();
                if (!DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var beat)
                    || beat.ToUniversalTime() < cutoff) {
                    File.Delete(path);
                }
            }
            catch {
                try { File.Delete(path); } catch { /* ignore */ }
            }
        }
    }
}
