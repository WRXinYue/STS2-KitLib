using System;
using System.IO;
using System.Linq;
using System.Threading;

namespace KitLib;

/// <summary>
/// Tracks live KitLib processes on this machine via heartbeat lock files under
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
            // Defer folder deletes; synchronous IO during scene-ready bootstrap is unsafe on stable.
            ThreadPool.QueueUserWorkItem(_ => SafeCleanupInstanceLogDirs());
        }
        catch (Exception) {
        }
    }

    static void SafeCleanupInstanceLogDirs() {
        try {
            CleanupInstanceLogDirs();
        }
        catch (Exception) {
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

    /// <summary>True when another live KitLib process is on this machine.</summary>
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

    /// <summary>
    /// Single-instance: remove all <c>instances/{pid}/</c> folders on startup.
    /// Dual-instance: remove only folders for processes that are no longer live.
    /// </summary>
    internal static void CleanupInstanceLogDirs() {
        if (!Directory.Exists(InstancesDir))
            return;

        if (!IsDualInstanceActive()) {
            PurgeAllInstanceDirs();
            return;
        }

        var now = DateTime.UtcNow;
        foreach (var dir in Directory.EnumerateDirectories(InstancesDir).ToArray()) {
            if (!int.TryParse(Path.GetFileName(dir), out var pid))
                continue;
            if (IsLockFresh(pid, now))
                continue;
            TryDeleteInstanceDir(dir);
        }
    }

    static void PurgeAllInstanceDirs() {
        foreach (var dir in Directory.EnumerateDirectories(InstancesDir).ToArray())
            TryDeleteInstanceDir(dir);
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

    private static bool IsLockFresh(int pid, DateTime now) {
        var lockPath = Path.Combine(InstancesDir, $"{pid}.lock");
        if (!File.Exists(lockPath))
            return false;

        try {
            var text = File.ReadAllText(lockPath).Trim();
            return DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var beat)
                   && beat.ToUniversalTime() >= now - StaleAfter;
        }
        catch {
            return false;
        }
    }

    private static bool TryDeleteInstanceDir(string path) {
        try {
            Directory.Delete(path, recursive: true);
            var lockPath = Path.Combine(InstancesDir, $"{Path.GetFileName(path)}.lock");
            if (File.Exists(lockPath))
                File.Delete(lockPath);
            return true;
        }
        catch {
            return false;
        }
    }
}
