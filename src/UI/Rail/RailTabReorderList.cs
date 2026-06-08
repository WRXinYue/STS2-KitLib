using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Icons;
using KitLib.Panels;
using KitLib.Settings;
using Godot;

namespace KitLib.UI;

/// <summary>Drag-reorderable list of rail tabs with per-row visibility toggles.</summary>
internal sealed partial class RailTabReorderList : VBoxContainer {
    public delegate void OrderChangedHandler(IReadOnlyList<string> orderedIds);
    public delegate void VisibilityChangedHandler(string tabId, bool visible);

    private readonly DevPanelTabGroup _group;
    private readonly OrderChangedHandler _onOrderChanged;
    private readonly VisibilityChangedHandler _onVisibilityChanged;
    private readonly List<RailTabRow> _rows = new();

    private RailTabRow? _dragRow;

    public RailTabReorderList(
        DevPanelTabGroup group,
        IReadOnlyList<RailTabEditorEntry> entries,
        OrderChangedHandler onOrderChanged,
        VisibilityChangedHandler onVisibilityChanged) {
        _group = group;
        _onOrderChanged = onOrderChanged;
        _onVisibilityChanged = onVisibilityChanged;

        AddThemeConstantOverride("separation", 4);

        foreach (var entry in entries)
            AddRow(entry);
    }

    private void AddRow(RailTabEditorEntry entry) {
        var row = new RailTabRow(entry, _group, OnRowDragStarted, OnVisibilityToggled);
        _rows.Add(row);
        AddChild(row);
    }

    private void OnVisibilityToggled(RailTabRow row, bool visible) {
        if (!visible && !RailTabPreferences.CanHide(row.TabId, _group)) {
            row.SetVisibleChecked(true);
            return;
        }
        row.SetVisibleChecked(visible);
        _onVisibilityChanged(row.TabId, visible);
    }

    private void OnRowDragStarted(RailTabRow row) {
        _dragRow = row;
        row.SetDragging(true);
    }

    private void EndDrag() {
        if (_dragRow == null)
            return;
        _dragRow.SetDragging(false);
        _dragRow = null;
        _onOrderChanged(CollectOrderedIds());
    }

    public override void _Process(double delta) {
        if (_dragRow == null)
            return;

        if (!Input.IsMouseButtonPressed(MouseButton.Left)) {
            EndDrag();
            return;
        }

        var mouse = GetViewport().GetMousePosition();
        int target = ComputeInsertIndex(mouse.Y);
        int current = _dragRow.GetIndex();
        if (target != current)
            MoveChild(_dragRow, target);
    }

    private int ComputeInsertIndex(float globalMouseY) {
        for (int i = 0; i < GetChildCount(); i++) {
            if (GetChild(i) is not RailTabRow row)
                continue;
            var rect = row.GetGlobalRect();
            float mid = rect.Position.Y + rect.Size.Y * 0.5f;
            if (globalMouseY < mid)
                return i;
        }
        return Math.Max(0, GetChildCount() - 1);
    }

    private IReadOnlyList<string> CollectOrderedIds() {
        _rows.Clear();
        foreach (var child in GetChildren()) {
            if (child is RailTabRow row)
                _rows.Add(row);
        }
        return _rows.Select(r => r.TabId).ToList();
    }

    private sealed partial class RailTabRow : PanelContainer {
        private readonly string _tabId;
        private readonly DevPanelTabGroup _group;
        private readonly Action<RailTabRow> _onDragStart;
        private readonly Action<RailTabRow, bool> _onVisibility;
        private readonly CheckBox _check;
        private readonly StyleBoxFlat _style;
        private readonly StyleBoxFlat _dragStyle;

        public string TabId => _tabId;

        public RailTabRow(
            RailTabEditorEntry entry,
            DevPanelTabGroup group,
            Action<RailTabRow> onDragStart,
            Action<RailTabRow, bool> onVisibility) {
            _tabId = entry.Id;
            _group = group;
            _onDragStart = onDragStart;
            _onVisibility = onVisibility;

            CustomMinimumSize = new Vector2(0, 38);
            SizeFlagsHorizontal = SizeFlags.ExpandFill;
            MouseFilter = MouseFilterEnum.Pass;

            _style = new StyleBoxFlat {
                BgColor = KitLibTheme.ButtonBgNormal,
                CornerRadiusTopLeft = 8,
                CornerRadiusTopRight = 8,
                CornerRadiusBottomLeft = 8,
                CornerRadiusBottomRight = 8,
                ContentMarginLeft = 6,
                ContentMarginRight = 8,
                ContentMarginTop = 4,
                ContentMarginBottom = 4,
                BorderWidthBottom = 1,
                BorderWidthTop = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderColor = KitLibTheme.Separator
            };
            _dragStyle = (StyleBoxFlat)_style.Duplicate();
            _dragStyle.BgColor = KitLibTheme.ButtonBgHover;
            _dragStyle.BorderColor = KitLibTheme.Accent;
            AddThemeStyleboxOverride("panel", _style);

            var row = new HBoxContainer {
                Alignment = BoxContainer.AlignmentMode.Center,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ExpandFill
            };
            row.AddThemeConstantOverride("separation", 6);
            AddChild(row);

            var grip = new TextureRect {
                Texture = MdiIcon.DragVertical.Texture(18, KitLibTheme.Subtle),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                CustomMinimumSize = new Vector2(22, 22),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                MouseFilter = MouseFilterEnum.Stop,
                TooltipText = I18N.T("rail.dragHandle", "Drag to reorder")
            };
            grip.GuiInput += e => {
                if (e is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                    _onDragStart(this);
            };
            row.AddChild(grip);

            var iconRect = new TextureRect {
                Texture = entry.Icon.Texture(18, KitLibTheme.TextPrimary),
                CustomMinimumSize = new Vector2(22, 22),
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore
            };
            row.AddChild(iconRect);

            var lbl = new Label {
                Text = entry.Label,
                SizeFlagsHorizontal = SizeFlags.ExpandFill,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                ClipText = true,
                MouseFilter = MouseFilterEnum.Ignore
            };
            lbl.AddThemeFontSizeOverride("font_size", 12);
            lbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            row.AddChild(lbl);

            _check = new CheckBox {
                ButtonPressed = entry.Visible,
                FocusMode = FocusModeEnum.None,
                SizeFlagsVertical = SizeFlags.ShrinkCenter,
                Disabled = entry.PinVisible,
                TooltipText = entry.PinVisible
                    ? I18N.T("rail.alwaysVisible", "Always shown")
                    : I18N.T("rail.showOnRail", "Show on sidebar")
            };
            _check.Toggled += v => _onVisibility(this, v);
            row.AddChild(_check);

            if (entry.ModeLocked) {
                Modulate = new Color(1, 1, 1, 0.45f);
                TooltipText = I18N.T("rail.modeLocked", "Not available in the current run mode");
                grip.MouseFilter = MouseFilterEnum.Ignore;
            }
        }

        public void SetVisibleChecked(bool visible) => _check.ButtonPressed = visible;

        public void SetDragging(bool dragging) {
            AddThemeStyleboxOverride("panel", dragging ? _dragStyle : _style);
            ZIndex = dragging ? 2 : 0;
        }
    }
}
