using Godot;
using KitLib.Dev;
using KitLib.Host;
using KitLib.Mcp;
using KitLib.Patches;

namespace KitLib;

/// <summary>
/// Lightweight Godot Node that hooks into the scene tree's _Process loop.
/// Drives RuntimeStatModifiers and AssetWarmupService each frame.
/// </summary>
internal partial class KitLibProcessNode : Node {
    internal static KitLibProcessNode? Instance { get; private set; }

    public override void _EnterTree() {
        Instance = this;
        ProcessPriority = 128;
        if (!ModuleBootstrap.IsBootstrapComplete)
            KitLibHost.TryRunDevBootstrap();
    }

    public override void _Process(double delta) {
        GlobalUiReadyPatch.Process(delta);
    }

    public override void _ExitTree() {
        if (Instance == this)
            Instance = null;
        McpBridge.Shutdown();
        LogStreamPipeServer.Stop();
    }
}
