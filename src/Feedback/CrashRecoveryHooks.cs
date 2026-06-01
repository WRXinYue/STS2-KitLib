using System;
using DevMode.UI;
using Godot;

namespace DevMode.Feedback;

internal static class CrashRecoveryHooks {
    private static bool _registered;

    internal static void Register() {
        if (_registered)
            return;
        _registered = true;

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => CrashRecoveryStore.MarkSessionCleanExit();
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

            Callable.From(() => ErrorFeedbackPromptUI.TryShowFromCrash(report)).CallDeferred();
        }
        catch {
            // UI scheduling is best-effort; next launch prompt is the fallback.
        }
    }
}
