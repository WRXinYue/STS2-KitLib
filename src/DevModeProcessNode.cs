using DevMode.Patches;
using DevMode.Scripts;
using Godot;

namespace DevMode;

/// <summary>
/// Lightweight Godot Node that hooks into the scene tree's _Process loop.
/// Drives RuntimeStatModifiers, AssetWarmupService, and script hot-reload each frame.
/// </summary>
internal partial class DevModeProcessNode : Node {
    private double _heartbeatAccum;

    public override void _Process(double delta) {
        _heartbeatAccum += delta;
        if (_heartbeatAccum >= 2.0) {
            _heartbeatAccum = 0;
            DevModeInstanceRegistry.Heartbeat();
        }

        GlobalUiReadyPatch.Process(delta);
        ScriptManager.ProcessPendingReload();
    }

    public override void _ExitTree() {
        InstanceLogWriter.Shutdown();
        DevModeInstanceRegistry.Unregister();
    }
}
