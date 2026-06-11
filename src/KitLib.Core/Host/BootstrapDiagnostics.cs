using System.Collections.Generic;
using Godot;
using KitLib;

namespace KitLib.Host;

/// <summary>Collects bootstrap failures during scene-ready init and logs after the fragile window.</summary>
internal static class BootstrapDiagnostics {
    private static readonly object Gate = new();
    private static readonly List<(string Step, string Message)> Pending = [];

    internal static void RecordFailure(string step, Exception ex) {
        lock (Gate)
            Pending.Add((step, ex.Message));
    }

    internal static void FlushToLogger() {
        lock (Gate) {
            if (Pending.Count == 0)
                return;

            if (KitLibBootstrapGate.Phase == KitLibBootstrapPhase.SceneReady)
                KitLibBootstrapGate.EnterInteractive();

            if (!KitLibBootstrapGate.CanUseMainFileLogger)
                return;

            foreach (var (step, message) in Pending)
                KitLog.Warn($"Bootstrap step '{step}' failed: {message}");
            Pending.Clear();
        }
    }

    internal static void FlushDeferred() => Callable.From(FlushToLogger).CallDeferred();
}
