using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Icons;
using Godot;

namespace KitLib.UI;

/// <summary>
/// Collapses mod character pool filters into one chip-style button with a checkable popup list.
/// Left-click toggles include; right-click toggles exclude.
/// </summary>
internal sealed partial class ModPoolFilterDropdown : Control {
    private static readonly MdiIcon ChevronDown = MdiIcon.From("chevron-down");

    private static readonly Color ColChipOff = KitLibTheme.ButtonBgNormal;
    private static readonly Color ColChipHover = KitLibTheme.ButtonBgHover;
    private static readonly Color ColChipOn = new(0.25f, 0.40f, 0.65f, 0.90f);
    private static readonly Color ColChipOnHover = new(0.30f, 0.48f, 0.75f, 0.95f);
    private static readonly Color ColChipExclude = new(0.65f, 0.22f, 0.22f, 0.92f);
    private static readonly Color ColChipExcludeHover = new(0.75f, 0.28f, 0.28f, 0.95f);
    private static readonly Color ColRowExclude = new(0.85f, 0.45f, 0.45f, 1f);

    private enum EntryMode { Off, Include, Exclude }

    private readonly IReadOnlyList<(string key, string label)> _entries;
    private readonly HashSet<string> _activeFilters;
    private readonly HashSet<string> _excludedFilters;
    private readonly Action _onFiltersChanged;
    private readonly string _chipLabelFallback;
    private readonly string _chipCountFormat;
    private readonly string _chipExcludedCountFormat;
    private readonly MdiIcon _chipIcon;
    private readonly string _chipTooltipFallback;
    private readonly Dictionary<string, CheckBox> _checkboxes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Control> _rows = new(StringComparer.Ordinal);
    private readonly Dictionary<string, EntryMode> _entryModes = new(StringComparer.Ordinal);

    private readonly PanelContainer _chip;
    private readonly Label _label;
    private readonly TextureRect _icon;
    private readonly TextureRect _chevron;
    private readonly PopupPanel _popup;
    private readonly LineEdit? _searchInput;
    private bool _hovered;

    public ModPoolFilterDropdown(
        IReadOnlyList<(string key, string label)> modEntries,
        HashSet<string> activeFilters,
        HashSet<string> excludedFilters,
        Action onFiltersChanged,
        string? chipLabel = null,
        string? chipCountFormat = null,
        string? chipExcludedCountFormat = null,
        MdiIcon? chipIcon = null,
        string? chipTooltip = null) {
        _entries = modEntries;
        _activeFilters = activeFilters;
        _excludedFilters = excludedFilters;
        _onFiltersChanged = onFiltersChanged;
        _chipLabelFallback = chipLabel ?? I18N.T("cardBrowser.poolMods", "Mods");
        _chipCountFormat = chipCountFormat ?? I18N.T("cardBrowser.poolModsCount", "Mods ({0})");
        _chipExcludedCountFormat = chipExcludedCountFormat
            ?? I18N.T("cardBrowser.poolModsExcludedCount", "Mods (−{0})");
        _chipIcon = chipIcon ?? MdiIcon.PuzzleOutline;
        _chipTooltipFallback = chipTooltip ?? _chipLabelFallback;

        SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        SizeFlagsVertical = SizeFlags.ShrinkCenter;

        _chip = new PanelContainer {
            MouseFilter = MouseFilterEnum.Stop,
            FocusMode = FocusModeEnum.None
        };
        _chip.MouseEntered += () => { _hovered = true; RefreshButtonPresentation(); };
        _chip.MouseExited += () => { _hovered = false; RefreshButtonPresentation(); };
        _chip.GuiInput += OnChipInput;
        AddChild(_chip);

        var chipRow = new HBoxContainer();
        chipRow.AddThemeConstantOverride("separation", 4);
        _chip.AddChild(chipRow);

        _icon = new TextureRect {
            CustomMinimumSize = new Vector2(14, 14),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        chipRow.AddChild(_icon);

        _label = new Label {
            MouseFilter = MouseFilterEnum.Ignore,
            VerticalAlignment = VerticalAlignment.Center
        };
        _label.AddThemeFontSizeOverride("font_size", 11);
        chipRow.AddChild(_label);

        _chevron = new TextureRect {
            CustomMinimumSize = new Vector2(12, 12),
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            MouseFilter = MouseFilterEnum.Ignore
        };
        chipRow.AddChild(_chevron);

        _popup = new PopupPanel {
            MinSize = new Vector2I(200, 0)
        };
        var popupStyle = new StyleBoxFlat {
            BgColor = KitLibTheme.PanelBg,
            BorderColor = KitLibTheme.PanelBorder,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 8,
            ContentMarginBottom = 8
        };
        _popup.AddThemeStyleboxOverride("panel", popupStyle);
        AddChild(_popup);

        var popupRoot = new VBoxContainer();
        popupRoot.AddThemeConstantOverride("separation", 6);
        _popup.AddChild(popupRoot);

        if (_entries.Count > 8) {
            _searchInput = new LineEdit {
                PlaceholderText = I18N.T("cardBrowser.poolModsSearch", "Search…"),
                ClearButtonEnabled = true
            };
            _searchInput.AddThemeFontSizeOverride("font_size", 11);
            _searchInput.TextChanged += ApplySearchFilter;
            popupRoot.AddChild(_searchInput);
        }

        var scroll = new ScrollContainer {
            CustomMinimumSize = new Vector2(0, Math.Min(280, _entries.Count * 28 + 8)),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        scroll.HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled;
        popupRoot.AddChild(scroll);

        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 2);
        list.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        scroll.AddChild(list);

        foreach (var (key, label) in _entries) {
            var row = new MarginContainer();
            var checkbox = new CheckBox {
                Text = label,
                FocusMode = Control.FocusModeEnum.None
            };
            checkbox.AddThemeFontSizeOverride("font_size", 11);
            checkbox.TooltipText = label;
            var capturedKey = key;
            checkbox.Toggled += on => {
                SetEntryMode(capturedKey, on ? EntryMode.Include : EntryMode.Off);
                RefreshButtonPresentation();
                _onFiltersChanged();
            };
            checkbox.GuiInput += evt => {
                if (evt is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Right })
                    return;
                var next = _entryModes.GetValueOrDefault(capturedKey) == EntryMode.Exclude
                    ? EntryMode.Off
                    : EntryMode.Exclude;
                SetEntryMode(capturedKey, next);
                RefreshButtonPresentation();
                _onFiltersChanged();
                checkbox.AcceptEvent();
            };
            row.AddChild(checkbox);
            list.AddChild(row);
            _checkboxes[key] = checkbox;
            _rows[key] = row;

            var initial = ResolveEntryMode(key);
            SetEntryMode(key, initial);
        }

        RefreshButtonPresentation();
    }

    public override Vector2 _GetMinimumSize() => _chip.GetMinimumSize();

    private EntryMode ResolveEntryMode(string key) {
        if (_activeFilters.Contains(key)) return EntryMode.Include;
        if (_excludedFilters.Contains(key)) return EntryMode.Exclude;
        return EntryMode.Off;
    }

    private void SetEntryMode(string key, EntryMode mode) {
        _entryModes[key] = mode;
        if (!_checkboxes.TryGetValue(key, out var checkbox)) return;

        switch (mode) {
            case EntryMode.Include:
                _activeFilters.Add(key);
                _excludedFilters.Remove(key);
                checkbox.Modulate = Colors.White;
                break;
            case EntryMode.Exclude:
                _activeFilters.Remove(key);
                _excludedFilters.Add(key);
                checkbox.Modulate = ColRowExclude;
                break;
            default:
                _activeFilters.Remove(key);
                _excludedFilters.Remove(key);
                checkbox.Modulate = Colors.White;
                break;
        }

        checkbox.SetPressedNoSignal(mode == EntryMode.Include);
    }

    private void OnChipInput(InputEvent @event) {
        if (@event is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) {
            OnButtonPressed();
            _chip.AcceptEvent();
        }
    }

    private void OnButtonPressed() {
        if (_searchInput != null)
            _searchInput.Text = "";
        ApplySearchFilter("");

        var pos = _chip.GlobalPosition;
        var size = _chip.Size;
        _popup.Popup(new Rect2I(
            (int)pos.X,
            (int)(pos.Y + size.Y + 2),
            Math.Max(200, (int)size.X),
            0));
    }

    private void ApplySearchFilter(string query) {
        var normalized = query.Trim();
        foreach (var (key, label) in _entries) {
            if (!_rows.TryGetValue(key, out var row)) continue;
            var visible = string.IsNullOrEmpty(normalized)
                || label.Contains(normalized, StringComparison.OrdinalIgnoreCase);
            row.Visible = visible;
        }
    }

    private void RefreshButtonPresentation() {
        var included = _entries.Where(e => _activeFilters.Contains(e.key)).ToList();
        var excluded = _entries.Where(e => _excludedFilters.Contains(e.key)).ToList();
        var includeActive = included.Count > 0;
        var excludeActive = excluded.Count > 0;
        var chipActive = includeActive || excludeActive;
        var iconColor = chipActive || _hovered ? KitLibTheme.TextPrimary : KitLibTheme.Subtle;

        _label.Text = includeActive switch {
            true when included.Count == 1 => included[0].label,
            true => string.Format(_chipCountFormat, included.Count),
            false when excludeActive => string.Format(_chipExcludedCountFormat, excluded.Count),
            _ => _chipLabelFallback
        };
        _label.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        _label.AddThemeColorOverride("font_color", iconColor);
        _icon.Texture = _chipIcon.Texture(14, iconColor);
        _chevron.Texture = ChevronDown.Texture(12, iconColor);
        _chip.TooltipText = includeActive
            ? string.Join(", ", included.Select(s => s.label))
            : excludeActive
                ? string.Join(", ", excluded.Select(s => "−" + s.label))
                : _chipTooltipFallback;

        ApplyChipStyle(_chip, includeActive, excludeActive, _hovered);
    }

    private static void ApplyChipStyle(PanelContainer chip, bool includeActive, bool excludeActive, bool hovered) {
        StyleBoxFlat MakeChipStyle(Color bg) => new() {
            BgColor = bg,
            CornerRadiusTopLeft = 12,
            CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12,
            CornerRadiusBottomRight = 12,
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 4,
            ContentMarginBottom = 4
        };

        Color bg;
        if (includeActive) {
            bg = hovered ? ColChipOnHover : ColChipOn;
        }
        else if (excludeActive) {
            bg = hovered ? ColChipExcludeHover : ColChipExclude;
        }
        else {
            bg = hovered ? ColChipHover : ColChipOff;
        }

        chip.AddThemeStyleboxOverride("panel", MakeChipStyle(bg));
    }
}
