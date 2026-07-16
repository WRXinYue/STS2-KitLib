using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string BrowserPanelAnimatingMetaKey = "_dm_browser_panel_animating";
    internal const string BrowserPanelClosingMetaKey = "_dm_browser_panel_closing";
    internal const string BrowserPanelClipHostName = "BrowserPanelClipHost";

    internal static Control CreateAndSetupRoot(NGlobalUi globalUi, string rootName, int zIndex) {
        var root = new Control { Name = rootName, MouseFilter = Control.MouseFilterEnum.Ignore, ZIndex = zIndex };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        SetupRailTransition(globalUi, root);
        return root;
    }

    private static void SetupRailTransition(NGlobalUi globalUi, Control root) {
        _browserOverlayCount++;
        PinRail();
        ReconcileBrowserRail(globalUi);
        MonsterIntentOverlayUI.SyncState(globalUi);
        root.TreeExiting += () => {
            _browserOverlayCount = Math.Max(0, _browserOverlayCount - 1);
            UnpinRail();
            ReconcileBrowserRail(globalUi);
            MonsterIntentOverlayUI.SyncState(globalUi);
            if (root.Name == LogCollector.LogViewerRootName) {
                Callable.From(() => {
                    LogCollector.SyncLogViewerOpen(globalUi);
                    RefreshRailHintPresentation();
                }).CallDeferred();
            }
            Callable.From(TryFinalizeHotkeyRailDismiss).CallDeferred();
        };
    }

    internal static Control CreateBrowserPanelClipHost() {
        var clipHost = new Control {
            Name = BrowserPanelClipHostName,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        clipHost.AnchorLeft = 0;
        clipHost.AnchorRight = 1;
        clipHost.AnchorTop = 0;
        clipHost.AnchorBottom = 1;
        clipHost.OffsetLeft = BrowserPanelLeft;
        clipHost.OffsetRight = 0;
        clipHost.OffsetTop = 0;
        clipHost.OffsetBottom = 0;
        return clipHost;
    }

    internal static void RequestCloseBrowserOverlay(NGlobalUi globalUi, string rootName, Action fallbackClose) {
        _controller.Deactivate();
        OnRailPanelDismissed();

        var parent = (Node)globalUi;
        var root = parent.GetNodeOrNull<Control>(rootName);
        if (root == null) {
            fallbackClose();
            return;
        }

        if (TryAnimateBrowserOverlayClose(parent, root))
            return;

        fallbackClose();
    }

    internal static bool TryAnimateBrowserOverlayClose(Node parent, Control root) {
        var clipHost = root.GetNodeOrNull<Control>(BrowserPanelClipHostName);
        if (!root.HasMeta(DualCarrierMetaKey))
            return false;
        var carrierName = root.GetMeta(DualCarrierMetaKey).AsString();
        var mover = clipHost?.GetNodeOrNull<Control>(carrierName);
        if (mover == null)
            return false;

        if (root.HasMeta(BrowserPanelClosingMetaKey) && root.GetMeta(BrowserPanelClosingMetaKey).AsBool())
            return true;

        root.SetMeta(BrowserPanelClosingMetaKey, true);
        root.SetMeta(BrowserPanelAnimatingMetaKey, true);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;

        float startX = mover.Position.X;
        float w = Mathf.Max(1f, mover.GetRect().Size.X);
        float endX = startX - w;

        var tween = mover.CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic);
        tween.SetEase(Tween.EaseType.In);
        tween.TweenProperty(mover, "position:x", endX, 0.26f);
        tween.Chain().TweenCallback(Callable.From(() => {
            root.SetMeta(BrowserPanelAnimatingMetaKey, false);
            if (root.IsInsideTree()) {
                var p = root.GetParent();
                p?.RemoveChild(root);
                root.QueueFree();
            }
        }));

        return true;
    }

    internal static void PlaySubPanelSlideOpenFromLeft(Control mover) {
        if (!mover.IsInsideTree())
            return;

        float w = Mathf.Max(1f, mover.GetRect().Size.X);
        if (w < 2f)
            return;

        mover.SetMeta(BrowserPanelAnimatingMetaKey, true);
        float targetX = mover.Position.X;
        float startX = targetX - w;
        mover.Position = new Vector2(startX, mover.Position.Y);

        var t = mover.CreateTween();
        t.SetTrans(Tween.TransitionType.Quint);
        t.SetEase(Tween.EaseType.Out);
        t.TweenProperty(mover, "position:x", targetX, 0.82f);
        t.Chain()
            .TweenCallback(Callable.From(() => mover.SetMeta(BrowserPanelAnimatingMetaKey, false)));
    }

    internal static void PlayBrowserPanelOpenFromLeft(PanelContainer panel, float durationSec = 0.82f) =>
        PlayControlSlideOpenFromLeft(panel, durationSec);

    internal static void PlayControlSlideOpenFromLeft(Control panel, float durationSec = 0.82f) {
        if (!panel.IsInsideTree())
            return;

        float panelWidth = panel.GetRect().Size.X;
        if (panelWidth < 1f)
            return;

        panel.SetMeta(BrowserPanelAnimatingMetaKey, true);
        float targetX = panel.Position.X;
        float startX = targetX - panelWidth;
        panel.Position = new Vector2(startX, panel.Position.Y);

        var t = panel.CreateTween();
        t.SetTrans(Tween.TransitionType.Quint);
        t.SetEase(Tween.EaseType.Out);
        t.TweenProperty(panel, "position:x", targetX, durationSec);
        t.Chain()
            .TweenCallback(Callable.From(() => {
                panel.Position = new Vector2(targetX, 0f);
                panel.SetMeta(BrowserPanelAnimatingMetaKey, false);
            }));
    }
}
