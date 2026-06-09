using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

/// <summary>
/// Page tab strip: full-width scroll row with LB/RB icons overlaid on the sides
/// (official settings uses a flat HBox; we add scroll for many Ritsu pages).
/// </summary>
public partial class ModPanelPageTabChrome : Control {
    public readonly record struct PageEntry(string Id, string Label);

    private const int FillWidthMaxTabs = 6;
    private const float TabMinWidth = 96f;
    private const float TabMinHeight = 28f;
    private const float TriggerWidth = 40f;
    private const float TriggerHeight = 28f;
    private const float TriggerGutter = 46f;
    private const float StripHeight = 34f;

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
        CustomMinimumSize = new Vector2(0f, StripHeight);
        ClipContents = false;

        _tabScroll = new ScrollContainer {
            Name = "ModPanelPageTabScroll",
            MouseFilter = MouseFilterEnum.Pass,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        _tabScroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        _tabScroll.GrowHorizontal = GrowDirection.Both;
        _tabScroll.GrowVertical = GrowDirection.Both;
        AddChild(_tabScroll);

        _tabRow = new HBoxContainer {
            Name = "ModPanelPageTabRow",
            SizeFlagsHorizontal = SizeFlags.ShrinkBegin,
            SizeFlagsVertical = SizeFlags.ShrinkBegin,
            MouseFilter = MouseFilterEnum.Ignore,
        };
        _tabRow.AddThemeConstantOverride("separation", 6);
        _tabScroll.AddChild(_tabRow);

        _leftTrigger = CreateTriggerIcon("LeftTriggerIcon", -1);
        AddChild(_leftTrigger);

        _rightTrigger = CreateTriggerIcon("RightTriggerIcon", 1);
        AddChild(_rightTrigger);

        Resized += OnChromeResized;
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
        var fillWidth = _pages.Count <= FillWidthMaxTabs;
        _tabRow.SizeFlagsHorizontal = fillWidth ? SizeFlags.ExpandFill : SizeFlags.ShrinkBegin;
        foreach (var entry in _pages) {
            var capturedId = entry.Id;
            var selected = string.Equals(capturedId, selectedPageId, StringComparison.OrdinalIgnoreCase);
            var tab = ModPanelUI.CreateDevModePageTab(capturedId, entry.Label, selected,
                () => SelectPage(capturedId, fromUser: true));
            tab.CustomMinimumSize = new Vector2(TabMinWidth, TabMinHeight);
            if (fillWidth) {
                tab.SizeFlagsHorizontal = SizeFlags.ExpandFill;
                tab.SizeFlagsStretchRatio = 1f;
            }
            _tabRow.AddChild(tab);
        }
        RefreshTabStyles();
        RefreshTriggerIcons();
        Callable.From(() => {
            ApplyScrollGutters();
            ScrollSelectedTabIntoView();
        }).CallDeferred();
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
        ApplyScrollGutters();
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
            ApplyScrollGutters();
            return;
        }
        var usingController = NControllerManager.Instance?.IsUsingController == true;
        _leftTrigger.Visible = usingController;
        _rightTrigger.Visible = usingController;
        if (usingController) {
            _leftTrigger.Texture = NInputManager.Instance.GetHotkeyIcon(TabLeftHotkey);
            _rightTrigger.Texture = NInputManager.Instance.GetHotkeyIcon(TabRightHotkey);
            _leftTrigger.Modulate = Colors.White;
            _rightTrigger.Modulate = Colors.White;
        }
        ApplyScrollGutters();
        LayoutTriggerIcons();
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

    private void ScrollSelectedTabIntoViewDeferred() {
        Callable.From(ScrollSelectedTabIntoView).CallDeferred();
    }

    private void ScrollSelectedTabIntoView() {
        var tab = FindSelectedTabButton();
        if (tab != null && GodotObject.IsInstanceValid(_tabScroll))
            _tabScroll.EnsureControlVisible(tab);
    }

    private void OnChromeResized() {
        ApplyScrollGutters();
        LayoutTriggerIcons();
    }

    private void ApplyScrollGutters() {
        if (!GodotObject.IsInstanceValid(_tabScroll))
            return;
        var gutter = _leftTrigger.Visible ? TriggerGutter : 0f;
        _tabScroll.OffsetLeft = gutter;
        _tabScroll.OffsetRight = -gutter;
        _tabScroll.OffsetTop = 0f;
        _tabScroll.OffsetBottom = 0f;
    }

    private void LayoutTriggerIcons() {
        if (!GodotObject.IsInstanceValid(_leftTrigger) || !GodotObject.IsInstanceValid(_rightTrigger))
            return;
        var h = Size.Y > 0f ? Size.Y : StripHeight;
        var y = (h - TriggerHeight) * 0.5f;
        _leftTrigger.Position = new Vector2(0f, y);
        _leftTrigger.Size = new Vector2(TriggerWidth, TriggerHeight);
        _rightTrigger.Position = new Vector2(Mathf.Max(0f, Size.X - TriggerWidth), y);
        _rightTrigger.Size = new Vector2(TriggerWidth, TriggerHeight);
        _leftTrigger.ZIndex = 2;
        _rightTrigger.ZIndex = 2;
    }

    private TextureRect CreateTriggerIcon(string name, int delta) {
        var icon = new TextureRect {
            Name = name,
            Visible = false,
            MouseFilter = MouseFilterEnum.Stop,
            MouseDefaultCursorShape = CursorShape.PointingHand,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
        };
        icon.GuiInput += ev => OnTriggerGuiInput(ev, delta);
        return icon;
    }

    private void OnTriggerGuiInput(InputEvent ev, int delta) {
        if (ev is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
            return;
        TrySwitchPage(delta);
        GetViewport()?.SetInputAsHandled();
    }
}
