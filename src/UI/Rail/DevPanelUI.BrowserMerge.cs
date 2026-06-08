using System;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private const float ContextPaneDefaultTop = 0.15f;
    private const float ContextPaneDefaultBottom = 0.85f;
    private const float MergeSnapPixels = 12f;
    private const float WidthMergeReleasePixels = 36f;

    private static NGlobalUi? _mergeGlobalUi;
    private static PanelContainer? _mergeBrowserPanel;
    private static VBoxContainer? _mergeContent;
    private static bool _mergeLayoutPending;
    private static bool _widthMergeActive;

    private static bool _mergeTrackingActive;
    private static readonly Callable MergeLayoutCallable = Callable.From((Action)OnMergeLayoutChanged);

    internal static void WireBrowserPanelMergeTracking(NGlobalUi globalUi, PanelContainer panel) {
        UnwireBrowserPanelMergeTracking();
        _mergeTrackingActive = true;
        _mergeGlobalUi = globalUi;
        _mergeBrowserPanel = panel;
        _mergeContent = panel.GetNodeOrNull<VBoxContainer>("Content");

        if (!panel.IsConnected(Control.SignalName.Resized, MergeLayoutCallable))
            panel.Connect(Control.SignalName.Resized, MergeLayoutCallable);
        panel.TreeExiting += OnMergeBrowserPanelExiting;

        if (_mergeContent != null) {
            if (!_mergeContent.IsConnected(Control.SignalName.Resized, MergeLayoutCallable))
                _mergeContent.Connect(Control.SignalName.Resized, MergeLayoutCallable);
            if (!_mergeContent.IsConnected(Control.SignalName.MinimumSizeChanged, MergeLayoutCallable))
                _mergeContent.Connect(Control.SignalName.MinimumSizeChanged, MergeLayoutCallable);
            WireScrollContainers(_mergeContent);
        }

        ScheduleBrowserContextMerge(globalUi);
    }

    private static void UnwireBrowserPanelMergeTracking() {
        if (!_mergeTrackingActive)
            return;

        _mergeTrackingActive = false;
        if (_mergeBrowserPanel != null && GodotObject.IsInstanceValid(_mergeBrowserPanel))
            DisconnectMergeLayout(_mergeBrowserPanel, Control.SignalName.Resized);
        if (_mergeContent != null && GodotObject.IsInstanceValid(_mergeContent)) {
            DisconnectMergeLayout(_mergeContent, Control.SignalName.Resized);
            DisconnectMergeLayout(_mergeContent, Control.SignalName.MinimumSizeChanged);
            UnwireScrollContainers(_mergeContent);
        }
        _mergeBrowserPanel = null;
        _mergeContent = null;
        _mergeGlobalUi = null;
        _widthMergeActive = false;
    }

    private static void DisconnectMergeLayout(GodotObject source, StringName signal) {
        if (!GodotObject.IsInstanceValid(source)) return;
        if (source.IsConnected(signal, MergeLayoutCallable))
            source.Disconnect(signal, MergeLayoutCallable);
    }

    private static void WireScrollContainers(Node parent) {
        foreach (var node in parent.GetChildren()) {
            if (!GodotObject.IsInstanceValid(node))
                continue;
            if (node is ScrollContainer scroll) {
                if (!scroll.IsConnected(Control.SignalName.Resized, MergeLayoutCallable))
                    scroll.Connect(Control.SignalName.Resized, MergeLayoutCallable);
                if (scroll.GetVScrollBar() is Godot.Range vBar
                    && !vBar.IsConnected(Godot.Range.SignalName.Changed, MergeLayoutCallable)) {
                    vBar.Connect(Godot.Range.SignalName.Changed, MergeLayoutCallable);
                }
                if (scroll.GetChildCount() > 0 && scroll.GetChild(0) is Control inner) {
                    if (!inner.IsConnected(Control.SignalName.Resized, MergeLayoutCallable))
                        inner.Connect(Control.SignalName.Resized, MergeLayoutCallable);
                    if (!inner.IsConnected(Control.SignalName.MinimumSizeChanged, MergeLayoutCallable))
                        inner.Connect(Control.SignalName.MinimumSizeChanged, MergeLayoutCallable);
                }
            }
            else if (node is Control && node.GetChildCount() > 0) {
                WireScrollContainers(node);
            }
        }
    }

    private static void UnwireScrollContainers(Node parent) {
        foreach (var node in parent.GetChildren()) {
            if (!GodotObject.IsInstanceValid(node))
                continue;
            if (node is ScrollContainer scroll) {
                DisconnectMergeLayout(scroll, Control.SignalName.Resized);
                if (scroll.GetVScrollBar() is Godot.Range vBar)
                    DisconnectMergeLayout(vBar, Godot.Range.SignalName.Changed);
                if (scroll.GetChildCount() > 0 && scroll.GetChild(0) is Control inner) {
                    DisconnectMergeLayout(inner, Control.SignalName.Resized);
                    DisconnectMergeLayout(inner, Control.SignalName.MinimumSizeChanged);
                }
            }
            else if (node is Control && node.GetChildCount() > 0) {
                UnwireScrollContainers(node);
            }
        }
    }

    private static void OnMergeBrowserPanelExiting() {
        var ui = _mergeGlobalUi;
        UnwireBrowserPanelMergeTracking();
        if (ui != null)
            ScheduleBrowserContextMerge(ui);
    }

    private static void OnMergeLayoutChanged() {
        if (_mergeGlobalUi != null)
            ScheduleBrowserContextMerge(_mergeGlobalUi);
    }

    private static void ScheduleBrowserContextMerge(NGlobalUi globalUi) {
        if (_mergeLayoutPending)
            return;
        _mergeLayoutPending = true;
        Callable.From(() => {
            _mergeLayoutPending = false;
            UpdateBrowserContextMerge(globalUi, (_browserOverlayCount + _browserRailHoldCount) > 0);
        }).CallDeferred();
    }

    private static void UpdateBrowserContextMerge(NGlobalUi globalUi, bool browserOpen) {
        if (!GodotObject.IsInstanceValid(_contextPane))
            return;

        if (!_contextPaneShown
            || !browserOpen
            || _mergeBrowserPanel == null
            || !GodotObject.IsInstanceValid(_mergeBrowserPanel)
            || _mergeContent == null
            || !GodotObject.IsInstanceValid(_mergeContent)) {
            ResetContextPaneLayout();
            SpliceContextPane(false);
            return;
        }

        var panel = _mergeBrowserPanel;
        bool widthMerge = ShouldWidthMerge(panel);
        if (widthMerge)
            SnapBrowserPanelToMergedWidth(panel);

        ApplyContextPaneLayout(panel, widthMerge);
        SpliceContextPane(widthMerge);
        SpliceBrowserPanelRight(panel, widthMerge);
    }

    internal static float GetMergedBrowserPanelWidth(PanelContainer panel) {
        var host = panel.GetParentControl();
        if (host == null)
            return GetMaxBrowserPanelWidth(panel);
        return Math.Max(BrowserPanelWidthMin, host.Size.X - EffectiveBrowserContentRight);
    }

    private static float GetCurrentBrowserPanelWidth(PanelContainer panel) =>
        panel.GetRect().Size.X;

    private static bool IsBrowserPanelFullWidth(PanelContainer panel) =>
        panel.AnchorRight >= 0.5f;

    private static bool ShouldWidthMerge(PanelContainer panel) {
        if (IsBrowserPanelFullWidth(panel)) {
            _widthMergeActive = true;
            return true;
        }

        float mergedMax = GetMergedBrowserPanelWidth(panel);
        float current = GetCurrentBrowserPanelWidth(panel);

        if (_widthMergeActive) {
            if (current <= mergedMax - WidthMergeReleasePixels)
                _widthMergeActive = false;
            else
                return true;
        }

        if (current >= mergedMax - MergeSnapPixels) {
            _widthMergeActive = true;
            return true;
        }

        return false;
    }

    private static void SnapBrowserPanelToMergedWidth(PanelContainer panel) {
        if (IsBrowserPanelFullWidth(panel))
            return;

        panel.AnchorLeft = 0;
        panel.AnchorRight = 1;
        panel.OffsetLeft = 0;
        panel.OffsetRight = -EffectiveBrowserContentRight;
    }

    internal static void NotifyBrowserContextLayoutChanged(NGlobalUi globalUi) {
        ScheduleBrowserContextMerge(globalUi);
        Callable.From(() => ScheduleBrowserContextMerge(globalUi)).CallDeferred();
        Callable.From(() => {
            Callable.From(() => ScheduleBrowserContextMerge(globalUi)).CallDeferred();
        }).CallDeferred();
    }

    private static void ApplyContextPaneLayout(PanelContainer browserPanel, bool widthMerge) {
        if (!GodotObject.IsInstanceValid(_contextPane))
            return;

        if (!widthMerge) {
            RestoreContextPaneStandaloneLayout(_contextPaneShown);
            return;
        }

        var parent = _contextPane.GetParentControl();
        if (parent == null)
            return;

        var browserRect = browserPanel.GetGlobalRect();
        var parentRect = parent.GetGlobalRect();
        float localTop = browserRect.Position.Y - parentRect.Position.Y;
        float paneHeight = browserRect.Size.Y;

        _contextPane.AnchorLeft = 1;
        _contextPane.AnchorTop = 0;
        _contextPane.AnchorRight = 1;
        _contextPane.AnchorBottom = 0;
        _contextPane.OffsetLeft = ContextPaneVisibleOffsetLeft;
        _contextPane.OffsetRight = ContextPaneVisibleOffsetRight;
        _contextPane.OffsetTop = localTop;
        _contextPane.OffsetBottom = localTop + paneHeight;
    }

    private static void ResetContextPaneLayout() {
        if (!GodotObject.IsInstanceValid(_contextPane))
            return;

        RestoreContextPaneStandaloneLayout(_contextPaneShown);

        if (_mergeBrowserPanel != null && GodotObject.IsInstanceValid(_mergeBrowserPanel))
            SpliceBrowserPanelRight(_mergeBrowserPanel, joined: false);
    }

    internal static void RestoreContextPaneStandaloneLayout(bool visible) {
        if (!GodotObject.IsInstanceValid(_contextPane))
            return;

        _contextPane.AnchorLeft = 1;
        _contextPane.AnchorTop = ContextPaneDefaultTop;
        _contextPane.AnchorRight = 1;
        _contextPane.AnchorBottom = ContextPaneDefaultBottom;
        _contextPane.OffsetTop = 0;
        _contextPane.OffsetBottom = 0;

        if (visible) {
            _contextPane.OffsetLeft = ContextPaneVisibleOffsetLeft;
            _contextPane.OffsetRight = ContextPaneVisibleOffsetRight;
        }
        else {
            _contextPane.OffsetLeft = ContextPaneHiddenOffsetLeft;
            _contextPane.OffsetRight = ContextPaneHiddenOffsetRight;
        }
    }

    private static void SpliceBrowserPanelRight(PanelContainer panel, bool joined) {
        if (!GodotObject.IsInstanceValid(panel))
            return;
        if (panel.GetThemeStylebox("panel") is not StyleBoxFlat sb)
            return;

        int r = joined ? 0 : BrowserRailRadius;
        sb.CornerRadiusTopRight = r;
        sb.CornerRadiusBottomRight = r;
        sb.BorderWidthRight = joined ? 0 : 1;
        if (joined) {
            sb.ShadowOffset = Vector2.Zero;
            sb.ShadowSize = 16;
        }
        else {
            sb.ShadowOffset = new Vector2(20, 0);
            sb.ShadowSize = 20;
        }
    }
}
