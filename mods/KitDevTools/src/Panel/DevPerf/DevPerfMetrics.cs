namespace KitLib.DevPerf;

public static class DevPerfMetrics {
    static readonly object Lock = new();
    static readonly List<IDevPerfLineProvider> Providers = [];

    public static void Register(IDevPerfLineProvider provider) {
        if (provider == null)
            return;

        lock (Lock) {
            if (!Providers.Contains(provider))
                Providers.Add(provider);
        }
    }

    public static void Unregister(IDevPerfLineProvider provider) {
        if (provider == null)
            return;

        lock (Lock)
            Providers.Remove(provider);
    }

    public static void CollectLines(List<DevPerfLine> sink) {
        if (sink == null)
            return;

        IDevPerfLineProvider[] snapshot;
        lock (Lock)
            snapshot = Providers.OrderBy(p => p.Order).ToArray();

        foreach (var provider in snapshot) {
            try {
                provider.AppendLines(sink);
            }
            catch {
                // Provider failures must not break the overlay.
            }
        }
    }

    internal static void ClearProvidersForTests() {
        lock (Lock)
            Providers.Clear();
    }
}
