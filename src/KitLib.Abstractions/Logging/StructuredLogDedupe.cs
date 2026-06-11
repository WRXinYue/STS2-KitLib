namespace KitLib.Logging;

/// <summary>Suppresses duplicate pipe publishes when KitLog lines also arrive via <c>Log.LogCallback</c>.</summary>
public static class StructuredLogDedupe {
    const int MaxRecent = 64;

    static readonly object Lock = new();
    static readonly Queue<string> Recent = new();

    public static void Mark(string fingerprint) {
        if (string.IsNullOrEmpty(fingerprint))
            return;

        lock (Lock) {
            Recent.Enqueue(fingerprint);
            while (Recent.Count > MaxRecent)
                Recent.Dequeue();
        }
    }

    public static bool TryConsume(string fingerprint) {
        if (string.IsNullOrEmpty(fingerprint))
            return false;

        lock (Lock) {
            if (Recent.Count == 0)
                return false;

            var items = Recent.ToArray();
            int index = Array.FindIndex(items, f => f == fingerprint);
            if (index < 0)
                return false;

            Recent.Clear();
            for (int i = 0; i < items.Length; i++) {
                if (i == index)
                    continue;
                Recent.Enqueue(items[i]);
            }

            return true;
        }
    }

    public static void Clear() {
        lock (Lock)
            Recent.Clear();
    }
}
