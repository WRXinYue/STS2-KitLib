using Godot;

namespace KitLib.Feedback;

/// <summary>
/// Clears the crash-recovery session marker on Godot shutdown (normal quit).
/// </summary>
internal partial class CrashRecoveryLifecycleNode : Node {
    public override void _Notification(int what) {
        if (what == NotificationWMCloseRequest)
            CrashRecoveryStore.MarkSessionCleanExit();
    }

    public override void _ExitTree() {
        CrashRecoveryStore.MarkSessionCleanExit();
    }
}
