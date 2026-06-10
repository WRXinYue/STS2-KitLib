using Godot;
using KitLib.Hotkeys;
using KitLib.UI;

namespace KitLib.DevPerf;

internal static class KitLibRootServices {
    internal const string RootNodeName = "KitLibRootServices";
    internal const int CanvasLayerId = 32;

    internal static KitLibRootServicesNode? Instance { get; set; }

    internal static void EnsureRootServicesNode() {
        if (Engine.GetMainLoop() is not SceneTree tree) {
            MainFile.Logger.Debug("[Perf] EnsureRootServicesNode skipped (no SceneTree).");
            return;
        }

        var root = tree.Root;
        if (root == null || !GodotObject.IsInstanceValid(root)) {
            MainFile.Logger.Debug("[Perf] EnsureRootServicesNode skipped (invalid root).");
            return;
        }

        var existing = root.GetNodeOrNull<KitLibRootServicesNode>(RootNodeName);
        if (existing != null && GodotObject.IsInstanceValid(existing)) {
            Instance = existing;
            existing.EnsureOverlayAttached();
            DevPerfOverlayUI.SyncVisibility();
            return;
        }

        var node = new KitLibRootServicesNode { Name = RootNodeName };
        root.AddChild(node);
        Instance = node;
        MainFile.Logger.Info("[Perf] Root services CanvasLayer attached.");
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
        SetProcessUnhandledInput(true);
        Callable.From(EnsureOverlayAttached).CallDeferred();
    }

    public override void _ExitTree() {
        if (KitLibRootServices.Instance == this)
            KitLibRootServices.Instance = null;
    }

    internal void EnsureOverlayAttached() {
        DevPerfOverlayUI.Attach(this);
    }

    public override void _UnhandledInput(InputEvent @event) {
        DevPerfHotkeys.TryHandle(@event, GetViewport());
    }

    public override void _Process(double delta) {
        DevPerfFrameTimeSampler.Process(delta);
    }
}
