namespace KitLib.DevPerf;

/// <summary>Ring buffer of recent transition timings for the perf overlay.</summary>
public static class DevPerfTransitionStore {
    public const int MaxRecords = 3;

    static readonly object Lock = new();
    static readonly List<DevPerfTransitionRecord> Records = [];

    public static void Record(string name, long elapsedMs) {
        if (string.IsNullOrWhiteSpace(name))
            return;

        var entry = new DevPerfTransitionRecord(name.Trim(), elapsedMs, DateTime.UtcNow);
        lock (Lock) {
            Records.Add(entry);
            while (Records.Count > MaxRecords)
                Records.RemoveAt(0);
        }
    }

    public static IReadOnlyList<DevPerfTransitionRecord> Snapshot() {
        lock (Lock)
            return Records.ToArray();
    }

    internal static void ClearForTests() {
        lock (Lock)
            Records.Clear();
    }
}
