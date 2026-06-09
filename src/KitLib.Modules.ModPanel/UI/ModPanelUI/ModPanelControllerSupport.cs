using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.ScreenContext;

namespace KitLib.UI;

/// <summary>Controller hotkey hints, LB/RB page tabs, and sidebar up/down mod cycling.</summary>
public partial class ModPanelControllerSupport : Node {
    private static readonly StringName TabLeftHotkey = MegaInput.viewDeckAndTabLeft;
    private static readonly StringName TabRightHotkey = MegaInput.viewExhaustPileAndTabRight;

    private HBoxContainer? _pageTabRow;
    private Func<string>? _getPageId;
    private Action<string>? _switchPage;
    private Control? _hintsRow;
    private TextureRect? _backIcon;
    private TextureRect? _selectIcon;
    private TextureRect? _tabLeftIcon;
    private TextureRect? _tabRightIcon;
    private Callable? _refreshHintsCallable;
    private ModPanelShellRoot? _shell;
    private IReadOnlyList<SidebarModRowVm> _sidebarRows = [];
    private Func<string>? _getSelectedModId;
    private Action<string>? _selectMod;
    private Control? _settingsContentRoot;

    public void Configure(HBoxContainer pageTabRow, Func<string> getPageId, Action<string> switchPage,
        Control hintsRow) {
        _pageTabRow = pageTabRow;
        _getPageId = getPageId;
        _switchPage = switchPage;
        _hintsRow = hintsRow;
        _backIcon = hintsRow.GetNodeOrNull<TextureRect>("BackHotkeyIcon");
        _selectIcon = hintsRow.GetNodeOrNull<TextureRect>("SelectHotkeyIcon");
        _tabLeftIcon = hintsRow.GetNodeOrNull<TextureRect>("TabLeftHotkeyIcon");
        _tabRightIcon = hintsRow.GetNodeOrNull<TextureRect>("TabRightHotkeyIcon");
    }

    internal void BindShell(ModPanelShellRoot shell) {
        _shell = shell;
    }

    internal void ConfigureSidebar(IReadOnlyList<SidebarModRowVm> rows, Func<string> getSelectedModId,
        Action<string> selectMod, Control settingsContentRoot) {
        _sidebarRows = rows;
        _getSelectedModId = getSelectedModId;
        _selectMod = selectMod;
        _settingsContentRoot = settingsContentRoot;
    }

    public override void _Ready() {
        _refreshHintsCallable = Callable.From(RefreshHints);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(TabLeftHotkey, TabLeft);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(TabRightHotkey, TabRight);
        if (NControllerManager.Instance != null) {
            NControllerManager.Instance.Connect(NControllerManager.SignalName.ControllerDetected,
                _refreshHintsCallable.Value);
            NControllerManager.Instance.Connect(NControllerManager.SignalName.MouseDetected,
                _refreshHintsCallable.Value);
        }
        if (NInputManager.Instance != null)
            NInputManager.Instance.Connect(NInputManager.SignalName.InputRebound, _refreshHintsCallable.Value);
        RefreshHints();
    }

    public override void _ExitTree() {
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(TabLeftHotkey, TabLeft);
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(TabRightHotkey, TabRight);
        if (_refreshHintsCallable == null)
            return;
        if (NControllerManager.Instance != null && GodotObject.IsInstanceValid(NControllerManager.Instance)) {
            NControllerManager.Instance.Disconnect(NControllerManager.SignalName.ControllerDetected,
                _refreshHintsCallable.Value);
            NControllerManager.Instance.Disconnect(NControllerManager.SignalName.MouseDetected,
                _refreshHintsCallable.Value);
        }
        if (NInputManager.Instance != null && GodotObject.IsInstanceValid(NInputManager.Instance))
            NInputManager.Instance.Disconnect(NInputManager.SignalName.InputRebound, _refreshHintsCallable.Value);
    }

    /// <summary>Handled from <see cref="ModPanelShellRoot._Input" /> (RitsuModSettingsSubmenu pattern).</summary>
    public bool TryHandleDirectionalInput(InputEvent @event) {
        if (_shell == null || !GodotObject.IsInstanceValid(_shell) || !_shell.Visible)
            return false;
        if (!ActiveScreenContext.Instance.IsCurrent(_shell))
            return false;
        if (NControllerManager.Instance?.IsUsingController != true)
            return false;
        if (@event.IsEcho())
            return false;

        int delta = 0;
        if (@event.IsActionPressed("ui_up") || @event.IsActionPressed(MegaInput.up))
            delta = -1;
        else if (@event.IsActionPressed("ui_down") || @event.IsActionPressed(MegaInput.down))
            delta = 1;
        else
            return false;

        if (_sidebarRows.Count == 0 || _getSelectedModId == null || _selectMod == null)
            return false;

        var focus = GetViewport()?.GuiGetFocusOwner() as Control;
        if (focus != null && _settingsContentRoot != null && GodotObject.IsInstanceValid(_settingsContentRoot)
            && _settingsContentRoot.IsAncestorOf(focus))
            return false;

        if (!CycleSidebarMod(delta))
            return false;
        RefreshHints();
        return true;
    }

    public void RefreshHints() {
        if (_hintsRow == null || !GodotObject.IsInstanceValid(_hintsRow))
            return;
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        _hintsRow.Visible = usingController;
        if (!usingController)
            return;
        if (_backIcon != null) {
            _backIcon.Visible = true;
            _backIcon.Texture = NInputManager.Instance.GetHotkeyIcon(MegaInput.cancel);
        }
        if (_selectIcon != null)
            _selectIcon.Visible = false;
        var tabCount = CountPageTabs();
        var showTabs = tabCount > 1;
        if (_tabLeftIcon != null) {
            _tabLeftIcon.Visible = showTabs;
            if (showTabs)
                _tabLeftIcon.Texture = NInputManager.Instance.GetHotkeyIcon(TabLeftHotkey);
        }
        if (_tabRightIcon != null) {
            _tabRightIcon.Visible = showTabs;
            if (showTabs)
                _tabRightIcon.Texture = NInputManager.Instance.GetHotkeyIcon(TabRightHotkey);
        }
    }

    private bool CycleSidebarMod(int delta) {
        var currentId = _getSelectedModId!();
        var idx = 0;
        for (var i = 0; i < _sidebarRows.Count; i++) {
            if (string.Equals(_sidebarRows[i].Id, currentId, StringComparison.OrdinalIgnoreCase)) {
                idx = i;
                break;
            }
        }
        var next = Mathf.Clamp(idx + delta, 0, _sidebarRows.Count - 1);
        if (next == idx)
            return false;
        _selectMod!(_sidebarRows[next].Id);
        return true;
    }

    private void TabLeft() {
        SwitchTab(-1);
    }

    private void TabRight() {
        SwitchTab(1);
    }

    private void SwitchTab(int delta) {
        if (_pageTabRow == null || _getPageId == null || _switchPage == null)
            return;
        if (_shell != null && !ActiveScreenContext.Instance.IsCurrent(_shell))
            return;
        var tabs = CollectTabs();
        if (tabs.Count <= 1)
            return;
        var currentId = _getPageId();
        var idx = 0;
        for (var i = 0; i < tabs.Count; i++) {
            if (!tabs[i].HasMeta("pageId"))
                continue;
            if (string.Equals(tabs[i].GetMeta("pageId").AsString(), currentId, StringComparison.OrdinalIgnoreCase)) {
                idx = i;
                break;
            }
        }
        var next = Mathf.Clamp(idx + delta, 0, tabs.Count - 1);
        if (next == idx)
            return;
        if (!tabs[next].HasMeta("pageId"))
            return;
        _switchPage(tabs[next].GetMeta("pageId").AsString());
        Callable.From(() => ((Control)tabs[next]).GrabFocus()).CallDeferred();
        RefreshHints();
    }

    private int CountPageTabs() => CollectTabs().Count;

    private List<Button> CollectTabs() {
        var tabs = new List<Button>();
        if (_pageTabRow == null)
            return tabs;
        foreach (var child in _pageTabRow.GetChildren()) {
            if (child is Button b && b.Visible)
                tabs.Add(b);
        }
        return tabs;
    }
}
