using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.Actions;
using Godot;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Combat;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace DevMode.UI;

/// <summary>
/// Encounter picker — spliced to the DevMode rail (full-width), matching card / relic browser layout.
/// Supports filtering by room type, search, and creature-visual preview.
/// </summary>
internal static partial class EnemySelectUI {
    private const string RootName = "DevModeEnemySelect";

    // Cache of monsters whose visuals failed to load — avoid retrying
    private static readonly HashSet<string> _failedVisuals = new();

    /// <summary>
    /// Safely try to create creature visuals. Returns null on failure.
    /// Caches failures to avoid repeated error spam.
    /// </summary>
    private static NCreatureVisuals? TryCreateVisuals(MonsterModel monster) {
        return TryCreateVisualsPublic(monster);
    }

    /// <summary>Public accessor for other patches to use safe visual loading.</summary>
    public static NCreatureVisuals? TryCreateVisualsPublic(MonsterModel monster) {
        var id = ((AbstractModel)monster).Id.Entry;
        if (_failedVisuals.Contains(id)) return null;

        try {
            // Use ToMutable().CreateVisuals() which respects any VisualsPath overrides.
            // AssetCache.GetScene will fallback to synchronous ResourceLoader.Load
            // if the scene isn't pre-cached — this is fine for preview purposes.
            return monster.ToMutable().CreateVisuals();
        }
        catch (Exception ex) {
            _failedVisuals.Add(id);
            MainFile.Logger.Warn($"EnemySelectUI: Visual load failed for {id}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Load creature visuals into a SubViewport, with fallback label on failure.
    /// Returns the list of created visuals for cleanup.
    /// </summary>
    private static List<NCreatureVisuals> LoadVisualsIntoViewport(
        SubViewport viewport, IList<MonsterModel> monsters, int maxCount = 3) {
        var result = new List<NCreatureVisuals>();
        int count = Math.Min(monsters.Count, maxCount);
        float totalWidth = viewport.Size.X;
        float spacing = totalWidth / Math.Max(count, 1);

        for (int i = 0; i < count; i++) {
            var visuals = TryCreateVisuals(monsters[i]);
            if (visuals != null) {
                float scale = count <= 1 ? 0.45f : count == 2 ? 0.35f : 0.3f;
                visuals.Scale = new Vector2(scale, scale);
                visuals.Position = new Vector2(spacing * i + spacing / 2, viewport.Size.Y * 0.75f);
                viewport.AddChild(visuals);

                // Start idle animation — _Ready() initializes SpineBody,
                // but GenerateAnimator is needed to drive the state machine.
                try {
                    if (visuals.SpineBody != null) {
                        var mutable = monsters[i].ToMutable();
                        mutable.GenerateAnimator(visuals.SpineBody);
                        visuals.SetUpSkin(mutable);
                    }
                }
                catch { /* non-critical: preview works without animation */ }

                result.Add(visuals);
            }
        }

        // If no visuals loaded at all, show a fallback label
        if (result.Count == 0 && monsters.Count > 0) {
            var fallback = new Label {
                Text = I18N.T("enemy.previewUnavailable", "Preview unavailable"),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            fallback.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            fallback.Position = new Vector2(totalWidth / 2 - 40, viewport.Size.Y / 2 - 10);
            viewport.AddChild(fallback);
        }

        return result;
    }

    /// <summary>Clear all creature visuals (and any fallback labels) from a viewport.</summary>
    private static void ClearViewport(SubViewport viewport, List<NCreatureVisuals> visuals) {
        foreach (var v in visuals)
            if (GodotObject.IsInstanceValid(v)) v.QueueFree();
        visuals.Clear();

        // Also remove any fallback labels
        foreach (var child in viewport.GetChildren())
            if (child is Label) child.QueueFree();
    }

    public static void Show(NGlobalUi globalUi, RoomType? filter, Action<EncounterModel> onSelected) {
        Hide(globalUi);

        var encounters = EnemyActions.GetAllEncounters(filter);
        if (encounters.Count == 0) return;

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, 0f, () => Hide(globalUi), contentSeparation: 10, backdropWhenFullWidth: true);

        BuildEncounterPicker(vbox, globalUi, filter, onSelected, new EncounterPickerOptions {
            CloseOnSelect = true,
            ShowTitle = true,
            OnFilterChanged = nextFilter => {
                Hide(globalUi);
                Show(globalUi, nextFilter, onSelected);
            },
        });

        ((Node)globalUi).AddChild(root);
        GrabEncounterSearchFocus(vbox);
    }

    private static void GrabEncounterSearchFocus(VBoxContainer vbox) {
        foreach (var child in vbox.GetChildren()) {
            if (child is not Control control)
                continue;
            if (TryGrabSearchFocus(control))
                return;
        }
    }

    private static bool TryGrabSearchFocus(Control control) {
        if (control is LineEdit search) {
            search.GrabFocus();
            return true;
        }

        foreach (var child in control.GetChildren()) {
            if (child is Control nested && TryGrabSearchFocus(nested))
                return true;
        }

        return false;
    }

    internal sealed class EncounterPickerOptions {
        public bool CloseOnSelect { get; init; } = true;
        public bool ShowTitle { get; init; } = true;
        public bool CompactEmbedded { get; init; }
        public string? PickerTitle { get; init; }
        public Action? OnBack { get; init; }
        public Action<RoomType?>? OnFilterChanged { get; init; }
    }

    internal static void BuildEncounterPicker(
        VBoxContainer vbox,
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onSelected,
        EncounterPickerOptions options) {
        var encounters = EnemyActions.GetAllEncounters(filter);
        if (encounters.Count == 0) {
            vbox.AddChild(new Label {
                Text = I18N.T("enemy.emptyEncounters", "No encounters found."),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        if (options.OnBack != null) {
            var backBtn = new Button {
                Text = I18N.T("enemy.pickerBack", "Back"),
                CustomMinimumSize = new Vector2(0, 32),
                FocusMode = Control.FocusModeEnum.None,
            };
            backBtn.Pressed += () => options.OnBack!();
            vbox.AddChild(backBtn);
        }

        if (!string.IsNullOrEmpty(options.PickerTitle)) {
            vbox.AddChild(DevPanelUI.CreatePanelTitle(options.PickerTitle!));
            vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        }
        else if (options.ShowTitle) {
            var titleText = filter switch {
                RoomType.Monster => I18N.T("enemy.selectNormal", "Select Normal Combat"),
                RoomType.Elite => I18N.T("enemy.selectElite", "Select Elite Combat"),
                RoomType.Boss => I18N.T("enemy.selectBoss", "Select Boss Combat"),
                _ => I18N.T("enemy.selectAny", "Select Combat Encounter")
            };
            vbox.AddChild(DevPanelUI.CreatePanelTitle(titleText));
            vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        }

        // ── Filter chips ──
        var filterBar = new HBoxContainer();
        filterBar.AddThemeConstantOverride("separation", 5);
        RoomType?[] filters = [null, RoomType.Monster, RoomType.Elite, RoomType.Boss];
        string[] filterNames =
        [
            I18N.T("enemy.filterAll",    "All"),
            I18N.T("enemy.filterNormal", "Normal"),
            I18N.T("enemy.filterElite",  "Elite"),
            I18N.T("enemy.filterBoss",   "Boss")
        ];
        for (int i = 0; i < filters.Length; i++) {
            int idx = i;
            bool active = filter == filters[idx];
            var chip = DevPanelUI.CreateFilterChip(filterNames[idx], active);
            chip.Toggled += on => {
                if (!on || filter == filters[idx])
                    return;
                if (options.OnFilterChanged != null)
                    options.OnFilterChanged(filters[idx]);
                else {
                    Hide(globalUi);
                    Show(globalUi, filters[idx], onSelected);
                }
            };
            filterBar.AddChild(chip);
        }
        vbox.AddChild(filterBar);

        // ── Search box ──
        var (searchRowCtrl, searchBox) = DevPanelUI.CreateSearchRow(
            I18N.T("enemy.searchPlaceholder", "Search encounters..."));
        vbox.AddChild(searchRowCtrl);

        // ── Main content: grid left, preview right (or compact single column) ──
        var contentHBox = new HBoxContainer();
        contentHBox.AddThemeConstantOverride("separation", 12);
        contentHBox.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
        if (!options.CompactEmbedded)
            vbox.AddChild(contentHBox);

        var gridContainer = new GridContainer {
            Columns = options.CompactEmbedded ? 1 : 3,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        gridContainer.AddThemeConstantOverride("h_separation", 6);
        gridContainer.AddThemeConstantOverride("v_separation", 6);

        // Embedded pickers live inside the map detail ScrollContainer; nesting another
        // ScrollContainer here collapses to zero height and hides every encounter row.
        if (options.CompactEmbedded) {
            vbox.AddChild(gridContainer);
        }
        else {
            var scroll = new ScrollContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                CustomMinimumSize = new Vector2(480, 0),
            };
            scroll.AddChild(gridContainer);
            contentHBox.AddChild(scroll);
        }

        PanelContainer? previewPanel = null;
        VBoxContainer? previewVBox = null;
        Label? previewNameLabel = null;
        VBoxContainer? previewIdContainer = null;
        Label? previewMonstersLabel = null;
        SubViewport? subViewport = null;

        if (!options.CompactEmbedded) {
            // Right: preview panel
            previewPanel = new PanelContainer {
                CustomMinimumSize = new Vector2(300, 0),
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            };
            var previewStyle = new StyleBoxFlat {
                BgColor = new Color(0.09f, 0.09f, 0.12f, 0.90f),
                CornerRadiusTopLeft = 10,
                CornerRadiusTopRight = 10,
                CornerRadiusBottomLeft = 10,
                CornerRadiusBottomRight = 10,
                ContentMarginLeft = 12,
                ContentMarginRight = 12,
                ContentMarginTop = 12,
                ContentMarginBottom = 12,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderColor = DevModeTheme.Separator,
            };
            previewPanel.AddThemeStyleboxOverride("panel", previewStyle);
            contentHBox.AddChild(previewPanel);

            previewVBox = new VBoxContainer();
            previewVBox.AddThemeConstantOverride("separation", 6);
            previewPanel.AddChild(previewVBox);

            previewNameLabel = new Label {
                Text = I18N.T("enemy.hoverPreview", "Hover to preview"),
                HorizontalAlignment = HorizontalAlignment.Center,
            };
            previewNameLabel.AddThemeFontSizeOverride("font_size", 14);
            previewNameLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            previewVBox.AddChild(previewNameLabel);

            previewIdContainer = new VBoxContainer {
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            previewVBox.AddChild(previewIdContainer);

            previewMonstersLabel = new Label {
                Text = "",
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
            previewMonstersLabel.AddThemeColorOverride("font_color", DevModeTheme.TextSecondary);
            previewMonstersLabel.AddThemeFontSizeOverride("font_size", 12);
            previewVBox.AddChild(previewMonstersLabel);

            var visualContainer = new SubViewportContainer {
                SizeFlagsVertical = Control.SizeFlags.ExpandFill,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                StretchShrink = 1,
                Stretch = true,
            };
            subViewport = new SubViewport {
                Size = new Vector2I(300, 280),
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            };
            visualContainer.AddChild(subViewport);
            previewVBox.AddChild(visualContainer);
        }

        var activeVisuals = new List<NCreatureVisuals>();

        void ShowPreview(EncounterModel enc) {
            if (options.CompactEmbedded || previewNameLabel == null || subViewport == null)
                return;

            var encTitle = enc.Title?.GetFormattedText();
            var encId = ((AbstractModel)enc).Id.Entry;
            previewNameLabel.Text = !string.IsNullOrEmpty(encTitle) ? encTitle : encId;
            if (previewIdContainer != null) {
                foreach (var c in previewIdContainer.GetChildren()) ((Node)c).QueueFree();
                if (!string.IsNullOrEmpty(encId) && encId != encTitle)
                    previewIdContainer.AddChild(DevModeTheme.CreateCopyableIdRow(encId));
            }

            var monsters = enc.AllPossibleMonsters?.ToList();
            if (previewMonstersLabel != null) {
                if (monsters != null && monsters.Count > 0) {
                    var names = monsters
                        .Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry)
                        .Distinct();
                    previewMonstersLabel.Text = string.Join(", ", names);
                }
                else {
                    previewMonstersLabel.Text = "";
                }
            }

            ClearViewport(subViewport, activeVisuals);
            if (monsters != null)
                activeVisuals = LoadVisualsIntoViewport(subViewport, monsters);
        }

        // Populate grid
        var cells = new List<(Control cell, string searchKey)>();
        foreach (var enc in encounters) {
            var encId = ((AbstractModel)enc).Id.Entry;
            var encTitle = enc.Title?.GetFormattedText();
            var displayName = !string.IsNullOrEmpty(encTitle) ? encTitle : encId;

            var roomTag = enc.RoomType switch {
                RoomType.Monster => I18N.T("enemy.tagNormal", "[Normal]"),
                RoomType.Elite => I18N.T("enemy.tagElite", "[Elite]"),
                RoomType.Boss => I18N.T("enemy.tagBoss", "[Boss]"),
                _ => ""
            };

            var tagColor = enc.RoomType switch {
                RoomType.Elite => new Color(1f, 0.8f, 0.27f),
                RoomType.Boss => new Color(1f, 0.27f, 0.27f),
                _ => new Color(0.53f, 0.8f, 0.53f)
            };

            var cellBaseBg = enc.RoomType switch {
                RoomType.Elite => new Color(0.18f, 0.14f, 0.07f, 0.75f),
                RoomType.Boss => new Color(0.18f, 0.08f, 0.08f, 0.75f),
                _ => new Color(0.10f, 0.10f, 0.14f, 0.70f)
            };
            var cellBorderRest = enc.RoomType switch {
                RoomType.Elite => new Color(0.80f, 0.60f, 0.20f, 0.18f),
                RoomType.Boss => new Color(0.90f, 0.25f, 0.25f, 0.18f),
                _ => DevModeTheme.Separator
            };

            var cell = new PanelContainer {
                CustomMinimumSize = new Vector2(options.CompactEmbedded ? 0 : 150, options.CompactEmbedded ? 44 : 52),
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            var cellStyle = new StyleBoxFlat {
                BgColor = cellBaseBg,
                ContentMarginLeft = 8,
                ContentMarginRight = 8,
                ContentMarginTop = 6,
                ContentMarginBottom = 6,
                CornerRadiusTopLeft = 6,
                CornerRadiusTopRight = 6,
                CornerRadiusBottomLeft = 6,
                CornerRadiusBottomRight = 6,
                BorderWidthTop = 1,
                BorderWidthBottom = 1,
                BorderWidthLeft = 1,
                BorderWidthRight = 1,
                BorderColor = cellBorderRest
            };
            cell.AddThemeStyleboxOverride("panel", cellStyle);

            var cellVBox = new VBoxContainer();
            cellVBox.AddThemeConstantOverride("separation", 1);
            cellVBox.MouseFilter = Control.MouseFilterEnum.Ignore;

            // Room type tag (if showing all)
            if (filter == null) {
                var tagLabel = new Label {
                    Text = roomTag,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    MouseFilter = Control.MouseFilterEnum.Ignore
                };
                tagLabel.AddThemeColorOverride("font_color", tagColor);
                tagLabel.AddThemeFontSizeOverride("font_size", 10);
                cellVBox.AddChild(tagLabel);
            }

            var nameLabel = new Label {
                Text = displayName,
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            nameLabel.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
            nameLabel.AddThemeFontSizeOverride("font_size", 12);
            cellVBox.AddChild(nameLabel);

            cell.AddChild(cellVBox);

            // Hover
            var captured = enc;
            if (!options.CompactEmbedded) {
                cell.MouseEntered += () => {
                    cellStyle.BorderColor = new Color(0.40f, 0.68f, 1f, 0.55f);
                    cellStyle.BgColor = cellBaseBg.Lightened(0.10f);
                    ShowPreview(captured);
                };
                cell.MouseExited += () => {
                    cellStyle.BorderColor = cellBorderRest;
                    cellStyle.BgColor = cellBaseBg;
                };
            }

            // Click
            cell.GuiInput += (InputEvent ev) => {
                if (ev is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) {
                    if (subViewport != null)
                        ClearViewport(subViewport, activeVisuals);
                    onSelected(captured);
                    if (options.CloseOnSelect)
                        Hide(globalUi);
                }
            };

            gridContainer.AddChild(cell);
            cells.Add((cell, $"{displayName} {encId}".ToLowerInvariant()));
        }

        // Search filtering
        searchBox.TextChanged += (string text) => {
            var query = text.Trim().ToLowerInvariant();
            foreach (var (cell, key) in cells)
                cell.Visible = string.IsNullOrEmpty(query) || key.Contains(query);
        };
    }

    public static void Hide(NGlobalUi globalUi) {
        _activeMapSession = null;
        CloseExtensionPicker();
        _mainDual = null;
        _mainGlobalUi = null;
        _extensionHost = null;

        ((Node)globalUi).GetNodeOrNull<Control>($"{RootName}EncounterOverlay")?.QueueFree();

        var node = ((Node)globalUi).GetNodeOrNull<Control>(RootName);
        if (node != null) {
            ((Node)globalUi).RemoveChild(node);
            node.QueueFree();
        }
    }

    // ── Per-floor picker (shows a number input + encounter selector) ──

    public static void ShowFloorPicker(NGlobalUi globalUi) {
        Hide(globalUi);

        var (root, _, vbox) = DevPanelUI.CreateBrowserOverlayShell(
            globalUi, RootName, 600f, () => Hide(globalUi), contentSeparation: 10);

        BuildFloorPicker(vbox, globalUi, embedded: false);
        ((Node)globalUi).AddChild(root);
    }

    internal static void BuildFloorPicker(
        VBoxContainer vbox,
        NGlobalUi globalUi,
        bool embedded,
        Action<string>? onStatusChanged = null) {
        if (!embedded) {
            vbox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("enemy.byFloorTitle", "Customize Enemies by Floor")));
            vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        }

        // Current floor overrides list
        var overridesScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 180)
        };
        var overridesList = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        };
        overridesList.AddThemeConstantOverride("separation", 2);
        overridesScroll.AddChild(overridesList);

        void RefreshOverridesList() {
            foreach (var child in overridesList.GetChildren())
                ((Node)child).QueueFree();

            if (DevModeState.FloorOverrides.Count == 0) {
                overridesList.AddChild(new Label {
                    Text = I18N.T("enemy.noFloorCustom", "No floor customizations"),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Modulate = DevModeTheme.Subtle
                });
                return;
            }

            foreach (var kv in DevModeState.FloorOverrides.OrderBy(x => x.Key)) {
                var hbox = new HBoxContainer();
                hbox.AddThemeConstantOverride("separation", 4);

                var label = new Label {
                    Text = I18N.T("enemy.floorEntry", "Floor {0}: {1}", kv.Key,
                        kv.Value != null ? EnemyActions.GetShortName(kv.Value) : I18N.T("enemy.floorEntryNone", "None")),
                    SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
                };
                hbox.AddChild(label);

                int floor = kv.Key;
                var delBtn = new Button {
                    Text = "✕",
                    CustomMinimumSize = new Vector2(28, 28),
                    FocusMode = Control.FocusModeEnum.None
                };
                delBtn.Pressed += () => {
                    EnemyActions.ClearFloorOverride(floor);
                    RefreshOverridesList();
                };
                hbox.AddChild(delBtn);
                overridesList.AddChild(hbox);
            }
        }

        RefreshOverridesList();
        vbox.AddChild(overridesScroll);

        // Add new floor override
        var addBar = new HBoxContainer();
        addBar.AddThemeConstantOverride("separation", 4);

        var floorInput = new SpinBox {
            MinValue = 1,
            MaxValue = 99,
            Value = 1,
            CustomMinimumSize = new Vector2(80, 32),
            Prefix = I18N.T("enemy.floorLabel", "Floor:")
        };
        addBar.AddChild(floorInput);

        var pickBtn = new Button {
            Text = I18N.T("enemy.selectEncounter", "Select Encounter"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 32),
            FocusMode = Control.FocusModeEnum.None
        };
        pickBtn.Pressed += () => {
            int floor = (int)floorInput.Value;
            if (embedded) {
                ShowEncounterOverlay(globalUi, null, enc => {
                    EnemyActions.SetFloorOverride(floor, enc);
                    RefreshOverridesList();
                });
                return;
            }

            Hide(globalUi);
            Show(globalUi, null, enc => {
                EnemyActions.SetFloorOverride(floor, enc);
                ShowFloorPicker(globalUi);
            });
        };
        addBar.AddChild(pickBtn);
        vbox.AddChild(addBar);

        // Clear buttons
        var bottomBar = new HBoxContainer();
        bottomBar.AddThemeConstantOverride("separation", 8);

        var clearFloorsBtn = new Button {
            Text = I18N.T("enemy.clearFloors", "Clear floor overrides"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = Control.FocusModeEnum.None
        };
        clearFloorsBtn.Pressed += () => {
            DevModeState.FloorOverrides.Clear();
            RefreshOverridesList();
            onStatusChanged?.Invoke(I18N.T(
                "enemy.byFloorHint",
                "Floor overrides apply on top of global / per-type settings."));
        };

        var clearAllBtn = new Button {
            Text = I18N.T("enemy.clearAllOverrides", "Clear all overrides"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 36),
            FocusMode = Control.FocusModeEnum.None
        };
        clearAllBtn.Pressed += () => {
            DevModeState.ClearEnemyOverrides();
            RefreshOverridesList();
            onStatusChanged?.Invoke(I18N.T("enemy.clearedOverrides", "All enemy overrides cleared."));
        };

        bottomBar.AddChild(clearFloorsBtn);
        bottomBar.AddChild(clearAllBtn);
        vbox.AddChild(bottomBar);
    }

    private static void ShowEncounterOverlay(NGlobalUi globalUi, RoomType? filter, Action<EncounterModel> onSelected) {
        var overlayName = $"{RootName}EncounterOverlay";
        ((Node)globalUi).GetNodeOrNull<Control>(overlayName)?.QueueFree();

        var backdrop = new ColorRect {
            Name = overlayName,
            Color = new Color(0f, 0f, 0f, 0.45f),
            MouseFilter = Control.MouseFilterEnum.Stop,
            AnchorRight = 1,
            AnchorBottom = 1,
            ZIndex = 1300,
        };
        backdrop.GuiInput += ev => {
            if (ev is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
                backdrop.QueueFree();
        };

        var panel = new PanelContainer {
            AnchorLeft = 0.5f,
            AnchorRight = 0.5f,
            AnchorTop = 0.5f,
            AnchorBottom = 0.5f,
            OffsetLeft = -420,
            OffsetRight = 420,
            OffsetTop = -280,
            OffsetBottom = 280,
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = DevModeTheme.PanelBg,
            BorderColor = DevModeTheme.PanelBorder,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            CornerRadiusTopLeft = 10,
            CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10,
            CornerRadiusBottomRight = 10,
            ContentMarginLeft = 12,
            ContentMarginRight = 12,
            ContentMarginTop = 12,
            ContentMarginBottom = 12,
        });

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 8);
        panel.AddChild(vbox);
        backdrop.AddChild(panel);

        BuildEncounterPicker(vbox, globalUi, filter, enc => {
            onSelected(enc);
            backdrop.QueueFree();
        }, new EncounterPickerOptions {
            CloseOnSelect = true,
            ShowTitle = false,
            OnFilterChanged = nextFilter => {
                backdrop.QueueFree();
                ShowEncounterOverlay(globalUi, nextFilter, onSelected);
            },
        });

        ((Node)globalUi).AddChild(backdrop);
        GrabEncounterSearchFocus(vbox);
    }

}
