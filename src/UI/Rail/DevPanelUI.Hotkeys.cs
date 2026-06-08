using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Panels;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private static bool _railShown;
    private static bool _keyboardRailPinned;
    private static Action<bool, bool>? _slideRail;

    internal static bool IsRailAttached => _railGlobalUi != null;

    internal static bool HasOpenPanel =>
        _controller.ActiveTabId != null || _activeOverlayId != null;

    internal static void BindRailSlide(Action<bool, bool> slideRail) => _slideRail = slideRail;

    internal static void ToggleRailExpanded() {
        if (_slideRail == null)
            return;

        if (_keyboardRailPinned || _railShown) {
            _keyboardRailPinned = false;
            if (_activeOverlayId == null && _pinRailCount == 0)
                _slideRail(false, false);
            return;
        }

        _keyboardRailPinned = true;
        _slideRail(true, true);
    }

    internal static void ToggleRailKeyboardPin() {
        if (_slideRail == null)
            return;

        _keyboardRailPinned = !_keyboardRailPinned;
        if (_keyboardRailPinned)
            _slideRail(true, true);
        else if (_activeOverlayId == null && _pinRailCount == 0 && _railShown)
            _slideRail(false, false);
    }

    internal static void EnsureRailExpanded() {
        if (_slideRail == null || _railShown)
            return;
        _keyboardRailPinned = true;
        _slideRail(true, true);
    }

    internal static void CloseActivePanel() {
        if (_railGlobalUi == null)
            return;
        _controller.CloseAll();
    }

    internal static void CycleRailTab(int direction) {
        if (_railGlobalUi == null || direction == 0)
            return;

        var tabs = GetOrderedVisibleRailTabs()
            .Where(t => {
                int idx = IndexOfRailButton(t.Id);
                return idx >= 0 && !_railButtons[idx].Disabled;
            })
            .ToList();
        if (tabs.Count == 0)
            return;

        string? activeId = _controller.ActiveTabId;
        int current = activeId != null
            ? tabs.FindIndex(t => t.Id == activeId)
            : -1;

        int next;
        if (current < 0)
            next = direction > 0 ? 0 : tabs.Count - 1;
        else {
            next = (current + direction + tabs.Count) % tabs.Count;
            if (next == current)
                return;
        }

        EnsureRailExpanded();
        ActivateRailTab(tabs[next].Id);
    }

    internal static void ActivateRailTab(string tabId) {
        if (_railGlobalUi == null)
            return;

        int idx = IndexOfRailButton(tabId);
        if (idx < 0 || idx >= _railButtons.Count)
            return;

        var btn = _railButtons[idx];
        if (btn.Disabled)
            return;

        IDevPanelTab? tab = null;
        foreach (var group in new[] { DevPanelTabGroup.Primary, DevPanelTabGroup.Utility }) {
            foreach (var candidate in RailTabPreferences.GetRailTabs(group)) {
                if (candidate.Id == tabId) {
                    tab = candidate;
                    break;
                }
            }
            if (tab != null)
                break;
        }
        if (tab == null)
            return;

        var globalUi = _railGlobalUi;
        var captured = tab;
        _controller.SwitchTo(tabId, () => {
            _moveRailIndicator?.Invoke(idx, true);
            captured.OnActivate(globalUi);
            ((Node)globalUi).GetViewport()?.GuiReleaseFocus();
        }, () => IsRailTabPanelVisible(globalUi, tabId));
    }

    internal static IReadOnlyList<IDevPanelTab> GetOrderedVisibleRailTabs() {
        var list = new List<IDevPanelTab>();
        list.AddRange(RailTabPreferences.GetRailTabs(DevPanelTabGroup.Primary));
        list.AddRange(RailTabPreferences.GetRailTabs(DevPanelTabGroup.Utility));
        return list;
    }

    internal static void RefreshPeekTabHotkeyHint() {
        if (_peekTabBtn == null || !GodotObject.IsInstanceValid(_peekTabBtn))
            return;
        if (!SettingsStore.Current.HotkeysEnabled) {
            _peekTabBtn.TooltipText = I18N.T("rail.peekHint", "Show DevMode sidebar");
            return;
        }
        var label = SettingsStore.Current.HotkeyToggleRail.FormatLabel();
        _peekTabBtn.TooltipText = I18N.T("rail.peekHotkey", "Show DevMode sidebar ({0})", label);
    }

    internal static void ResetRailHotkeyState() {
        _railShown = false;
        _keyboardRailPinned = false;
        _slideRail = null;
    }
}
