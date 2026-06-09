using System;
using System.Collections.Generic;
using Godot;
using KitLib.Abstractions.Modding;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
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
    private ModPanelSubmenu? _submenu;
    private IReadOnlyList<SidebarModRowVm> _sidebarRows = [];
    private Func<string>? _getSelectedModId;
    private Action<string>? _selectMod;
    private Control? _settingsContentRoot;
    private bool _lastUsingController;

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

    internal void BindSubmenu(ModPanelSubmenu submenu) {
        _submenu = submenu;
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
        _lastUsingController = NControllerManager.Instance?.IsUsingController == true;
        RefreshHints();
    }

    public override void _Process(double delta) {
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        if (usingController == _lastUsingController)
            return;
        _lastUsingController = usingController;
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

    /// <summary>Handled from <see cref="ModPanelSubmenu._Input" /> (RitsuModSettingsSubmenu pattern).</summary>
    public bool TryHandleDirectionalInput(InputEvent @event) {
        if (!IsDirectionalPress(@event, out var delta, out var action))
            return false;

        var skip = DescribeSkipReason();
        if (skip != null) {
            MainFile.Logger.Info(ModPanelDiagnosticLog.FormatControllerInput(
                action, handled: false, skip, _getSelectedModId?.Invoke()));
            return false;
        }

        var focus = GetViewport()?.GuiGetFocusOwner() as Control;
        if (focus != null && _settingsContentRoot != null && GodotObject.IsInstanceValid(_settingsContentRoot)
            && _settingsContentRoot.IsAncestorOf(focus)) {
            MainFile.Logger.Info(ModPanelDiagnosticLog.FormatControllerInput(
                action, handled: false, "focusInSettingsContent", _getSelectedModId?.Invoke()));
            return false;
        }

        if (!CycleSidebarMod(delta)) {
            MainFile.Logger.Info(ModPanelDiagnosticLog.FormatControllerInput(
                action, handled: false, "cycleBlocked", _getSelectedModId?.Invoke()));
            return false;
        }

        SidebarModRowVm? focusedRow = null;
        var currentId = _getSelectedModId!();
        foreach (var row in _sidebarRows) {
            if (string.Equals(row.Id, currentId, StringComparison.OrdinalIgnoreCase)) {
                focusedRow = row;
                break;
            }
        }
        if (focusedRow != null)
            Callable.From(() => focusedRow.Host.TryGrabFocus()).CallDeferred();

        MainFile.Logger.Info(ModPanelDiagnosticLog.FormatControllerInput(
            action, handled: true, null, _getSelectedModId?.Invoke()));
        RefreshHints();
        return true;
    }

    private static bool IsDirectionalPress(InputEvent @event, out int delta, out string action) {
        delta = 0;
        action = "";
        if (@event.IsEcho())
            return false;
        if (IsUpPress(@event)) {
            delta = -1;
            action = "up";
            return true;
        }
        if (IsDownPress(@event)) {
            delta = 1;
            action = "down";
            return true;
        }
        return false;
    }

    private static bool IsUpPress(InputEvent @event) =>
        @event.IsActionPressed("ui_up")
        || @event.IsActionPressed(MegaInput.up)
        || @event.IsActionPressed(Controller.dPadNorth)
        || @event.IsActionPressed(Controller.joystickUp);

    private static bool IsDownPress(InputEvent @event) =>
        @event.IsActionPressed("ui_down")
        || @event.IsActionPressed(MegaInput.down)
        || @event.IsActionPressed(Controller.dPadSouth)
        || @event.IsActionPressed(Controller.joystickDown);

    private string? DescribeSkipReason() {
        if (_submenu == null || !GodotObject.IsInstanceValid(_submenu))
            return "submenuMissing";
        if (!_submenu.Visible)
            return "submenuNotVisible";
        if (!ActiveScreenContext.Instance.IsCurrent(_submenu)) {
            var current = ActiveScreenContext.Instance.GetCurrentScreen();
            return $"notCurrent(screen={current?.GetType().Name ?? "null"})";
        }
        if (NControllerManager.Instance?.IsUsingController != true)
            return "mouseMode(IsUsingController=false)";
        if (_sidebarRows.Count == 0 || _getSelectedModId == null || _selectMod == null)
            return "sidebarNotReady";
        return null;
    }

    public void RefreshHints() {
        if (_hintsRow == null || !GodotObject.IsInstanceValid(_hintsRow))
            return;
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        _hintsRow.Visible = usingController;
        MainFile.Logger.Info(ModPanelDiagnosticLog.FormatControllerHints(
            usingController, _hintsRow.Visible, CountPageTabs()));
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
        if (_submenu != null && !ActiveScreenContext.Instance.IsCurrent(_submenu))
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
