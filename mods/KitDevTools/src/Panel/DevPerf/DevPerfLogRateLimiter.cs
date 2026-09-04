namespace KitLib.DevPerf;

/// <summary>Minimum interval between repeated log categories (e.g. frame spikes).</summary>
public sealed class DevPerfLogRateLimiter {
    readonly object _lock = new();
    readonly Dictionary<string, DateTime> _lastByCategory = new(StringComparer.Ordinal);
    readonly Func<DateTime> _utcNow;

    public DevPerfLogRateLimiter(Func<DateTime>? utcNow = null) =>
        _utcNow = utcNow ?? (() => DateTime.UtcNow);

    public bool ShouldLog(string category, TimeSpan minInterval) {
        if (string.IsNullOrWhiteSpace(category))
            return true;

        var now = _utcNow();
        lock (_lock) {
            if (_lastByCategory.TryGetValue(category, out var last) && now - last < minInterval)
                return false;

            _lastByCategory[category] = now;
            return true;
        }
    }

    internal void ResetForTests() {
        lock (_lock)
            _lastByCategory.Clear();
    }
}
