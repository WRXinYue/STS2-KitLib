using System;
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
    private Action? _closeAllPanels;

    // ── Lifecycle ─────────────────────────────────────────────────────────

    /// <summary>
    /// Binds the controller to a rail session.
    /// <paramref name="closeAllPanels"/> performs the Godot-side close work
    /// (animations, QueueFree, rail hold/release) without touching tab state.
    /// </summary>
    public void Attach(Action closeAllPanels) {
        _closeAllPanels = closeAllPanels;
        Reset();
    }

    /// <summary>Id of the tab whose panel is open, or null.</summary>
    public string? ActiveTabId => _activeTabId;

    /// <summary>Unbinds the controller when the rail is detached.</summary>
    public void Detach() {
        Reset();
        _closeAllPanels = null;
    }

    // ── Core operations ───────────────────────────────────────────────────

    /// <summary>
    /// Switches to <paramref name="tabId"/>. No-op if that tab is already active and
    /// <paramref name="isPanelVisible"/> is null or returns true. When the tab is marked
    /// active but its panel was removed without <see cref="Deactivate"/> (e.g. header close),
    /// pass <paramref name="isPanelVisible"/> so the panel can be reopened.
    /// </summary>
    public void SwitchTo(string tabId, Action openPanel, Func<bool>? isPanelVisible = null) {
        if (_activeTabId == tabId) {
            if (isPanelVisible?.Invoke() ?? true)
                return;
            Reset();
        }

        _activeTabId = tabId;
        _closeAllPanels?.Invoke();
        openPanel();
    }

    /// <summary>
    /// Clears the active tab (user explicitly closed the panel via backdrop/button).
    /// Allows the same tab to be reopened on the next click.
    /// </summary>
    public void Deactivate() => Reset();

    /// <summary>
    /// Closes all open panels visually without touching <c>_activeTabId</c>.
    /// Used by <see cref="DevPanelUI.CloseAllOverlays"/>, which is called from
    /// <c>TryDismissCurrent</c> during panel switching. At that point
    /// <c>_activeTabId</c> already holds the incoming tab's ID, so clearing
    /// it here would destroy the duplicate-click guard on the very next click.
    /// </summary>
    public void CloseVisuals() => _closeAllPanels?.Invoke();

    /// <summary>
    /// Deactivates the current tab AND closes all open panels.
    /// For full resets only (scene transitions go through <see cref="Detach"/> instead).
    /// </summary>
    public void CloseAll() {
        Deactivate();
        _closeAllPanels?.Invoke();
    }

    // ── Private ───────────────────────────────────────────────────────────

    private void Reset() => _activeTabId = null;
}
