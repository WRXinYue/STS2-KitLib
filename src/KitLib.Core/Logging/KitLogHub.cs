using KitLib.Logging;

namespace KitLib;

/// <summary>Fan-out hub for optional <see cref="IKitLibLogSink"/> registrations.</summary>
public static class KitLogHub {
    private static readonly List<IKitLibLogSink> Sinks = [];
    private static readonly object Lock = new();

    public static void RegisterSink(IKitLibLogSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        lock (Lock) {
            if (!Sinks.Contains(sink))
                Sinks.Add(sink);
        }
    }

    public static void UnregisterSink(IKitLibLogSink sink) {
        ArgumentNullException.ThrowIfNull(sink);
        lock (Lock)
            Sinks.Remove(sink);
    }

    internal static void Publish(KitLogLevel level, string source, string message) {
        IKitLibLogSink[] snapshot;
        lock (Lock)
            snapshot = Sinks.ToArray();

        foreach (var sink in snapshot) {
            try {
                sink.Write(level, source, message);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[KitLog] Sink failed ({sink.GetType().Name}): {ex.Message}");
            }
        }
    }
}
