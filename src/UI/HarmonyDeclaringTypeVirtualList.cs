using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Interop;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Virtualized list of declaring-type paths: only visible rows are drawn; scroll via wheel or scrollbar.
/// </summary>
internal sealed partial class HarmonyDeclaringTypeVirtualList : Control {
    private const int RowHeight = 20;
    private const int FontSize = 11;
    private const int MaxLabelChars = 96;

    private readonly List<HarmonySmartAnalysis.DeclaringTypePatchInfo> _rows = new();
    private int _scrollIndex;
    private int _selectedIndex = -1;
    private VScrollBar? _scrollBar;
    private int _visibleRows = 1;

    public HarmonyDeclaringTypeVirtualList() {
        MouseFilter = MouseFilterEnum.Stop;
        FocusMode = FocusModeEnum.Click;
        ClipContents = true;
    }

    /// <summary>Invoked when the user selects a row (filtered index).</summary>
    public event Action<HarmonySmartAnalysis.DeclaringTypePatchInfo?>? ItemSelected;

    public void BindScrollBar(VScrollBar bar) {
        _scrollBar = bar;
        bar.ValueChanged += OnScrollBarValue;
    }

    /// <param name="resetSelection">When true, select the first row (e.g. full refresh or filter change).</param>
    public void SetData(IReadOnlyList<HarmonySmartAnalysis.DeclaringTypePatchInfo> all, string filter, bool resetSelection = false) {
        _rows.Clear();
        var f = filter.Trim();
        foreach (var t in all.OrderBy(x => x.DeclaringTypeFullName, StringComparer.Ordinal)) {
            if (f.Length > 0 &&
                t.DeclaringTypeFullName.IndexOf(f, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            _rows.Add(t);
        }

        if (resetSelection) {
            _scrollIndex = 0;
            _selectedIndex = _rows.Count > 0 ? 0 : -1;
        }
        else {
            _selectedIndex = _rows.Count > 0 ? Math.Clamp(_selectedIndex, 0, _rows.Count - 1) : -1;
            if (_rows.Count > 0 && _selectedIndex < 0)
                _selectedIndex = 0;
        }

        _scrollIndex = Math.Clamp(_scrollIndex, 0, MaxScrollIndex());

        SyncScrollBar();
        QueueRedraw();
        if (_selectedIndex >= 0 && _selectedIndex < _rows.Count)
            ItemSelected?.Invoke(_rows[_selectedIndex]);
        else
            ItemSelected?.Invoke(null);
    }

    public HarmonySmartAnalysis.DeclaringTypePatchInfo? GetSelected() =>
        _selectedIndex >= 0 && _selectedIndex < _rows.Count ? _rows[_selectedIndex] : null;

    private void OnResized() {
        _visibleRows = Math.Max(1, (int)(Size.Y / RowHeight));
        SyncScrollBar();
        QueueRedraw();
    }

    private void OnScrollBarValue(double v) {
        _scrollIndex = (int)v;
        _scrollIndex = Math.Clamp(_scrollIndex, 0, MaxScrollIndex());
        QueueRedraw();
    }

    private int MaxScrollIndex() => Math.Max(0, _rows.Count - _visibleRows);

    private void SyncScrollBar() {
        _visibleRows = Math.Max(1, (int)(Size.Y / RowHeight));
        if (_scrollBar == null) return;

        var maxScroll = MaxScrollIndex();
        _scrollBar.MinValue = 0;
        _scrollBar.MaxValue = maxScroll;
        _scrollBar.Page = Math.Min(_visibleRows, Math.Max(1, _rows.Count));
        _scrollBar.Visible = _rows.Count > _visibleRows;
        _scrollBar.Value = Math.Clamp(_scrollIndex, 0, maxScroll);
    }

    public override void _Notification(int what) {
        if (what == NotificationResized)
            OnResized();
    }

    public override void _GuiInput(InputEvent @event) {
        if (_rows.Count == 0) return;

        if (@event is InputEventMouseButton mb && mb.Pressed) {
            if (mb.ButtonIndex == MouseButton.Left) {
                var idx = RowIndexFromY(mb.Position.Y);
                if (idx >= 0 && idx < _rows.Count) {
                    _selectedIndex = idx;
                    EnsureVisible(_selectedIndex);
                    SyncScrollBar();
                    QueueRedraw();
                    ItemSelected?.Invoke(_rows[_selectedIndex]);
                }

                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelUp) {
                _scrollIndex = Math.Max(0, _scrollIndex - 3);
                if (_scrollBar != null) _scrollBar.Value = _scrollIndex;
                QueueRedraw();
                AcceptEvent();
            }
            else if (mb.ButtonIndex == MouseButton.WheelDown) {
                _scrollIndex = Math.Min(MaxScrollIndex(), _scrollIndex + 3);
                if (_scrollBar != null) _scrollBar.Value = _scrollIndex;
                QueueRedraw();
                AcceptEvent();
            }
        }
    }

    private int RowIndexFromY(float y) {
        var rel = (int)(y / RowHeight);
        return _scrollIndex + rel;
    }

    private void EnsureVisible(int index) {
        if (index < _scrollIndex)
            _scrollIndex = index;
        else if (index >= _scrollIndex + _visibleRows)
            _scrollIndex = Math.Max(0, index - _visibleRows + 1);
        _scrollIndex = Math.Clamp(_scrollIndex, 0, MaxScrollIndex());
    }

    public override void _Draw() {
        var font = GetThemeFont("font", "Label") ?? ThemeDB.FallbackFont;
        var textCol = KitLibTheme.TextPrimary;
        var selCol = new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.28f);
        var subtle = KitLibTheme.Subtle;

        _visibleRows = Math.Max(1, (int)(Size.Y / RowHeight));
        var count = Math.Min(_visibleRows, Math.Max(0, _rows.Count - _scrollIndex));
        for (var i = 0; i < count; i++) {
            var idx = _scrollIndex + i;
            if (idx >= _rows.Count) break;
            var y = i * RowHeight;
            var row = _rows[idx];
            if (idx == _selectedIndex)
                DrawRect(new Rect2(0, y, Size.X, RowHeight), selCol);

            var label = FormatRowLabel(row);
            DrawString(font, new Vector2(6, y + FontSize + 1), label, HorizontalAlignment.Left, -1, FontSize, textCol);

            var meta = $"  {row.TotalPatchOperations} ops · {row.DistinctOwnerCount} mods";
            var tw = font.GetStringSize(label, HorizontalAlignment.Left, -1, FontSize).X;
            if (tw + font.GetStringSize(meta, HorizontalAlignment.Left, -1, FontSize).X < Size.X - 8)
                DrawString(font, new Vector2(6 + tw, y + FontSize + 1), meta, HorizontalAlignment.Left, -1, FontSize, subtle);
        }

        if (_rows.Count == 0) {
            DrawString(font, new Vector2(6, FontSize + 4), "—", HorizontalAlignment.Left, -1, FontSize, subtle);
        }
    }

    private static string FormatRowLabel(HarmonySmartAnalysis.DeclaringTypePatchInfo row) {
        var name = row.DeclaringTypeFullName;
        if (name.Length <= MaxLabelChars)
            return $"[{name}]";
        var half = (MaxLabelChars - 5) / 2;
        return $"[{name[..half]}…{name[^half..]}]";
    }
}
