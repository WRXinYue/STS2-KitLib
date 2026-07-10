using System.Diagnostics;
using System.Text;

namespace KitLib.Diagnostics;

/// <summary>
/// Accumulates wall-clock durations of KitLib's own startup phases (core init, satellite loads,
/// deferred Dev bootstrap) and emits consolidated audit reports to the log. Only time spent inside
/// KitLib code is recorded; engine and other-mod gaps are excluded.
/// </summary>
internal static class KitLibStartupAudit {
    private static readonly object Gate = new();
    private static readonly List<PhaseTiming> Phases = [];
    private static int _reportedCount;

    [ThreadStatic] private static MeasureScope? _currentScope;

    internal static void Measure(string phase, Action action) {
        var scope = PushScope(phase);
        var sw = Stopwatch.StartNew();
        try {
            action();
        }
        finally {
            sw.Stop();
            PopScope(scope, sw.Elapsed.TotalMilliseconds);
        }
    }

    internal static T Measure<T>(string phase, Func<T> func) {
        var scope = PushScope(phase);
        var sw = Stopwatch.StartNew();
        try {
            return func();
        }
        finally {
            sw.Stop();
            PopScope(scope, sw.Elapsed.TotalMilliseconds);
        }
    }

    /// <summary>Logs new phases since the last report; no-op when nothing was recorded.</summary>
    internal static void LogReport(string title) {
        lock (Gate) {
            if (Phases.Count == 0 || Phases.Count <= _reportedCount)
                return;

            var total = Phases.Sum(static entry => entry.ExclusiveMilliseconds);
            var text = new StringBuilder()
                .AppendLine()
                .AppendLine($"=== KitLib Startup Audit: {title} ===");

            foreach (var timing in Phases) {
                text.Append($"  {timing.Phase}: {timing.ExclusiveMilliseconds:F1} ms");
                if (Math.Abs(timing.InclusiveMilliseconds - timing.ExclusiveMilliseconds) >= 0.05d)
                    text.Append($" (inclusive {timing.InclusiveMilliseconds:F1} ms)");

                text.AppendLine();
            }

            text.AppendLine("  ---")
                .Append($"  KitLib exclusive self-time total: {total:F1} ms");

            _reportedCount = Phases.Count;
            MainFile.Logger.Info(text.ToString());
        }
    }

    internal static void LogCoreOnlyReportIfNeeded() {
        if (!Host.ModuleCatalog.IsLoaded(Host.ModuleIds.Dev))
            LogReport("initialization");
    }

    private static MeasureScope PushScope(string phase) {
        var scope = new MeasureScope(phase, _currentScope);
        _currentScope = scope;
        return scope;
    }

    private static void PopScope(MeasureScope scope, double inclusiveMilliseconds) {
        _currentScope = scope.Parent;
        scope.Parent?.AddChild(inclusiveMilliseconds);

        var exclusiveMilliseconds = Math.Max(0d, inclusiveMilliseconds - scope.ChildMilliseconds);
        lock (Gate) {
            Phases.Add(new(scope.Phase, inclusiveMilliseconds, exclusiveMilliseconds));
        }
    }

    private sealed class MeasureScope(string phase, MeasureScope? parent) {
        internal string Phase { get; } = phase;
        internal MeasureScope? Parent { get; } = parent;
        internal double ChildMilliseconds { get; private set; }

        internal void AddChild(double milliseconds) {
            ChildMilliseconds += milliseconds;
        }
    }

    private readonly record struct PhaseTiming(
        string Phase,
        double InclusiveMilliseconds,
        double ExclusiveMilliseconds);
}
