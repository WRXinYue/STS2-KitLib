using System;
using Godot;
using KitLib.Host;

namespace KitLib.Feedback;

internal static class CrashRecoveryHooks {
    private const string LifecycleNodeName = "KitLibCrashRecoveryLifecycle";

    private static bool _registered;

    internal static void Register() {
        if (_registered)
            return;
        _registered = true;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CrashRecoveryStore.MarkSessionCleanExit();
        Callable.From(EnsureLifecycleNode).CallDeferred();
    }

    /// <summary>
    /// Hooks Godot shutdown so <see cref="CrashRecoveryStore.MarkSessionCleanExit"/> runs
    /// even when <see cref="AppDomain.ProcessExit"/> does not (common on normal game quit).
    /// </summary>
    internal static void EnsureLifecycleNode() {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;

        var root = tree.Root;
        if (root == null || !GodotObject.IsInstanceValid(root))
            return;

        if (root.GetNodeOrNull<Node>(LifecycleNodeName) != null)
            return;

        root.AddChild(new CrashRecoveryLifecycleNode { Name = LifecycleNodeName });
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e) {
        var exception = e.ExceptionObject as Exception;
        CrashRecoveryStore.RecordCrash(exception);

        try {
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree == null)
                return;

            var report = CrashRecoveryStore.TryConsumePendingReport();
            if (report == null)
                return;

            Callable.From(() => KitLibPanelOps.ShowErrorFeedbackFromCrash?.Invoke(report)).CallDeferred();
        }
        catch {
            // UI scheduling is best-effort; next launch prompt is the fallback.
        }
    }
}
