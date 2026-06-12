using Godot;
using KitLib.Feedback;
using KitLib.Host;
using KitLib.UI;

namespace KitLib.DevPerf;

internal static class KitLibRootServices {
    internal const string RootNodeName = "KitLibRootServices";
    internal const int CanvasLayerId = 32;

    internal static KitLibRootServicesNode? Instance { get; set; }

    internal static void EnsureRootServicesNode() {
        if (Engine.GetMainLoop() is not SceneTree tree)
            return;

        var root = tree.Root;
        if (root == null || !GodotObject.IsInstanceValid(root))
            return;

        var existing = root.GetNodeOrNull<KitLibRootServicesNode>(RootNodeName);
        if (existing != null && GodotObject.IsInstanceValid(existing)) {
            Instance = existing;
            existing.EnsureOverlayAttached();
            KitLibHost.TryRunDevBootstrap();
            return;
        }

        var node = new KitLibRootServicesNode { Name = RootNodeName };
        root.AddChild(node);
        Instance = node;
        node.EnsureOverlayAttached();
        KitLibHost.TryRunDevBootstrap();
    }
}

internal partial class KitLibRootServicesNode : CanvasLayer {
    public KitLibRootServicesNode() {
        Layer = KitLibRootServices.CanvasLayerId;
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _EnterTree() {
        KitLibRootServices.Instance = this;
        SetProcess(true);
        Callable.From(EnsureOverlayAttached).CallDeferred();
        Callable.From(() => CrashRecoveryHooks.EnsureLifecycleNode(this)).CallDeferred();
    }

    public override void _ExitTree() {
        if (KitLibRootServices.Instance == this)
            KitLibRootServices.Instance = null;
    }

    internal void EnsureOverlayAttached() {
        DevPerfOverlayUI.Attach(this);
        DevPerfOverlayUI.SyncVisibility();
    }

    public override void _Process(double delta) {
        DevPerfFrameTimeSampler.Process(delta);
    }
}
