using KitLib.Host;

namespace KitLib;

/// <summary>Fan-out hub for optional log sink registrations (implementations from KitLib.Abstractions).</summary>
public static class KitLogHub {
    static readonly List<object> Sinks = [];
    static readonly object Lock = new();

    public static void RegisterSink(object sink) {
        ArgumentNullException.ThrowIfNull(sink);
        lock (Lock) {
            if (!Sinks.Contains(sink))
                Sinks.Add(sink);
        }
    }

    public static void UnregisterSink(object sink) {
        ArgumentNullException.ThrowIfNull(sink);
        lock (Lock)
            Sinks.Remove(sink);
    }

    internal static void Publish(KitLogLevel level, string source, string message) {
        object[] snapshot;
        lock (Lock)
            snapshot = Sinks.ToArray();

        foreach (var sink in snapshot) {
            try {
                HostReflection.InvokeLogSinkWrite(sink, level, source, message);
            }
            catch (Exception ex) {
                KitLog.Warn("KitLog", $"Sink failed ({sink.GetType().Name}): {ex.Message}");
            }
        }
    }
}
