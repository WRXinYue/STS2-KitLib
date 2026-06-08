using System;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal sealed partial class BrowserPanelWidthGrip : Control {
    private const float MinPanelWidth = 320f;
    private const float GripWidth = 8f;

    private readonly Control _root;
    private readonly PanelContainer _panel;
    private readonly string _rootName;
    private Viewport _viewport = null!;

    private bool _isDragging;
    private double _dragStartMouseX;
    private double _dragStartWidth;
    private bool _isConnected;
    private bool _syncScheduled;

    public BrowserPanelWidthGrip(Control root, PanelContainer panel, string rootName) {
        _root = root ?? throw new ArgumentNullException(nameof(root));
        _panel = panel ?? throw new ArgumentNullException(nameof(panel));
        _rootName = rootName ?? throw new ArgumentNullException(nameof(rootName));

        Name = "PanelWidthGrip";
        MouseFilter = MouseFilterEnum.Stop;
        MouseDefaultCursorShape = CursorShape.Hsplit;
    }

    public override void _Ready() {
        _viewport = GetViewport();
        ZIndex = 20;
        ZAsRelative = false;

        ConnectSignals();
        CallDeferred(nameof(Sync));
    }

    private void ConnectSignals() {
        if (!_isConnected) {
            _root.Resized += OnResized;
            _panel.Resized += OnResized;
            _isConnected = true;
        }
    }

    public override void _ExitTree() {
        DisconnectSignals();
        base._ExitTree();
    }

    private void DisconnectSignals() {
        if (_isConnected) {
            _root.Resized -= OnResized;
            _panel.Resized -= OnResized;
            _isConnected = false;
        }
    }

    public override void _Process(double delta) {
        if (!_isDragging) {
            SetProcess(false);
            return;
        }

        if (!Input.IsMouseButtonPressed(MouseButton.Left)) {
            EndDragCommit();
            return;
        }

        UpdatePanelWidth();
    }

    private void UpdatePanelWidth() {
        var mouseX = _viewport.GetMousePosition().X;
        var newWidth = Math.Clamp(_dragStartWidth + (mouseX - _dragStartMouseX), MinPanelWidth, GetMaxWidth());
        ApplyFixedWidthForCurrentHost((float)newWidth);
        Sync();
    }

    public override void _GuiInput(InputEvent @event) {
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left } mb)
            return;

        if (mb.Pressed)
            StartDrag();
        else
            EndDragCommit();
    }

    private void StartDrag() {
        var mousePosition = _viewport.GetMousePosition();
        _dragStartMouseX = mousePosition.X;

        if (_panel.AnchorRight > 0.5f) {
            ApplyFixedWidthForCurrentHost(_panel.GetRect().Size.X);
        }
        _dragStartWidth = _panel.GetRect().Size.X;
        _isDragging = true;
        SetProcess(true);
    }

    private void EndDragCommit() {
        if (!_isDragging)
            return;
        _isDragging = false;

        var width = (int)Math.Max(MinPanelWidth, Math.Round(_panel.GetRect().Size.X));
        SaveWidth(width);
    }

    private void SaveWidth(int width) {
        var settings = SettingsStore.Current;
        settings.BrowserPanelWidths ??= new();
        settings.BrowserPanelWidths[_rootName] = width;
        SettingsStore.Save();
    }

    private void OnResized() {
        // Opening animation changes panel width every frame; skip grip relayout until it settles.
        if (!_isDragging
            && _panel.HasMeta("_dm_browser_panel_animating")
            && _panel.GetMeta("_dm_browser_panel_animating").AsBool()) {
            return;
        }

        if (!_syncScheduled) {
            _syncScheduled = true;
            CallDeferred(nameof(SyncDeferred));
        }
    }

    private void SyncDeferred() {
        _syncScheduled = false;
        Sync();
    }

    public void Sync() {
        if (!IsInsideTree())
            return;

        var globalRect = _panel.GetGlobalRect();
        if (globalRect.Size.Y < 1f) return;
        Size = new Vector2(GripWidth, globalRect.Size.Y);
        GlobalPosition = new Vector2(globalRect.End.X - GripWidth, globalRect.Position.Y);
    }

    private void ApplyFixedWidthForCurrentHost(float width) {
        if (_panel.GetParent() is Control host && host.Name.ToString() == "BrowserPanelClipHost") {
            float clamped = Math.Max(MinPanelWidth, width);
            _panel.AnchorLeft = 0;
            _panel.AnchorRight = 0;
            _panel.OffsetLeft = 0;
            _panel.OffsetRight = clamped;
            return;
        }

        DevPanelUI.ApplyFixedWidthToBrowserPanel(_panel, width);
    }

    private double GetMaxWidth() => DevPanelUI.GetMaxBrowserPanelWidth(_root);
}
