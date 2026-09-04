using System.Diagnostics;

namespace KitLib;

/// <summary>Detects multiple STS2 processes on this machine without lock files under <c>instances/</c>.</summary>
public static class KitLibProcessScope {
    static readonly string[] Sts2ProcessNames = ["SlayTheSpire2", "Slay the Spire 2"];

    public static IReadOnlyList<int> GetRunningProcessIds() {
        var ids = new HashSet<int>();
        foreach (var name in Sts2ProcessNames) {
            Process[] processes;
            try {
                processes = Process.GetProcessesByName(name);
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

        return ids.OrderBy(id => id).ToArray();
    }

    public static bool IsDualInstanceActive() => GetRunningProcessIds().Count > 1;
}
