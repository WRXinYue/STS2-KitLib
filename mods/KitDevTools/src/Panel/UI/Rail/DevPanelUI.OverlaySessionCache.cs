using System;
using System.Collections.Generic;
using Godot;
using KitLib.UI.Diagnostics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal const string SessionCachedMetaKey = "dm_session_cached";
    private const string OverlayRailPinnedMetaKey = "dm_overlay_rail_pinned";

    private static readonly Dictionary<string, Func<NGlobalUi, bool>> SessionRevealHandlers = new(StringComparer.Ordinal);
    private static bool _sessionRevealRegistered;

    internal static void EnsureSessionRevealHandlers() {
        if (_sessionRevealRegistered)
            return;
        _sessionRevealRegistered = true;
        RegisterSessionReveal("devmode.powers", PowerSelectUI.TryReveal);
        RegisterSessionReveal("devmode.relics", RelicBrowserUI.TryReveal);
        // Card browser is shared with Card Test (picker mode) — never session-reveal it.
    }

    internal static void RegisterSessionReveal(string tabId, Func<NGlobalUi, bool> handler) {
        SessionRevealHandlers[tabId] = handler;
    }

    internal static bool IsSessionCached(Control root) =>
        GodotObject.IsInstanceValid(root) && root.HasMeta(SessionCachedMetaKey)
        && root.GetMeta(SessionCachedMetaKey).AsBool();

    internal static void MarkSessionCached(Control root) => root.SetMeta(SessionCachedMetaKey, true);

    internal static bool TryRevealRailTab(NGlobalUi globalUi, string tabId) {
        EnsureSessionRevealHandlers();
        if (SessionRevealHandlers.TryGetValue(tabId, out var handler) && handler(globalUi))
            return true;
        return TryRevealGenericSessionOverlay(globalUi, tabId);
    }

    internal static void HideAllSessionOverlays(NGlobalUi globalUi) {
        var close = CardBrowserPerf.Start();
        HoldBrowserRail(globalUi);
        CloseOverlay(globalUi);

        var parent = (Node)globalUi;
        int hidden = 0;
        foreach (var child in parent.GetChildren()) {
            if (child is not Control ctrl)
                continue;
            string name = ctrl.Name.ToString();
            if (!ShouldManageSessionOverlay(name))
                continue;
            if (!ctrl.Visible && IsSessionCached(ctrl))
                continue;
            // Card browser is shared by Cards + Card Test; caching one mode poisons the other.
            if (name == CardBrowserUI.NodeName) {
                ReleaseOverlayRailPin(globalUi, ctrl);
                parent.RemoveChild(ctrl);
                ctrl.QueueFree();
                hidden++;
                continue;
            }
            HideSessionOverlay(globalUi, ctrl);
            hidden++;
        }

        ReconcileBrowserRail(globalUi);
        Callable.From(() => ReleaseBrowserRail(globalUi)).CallDeferred();
        CardBrowserPerf.LogRail("closeAll.work", close, $"hidden={hidden}");
    }

    internal static void DestroyAllSessionOverlays(NGlobalUi globalUi) {
        HoldBrowserRail(globalUi);
        CloseOverlay(globalUi);

        var parent = (Node)globalUi;
        for (int i = parent.GetChildCount() - 1; i >= 0; i--) {
            if (parent.GetChild(i) is not Control ctrl)
                continue;
            if (!ShouldManageSessionOverlay(ctrl.Name.ToString()))
                continue;
            parent.RemoveChild(ctrl);
            ctrl.QueueFree();
        }

        Callable.From(() => ReleaseBrowserRail(globalUi)).CallDeferred();
    }

    internal static void DestroySessionOverlay(NGlobalUi globalUi, string rootName) {
        var parent = (Node)globalUi;
        for (int i = parent.GetChildCount() - 1; i >= 0; i--) {
            if (parent.GetChild(i) is not Control child)
                continue;
            var name = child.Name.ToString();
            if (!IsOverlayNameOrDuplicate(name, rootName))
                continue;
            parent.RemoveChild(child);
            child.QueueFree();
            return;
        }
    }

    internal static void RevealSessionOverlay(NGlobalUi globalUi, Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;

        ResetSessionOverlayMover(root);
        root.Visible = true;
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AcquireOverlayRailPin(globalUi, root);
        MonsterIntentOverlayUI.SyncState(globalUi);
        PlaySessionRevealOpenAnimation(root);
    }

    private static void PlaySessionRevealOpenAnimation(Control root) {
        if (!root.HasMeta(DualCarrierMetaKey))
            return;

        var clipHost = root.GetNodeOrNull<Control>(BrowserPanelClipHostName);
        var carrierName = root.GetMeta(DualCarrierMetaKey).AsString();
        var mover = clipHost?.GetNodeOrNull<Control>(carrierName);
        if (mover == null)
            return;

        Callable.From(() => PlaySubPanelSlideOpenFromLeft(mover)).CallDeferred();
    }

    /// <summary>
    /// Pair with <see cref="ReleaseOverlayRailPin"/> so session-hidden overlays do not keep the
    /// rail spliced / pinned after they are no longer visible.
    /// </summary>
    internal static void AcquireOverlayRailPin(NGlobalUi globalUi, Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;
        if (root.HasMeta(OverlayRailPinnedMetaKey) && root.GetMeta(OverlayRailPinnedMetaKey).AsBool())
            return;

        root.SetMeta(OverlayRailPinnedMetaKey, true);
        _browserOverlayCount++;
        PinRail();
        ReconcileBrowserRail(globalUi);
    }

    internal static void ReleaseOverlayRailPin(NGlobalUi globalUi, Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;
        if (!root.HasMeta(OverlayRailPinnedMetaKey) || !root.GetMeta(OverlayRailPinnedMetaKey).AsBool())
            return;

        root.SetMeta(OverlayRailPinnedMetaKey, false);
        _browserOverlayCount = Math.Max(0, _browserOverlayCount - 1);
        UnpinRail();
        ReconcileBrowserRail(globalUi);
    }

    private static bool TryRevealGenericSessionOverlay(NGlobalUi globalUi, string tabId) {
        if (!BrowserOverlayRootByTabId.TryGetValue(tabId, out var rootName))
            return false;

        var root = ((Node)globalUi).GetNodeOrNull<Control>(rootName);
        if (root == null || !IsSessionCached(root))
            return false;

        RevealSessionOverlay(globalUi, root);
        return true;
    }

    private static bool ShouldManageSessionOverlay(string name) =>
        name.StartsWith("KitLib", StringComparison.Ordinal) && !_keepNodes.Contains(name);

    private static void HideSessionOverlay(NGlobalUi globalUi, Control root) {
        if (!GodotObject.IsInstanceValid(root))
            return;

        KillSessionOverlayMotion(root);
        MarkSessionCached(root);
        root.Visible = false;
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        ReleaseOverlayRailPin(globalUi, root);
    }

    private static void ResetSessionOverlayMover(Control root) {
        KillSessionOverlayMotion(root);
        if (!root.HasMeta(DualCarrierMetaKey))
            return;

        var clipHost = root.GetNodeOrNull<Control>(BrowserPanelClipHostName);
        var carrierName = root.GetMeta(DualCarrierMetaKey).AsString();
        var mover = clipHost?.GetNodeOrNull<Control>(carrierName);
        if (mover == null)
            return;

        mover.Position = new Vector2(0f, mover.Position.Y);
        if (mover.HasMeta(BrowserPanelAnimatingMetaKey))
            mover.RemoveMeta(BrowserPanelAnimatingMetaKey);
    }

    private static void KillSessionOverlayMotion(Control root) {
        if (root.HasMeta(BrowserPanelClosingMetaKey))
            root.RemoveMeta(BrowserPanelClosingMetaKey);
        if (root.HasMeta(BrowserPanelAnimatingMetaKey))
            root.RemoveMeta(BrowserPanelAnimatingMetaKey);
    }
}
