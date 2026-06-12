using Godot;

namespace KitLib.Feedback;

/// <summary>Clears the crash-recovery session marker on Godot shutdown (normal quit).</summary>
internal partial class CrashRecoveryLifecycleNode : Node {
    private Window? _rootWindow;

    public override void _Ready() {
        ProcessMode = ProcessModeEnum.Always;
        _rootWindow = GetTree()?.Root;
        if (_rootWindow != null)
            _rootWindow.TreeExiting += OnTreeExiting;
    }

    public override void _Notification(int what) {
        if (what == NotificationWMCloseRequest)
            CrashRecoveryStore.MarkSessionCleanExit();
    }

    public override void _ExitTree() {
        if (_rootWindow != null) {
            _rootWindow.TreeExiting -= OnTreeExiting;
            _rootWindow = null;
        }
    }

    private void OnTreeExiting() => CrashRecoveryStore.MarkSessionCleanExit();
}
