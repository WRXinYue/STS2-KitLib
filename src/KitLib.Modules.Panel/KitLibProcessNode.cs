using Godot;
using KitLib.Dev;
using KitLib.Feedback;
using KitLib.Host;
using KitLib.Hotkeys;
using KitLib.Mcp;
using KitLib.Patches;
using KitLib.Scripts;
using KitLib.Settings;

namespace KitLib;

/// <summary>
/// Lightweight Godot Node that hooks into the scene tree's _Process loop.
/// Drives RuntimeStatModifiers, AssetWarmupService, and script hot-reload each frame.
/// </summary>
internal partial class KitLibProcessNode : Node {
    internal static KitLibProcessNode? Instance { get; private set; }

    private double _heartbeatAccum;
    private double _logFlushAccum;

    public override void _EnterTree() {
        Instance = this;
        ProcessPriority = 128;
        SetProcessInput(true);
        if (!ModuleBootstrap.IsBootstrapComplete)
            KitLibHost.TryRunDevBootstrap();
    }

    public override void _Input(InputEvent @event) {
        if (HotkeyCapture.TryCapture(@event, GetViewport()))
            return;
        if (DevPanelHotkeys.TryHandle(@event, GetViewport()))
            return;
        QuickSlHotkeys.TryHandle(@event, GetViewport());
    }

    public override void _Process(double delta) {
        _heartbeatAccum += delta;
        if (_heartbeatAccum >= 2.0) {
            _heartbeatAccum = 0;
            KitLibInstanceRegistry.Heartbeat();
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
        if (Instance == this)
            Instance = null;
        CrashRecoveryStore.MarkSessionCleanExit();
        McpBridge.Shutdown();
        LogStreamPipeServer.Stop();
        InstanceLogWriter.Shutdown();
        KitLibInstanceRegistry.Unregister();
    }
}
