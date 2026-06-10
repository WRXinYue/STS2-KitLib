using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using KitLib.Dev;

namespace KitLib;

/// <summary>
/// Tracks live DevMode processes on this machine via heartbeat lock files under
/// <c>mod_data/KitLib/instances/</c>.
/// </summary>
internal static class KitLibInstanceRegistry {
    private static readonly TimeSpan StaleAfter = TimeSpan.FromSeconds(45);
    private static readonly TimeSpan MaxInstanceLogAge = TimeSpan.FromDays(14);
    private const int MaxRetainedInstanceLogs = 30;

    private static string InstancesDir => DevModDataPaths.InstancesDir;
    private static string LockPath => Path.Combine(InstancesDir, $"{KitLibInstance.ProcessId}.lock");

    public static void Register() {
        try {
            Directory.CreateDirectory(InstancesDir);
            WriteLock();
            CleanupStaleLocks();
            // Defer heavy folder deletes; synchronous IO during scene-ready bootstrap is unsafe on stable.
            ThreadPool.QueueUserWorkItem(_ => SafeCleanupStaleInstanceLogs());
        }
        catch (Exception) {
        }
    }

    static void SafeCleanupStaleInstanceLogs() {
        try {
            CleanupStaleInstanceLogs();
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

    /// <summary>
    /// Drops old <c>instances/{pid}/session.log</c> folders. Keeps the current process,
    /// any live peer (fresh lock), the newest <see cref="MaxRetainedInstanceLogs"/>, and
    /// anything touched within <see cref="MaxInstanceLogAge"/>.
    /// </summary>
    private static void CleanupStaleInstanceLogs() {
        if (!Directory.Exists(InstancesDir))
            return;

        var now = DateTime.UtcNow;
        var ageCutoff = now - MaxInstanceLogAge;
        var currentPid = KitLibInstance.ProcessId;
        var candidates = new List<(string Path, DateTime LastWriteUtc)>();

        foreach (var dir in Directory.EnumerateDirectories(InstancesDir)) {
            var name = Path.GetFileName(dir);
            if (!int.TryParse(name, out var pid) || pid == currentPid)
                continue;
            if (IsLockFresh(pid, now))
                continue;

            candidates.Add((dir, GetInstanceLastWriteUtc(dir)));
        }

        var removed = 0;
        foreach (var (path, lastWriteUtc) in candidates.ToArray()) {
            if (lastWriteUtc >= ageCutoff)
                continue;
            if (TryDeleteInstanceDir(path))
                removed++;
        }

        candidates.RemoveAll(c => !Directory.Exists(c.Path));
        foreach (var (path, _) in candidates
                     .OrderBy(c => c.LastWriteUtc)
                     .Take(Math.Max(0, candidates.Count - MaxRetainedInstanceLogs))) {
            if (TryDeleteInstanceDir(path))
                removed++;
        }

        if (removed > 0) {
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

    private static DateTime GetInstanceLastWriteUtc(string dir) {
        try {
            var sessionLog = Path.Combine(dir, "session.log");
            if (File.Exists(sessionLog))
                return File.GetLastWriteTimeUtc(sessionLog);
            return Directory.GetLastWriteTimeUtc(dir);
        }
        catch {
            return DateTime.MinValue;
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
        catch (Exception ex) {
            return false;
        }
    }
}
