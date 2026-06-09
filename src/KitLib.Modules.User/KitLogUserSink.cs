using KitLib.Logging;
using MegaCrit.Sts2.Core.Logging;

namespace KitLib;

/// <summary>
/// Bridges <see cref="KitLogHub"/> to the User log pipeline when messages bypass <see cref="Log.LogCallback"/>.
/// Normal <see cref="KitLog"/> calls already reach LogCollector via the game logger callback.
/// </summary>
internal sealed class KitLogUserSink : IKitLibLogSink {
    public void Write(KitLogLevel level, string source, string message) {
        if (!InstanceLogWriter.IsActive)
            return;

        var text = string.IsNullOrEmpty(message)
            ? $"[{source}]"
            : $"[{source}] {message}";

        if (!LogCollector.TryContainsLiveText(text))
            LogCollector.Inject(text, ToLogLevel(level));
    }

    static LogLevel ToLogLevel(KitLogLevel level) => level switch {
        KitLogLevel.Error => LogLevel.Error,
        KitLogLevel.Warn => LogLevel.Warn,
        KitLogLevel.Debug => LogLevel.Debug,
        _ => LogLevel.Info,
    };
}
