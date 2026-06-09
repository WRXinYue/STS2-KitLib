using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>Official settings layout: LB/RB flanking a full row of page tabs.</summary>
public partial class ModPanelPageTabChrome : Control {
    public readonly record struct PageEntry(string Id, string Label);

    private static readonly StringName TabLeftHotkey = MegaInput.viewDeckAndTabLeft;
    private static readonly StringName TabRightHotkey = MegaInput.viewExhaustPileAndTabRight;

    private TextureRect _leftTrigger = null!;
    private TextureRect _rightTrigger = null!;
    private ScrollContainer _tabScroll = null!;
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
        CustomMinimumSize = new Vector2(0f, 48f);

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

        _tabScroll = new ScrollContainer {
            Name = "ModPanelPageTabScroll",
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
            SizeFlagsVertical = SizeFlags.ShrinkCenter,
            CustomMinimumSize = new Vector2(80f, 40f),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
            MouseFilter = MouseFilterEnum.Pass,
        };
        _tabRow = new HBoxContainer {
            Name = "ModPanelPageTabRow",
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _tabRow.AddThemeConstantOverride("separation", 8);
        _tabScroll.AddChild(_tabRow);
        row.AddChild(_tabScroll);

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
            var tab = ModPanelUI.CreateDevModePageTab(capturedId, entry.Label, selected, () => SelectPage(capturedId, fromUser: true));
            _tabRow.AddChild(tab);
        }
        RefreshTabStyles();
        RefreshTriggerIcons();
        RefitTabLayoutDeferred();
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
        ScrollSelectedTabIntoViewDeferred();
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

    private Button? FindSelectedTabButton() {
        foreach (var child in _tabRow.GetChildren()) {
            if (child is Button b && b.HasMeta("pageId")
                && string.Equals(b.GetMeta("pageId").AsString(), _selectedPageId, StringComparison.OrdinalIgnoreCase))
                return b;
        }
        return null;
    }

    private void RefitTabLayoutDeferred() {
        Callable.From(RefitTabLayout).CallDeferred();
    }

    private void RefitTabLayout() {
        if (!GodotObject.IsInstanceValid(_tabScroll) || !GodotObject.IsInstanceValid(_tabRow))
            return;
        _tabRow.SizeFlagsHorizontal = SizeFlags.ShrinkBegin;
        _tabRow.SizeFlagsVertical = SizeFlags.ShrinkBegin;
        ScrollSelectedTabIntoView();
    }

    private void ScrollSelectedTabIntoViewDeferred() {
        Callable.From(ScrollSelectedTabIntoView).CallDeferred();
    }

    private void ScrollSelectedTabIntoView() {
        var tab = FindSelectedTabButton();
        if (tab != null && GodotObject.IsInstanceValid(_tabScroll))
            _tabScroll.EnsureControlVisible(tab);
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
            CustomMinimumSize = new Vector2(52f, 36f),
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
    }
}
