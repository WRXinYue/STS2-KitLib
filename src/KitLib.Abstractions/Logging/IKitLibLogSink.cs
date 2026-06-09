namespace KitLib.Logging;

/// <summary>Optional fan-out target for <see cref="KitLog"/> (registered by KitLib.User at runtime).</summary>
public interface IKitLibLogSink {
    void Write(KitLogLevel level, string source, string message);
}
