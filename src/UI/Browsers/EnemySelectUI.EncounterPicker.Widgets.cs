using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    private sealed class PickerPreviewBand {
        internal required Control Root { get; init; }
        internal required Action<EncounterModel> ShowEncounter { get; init; }
        internal required Action<MonsterModel> ShowMonster { get; init; }
        internal required Action Clear { get; init; }
    }

    private sealed class EncounterPickerListController {
        private readonly RoomType? _filter;
        private readonly PickerCellStyle _cellStyle;
        private readonly PickerPreviewBand _preview;
        private readonly VBoxContainer _list;
        private readonly Label _statusLabel;
        private readonly LineEdit _searchBox;
        private readonly Action<EncounterModel> _onEncounterSelected;
        private readonly Action<MonsterModel>? _onMonsterSelected;
        private readonly IReadOnlyList<EncounterModel> _encounters;
        private readonly IReadOnlyList<MonsterModel>? _monsters;
        private readonly HBoxContainer? _roomFilterBar;
        private PickerContentTab _contentTab = PickerContentTab.Encounters;

        internal EncounterPickerListController(
            RoomType? filter,
            PickerCellStyle cellStyle,
            PickerPreviewBand preview,
            VBoxContainer list,
            Label statusLabel,
            LineEdit searchBox,
            Action<EncounterModel> onEncounterSelected,
            Action<MonsterModel>? onMonsterSelected,
            IReadOnlyList<EncounterModel> encounters,
            IReadOnlyList<MonsterModel>? monsters,
            HBoxContainer? roomFilterBar) {
            _filter = filter;
            _cellStyle = cellStyle;
            _preview = preview;
            _list = list;
            _statusLabel = statusLabel;
            _searchBox = searchBox;
            _onEncounterSelected = onEncounterSelected;
            _onMonsterSelected = onMonsterSelected;
            _encounters = encounters;
            _monsters = monsters;
            _roomFilterBar = roomFilterBar;
        }

        internal void BindSearch() => _searchBox.TextChanged += _ => Rebuild();

        internal void BindContentTabs(HBoxContainer tabBar) {
            WireContentTabs(tabBar, tab => {
                _contentTab = tab;
                if (_roomFilterBar != null)
                    _roomFilterBar.Visible = tab == PickerContentTab.Encounters;
                if (_list.GetParent() is ScrollContainer scroll)
                    scroll.ScrollVertical = 0;
                Rebuild();
            });
        }

        internal void Rebuild() {
            _preview.Clear();
            _list.CustomMinimumSize = Vector2.Zero;
            ClearPickerListChildren(_list);

            var query = _searchBox.Text.Trim().ToLowerInvariant();

            if (_monsters != null && _contentTab == PickerContentTab.Monsters) {
                var filtered = string.IsNullOrEmpty(query)
                    ? _monsters
                    : _monsters.Where(m => GetMonsterSearchKey(m).Contains(query)).ToList();

                if (filtered.Count == 0) {
                    _statusLabel.Text = I18N.T("enemy.emptyMonsters", "No monsters found.");
                    RefreshPickerListScrollSize(_list);
                    return;
                }

                foreach (var mon in filtered) {
                    var captured = mon;
                    _list.AddChild(CreateMonsterCell(
                        captured,
                        _cellStyle,
                        () => _onMonsterSelected?.Invoke(captured),
                        () => _preview.ShowMonster(captured),
                        _preview.Clear));
                }
                _statusLabel.Text = I18N.T("enemy.pickerCountMonsters", "{0} monsters", filtered.Count);
                RefreshPickerListScrollSize(_list);
                return;
            }

            var filteredEncounters = string.IsNullOrEmpty(query)
                ? _encounters
                : _encounters.Where(enc => GetEncounterSearchKey(enc).Contains(query)).ToList();

            if (filteredEncounters.Count == 0) {
                _statusLabel.Text = I18N.T("enemy.emptyEncounters", "No encounters found.");
                RefreshPickerListScrollSize(_list);
                return;
            }

            foreach (var enc in filteredEncounters) {
                var captured = enc;
                _list.AddChild(CreateEncounterCell(
                    captured,
                    _filter,
                    _cellStyle,
                    () => _onEncounterSelected(captured),
                    () => _preview.ShowEncounter(captured),
                    _preview.Clear));
            }
            _statusLabel.Text = I18N.T("enemy.pickerCountEncounters", "{0} encounters", filteredEncounters.Count);
            RefreshPickerListScrollSize(_list);
        }
    }

    private static void ClearPickerListChildren(VBoxContainer list) {
        while (list.GetChildCount() > 0) {
            var child = list.GetChild(0);
            list.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void RefreshPickerListScrollSize(VBoxContainer list) {
        Callable.From(() => ApplyPickerListScrollSize(list)).CallDeferred();
    }

    private static void ApplyPickerListScrollSize(VBoxContainer list) {
        if (!GodotObject.IsInstanceValid(list))
            return;

        list.CustomMinimumSize = Vector2.Zero;
        var childCount = list.GetChildCount();
        if (childCount == 0)
            return;

        var separation = list.GetThemeConstant("separation");
        var height = 0f;
        var activeIndex = 0;
        for (var i = 0; i < childCount; i++) {
            if (list.GetChild(i) is not Control child || child.IsQueuedForDeletion())
                continue;

            height += child.GetCombinedMinimumSize().Y;
            if (activeIndex > 0)
                height += separation;
            activeIndex++;
        }

        // Extra separation keeps the last row from sitting under the scroll clip edge.
        height += separation;
        if (height > 0f)
            list.CustomMinimumSize = new Vector2(0, height);
    }

    private static (Control listRegion, ScrollContainer scroll, VBoxContainer list, Label status)
        CreatePickerListSection(int listSeparation) {
        var listRegion = new Control {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            ClipContents = true,
            CustomMinimumSize = Vector2.Zero,
        };

        var scroll = new ScrollContainer {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
            ClipContents = true,
        };
        scroll.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        scroll.OffsetLeft = 0;
        scroll.OffsetTop = 0;
        scroll.OffsetRight = 0;
        scroll.OffsetBottom = 0;

        var list = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        list.AddThemeConstantOverride("separation", listSeparation);
        scroll.AddChild(list);
        listRegion.AddChild(scroll);

        var statusLabel = new Label {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);

        return (listRegion, scroll, list, statusLabel);
    }

    private static string ResolvePickerTitle(RoomType? filter, EncounterPickerOptions options) {
        if (!string.IsNullOrEmpty(options.PickerTitle))
            return options.PickerTitle!;
        if (options.Purpose == EncounterPickerPurpose.CombatAdd)
            return I18N.T("enemy.combatSidebar.addMenu", "Add enemies to combat");
        if (options.ShowTitle) {
            return filter switch {
                RoomType.Monster => I18N.T("enemy.selectNormal", "Select Normal Combat"),
                RoomType.Elite => I18N.T("enemy.selectElite", "Select Elite Combat"),
                RoomType.Boss => I18N.T("enemy.selectBoss", "Select Boss Combat"),
                _ => I18N.T("enemy.selectAny", "Select Combat Encounter"),
            };
        }
        return I18N.T("enemy.selectAny", "Select Combat Encounter");
    }

    private static void BuildPickerNavTab(VBoxContainer vbox, string title) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 0);

        var tab = new Button {
            Text = title,
            FocusMode = Control.FocusModeEnum.None,
            CustomMinimumSize = new Vector2(0, 32),
        };
        var flat = new StyleBoxFlat {
            BgColor = Colors.Transparent,
            ContentMarginLeft = 16,
            ContentMarginRight = 16,
            ContentMarginTop = 4,
            ContentMarginBottom = 6,
        };
        foreach (var state in new[] { "normal", "hover", "pressed", "focus" })
            tab.AddThemeStyleboxOverride(state, flat);
        tab.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        tab.AddThemeFontSizeOverride("font_size", 13);
        row.AddChild(tab);
        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        vbox.AddChild(row);

        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
    }

    private static void BuildCombatAddTitle(VBoxContainer vbox) {
        var titleLabel = new Label {
            Text = I18N.T("enemy.combatSidebar.addMenu", "Add enemies to combat"),
        };
        titleLabel.AddThemeFontSizeOverride("font_size", 13);
        titleLabel.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(titleLabel);

        vbox.AddChild(new ColorRect {
            CustomMinimumSize = new Vector2(0, 1),
            Color = KitLibTheme.Separator,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });
    }

    private static void WireContentTabs(HBoxContainer bar, Action<PickerContentTab> onChanged) {
        var encountersChip = DevPanelUI.CreateFilterChip(
            I18N.T("enemy.pickerTab.encounters", "Encounters"),
            active: true);
        var monstersChip = DevPanelUI.CreateFilterChip(
            I18N.T("enemy.pickerTab.monsters", "Monsters"),
            active: false);

        encountersChip.Toggled += on => {
            if (!on) return;
            monstersChip.SetPressedNoSignal(false);
            onChanged(PickerContentTab.Encounters);
        };
        monstersChip.Toggled += on => {
            if (!on) return;
            encountersChip.SetPressedNoSignal(false);
            onChanged(PickerContentTab.Monsters);
        };

        bar.AddChild(encountersChip);
        bar.AddChild(monstersChip);
    }

    private static void BuildRoomFilterBar(
        HBoxContainer filterBar,
        RoomType? filter,
        EncounterPickerOptions options) {
        RoomType?[] filters = [null, RoomType.Monster, RoomType.Elite, RoomType.Boss];
        string[] filterNames =
        [
            I18N.T("enemy.filterAll", "All"),
            I18N.T("enemy.filterNormal", "Normal"),
            I18N.T("enemy.filterElite", "Elite"),
            I18N.T("enemy.filterBoss", "Boss"),
        ];

        for (int i = 0; i < filters.Length; i++) {
            int idx = i;
            bool active = filter == filters[idx];
            var chip = DevPanelUI.CreateFilterChip(filterNames[idx], active);
            chip.Toggled += on => {
                if (!on || filter == filters[idx])
                    return;
                options.OnFilterChanged?.Invoke(filters[idx]);
            };
            filterBar.AddChild(chip);
        }
    }

    private static PickerPreviewBand BuildPickerPreview(PickerPreviewLayout layout) =>
        BuildTopBandPreview();

    private static PickerPreviewBand BuildTopBandPreview(float bandHeight = 96f) {
        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(0, bandHeight + 40),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkBegin,
        };
        panel.AddThemeStyleboxOverride("panel", CreatePreviewPanelStyle(contentMargin: 10));

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        panel.AddChild(vbox);

        var nameLabel = new Label {
            Text = I18N.T("enemy.hoverPreview", "Hover to preview"),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(nameLabel);

        var monstersLabel = new Label {
            Text = "",
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        monstersLabel.AddThemeFontSizeOverride("font_size", 11);
        monstersLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(monstersLabel);

        var (viewportContainer, viewport) = CreatePreviewViewport(0, bandHeight);
        vbox.AddChild(viewportContainer);

        viewportContainer.Resized += () => {
            var size = viewportContainer.Size;
            if (size.X >= 1f && size.Y >= 1f)
                viewport.Size = new Vector2I(Mathf.Max(1, (int)size.X), Mathf.Max(1, (int)size.Y));
        };

        var activeVisuals = new List<NCreatureVisuals>();

        void ResetLabels() {
            nameLabel.Text = I18N.T("enemy.hoverPreview", "Hover to preview");
            monstersLabel.Text = "";
        }

        return new PickerPreviewBand {
            Root = panel,
            ShowEncounter = enc => {
                var encTitle = enc.Title?.GetFormattedText();
                var encId = ((AbstractModel)enc).Id.Entry;
                nameLabel.Text = !string.IsNullOrEmpty(encTitle) ? encTitle : encId;

                var monsters = enc.AllPossibleMonsters?.ToList();
                if (monsters != null && monsters.Count > 0) {
                    var names = monsters
                        .Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry)
                        .Distinct();
                    monstersLabel.Text = string.Join(", ", names);
                }
                else {
                    monstersLabel.Text = "";
                }

                ClearViewport(viewport, activeVisuals);
                if (monsters != null)
                    activeVisuals.AddRange(LoadVisualsIntoViewport(viewport, monsters));
            },
            ShowMonster = mon => {
                var monTitle = mon.Title?.GetFormattedText();
                var monId = ((AbstractModel)mon).Id.Entry;
                nameLabel.Text = !string.IsNullOrEmpty(monTitle) ? monTitle : monId;
                monstersLabel.Text = monId;

                ClearViewport(viewport, activeVisuals);
                activeVisuals.AddRange(LoadVisualsIntoViewport(viewport, [mon], maxCount: 1));
            },
            Clear = () => {
                ClearViewport(viewport, activeVisuals);
                ResetLabels();
            },
        };
    }

    private static StyleBoxFlat CreatePreviewPanelStyle(int contentMargin) => new() {
        BgColor = new Color(0.09f, 0.09f, 0.12f, 0.90f),
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ContentMarginLeft = contentMargin,
        ContentMarginRight = contentMargin,
        ContentMarginTop = contentMargin - 2,
        ContentMarginBottom = contentMargin - 2,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderColor = KitLibTheme.Separator,
    };

    private static (SubViewportContainer container, SubViewport viewport) CreatePreviewViewport(
        float width,
        float height) {
        var viewportContainer = new SubViewportContainer {
            CustomMinimumSize = new Vector2(width > 0 ? width : 0, height),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            StretchShrink = 1,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var viewport = new SubViewport {
            Size = new Vector2I(
                width > 0 ? (int)width : 320,
                (int)height),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        viewportContainer.AddChild(viewport);
        return (viewportContainer, viewport);
    }

    private static Control CreateEncounterCell(
        EncounterModel enc,
        RoomType? filter,
        PickerCellStyle style,
        Action onSelected,
        Action? onHover = null,
        Action? onHoverExit = null) {
        var encId = ((AbstractModel)enc).Id.Entry;
        var encTitle = enc.Title?.GetFormattedText();
        var displayName = !string.IsNullOrEmpty(encTitle) ? encTitle : encId;

        var roomTag = enc.RoomType switch {
            RoomType.Monster => I18N.T("enemy.tagNormal", "[Normal]"),
            RoomType.Elite => I18N.T("enemy.tagElite", "[Elite]"),
            RoomType.Boss => I18N.T("enemy.tagBoss", "[Boss]"),
            _ => "",
        };

        var tagColor = enc.RoomType switch {
            RoomType.Elite => new Color(1f, 0.8f, 0.27f),
            RoomType.Boss => new Color(1f, 0.27f, 0.27f),
            _ => new Color(0.53f, 0.8f, 0.53f),
        };

        var cellBaseBg = enc.RoomType switch {
            RoomType.Elite => new Color(0.18f, 0.14f, 0.07f, 0.75f),
            RoomType.Boss => new Color(0.18f, 0.08f, 0.08f, 0.75f),
            _ => new Color(0.10f, 0.10f, 0.14f, 0.70f),
        };
        var cellBorderRest = enc.RoomType switch {
            RoomType.Elite => new Color(0.80f, 0.60f, 0.20f, 0.18f),
            RoomType.Boss => new Color(0.90f, 0.25f, 0.25f, 0.18f),
            _ => KitLibTheme.Separator,
        };

        bool compact = style == PickerCellStyle.Compact;
        var cellStyle = new StyleBoxFlat {
            BgColor = cellBaseBg,
            ContentMarginLeft = compact ? 6 : 8,
            ContentMarginRight = compact ? 6 : 8,
            ContentMarginTop = compact ? 4 : 6,
            ContentMarginBottom = compact ? 4 : 6,
            CornerRadiusTopLeft = compact ? 5 : 6,
            CornerRadiusTopRight = compact ? 5 : 6,
            CornerRadiusBottomLeft = compact ? 5 : 6,
            CornerRadiusBottomRight = compact ? 5 : 6,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = cellBorderRest,
        };

        var cell = new PanelContainer {
            CustomMinimumSize = new Vector2(0, compact ? CombatAddCellHeight : 44),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = BuildEncounterTooltip(enc),
        };
        cell.AddThemeStyleboxOverride("panel", cellStyle);

        if (compact) {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 4);
            row.MouseFilter = Control.MouseFilterEnum.Ignore;

            if (filter == null && !string.IsNullOrEmpty(roomTag)) {
                var tagLabel = new Label {
                    Text = roomTag,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                tagLabel.AddThemeColorOverride("font_color", tagColor);
                tagLabel.AddThemeFontSizeOverride("font_size", 9);
                row.AddChild(tagLabel);
            }

            var nameLabel = new Label {
                Text = displayName,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                ClipText = true,
                TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            nameLabel.AddThemeFontSizeOverride("font_size", 11);
            row.AddChild(nameLabel);
            cell.AddChild(row);
        }
        else {
            var cellVBox = new VBoxContainer();
            cellVBox.AddThemeConstantOverride("separation", 1);
            cellVBox.MouseFilter = Control.MouseFilterEnum.Ignore;

            if (filter == null) {
                var tagLabel = new Label {
                    Text = roomTag,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore,
                };
                tagLabel.AddThemeColorOverride("font_color", tagColor);
                tagLabel.AddThemeFontSizeOverride("font_size", 10);
                cellVBox.AddChild(tagLabel);
            }

            var nameLabel = new Label {
                Text = displayName,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            cellVBox.AddChild(nameLabel);
            cell.AddChild(cellVBox);
        }

        cell.MouseEntered += () => {
            cellStyle.BorderColor = new Color(0.40f, 0.68f, 1f, 0.55f);
            cellStyle.BgColor = cellBaseBg.Lightened(0.10f);
            onHover?.Invoke();
        };
        cell.MouseExited += () => {
            cellStyle.BorderColor = cellBorderRest;
            cellStyle.BgColor = cellBaseBg;
            onHoverExit?.Invoke();
        };
        cell.GuiInput += ev => {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                onSelected();
        };

        return cell;
    }

    private static Control CreateMonsterCell(
        MonsterModel mon,
        PickerCellStyle style,
        Action onSelected,
        Action? onHover = null,
        Action? onHoverExit = null) {
        var monId = ((AbstractModel)mon).Id.Entry;
        var monTitle = mon.Title?.GetFormattedText();
        var displayName = !string.IsNullOrEmpty(monTitle) ? monTitle : monId;

        bool compact = style == PickerCellStyle.Compact;
        var cellBaseBg = new Color(0.10f, 0.12f, 0.18f, 0.72f);
        var cellBorderRest = new Color(0.45f, 0.58f, 0.90f, 0.28f);
        var cellStyle = new StyleBoxFlat {
            BgColor = cellBaseBg,
            ContentMarginLeft = compact ? 6 : 8,
            ContentMarginRight = compact ? 6 : 8,
            ContentMarginTop = compact ? 4 : 6,
            ContentMarginBottom = compact ? 4 : 6,
            CornerRadiusTopLeft = compact ? 5 : 6,
            CornerRadiusTopRight = compact ? 5 : 6,
            CornerRadiusBottomLeft = compact ? 5 : 6,
            CornerRadiusBottomRight = compact ? 5 : 6,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = cellBorderRest,
        };

        var cell = new PanelContainer {
            CustomMinimumSize = new Vector2(0, compact ? CombatAddCellHeight : 44),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
            TooltipText = monId,
        };
        cell.AddThemeStyleboxOverride("panel", cellStyle);

        var nameLabel = new Label {
            Text = displayName,
            ClipText = compact,
            TextOverrunBehavior = compact ? TextServer.OverrunBehavior.TrimEllipsis : TextServer.OverrunBehavior.NoTrimming,
            HorizontalAlignment = compact ? HorizontalAlignment.Left : HorizontalAlignment.Center,
            AutowrapMode = compact ? TextServer.AutowrapMode.Off : TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        nameLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        nameLabel.AddThemeFontSizeOverride("font_size", compact ? 11 : 12);
        cell.AddChild(nameLabel);

        cell.MouseEntered += () => {
            cellStyle.BorderColor = new Color(0.40f, 0.68f, 1f, 0.55f);
            cellStyle.BgColor = cellBaseBg.Lightened(0.10f);
            onHover?.Invoke();
        };
        cell.MouseExited += () => {
            cellStyle.BorderColor = cellBorderRest;
            cellStyle.BgColor = cellBaseBg;
            onHoverExit?.Invoke();
        };
        cell.GuiInput += ev => {
            if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true })
                onSelected();
        };

        return cell;
    }

    private static string BuildEncounterTooltip(EncounterModel enc) {
        var encId = ((AbstractModel)enc).Id.Entry;
        var monsters = enc.AllPossibleMonsters?.ToList();
        if (monsters == null || monsters.Count == 0)
            return encId;

        var names = monsters
            .Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry)
            .Distinct();
        return $"{encId}\n{string.Join(", ", names)}";
    }

    private static string GetEncounterSearchKey(EncounterModel enc) {
        var encId = ((AbstractModel)enc).Id.Entry;
        var encTitle = enc.Title?.GetFormattedText();
        var displayName = !string.IsNullOrEmpty(encTitle) ? encTitle : encId;
        return $"{displayName} {encId}".ToLowerInvariant();
    }

    private static string GetMonsterSearchKey(MonsterModel mon) {
        var monId = ((AbstractModel)mon).Id.Entry;
        var monTitle = mon.Title?.GetFormattedText();
        var displayName = !string.IsNullOrEmpty(monTitle) ? monTitle : monId;
        return $"{displayName} {monId}".ToLowerInvariant();
    }
}
