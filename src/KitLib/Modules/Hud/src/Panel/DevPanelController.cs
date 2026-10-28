using System;
using System.Diagnostics;
using KitLib.UI;
using KitLib.UI.Diagnostics;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.Panels;

/// <summary>
/// Controls which DevMode panel is currently open and mediates all tab-switch operations.
///
/// Architecture role (analogous to VSCode's ViewletService):
///   - Single authority on which tab is active (_activeTabId)
///   - Drives the open/switch/close lifecycle via injected Godot callbacks
///   - State is set/cleared ONLY by this class — never by view lifecycle events
///     (TreeExiting, QueueFree) — see DevPanelUI.BrowserOverlay.cs for that guarantee
///
/// Why no scene-tree lookup:
///   GetNodeOrNull / StringName comparisons introduce same-frame timing issues and
///   Godot C# managed-wrapper edge cases. A plain string field is always authoritative
///   when TreeExiting no longer clears it (fixed in SetupRailTransition).
/// </summary>
internal sealed class DevPanelController {
    private string? _activeTabId;
    private Action? _hideAllPanels;
    private Action? _destroyAllPanels;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Binds the controller to a rail session.
    /// <paramref name="hideAllPanels"/> hides cached browser overlays when switching tabs.
    /// <paramref name="destroyAllPanels"/> frees all overlays on full close / detach.
    /// </summary>
    public void Attach(Action hideAllPanels, Action destroyAllPanels) {
        _hideAllPanels = hideAllPanels;
        _destroyAllPanels = destroyAllPanels;
        Reset();
    }

    /// <summary>Id of the tab whose panel is open, or null.</summary>
    public string? ActiveTabId => _activeTabId;

    /// <summary>Unbinds the controller when the rail is detached.</summary>
    public void Detach() {
        Reset();
        _hideAllPanels = null;
        _destroyAllPanels = null;
    }

    // ── Core operations ───────────────────────────────────────────────────

    /// <summary>
    /// Switches to <paramref name="tabId"/>. No-op if that tab is already active and
    /// <paramref name="isPanelVisible"/> is null or returns true. When the tab is marked
    /// active but its panel was removed without <see cref="Deactivate"/> (e.g. header close),
    /// pass <paramref name="isPanelVisible"/> so the panel can be reopened.
    /// </summary>
    public void SwitchTo(string tabId, Action openPanel, Func<bool>? isPanelVisible = null) {
        var total = CardBrowserPerf.Start();
        if (_activeTabId == tabId) {
            var vis = CardBrowserPerf.Start();
            bool visible = isPanelVisible?.Invoke() ?? true;
            CardBrowserPerf.LogRail("visibleCheck", vis, $"tab={tabId} visible={visible}");
            if (visible) {
                CardBrowserPerf.LogRail("skipSameTab", total, $"tab={tabId}");
                return;
            }
            Reset();
        }

        var from = _activeTabId ?? "none";
        _activeTabId = tabId;
        var close = CardBrowserPerf.Start();
        _hideAllPanels?.Invoke();
        CardBrowserPerf.LogRail("closeAll", close, $"from={from} to={tabId}");
        var open = CardBrowserPerf.Start();
        openPanel();
        CardBrowserPerf.LogRail("openPanel", open, $"tab={tabId}");
        CardBrowserPerf.LogRail("total", total, $"from={from} to={tabId}");
    }

    /// <summary>
    /// Clears the active tab (user explicitly closed the panel via backdrop/button).
    /// Allows the same tab to be reopened on the next click.
    /// </summary>
    public void Deactivate() => Reset();

    /// <summary>
    /// Hides cached browser overlays without freeing them or clearing <c>_activeTabId</c>.
    /// Used by <see cref="DevPanelUI.CloseAllOverlays"/> / <c>TryDismissCurrent</c> while
    /// switching tabs so session-cached panels survive.
    /// </summary>
    public void CloseVisuals() => _hideAllPanels?.Invoke();

    /// <summary>
    /// Deactivates the current tab AND closes all open panels.
    /// For full resets only (scene transitions go through <see cref="Detach"/> instead).
    /// </summary>
    public void CloseAll() {
        Deactivate();
        _destroyAllPanels?.Invoke();
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void Reset() => _activeTabId = null;
}
