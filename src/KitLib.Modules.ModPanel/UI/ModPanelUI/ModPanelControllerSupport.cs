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

    private ModPanelPageTabChrome? _pageTabChrome;
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
    private bool _tabHotkeysEnabled;

    public void Configure(ModPanelPageTabChrome pageTabChrome, Control hintsRow) {
        _pageTabChrome = pageTabChrome;
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

    /// <summary>Push LB/RB bindings while this submenu is active (NSettingsTabManager.Enable pattern).</summary>
    public void EnableTabHotkeys() {
        if (_tabHotkeysEnabled || NHotkeyManager.Instance == null)
            return;
        NHotkeyManager.Instance.PushHotkeyPressedBinding(TabLeftHotkey, TabLeft);
        NHotkeyManager.Instance.PushHotkeyPressedBinding(TabRightHotkey, TabRight);
        _tabHotkeysEnabled = true;
    }

    /// <summary>Pop LB/RB bindings when the submenu closes or hides.</summary>
    public void DisableTabHotkeys() {
        if (!_tabHotkeysEnabled || NHotkeyManager.Instance == null
            || !GodotObject.IsInstanceValid(NHotkeyManager.Instance))
            return;
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(TabLeftHotkey, TabLeft);
        NHotkeyManager.Instance.RemoveHotkeyPressedBinding(TabRightHotkey, TabRight);
        _tabHotkeysEnabled = false;
    }

    public override void _Ready() {
        _refreshHintsCallable = Callable.From(RefreshHints);
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
        _pageTabChrome?.RefreshTriggerIcons();
    }

    public override void _Process(double delta) {
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        if (usingController != _lastUsingController) {
            _lastUsingController = usingController;
            RefreshHints();
            _pageTabChrome?.RefreshTriggerIcons();
        }
    }

    public override void _ExitTree() {
        DisableTabHotkeys();
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
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                action, handled: false, skip, _getSelectedModId?.Invoke()));
            return false;
        }

        var focus = GetViewport()?.GuiGetFocusOwner() as Control;
        if (focus != null && _settingsContentRoot != null && GodotObject.IsInstanceValid(_settingsContentRoot)
            && _settingsContentRoot.IsAncestorOf(focus)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                action, handled: false, "focusInSettingsContent", _getSelectedModId?.Invoke()));
            return false;
        }

        if (!CycleSidebarMod(delta)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
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
#if STS2_BETA_PROFILE
        || @event.IsActionPressed(Controller.dPadUp)
        || @event.IsActionPressed(Controller.lStickUp);
#else
        || @event.IsActionPressed(Controller.dPadNorth)
        || @event.IsActionPressed(Controller.joystickUp);
#endif

    private static bool IsDownPress(InputEvent @event) =>
        @event.IsActionPressed("ui_down")
        || @event.IsActionPressed(MegaInput.down)
#if STS2_BETA_PROFILE
        || @event.IsActionPressed(Controller.dPadDown)
        || @event.IsActionPressed(Controller.lStickDown);
#else
        || @event.IsActionPressed(Controller.dPadSouth)
        || @event.IsActionPressed(Controller.joystickDown);
#endif

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
        KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerHints(
            usingController, _hintsRow.Visible, _pageTabChrome?.PageCount ?? 0));
        if (!usingController)
            return;
        if (_backIcon != null) {
            _backIcon.Visible = true;
            _backIcon.Texture = NInputManager.Instance.GetHotkeyIcon(MegaInput.cancel);
        }
        if (_selectIcon != null)
            _selectIcon.Visible = false;
        if (_tabLeftIcon != null)
            _tabLeftIcon.Visible = false;
        if (_tabRightIcon != null)
            _tabRightIcon.Visible = false;
        _pageTabChrome?.RefreshTriggerIcons();
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

    /// <summary>LB/RB from <see cref="ModPanelSubmenu" /> input hooks (NStatsTabManager pattern).</summary>
    public bool TryHandleTabInput(InputEvent @event) {
        if (@event.IsEcho())
            return false;
        if (!CanHandleTabHotkeys(out var skip))
            return false;
        if (_pageTabChrome == null || _pageTabChrome.PageCount <= 1) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                "tab", handled: false, "pageTabsUnavailable", _getSelectedModId?.Invoke()));
            return false;
        }

        if (@event.IsActionPressed(TabLeftHotkey)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                "tabLeft", handled: true, null, _getSelectedModId?.Invoke()));
            SwitchTab(-1);
            return true;
        }
        if (@event.IsActionPressed(TabRightHotkey)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                "tabRight", handled: true, null, _getSelectedModId?.Invoke()));
            SwitchTab(1);
            return true;
        }
        return false;
    }

    private bool CanHandleTabHotkeys(out string? skipReason) {
        skipReason = DescribeSkipReason();
        if (skipReason != null)
            return false;
        if (_submenu == null || !GodotObject.IsInstanceValid(_submenu) || !_submenu.IsVisibleInTree())
            return false;
        return true;
    }

    private void SwitchTab(int delta) {
        if (_pageTabChrome == null)
            return;
        if (!CanHandleTabHotkeys(out var skip)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                delta < 0 ? "tabLeft" : "tabRight", handled: false, skip ?? "tabHotkeyBlocked",
                _getSelectedModId?.Invoke()));
            return;
        }
        if (!_pageTabChrome.TrySwitchPage(delta)) {
            KitLog.Info(ModPanelDiagnosticLog.Scope, ModPanelDiagnosticLog.FormatControllerInput(
                delta < 0 ? "tabLeft" : "tabRight", handled: false, "tabAtEdge", _getSelectedModId?.Invoke()));
            return;
        }
        var tab = FindSelectedTabButton();
        if (tab != null)
            Callable.From(() => tab.TryGrabFocus()).CallDeferred();
        RefreshHints();
    }

    private Button? FindSelectedTabButton() {
        if (_pageTabChrome == null)
            return null;
        var selectedId = _pageTabChrome.GetSelectedPageId();
        if (selectedId == null)
            return null;
        foreach (var child in _pageTabChrome.TabRow.GetChildren()) {
            if (child is Button b && b.HasMeta("pageId")
                && string.Equals(b.GetMeta("pageId").AsString(), selectedId, StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }
}
