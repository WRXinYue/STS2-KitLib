using DevMode.Feedback;
using DevMode.Hotkeys;
using DevMode.Patches;
using DevMode.Scripts;
using DevMode.Settings;
using Godot;

namespace DevMode;

/// <summary>
/// Lightweight Godot Node that hooks into the scene tree's _Process loop.
/// Drives RuntimeStatModifiers, AssetWarmupService, and script hot-reload each frame.
/// </summary>
internal partial class DevModeProcessNode : Node {
    private double _heartbeatAccum;
    private double _logFlushAccum;

    public override void _EnterTree() {
        ProcessPriority = 128;
        SetProcessInput(true);
    }

    public override void _Input(InputEvent @event) {
        if (HotkeyCapture.TryCapture(@event, GetViewport()))
            return;
        DevPanelHotkeys.TryHandle(@event, GetViewport());
    }

    public override void _Process(double delta) {
        _heartbeatAccum += delta;
        if (_heartbeatAccum >= 2.0) {
            _heartbeatAccum = 0;
            DevModeInstanceRegistry.Heartbeat();
        }

        _logFlushAccum += delta;
        if (_logFlushAccum >= InstanceLogWriter.FlushIntervalSeconds) {
            _logFlushAccum = 0;
            InstanceLogWriter.TryFlush();
        }

        GlobalUiReadyPatch.Process(delta);
        ScriptManager.ProcessPendingReload();
    }

    public override void _ExitTree() {
        CrashRecoveryStore.MarkSessionCleanExit();
        InstanceLogWriter.Shutdown();
        DevModeInstanceRegistry.Unregister();
    }
}
