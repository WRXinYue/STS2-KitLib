namespace KitLib.Logging;

/// <summary>Optional fan-out target for KitLib logging (registered by KitLib.User at runtime).</summary>
public interface IKitLibLogSink {
    void Write(KitLogLevel level, string source, string message);
}
