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
        AcquireOverlayRailPin(globalUi, root);
        MonsterIntentOverlayUI.SyncState(globalUi);
        root.TreeExiting += () => {
            ReleaseOverlayRailPin(globalUi, root);
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

    /// <summary>
    /// Detach and free an overlay immediately so a same-name replacement cannot stack with a
    /// still-queued-free node (e.g. Settings theme rebuild).
    /// </summary>
    internal static void DiscardOverlayImmediate(NGlobalUi globalUi, string rootName) {
        var parent = (Node)globalUi;
        for (var i = parent.GetChildCount() - 1; i >= 0; i--) {
            if (parent.GetChild(i) is not Control child)
                continue;
            var name = child.Name.ToString();
            if (!IsOverlayNameOrDuplicate(name, rootName))
                continue;
            parent.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static bool IsOverlayNameOrDuplicate(string name, string rootName) {
        if (name == rootName)
            return true;
        if (!name.StartsWith(rootName, StringComparison.Ordinal) || name.Length <= rootName.Length)
            return false;
        for (var i = rootName.Length; i < name.Length; i++) {
            if (!char.IsDigit(name[i]))
                return false;
        }
        return true;
    }

    internal static bool TryAnimateBrowserOverlayClose(Node parent, Control root) {
        return AnimateOverlayOut(parent, root, () => AnimateOverlayOutFree(root));
    }

    private static void AnimateOverlayOutFree(Control root) {
        if (root.IsInsideTree()) {
            var p = root.GetParent();
            p?.RemoveChild(root);
            root.QueueFree();
        }
    }

    /// <summary>
    /// Slides the overlay's carrier out to the left, then invokes <paramref name="onComplete"/>.
    /// Handles the closing/animating meta so a same-name replacement cannot stack with a
    /// still-queued-free node. Callers choose the terminal action:
    /// destroy (via <see cref="TryAnimateBrowserOverlayClose"/>) or persist-hide.
    /// </summary>
    private static bool AnimateOverlayOut(Node parent, Control root, Action onComplete) {
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

        // Center fade-out (was slide-out to the left).
        mover.PivotOffset = mover.Size * 0.5f;
        var tween = mover.CreateTween();
        tween.SetTrans(Tween.TransitionType.Quart);
        tween.SetEase(Tween.EaseType.In);
        tween.Parallel().TweenProperty(mover, "modulate:a", 0f, 0.18f);
        tween.Parallel().TweenProperty(mover, "scale", new Vector2(0.94f, 0.94f), 0.18f);
        tween.Chain().TweenCallback(Callable.From(() => {
            root.SetMeta(BrowserPanelAnimatingMetaKey, false);
            onComplete();
        }));

        return true;
    }

    /// <summary>
    /// Persistent hide (always-visible collapse): slides the content panel out, marks it
    /// session-cached and hides it (without destroying), so the next rail click on that tab
    /// restores it via <see cref="DevPanelUI.TryRevealRailTab"/>.
    /// </summary>
    internal static void HideBrowserOverlayPersistent(NGlobalUi globalUi, Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;

        _controller.Deactivate();
        OnRailPanelDismissed();

        var parent = (Node)globalUi;
        if (AnimateOverlayOut(parent, root, () => HideSessionOverlay(globalUi, root)))
            return;

        HideSessionOverlay(globalUi, root);
    }

    internal static void PlaySubPanelSlideOpenFromLeft(Control mover, Action? onFinished = null) =>
        PlayCenterPop(mover, onFinished: onFinished);

    internal static void PlayBrowserPanelOpenFromLeft(PanelContainer panel, float durationSec = 0.82f) =>
        PlayCenterPop(panel, durationSec);

    internal static void PlayControlSlideOpenFromLeft(Control panel, float durationSec = 0.82f) =>
        PlayCenterPop(panel, durationSec);

    /// <summary>Pops a panel into view from the center (fade + slight scale), keeping meta-key state.</summary>
    private static void PlayCenterPop(Control panel, float durationSec = 0.82f, Action? onFinished = null) {
        if (!panel.IsInsideTree())
            return;

        panel.SetMeta(BrowserPanelAnimatingMetaKey, true);
        panel.PivotOffset = panel.Size * 0.5f;
        panel.Scale = new Vector2(0.82f, 0.82f);
        panel.Modulate = new Color(1f, 1f, 1f, 0f);
        panel.Visible = true;

        var t = panel.CreateTween();
        t.SetTrans(Tween.TransitionType.Quart);
        t.SetEase(Tween.EaseType.Out);
        t.Parallel().TweenProperty(panel, "scale", Vector2.One, durationSec);
        t.Parallel().TweenProperty(panel, "modulate:a", 1f, durationSec * 0.6f);
        t.Chain().TweenCallback(Callable.From(() => {
            panel.Scale = Vector2.One;
            panel.Modulate = new Color(1f, 1f, 1f, 1f);
            panel.SetMeta(BrowserPanelAnimatingMetaKey, false);
            onFinished?.Invoke();
        }));
    }
}
