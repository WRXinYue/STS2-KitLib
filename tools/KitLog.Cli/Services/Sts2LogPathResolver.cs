namespace KitLog.Cli.Services;

internal sealed record SessionLogEntry(int? Pid, string Path, DateTime LastWriteUtc, bool IsLatest);

internal static class Sts2LogPathResolver {
    static string InstancesDir(string accountDir) => Path.Combine(accountDir, "mod_data", "KitLib", "instances");

    public static IReadOnlyList<string> UserDataRoots() {
        var roots = new List<string>();

        if (OperatingSystem.IsWindows()) {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (!string.IsNullOrEmpty(appData))
                roots.Add(Path.Combine(appData, "SlayTheSpire2"));
        }
        else if (OperatingSystem.IsMacOS()) {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, "Library", "Application Support", "SlayTheSpire2"));
        }
        else {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            roots.Add(Path.Combine(home, ".local", "share", "SlayTheSpire2"));
            roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", "data", "SlayTheSpire2"));
            roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "SlayTheSpire2"));
        }

        return roots
            .Where(Directory.Exists)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static IReadOnlyList<SessionLogEntry> ListSessionLogs() {
        var rows = new List<SessionLogEntry>();

        foreach (var root in UserDataRoots()) {
            var steamDir = Path.Combine(root, "steam");
            if (!Directory.Exists(steamDir))
                continue;

            foreach (var accountDir in Directory.EnumerateDirectories(steamDir)) {
                var instancesDir = InstancesDir(accountDir);
                if (!Directory.Exists(instancesDir))
                    continue;

                foreach (var instanceDir in Directory.EnumerateDirectories(instancesDir)) {
                    var name = Path.GetFileName(instanceDir);
                    if (!int.TryParse(name, out var pid))
                        continue;

                    var path = Path.Combine(instanceDir, "session.log");
                    if (!File.Exists(path))
                        continue;

                    rows.Add(new SessionLogEntry(
                        pid,
                        path,
                        File.GetLastWriteTimeUtc(path),
                        IsLatest: false));
                }
            }
        }

        if (rows.Count == 0)
            return rows;

        var latest = rows.OrderByDescending(r => r.LastWriteUtc).First().Path;
        return rows
            .Select(r => r with { IsLatest = string.Equals(r.Path, latest, StringComparison.OrdinalIgnoreCase) })
            .OrderByDescending(r => r.IsLatest)
            .ThenByDescending(r => r.LastWriteUtc)
            .ToList();
    }

    public static string? ResolveSessionLogPath(int? pid) {
        if (pid is int id) {
            foreach (var root in UserDataRoots()) {
                var steamDir = Path.Combine(root, "steam");
                if (!Directory.Exists(steamDir))
                    continue;

                foreach (var accountDir in Directory.EnumerateDirectories(steamDir)) {
                    var path = Path.Combine(accountDir, "mod_data", "KitLib", "instances", id.ToString(), "session.log");
                    if (File.Exists(path))
                        return path;
                }
            }

            return null;
        }

        var latest = ListSessionLogs().FirstOrDefault(e => e.IsLatest);
        return latest?.Path;
    }

    public static string? ResolveGodotLogPath() {
        foreach (var root in UserDataRoots()) {
            var path = Path.Combine(root, "logs", "godot.log");
            if (File.Exists(path))
                return path;
        }

        return null;
    }

    public static string? ResolveLogPath(int? pid, string? explicitFile) {
        if (!string.IsNullOrWhiteSpace(explicitFile))
            return File.Exists(explicitFile) ? Path.GetFullPath(explicitFile) : null;

        var session = ResolveSessionLogPath(pid);
        if (session != null)
            return session;

        return ResolveGodotLogPath();
    }

    public static bool TailContainsSessionMarker(string path) {
        try {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (fs.Length == 0)
                return false;

            const int scanBytes = 256 * 1024;
            var readLen = (int)Math.Min(scanBytes, fs.Length);
            fs.Seek(-readLen, SeekOrigin.End);
            var buffer = new byte[readLen];
            var offset = 0;
            while (offset < readLen) {
                var n = fs.Read(buffer, offset, readLen - offset);
                if (n <= 0)
                    break;
                offset += n;
            }

            var text = System.Text.Encoding.UTF8.GetString(buffer);
            return KitLogMarkers.ContainsAnySessionBoundary(text);
        }
        catch {
            return false;
        }
    }
}
