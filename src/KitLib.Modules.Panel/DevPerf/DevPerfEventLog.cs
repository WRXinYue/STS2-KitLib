using KitLib.DevPerf;

namespace KitLib.DevPerf;

internal static class DevPerfEventLog {
    static readonly DevPerfLogRateLimiter SpikeLimiter = new();

    internal static void LogTransition(string name, long elapsedMs, int? assetCount = null) {
        DevPerfTransitionStore.Record(name, elapsedMs);
        var assets = assetCount.HasValue ? $" assets={assetCount.Value}" : "";
        MainFile.Logger.Info($"[Perf] {name} {elapsedMs}ms{assets}");
        DevPerfTraceWriter.TryAppendTransition(name, elapsedMs);
    }

    internal static void LogFrameSpike(double elapsedMs) {
        if (!SpikeLimiter.ShouldLog("frame_spike", TimeSpan.FromSeconds(5)))
            return;

        MainFile.Logger.Info($"[Perf] Frame spike {elapsedMs:F0}ms");
        DevPerfTraceWriter.TryAppendFrameSpike(elapsedMs);
    }

    internal static void LogDetail(string message) =>
        MainFile.Logger.Info($"[Perf] {message}");
}
