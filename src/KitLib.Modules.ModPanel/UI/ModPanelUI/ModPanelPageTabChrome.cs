using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>LB/RB flanking a full-width row of page tabs (equal stretch across available width).</summary>
public partial class ModPanelPageTabChrome : Control {
    public readonly record struct PageEntry(string Id, string Label);

    private static readonly StringName TabLeftHotkey = MegaInput.viewDeckAndTabLeft;
    private static readonly StringName TabRightHotkey = MegaInput.viewExhaustPileAndTabRight;

    private TextureRect _leftTrigger = null!;
    private TextureRect _rightTrigger = null!;
    private HBoxContainer _tabRow = null!;
    private readonly List<PageEntry> _pages = [];
    private string _selectedPageId = "";

    public event Action<string>? PageSelected;

    public int PageCount => _pages.Count;

    public HBoxContainer TabRow => _tabRow;

    public ModPanelPageTabChrome() {
        Name = "ModPanelPageTabChrome";
        MouseFilter = MouseFilterEnum.Ignore;
        SizeFlagsHorizontal = SizeFlags.ExpandFill;
        SizeFlagsVertical = SizeFlags.ShrinkBegin;
        CustomMinimumSize = new Vector2(0f, 52f);

        var row = new HBoxContainer {
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        row.AddThemeConstantOverride("separation", 10);
        AddChild(row);

        _leftTrigger = CreateTriggerIcon("LeftTriggerIcon");
        _leftTrigger.GuiInput += ev => OnTriggerGuiInput(ev, -1);
        row.AddChild(_leftTrigger);

        _tabRow = new HBoxContainer {
            Name = "ModPanelPageTabRow",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ExpandFill,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _tabRow.AddThemeConstantOverride("separation", 8);
        row.AddChild(_tabRow);

        _rightTrigger = CreateTriggerIcon("RightTriggerIcon");
        _rightTrigger.GuiInput += ev => OnTriggerGuiInput(ev, 1);
        row.AddChild(_rightTrigger);
    }

    public void SetPages(IReadOnlyList<PageEntry> pages, string selectedPageId) {
        _pages.Clear();
        _pages.AddRange(pages);
        _selectedPageId = selectedPageId;
        while (_tabRow.GetChildCount() > 0) {
            var child = _tabRow.GetChild(0);
            _tabRow.RemoveChild(child);
            child.QueueFree();
        }
        Visible = _pages.Count > 1;
        foreach (var entry in _pages) {
            var capturedId = entry.Id;
            var selected = string.Equals(capturedId, selectedPageId, StringComparison.OrdinalIgnoreCase);
            var tab = ModPanelUI.CreateDevModePageTab(capturedId, entry.Label, selected,
                () => SelectPage(capturedId, fromUser: true));
            tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            tab.SizeFlagsVertical = SizeFlags.ExpandFill;
            tab.SizeFlagsStretchRatio = 1f;
            _tabRow.AddChild(tab);
        }
        RefreshTabStyles();
        RefreshTriggerIcons();
    }

    public void ClearPages() {
        _pages.Clear();
        _selectedPageId = "";
        while (_tabRow.GetChildCount() > 0) {
            var child = _tabRow.GetChild(0);
            _tabRow.RemoveChild(child);
            child.QueueFree();
        }
        Visible = false;
        _leftTrigger.Visible = false;
        _rightTrigger.Visible = false;
    }

    public string? GetSelectedPageId() => string.IsNullOrEmpty(_selectedPageId) ? null : _selectedPageId;

    public bool TrySwitchPage(int delta) {
        if (_pages.Count <= 1)
            return false;
        var idx = FindSelectedIndex();
        var next = Mathf.Clamp(idx + delta, 0, _pages.Count - 1);
        if (next == idx)
            return false;
        SelectPage(_pages[next].Id, fromUser: true);
        return true;
    }

    public void RefreshTriggerIcons() {
        var show = _pages.Count > 1;
        if (!show) {
            _leftTrigger.Visible = false;
            _rightTrigger.Visible = false;
            return;
        }
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        _leftTrigger.Visible = usingController;
        _rightTrigger.Visible = usingController;
        if (!usingController)
            return;
        _leftTrigger.Texture = NInputManager.Instance.GetHotkeyIcon(TabLeftHotkey);
        _rightTrigger.Texture = NInputManager.Instance.GetHotkeyIcon(TabRightHotkey);
        _leftTrigger.Modulate = Colors.White;
        _rightTrigger.Modulate = Colors.White;
    }

    private void SelectPage(string pageId, bool fromUser) {
        if (string.IsNullOrEmpty(pageId))
            return;
        var changed = !string.Equals(_selectedPageId, pageId, StringComparison.OrdinalIgnoreCase);
        _selectedPageId = pageId;
        RefreshTabStyles();
        if (changed && fromUser)
            PageSelected?.Invoke(pageId);
    }

    private void RefreshTabStyles() {
        foreach (var child in _tabRow.GetChildren()) {
            if (child is not Button b || !b.HasMeta("pageId"))
                continue;
            var id = b.GetMeta("pageId").AsString();
            ModPanelUI.ApplyDevModeTabButtonStyle(b,
                string.Equals(id, _selectedPageId, StringComparison.OrdinalIgnoreCase));
        }
    }

    private int FindSelectedIndex() {
        for (var i = 0; i < _pages.Count; i++) {
            if (string.Equals(_pages[i].Id, _selectedPageId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        return 0;
    }

    private void OnTriggerGuiInput(InputEvent ev, int delta) {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;
        TrySwitchPage(delta);
        GetViewport()?.SetInputAsHandled();
    }

    private static TextureRect CreateTriggerIcon(string name) {
        return new TextureRect {
            Name = name,
            Visible = false,
            CustomMinimumSize = new Vector2(52f, 40f),
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
    }
}
