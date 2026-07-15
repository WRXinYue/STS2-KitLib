namespace KitLog.Cli.Services;

internal sealed record ActiveInstanceEntry(int Pid, bool IsLatest);

internal static class Sts2LogPathResolver {
    static readonly string[] Sts2ProcessNames = ["SlayTheSpire2", "Slay the Spire 2"];

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

    public static IReadOnlyList<ActiveInstanceEntry> ListActiveInstances() {
        var ids = new HashSet<int>();
        foreach (var name in Sts2ProcessNames) {
            System.Diagnostics.Process[] processes;
            try {
                processes = System.Diagnostics.Process.GetProcessesByName(name);
            }
            catch {
                continue;
            }

            foreach (var process in processes) {
                try {
                    ids.Add(process.Id);
                }
                finally {
                    process.Dispose();
                }
            }
        }

        if (ids.Count == 0)
            return [];

        var current = Environment.ProcessId;
        var latest = ids.Contains(current) ? current : ids.Max();
        return ids
            .Select(pid => new ActiveInstanceEntry(pid, pid == latest))
            .OrderByDescending(e => e.IsLatest)
            .ThenByDescending(e => e.Pid)
            .ToList();
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
