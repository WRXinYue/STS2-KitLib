namespace KitLib.Logging;

/// <summary>In-process fan-out for structured log entries and pipe replay history.</summary>
public static class LogStreamHub {
    static readonly List<Action<LogStreamEntry>> Subscribers = [];
    static readonly Queue<LogStreamEntry> History = new();
    static readonly object Lock = new();
    static LogViewerFilterSnapshot? _currentFilter;

    public static LogViewerFilterSnapshot? CurrentFilter {
        get {
            lock (Lock)
                return _currentFilter;
        }
    }

    public static void Subscribe(Action<LogStreamEntry> subscriber) {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (Lock) {
            if (!Subscribers.Contains(subscriber))
                Subscribers.Add(subscriber);
        }
    }

    /// <summary>Replays buffered history, then receives live entries until <see cref="Unsubscribe"/>.</summary>
    public static void SubscribeWithReplay(Action<LogStreamEntry> subscriber) {
        ArgumentNullException.ThrowIfNull(subscriber);
        LogStreamEntry[] replay;
        LogViewerFilterSnapshot? filter;
        lock (Lock) {
            replay = History.ToArray();
            filter = _currentFilter;
            if (!Subscribers.Contains(subscriber))
                Subscribers.Add(subscriber);
        }

        if (filter != null)
            subscriber(LogStreamEntry.FromFilterSnapshot(filter));

        foreach (var entry in replay)
            subscriber(entry);
    }

    public static void Unsubscribe(Action<LogStreamEntry> subscriber) {
        ArgumentNullException.ThrowIfNull(subscriber);
        lock (Lock)
            Subscribers.Remove(subscriber);
    }

    public static void Publish(LogStreamEntry entry) {
        if (entry.IsFilterFrame) {
            PublishFilter(entry.Filter ?? new LogViewerFilterSnapshot());
            return;
        }

        Action<LogStreamEntry>[] snapshot;
        lock (Lock) {
            History.Enqueue(entry);
            while (History.Count > LogStreamContract.MaxHistoryEntries)
                History.Dequeue();

            snapshot = Subscribers.ToArray();
        }

        foreach (var subscriber in snapshot) {
            try {
                subscriber(entry);
            }
            catch {
                // Best-effort fan-out; pipe failures must not break logging.
            }
        }
    }

    public static void PublishFilter(LogViewerFilterSnapshot snapshot) {
        ArgumentNullException.ThrowIfNull(snapshot);
        Action<LogStreamEntry>[] subscribers;
        lock (Lock) {
            if (_currentFilter != null && FilterEquals(_currentFilter, snapshot))
                return;

            _currentFilter = snapshot;
            subscribers = Subscribers.ToArray();
        }

        var entry = LogStreamEntry.FromFilterSnapshot(snapshot);
        foreach (var subscriber in subscribers) {
            try {
                subscriber(entry);
            }
            catch {
                // Best-effort fan-out only.
            }
        }
    }

    static bool FilterEquals(LogViewerFilterSnapshot a, LogViewerFilterSnapshot b) {
        if (!string.Equals(a.MinLevel, b.MinLevel, StringComparison.Ordinal))
            return false;
        if (!string.Equals(a.TextFilter, b.TextFilter, StringComparison.Ordinal))
            return false;
        if (!SequenceEqual(a.HiddenSources, b.HiddenSources))
            return false;
        if (!SequenceEqual(a.LoadedModIds, b.LoadedModIds))
            return false;
        if (!DictionaryEqual(a.ModIdAliases, b.ModIdAliases))
            return false;
        if (a.SuppressRules.Length != b.SuppressRules.Length)
            return false;

        for (int i = 0; i < a.SuppressRules.Length; i++) {
            if (!string.Equals(a.SuppressRules[i].Pattern, b.SuppressRules[i].Pattern, StringComparison.Ordinal))
                return false;
            if (a.SuppressRules[i].Enabled != b.SuppressRules[i].Enabled)
                return false;
        }

        return true;
    }

    static bool SequenceEqual(string[] a, string[] b) {
        if (a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++) {
            if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    static bool DictionaryEqual(Dictionary<string, string> a, Dictionary<string, string> b) {
        if (a.Count != b.Count)
            return false;
        foreach (var (key, value) in a) {
            if (!b.TryGetValue(key, out var other) || !string.Equals(value, other, StringComparison.Ordinal))
                return false;
        }

        return true;
    }

    public static LogStreamEntry[] GetHistorySnapshot() {
        lock (Lock)
            return History.ToArray();
    }

    public static void Clear() {
        lock (Lock) {
            History.Clear();
            Subscribers.Clear();
            _currentFilter = null;
        }
    }
}
