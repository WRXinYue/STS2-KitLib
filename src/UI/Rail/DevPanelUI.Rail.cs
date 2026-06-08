using System;
using System.Collections.Generic;
using KitLib.Multiplayer.Cheat;
using KitLib.Panels;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    private static VBoxContainer? _railVBox;
    private static Panel? _railIndicator;
    private static readonly List<Button> _railButtons = new();
    private static Action<int, bool>? _moveRailIndicator;

    /// <summary>Rebuilds rail icon buttons after settings change. No-op if rail is not attached.</summary>
    internal static void RebuildRail(NGlobalUi globalUi) {
        if (_railVBox == null || _railIndicator == null)
            return;

        string? activeId = _controller.ActiveTabId;
        if (activeId != null && !IsTabShownOnRail(activeId))
            _controller.CloseAll();

        ClearRailButtons();
        PopulatePrimaryRailButtons(globalUi, _railVBox, _railButtons);
        PopulateUtilityRailButtons(globalUi, _railVBox, _railButtons);

        if (activeId != null) {
            int idx = IndexOfRailButton(activeId);
            if (idx >= 0)
                _moveRailIndicator?.Invoke(idx, false);
            else {
                _railIndicator.Visible = false;
                _activeRailBtnIdx = -1;
            }
        }
        else {
            _railIndicator.Visible = false;
            _activeRailBtnIdx = -1;
        }

        RefreshRailIconTints();
        ReconcileBrowserRail(globalUi);
        RefreshRailHintPresentation();
    }

    private static bool IsTabShownOnRail(string tabId) {
        foreach (var group in new[] { DevPanelTabGroup.Primary, DevPanelTabGroup.Utility }) {
            foreach (var tab in RailTabPreferences.GetRailTabs(group)) {
                if (tab.Id == tabId)
                    return true;
            }
        }
        return false;
    }

    private static int IndexOfRailButton(string tabId) {
        for (int i = 0; i < _railButtons.Count; i++) {
            if (_railButtons[i].GetMeta("tab_id").AsString() == tabId)
                return i;
        }
        return -1;
    }

    private static void ClearRailButtons() {
        if (_railVBox == null)
            return;

        var toRemove = new List<Node>();
        foreach (var child in _railVBox.GetChildren()) {
            if (child is Button)
                toRemove.Add(child);
        }
        foreach (var node in toRemove) {
            _railVBox.RemoveChild(node);
            node.QueueFree();
        }

        _railButtons.Clear();
        _railIconButtons.Clear();
    }

    private static int FindSeparatorIndex(VBoxContainer railVBox) {
        for (int i = 0; i < railVBox.GetChildCount(); i++) {
            if (railVBox.GetChild(i) is HSeparator)
                return i;
        }
        return railVBox.GetChildCount();
    }

    private static void PopulatePrimaryRailButtons(NGlobalUi globalUi, VBoxContainer railVBox, List<Button> railButtons) {
        int insert = 0;
        foreach (var tab in RailTabPreferences.GetRailTabs(DevPanelTabGroup.Primary)) {
            var btn = CreateRailButton(globalUi, tab, railButtons);
            railVBox.AddChild(btn);
            railVBox.MoveChild(btn, insert++);
        }
    }

    private static void PopulateUtilityRailButtons(NGlobalUi globalUi, VBoxContainer railVBox, List<Button> railButtons) {
        int insert = FindSeparatorIndex(railVBox) + 1;
        foreach (var tab in RailTabPreferences.GetRailTabs(DevPanelTabGroup.Utility)) {
            var btn = CreateRailButton(globalUi, tab, railButtons);
            railVBox.AddChild(btn);
            railVBox.MoveChild(btn, insert++);
        }
    }

    private static Button CreateRailButton(NGlobalUi globalUi, IDevPanelTab tab, List<Button> railButtons) {
        var btn = CreateRailIcon(tab.Icon, tab.DisplayName);
        btn.SetMeta("tab_id", tab.Id);
        btn.SetMeta("tab_label", tab.DisplayName);
        ApplyRailTabAvailability(btn);
        var t = tab;
        btn.Pressed += () => {
            if (btn.Disabled) return;
            _controller.SwitchTo(t.Id, () => {
                int idx = railButtons.IndexOf(btn);
                if (idx >= 0)
                    _moveRailIndicator?.Invoke(idx, true);
                t.OnActivate(globalUi);
            }, () => DevPanelUI.IsRailTabPanelVisible(globalUi, t.Id));
        };
        railButtons.Add(btn);
        _railIconButtons.Add((btn, tab.Icon));
        return btn;
    }

    internal static void RefreshRailTabAvailability() {
        foreach (var btn in _railButtons)
            ApplyRailTabAvailability(btn);

        if (_railGlobalUi == null || !MpCheatUi.IsHooksDisabledInMultiplayer)
            return;
        if (_controller.ActiveTabId != MpCheatUi.HooksTabId)
            return;

        HookConfigUI.Remove(_railGlobalUi);
        _controller.Deactivate();
        _activeRailBtnIdx = -1;
        if (_railIndicator != null)
            _railIndicator.Visible = false;
        RefreshRailIconTints();
    }

    private static void ApplyRailTabAvailability(Button btn) {
        string tabId = btn.GetMeta("tab_id").AsString();
        bool disabled = tabId == MpCheatUi.HooksTabId && MpCheatUi.IsHooksDisabledInMultiplayer;
        btn.Disabled = disabled;
        btn.Modulate = disabled ? ColIconDisabled : Colors.White;
        btn.TooltipText = disabled
            ? I18N.T("mpcheat.hooks.railDisabled", "Hooks are disabled in multiplayer (not synced).")
            : btn.GetMeta("tab_label").AsString();
    }

    private static void WireRailIndicator(Panel railIndicator, List<Button> railButtons) {
        _moveRailIndicator = (btnIdx, animate) => {
            if (btnIdx < 0 || btnIdx >= railButtons.Count)
                return;
            _activeRailBtnIdx = btnIdx;
            var btn = railButtons[btnIdx];
            float top = btn.Position.Y;
            float bottom = top + btn.Size.Y;

            railIndicator.Visible = true;

            if (animate && railIndicator.IsInsideTree()) {
                var tw = railIndicator.CreateTween();
                tw.SetParallel(true);
                tw.TweenProperty(railIndicator, "offset_top", top, 0.25f)
                  .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
                tw.TweenProperty(railIndicator, "offset_bottom", bottom, 0.25f)
                  .SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            }
            else {
                railIndicator.OffsetTop = top;
                railIndicator.OffsetBottom = bottom;
            }

            RefreshRailIconTints();
        };
    }
}
