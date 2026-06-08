using System;
using System.Collections.Generic;
using System.Linq;
using KitLib;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    private enum MapPickTarget {
        Global,
        RoomType,
        Floor,
    }

    private static MapEditorSession? _activeMapSession;

    internal static void RefreshMapCombatDetailIfOpen() {
        if (_activeMapSession == null)
            return;
        RebuildMapDetail(_activeMapSession);
    }

    private sealed class MapEditorSession {
        public required RunState RunState;
        public required MainBrowserState Browser;
        public required EnemyMapCanvas Canvas;
        public required VBoxContainer DetailHost;
        public required Control HoverHost;
        public required ScrollContainer MapScroll;
        public MapPoint? SelectedPoint;
        public MapPickTarget PickTarget;
        public RoomType? PickRoomType;
        public int? PickFloor;
        public Control? HoverPopup;
        public required Action RefreshAll;
    }

    private static void BuildMapTab(MainBrowserState state) {
        _activeMapSession = null;

        var runState = RunManager.Instance?.DebugOnlyGetState();
        if (runState?.Map == null || !KitLibState.InDevRun) {
            state.ContentHost.AddChild(new Label {
                Text = I18N.T("enemy.mapUnavailable", "Start a dev run to edit encounters on the map."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        var split = new HBoxContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        split.AddThemeConstantOverride("separation", 12);

        var mapColumn = new Control {
            CustomMinimumSize = new Vector2(360, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        var mapScroll = new ScrollContainer {
            HorizontalScrollMode = ScrollContainer.ScrollMode.Auto,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        mapScroll.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var hoverHost = new Control {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 10,
        };
        hoverHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        MapEditorSession? session = null;
        session = new MapEditorSession {
            RunState = runState,
            Browser = state,
            Canvas = null!,
            DetailHost = new VBoxContainer(),
            HoverHost = hoverHost,
            MapScroll = mapScroll,
            RefreshAll = () => {
                if (session == null) return;
                HideMapHoverPopup(session);
                session.Canvas.Rebuild(session.SelectedPoint);
                RebuildMapDetail(session);
            },
        };

        session.Canvas = new EnemyMapCanvas(
            runState,
            point => {
                session!.SelectedPoint = point;
                session.Canvas.Rebuild(point);
                RebuildMapDetail(session);
            },
            (anchor, point) => ShowMapHoverPopup(session, anchor, point),
            () => HideMapHoverPopup(session));

        mapScroll.AddChild(session.Canvas);
        mapColumn.AddChild(mapScroll);
        mapColumn.AddChild(hoverHost);
        split.AddChild(mapColumn);

        var detailPanel = new PanelContainer {
            CustomMinimumSize = new Vector2(320, 0),
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        detailPanel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(KitLibTheme.PanelBg.R, KitLibTheme.PanelBg.G, KitLibTheme.PanelBg.B, 0.85f),
            BorderColor = KitLibTheme.PanelBorder,
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

        var detailScroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto,
        };
        session.DetailHost.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        session.DetailHost.AddThemeConstantOverride("separation", 8);
        detailScroll.AddChild(session.DetailHost);
        detailPanel.AddChild(detailScroll);
        split.AddChild(detailPanel);

        state.ContentHost.AddChild(split);
        _activeMapSession = session;
        session.Canvas.Rebuild(null);
        RebuildMapDetail(session);
    }

    private static void RebuildMapDetail(MapEditorSession session) {
        ClearDetailHost(session.DetailHost);
        BuildMapSummaryDetail(session);
    }

    private static void BuildMapSummaryDetail(MapEditorSession session) {
        BuildCurrentCombatDetailSection(
            session.DetailHost,
            session.Browser.GlobalUi);

        session.DetailHost.AddChild(MakeSubtleLabel(
            I18N.T("enemy.runScopeHint", "Active for this run only · cleared when the run ends.")));

        var rulesTitle = new Label {
            Text = I18N.T("enemy.runRulesTitle", "Run rules"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        rulesTitle.AddThemeFontSizeOverride("font_size", 13);
        session.DetailHost.AddChild(rulesTitle);

        AddRunRuleRow(
            session,
            I18N.T("enemy.runRuleAll", "All combats"),
            FormatRunRuleValue(KitLibState.EnemyMode == EnemyMode.Global
                ? KitLibState.GlobalEncounterOverride
                : null),
            KitLibState.EnemyMode == EnemyMode.Global && KitLibState.GlobalEncounterOverride != null,
            () => {
                session.Browser.EncounterFilter = null;
                OpenMapPicker(session, MapPickTarget.Global, null, null);
            },
            () => {
                EnemyActions.ClearGlobalOverride();
                session.Browser.StatusLabel.Text = I18N.T("enemy.clearedGlobal", "Cleared global run rule.");
                session.RefreshAll();
            });

        foreach (var roomType in new[] { RoomType.Monster, RoomType.Elite, RoomType.Boss }) {
            var enc = KitLibState.RoomTypeOverrides.TryGetValue(roomType, out var rtEnc) ? rtEnc : null;
            bool active = KitLibState.EnemyMode == EnemyMode.PerType && enc != null;
            AddRunRuleRow(
                session,
                RoomTypeLabel(roomType),
                FormatRunRuleValue(active ? enc : null),
                active,
                () => OpenMapPicker(session, MapPickTarget.RoomType, roomType, null),
                () => {
                    EnemyActions.ClearRoomTypeOverride(roomType);
                    session.Browser.StatusLabel.Text = I18N.T(
                        "enemy.clearedByType",
                        "Cleared {0} run rule.",
                        roomType);
                    session.RefreshAll();
                });
        }

        session.DetailHost.AddChild(new HSeparator());
        var nodeTitle = new Label {
            Text = I18N.T("enemy.selectedNode", "Selected node"),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        nodeTitle.AddThemeFontSizeOverride("font_size", 13);
        session.DetailHost.AddChild(nodeTitle);

        if (session.SelectedPoint == null)
            session.DetailHost.AddChild(MakeSubtleLabel(
                I18N.T("enemy.mapSelectHint", "Click a combat node on the map to view or edit its encounter.")));
        else
            BuildSelectedNodeSection(session, session.SelectedPoint);

        AddMapClearButtons(session);
    }

    private static void AddRunRuleRow(
        MapEditorSession session,
        string label,
        string value,
        bool canClear,
        Action onChange,
        Action onClear) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var textBox = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        textBox.AddThemeConstantOverride("separation", 1);
        var nameLabel = new Label {
            Text = label,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 12);
        textBox.AddChild(nameLabel);
        var valueLabel = new Label {
            Text = value,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        valueLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        valueLabel.AddThemeFontSizeOverride("font_size", 11);
        textBox.AddChild(valueLabel);
        row.AddChild(textBox);

        var changeBtn = new Button {
            Text = I18N.T("enemy.changeRule", "Change"),
            CustomMinimumSize = new Vector2(72, 30),
            FocusMode = Control.FocusModeEnum.None,
        };
        changeBtn.Pressed += onChange;
        row.AddChild(changeBtn);

        if (canClear) {
            var clearBtn = new Button {
                Text = I18N.T("enemy.clearRule", "Clear"),
                CustomMinimumSize = new Vector2(64, 30),
                FocusMode = Control.FocusModeEnum.None,
            };
            clearBtn.Pressed += onClear;
            row.AddChild(clearBtn);
        }

        session.DetailHost.AddChild(row);
    }

    private static string FormatRunRuleValue(EncounterModel? encounter) {
        if (encounter == null)
            return I18N.T("enemy.runRuleUnset", "Not set");
        return EnemyActions.GetShortName(encounter);
    }

    private static string RoomTypeLabel(RoomType roomType) => roomType switch {
        RoomType.Monster => I18N.T("enemy.filterNormal", "Normal"),
        RoomType.Elite => I18N.T("enemy.filterElite", "Elite"),
        RoomType.Boss => I18N.T("enemy.filterBoss", "Boss"),
        _ => roomType.ToString(),
    };

    private static void OpenMapPicker(
        MapEditorSession session,
        MapPickTarget target,
        RoomType? roomType,
        int? floor) {
        session.PickTarget = target;
        session.PickRoomType = roomType;
        session.PickFloor = floor;
        HideMapHoverPopup(session);

        RoomType? filter = target switch {
            MapPickTarget.RoomType => roomType,
            MapPickTarget.Floor => roomType,
            MapPickTarget.Global => session.Browser.EncounterFilter,
            _ => session.Browser.EncounterFilter,
        };

        string title = target switch {
            MapPickTarget.Global => I18N.T("enemy.pickGlobalRule", "Set run rule — all combats"),
            MapPickTarget.RoomType => I18N.T("enemy.pickTypeRule", "Set run rule — {0}", RoomTypeLabel(roomType!.Value)),
            MapPickTarget.Floor => I18N.T("enemy.pickFloorRule", "Replace node encounter"),
            _ => I18N.T("enemy.selectAny", "Select Combat Encounter"),
        };

        ShowEncounterInExtension(
            session.Browser.GlobalUi,
            filter,
            enc => {
                switch (target) {
                    case MapPickTarget.Global:
                        EnemyActions.SetGlobalOverride(enc);
                        session.Browser.StatusLabel.Text = I18N.T(
                            "enemy.appliedGlobal",
                            "Applied global override: {0}",
                            EnemyActions.GetShortName(enc));
                        break;
                    case MapPickTarget.RoomType:
                        EnemyActions.SetRoomTypeOverride(roomType!.Value, enc);
                        session.Browser.StatusLabel.Text = I18N.T(
                            "enemy.appliedByType",
                            "Applied {0} override: {1}",
                            roomType,
                            EnemyActions.GetShortName(enc));
                        break;
                    case MapPickTarget.Floor:
                        EnemyActions.SetFloorOverride(floor!.Value, enc);
                        session.Browser.StatusLabel.Text = I18N.T(
                            "enemy.appliedFloor",
                            "Floor {0} set to {1}.",
                            floor,
                            EnemyActions.GetShortName(enc));
                        break;
                }

                session.RefreshAll();
            },
            new EncounterPickerOptions {
                CloseOnSelect = true,
                ShowTitle = false,
                PickerTitle = title,
                Purpose = EncounterPickerPurpose.RunRule,
                OnFilterChanged = nextFilter => {
                    session.Browser.EncounterFilter = nextFilter;
                    OpenMapPicker(session, target, roomType, floor);
                },
            });
    }

    private static void BuildSelectedNodeSection(MapEditorSession session, MapPoint point) {
        if (!MapEncounterPreview.IsCombatNode(point.PointType)) {
            session.DetailHost.AddChild(new Label {
                Text = I18N.T("enemy.mapNonCombat", "This node is not a combat room."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });
            return;
        }

        var preview = MapEncounterPreview.Build(session.RunState, point);
        if (preview == null) {
            session.DetailHost.AddChild(new Label {
                Text = I18N.T("enemy.mapNoPreview", "Could not preview this node."),
            });
            return;
        }

        string typeTag = preview.CombatRoomType switch {
            RoomType.Monster => I18N.T("map.roomNormal", "Normal"),
            RoomType.Elite => I18N.T("map.roomElite", "Elite"),
            RoomType.Boss => I18N.T("map.roomBoss", "Boss"),
            _ => preview.CombatRoomType.ToString(),
        };

        var header = new Label {
            Text = I18N.T("map.tooltipHeader", "[{0}] Floor {1}", typeTag, preview.Floor),
        };
        header.AddThemeFontSizeOverride("font_size", 14);
        header.AddThemeColorOverride("font_color", RoomTypeColor(preview.CombatRoomType));
        session.DetailHost.AddChild(header);

        if (preview.IsCurrentRoom) {
            session.DetailHost.AddChild(MakeSubtleLabel(
                I18N.T("enemy.mapCurrentRoom", "You are currently in this room.")));
        }

        if (preview.Encounter is { } encounter) {
            string encounterName = encounter.Title?.GetFormattedText()
                ?? ((AbstractModel)encounter).Id.Entry;
            session.DetailHost.AddChild(new Label {
                Text = preview.IsOverride
                    ? I18N.T("map.override", "{0} (Override)", encounterName)
                    : encounterName,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            });

            string encId = ((AbstractModel)encounter).Id.Entry;
            if (encId != encounterName)
                session.DetailHost.AddChild(MakeSubtleLabel(encId));

            var monsters = encounter.AllPossibleMonsters?.ToList();
            if (monsters is { Count: > 0 }) {
                session.DetailHost.AddChild(BuildMonsterPreviewRow(monsters));
                session.DetailHost.AddChild(MakeSubtleLabel(string.Join(", ",
                    monsters.Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry).Distinct())));
            }
        }
        else {
            session.DetailHost.AddChild(MakeSubtleLabel(
                I18N.T("enemy.mapNoEncounter", "No encounter predicted for this node.")));
        }

        session.DetailHost.AddChild(MakeSubtleLabel(DescribeOverrideSource(preview)));

        if (!preview.IsCurrentRoom) {
            var replaceBtn = new Button {
                Text = I18N.T("enemy.mapReplaceEncounter", "Replace encounter"),
                CustomMinimumSize = new Vector2(0, 34),
                FocusMode = Control.FocusModeEnum.None,
            };
            replaceBtn.Pressed += () => OpenMapPicker(
                session,
                MapPickTarget.Floor,
                preview.CombatRoomType,
                preview.Floor);
            session.DetailHost.AddChild(replaceBtn);

            if (preview.IsFloorOverride) {
                var clearFloorBtn = new Button {
                    Text = I18N.T("enemy.mapClearFloor", "Clear floor override"),
                    CustomMinimumSize = new Vector2(0, 32),
                    FocusMode = Control.FocusModeEnum.None,
                };
                clearFloorBtn.Pressed += () => {
                    EnemyActions.ClearFloorOverride(preview.Floor);
                    session.Browser.StatusLabel.Text = I18N.T(
                        "enemy.clearedFloor",
                        "Cleared floor {0} override.",
                        preview.Floor);
                    session.RefreshAll();
                };
                session.DetailHost.AddChild(clearFloorBtn);
            }
        }
    }

    private static string DescribeOverrideSource(MapEncounterPreview.Preview preview) {
        if (preview.IsFloorOverride)
            return I18N.T("enemy.mapFloorOverride", "Floor override is active for this node.");
        if (preview.IsGlobalOrTypeOverride)
            return I18N.T("enemy.mapInheritedOverride", "Using a run rule (no floor override).");
        return I18N.T("enemy.mapVanillaEncounter", "Using the act's default encounter pool.");
    }

    private static void AddMapClearButtons(MapEditorSession session) {
        session.DetailHost.AddChild(new HSeparator());

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        var clearFloorsBtn = new Button {
            Text = I18N.T("enemy.clearFloors", "Clear floor overrides"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        clearFloorsBtn.Pressed += () => {
            KitLibState.FloorOverrides.Clear();
            session.Browser.StatusLabel.Text = I18N.T(
                "enemy.clearedFloors",
                "Cleared all floor overrides.");
            session.RefreshAll();
        };

        var clearAllBtn = new Button {
            Text = I18N.T("enemy.clearAllOverrides", "Clear all overrides"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        clearAllBtn.Pressed += () => {
            KitLibState.ClearEnemyOverrides();
            session.Browser.StatusLabel.Text = I18N.T(
                "enemy.clearedOverrides", "All enemy overrides cleared.");
            session.RefreshAll();
        };

        row.AddChild(clearFloorsBtn);
        row.AddChild(clearAllBtn);
        session.DetailHost.AddChild(row);
    }

    private static void ShowMapHoverPopup(MapEditorSession session, Control anchor, MapPoint point) {
        HideMapHoverPopup(session);

        var preview = MapEncounterPreview.Build(session.RunState, point);
        if (preview?.Encounter == null)
            return;

        session.HoverPopup = BuildEncounterHoverPopup(preview);
        session.HoverPopup.Name = "MapNodeHoverPopup";
        session.HoverPopup.ZIndex = 20;
        session.HoverHost.AddChild(session.HoverPopup);
        PositionMapHoverPopup(session, anchor);
    }

    private static void HideMapHoverPopup(MapEditorSession? session) {
        if (session?.HoverPopup == null)
            return;
        session.HoverPopup.QueueFree();
        session.HoverPopup = null;
    }

    private static void PositionMapHoverPopup(MapEditorSession session, Control anchor) {
        if (session.HoverPopup == null || !GodotObject.IsInstanceValid(session.HoverPopup))
            return;

        void Place(bool retry) {
            if (session.HoverPopup == null || !GodotObject.IsInstanceValid(session.HoverPopup))
                return;

            const float margin = 8f;
            const float gap = 8f;

            var popupSize = session.HoverPopup.Size;
            if (popupSize.X <= 0f || popupSize.Y <= 0f)
                popupSize = session.HoverPopup.GetCombinedMinimumSize();

            if (popupSize.Y <= 0f && retry) {
                Callable.From(() => Place(false)).CallDeferred();
                return;
            }

            var anchorRect = anchor.GetGlobalRect();
            var viewRect = session.MapScroll.GetGlobalRect();

            float globalX = anchorRect.GetCenter().X - popupSize.X * 0.5f;
            globalX = Mathf.Clamp(globalX, viewRect.Position.X + margin, viewRect.End.X - popupSize.X - margin);

            float yAbove = anchorRect.Position.Y - popupSize.Y - gap;
            float yBelow = anchorRect.End.Y + gap;
            float globalY;
            if (yAbove >= viewRect.Position.Y + margin)
                globalY = yAbove;
            else if (yBelow + popupSize.Y <= viewRect.End.Y - margin)
                globalY = yBelow;
            else
                globalY = Mathf.Clamp(yAbove, viewRect.Position.Y + margin, viewRect.End.Y - popupSize.Y - margin);

            session.HoverPopup.Position = session.HoverHost.GetGlobalTransformWithCanvas().AffineInverse()
                * new Vector2(globalX, globalY);
        }

        Callable.From(() => Place(true)).CallDeferred();
    }

    private static Control BuildMonsterPreviewRow(IList<MonsterModel> monsters, float bandHeight = 90f) {
        float width = Math.Min(monsters.Count * 72, 216);
        var container = new SubViewportContainer {
            CustomMinimumSize = new Vector2(width, bandHeight),
            StretchShrink = 1,
            Stretch = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var viewport = new SubViewport {
            Size = new Vector2I((int)width, (int)bandHeight),
            TransparentBg = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        container.AddChild(viewport);
        LoadVisualsIntoViewport(viewport, monsters, maxCount: 3);
        return container;
    }

    private static PanelContainer BuildEncounterHoverPopup(MapEncounterPreview.Preview preview) {
        var encounter = preview.Encounter!;
        string encounterName = encounter.Title?.GetFormattedText()
            ?? ((AbstractModel)encounter).Id.Entry;

        string typeTag = preview.CombatRoomType switch {
            RoomType.Monster => I18N.T("map.roomNormal", "Normal"),
            RoomType.Elite => I18N.T("map.roomElite", "Elite"),
            RoomType.Boss => I18N.T("map.roomBoss", "Boss"),
            _ => preview.CombatRoomType.ToString(),
        };

        var panel = new PanelContainer {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            CustomMinimumSize = new Vector2(220, 0),
        };
        panel.AddThemeStyleboxOverride("panel", new StyleBoxFlat {
            BgColor = new Color(0.06f, 0.06f, 0.1f, 0.96f),
            ContentMarginLeft = 8,
            ContentMarginRight = 8,
            ContentMarginTop = 6,
            ContentMarginBottom = 6,
            CornerRadiusTopLeft = 8,
            CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8,
            CornerRadiusBottomRight = 8,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderColor = new Color(0.4f, 0.4f, 0.55f, 0.75f),
        });

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 3);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        var header = new Label {
            Text = I18N.T("map.tooltipHeader", "[{0}] Floor {1}", typeTag, preview.Floor),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        header.AddThemeFontSizeOverride("font_size", 11);
        header.AddThemeColorOverride("font_color", RoomTypeColor(preview.CombatRoomType));
        vbox.AddChild(header);

        vbox.AddChild(new Label {
            Text = preview.IsOverride
                ? I18N.T("map.override", "{0} (Override)", encounterName)
                : encounterName,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MaxLinesVisible = 2,
            TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        });

        var monsters = encounter.AllPossibleMonsters?.ToList();
        if (monsters is { Count: > 0 })
            vbox.AddChild(BuildMonsterPreviewRow(monsters, bandHeight: 72f));

        panel.AddChild(vbox);
        return panel;
    }

    private static Label MakeSubtleLabel(string text) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    private static void ClearDetailHost(VBoxContainer host) {
        foreach (var child in host.GetChildren())
            ((Node)child).QueueFree();
    }

    private static Color RoomTypeColor(RoomType roomType) => roomType switch {
        RoomType.Elite => new Color(1f, 0.8f, 0.27f),
        RoomType.Boss => new Color(1f, 0.27f, 0.27f),
        _ => new Color(0.53f, 0.8f, 0.53f),
    };

    private static Color NodeFillColor(MapPointType type) => type switch {
        MapPointType.Monster => new Color(0.18f, 0.38f, 0.22f, 0.95f),
        MapPointType.Elite => new Color(0.38f, 0.28f, 0.08f, 0.95f),
        MapPointType.Boss => new Color(0.42f, 0.12f, 0.12f, 0.95f),
        MapPointType.Shop => new Color(0.16f, 0.20f, 0.34f, 0.85f),
        MapPointType.RestSite => new Color(0.14f, 0.28f, 0.34f, 0.85f),
        MapPointType.Treasure => new Color(0.30f, 0.24f, 0.10f, 0.85f),
        _ => new Color(0.16f, 0.16f, 0.22f, 0.80f),
    };

    private sealed partial class EnemyMapCanvas : Control {
        private const float Pad = 24f;
        private const float CellW = 52f;
        private const float CellH = 58f;
        private const float NodeR = 14f;

        private readonly RunState _runState;
        private readonly Action<MapPoint> _onSelect;
        private readonly Action<Control, MapPoint> _onHoverShow;
        private readonly Action _onHoverHide;
        private readonly List<(MapPoint from, MapPoint to)> _edges = new();
        private readonly Dictionary<MapCoord, MapPoint> _pointsByCoord = new();
        private MapCoord? _currentCoord;
        private MapPoint? _selectedPoint;

        public EnemyMapCanvas(
            RunState runState,
            Action<MapPoint> onSelect,
            Action<Control, MapPoint> onHoverShow,
            Action onHoverHide) {
            _runState = runState;
            _onSelect = onSelect;
            _onHoverShow = onHoverShow;
            _onHoverHide = onHoverHide;
            MouseFilter = MouseFilterEnum.Ignore;
            ClipContents = false;
            Rebuild(null);
        }

        public void Rebuild(MapPoint? selectedPoint) {
            _selectedPoint = selectedPoint;
            _currentCoord = _runState.CurrentMapCoord;
            _onHoverHide();

            foreach (var child in GetChildren())
                child.QueueFree();

            _edges.Clear();
            _pointsByCoord.Clear();

            var map = _runState.Map;
            if (map == null) return;

            var points = map.GetAllMapPoints().ToList();
            if (points.Count == 0) return;

            foreach (var point in points)
                _pointsByCoord[point.coord] = point;

            int minCol = points.Min(p => p.coord.col);
            int maxCol = points.Max(p => p.coord.col);
            int minRow = points.Min(p => p.coord.row);
            int maxRow = points.Max(p => p.coord.row);

            CustomMinimumSize = new Vector2(
                Pad * 2 + (maxCol - minCol + 1) * CellW,
                Pad * 2 + (maxRow - minRow + 1) * CellH);
            Size = CustomMinimumSize;

            foreach (var point in points) {
                foreach (var child in point.Children) {
                    if (_pointsByCoord.ContainsKey(child.coord))
                        _edges.Add((point, child));
                }
            }

            var lines = new MapEdgeLayer(_edges, minCol, maxRow, Pad, CellW, CellH);
            AddChild(lines);
            MoveChild(lines, 0);

            foreach (var point in points)
                AddChild(CreateNodeButton(point, minCol, maxRow));

            QueueRedraw();
        }

        private Control CreateNodeButton(MapPoint point, int minCol, int maxRow) {
            bool isCombat = MapEncounterPreview.IsCombatNode(point.PointType);
            bool isCurrent = _currentCoord.HasValue && point.coord.Equals(_currentCoord.Value);
            bool isSelected = _selectedPoint != null && point.coord.Equals(_selectedPoint.coord);
            bool hasFloorOverride = KitLibState.FloorOverrides.ContainsKey(point.coord.row + 1);

            var btn = new Button {
                FocusMode = FocusModeEnum.None,
                MouseFilter = MouseFilterEnum.Stop,
                MouseDefaultCursorShape = isCombat ? CursorShape.PointingHand : CursorShape.Arrow,
                Text = NodeGlyph(point.PointType),
                Position = NodePos(point.coord, minCol, maxRow) - new Vector2(NodeR, NodeR),
                Size = new Vector2(NodeR * 2, NodeR * 2),
                Disabled = !isCombat,
            };
            btn.AddThemeFontSizeOverride("font_size", 10);

            if (isCombat) {
                btn.TooltipText = "";
                btn.MouseEntered += () => _onHoverShow(btn, point);
                btn.MouseExited += _onHoverHide;
            }
            else {
                btn.TooltipText = BuildNodeTooltip(point);
            }

            var fill = NodeFillColor(point.PointType);
            if (isSelected)
                fill = fill.Lightened(0.18f);

            var border = isSelected
                ? KitLibTheme.Accent
                : isCurrent
                    ? new Color(0.95f, 0.85f, 0.35f)
                    : hasFloorOverride && isCombat
                        ? new Color(KitLibTheme.Accent.R, KitLibTheme.Accent.G, KitLibTheme.Accent.B, 0.85f)
                        : KitLibTheme.PanelBorder;

            int bw = isSelected || isCurrent ? 2 : 1;
            btn.AddThemeStyleboxOverride("normal", MakeNodeStyle(fill, border, bw));
            btn.AddThemeStyleboxOverride("hover", MakeNodeStyle(fill.Lightened(0.12f), border.Lightened(0.1f), bw));
            btn.AddThemeStyleboxOverride("pressed", MakeNodeStyle(fill.Darkened(0.08f), border, bw));
            btn.AddThemeStyleboxOverride("disabled", MakeNodeStyle(fill.Darkened(0.15f), border, bw));
            btn.AddThemeStyleboxOverride("focus", MakeNodeStyle(fill, border, bw));
            btn.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
            btn.AddThemeColorOverride("font_disabled_color", KitLibTheme.Subtle);

            if (isCombat)
                btn.Pressed += () => _onSelect(point);

            return btn;
        }

        private static StyleBoxFlat MakeNodeStyle(Color bg, Color border, int borderWidth) => new() {
            BgColor = bg,
            BorderColor = border,
            BorderWidthLeft = borderWidth,
            BorderWidthRight = borderWidth,
            BorderWidthTop = borderWidth,
            BorderWidthBottom = borderWidth,
            CornerRadiusTopLeft = 999,
            CornerRadiusTopRight = 999,
            CornerRadiusBottomLeft = 999,
            CornerRadiusBottomRight = 999,
            ContentMarginLeft = 0,
            ContentMarginRight = 0,
            ContentMarginTop = 0,
            ContentMarginBottom = 0,
        };

        private static Vector2 NodePos(MapCoord coord, int minCol, int maxRow) =>
            new(
                Pad + (coord.col - minCol) * CellW + CellW * 0.5f,
                Pad + (maxRow - coord.row) * CellH + CellH * 0.5f);

        private string BuildNodeTooltip(MapPoint point) {
            int floor = point.coord.row + 1;
            if (!MapEncounterPreview.IsCombatNode(point.PointType))
                return I18N.T("enemy.mapNodeFloor", "Floor {0} · {1}", floor, point.PointType);

            var preview = MapEncounterPreview.Build(_runState, point);
            if (preview?.Encounter == null)
                return I18N.T("enemy.mapNodeFloor", "Floor {0} · {1}", floor, point.PointType);

            string name = preview.Encounter.Title?.GetFormattedText()
                ?? ((AbstractModel)preview.Encounter).Id.Entry;
            if (preview.IsOverride)
                name = I18N.T("map.override", "{0} (Override)", name);
            return I18N.T("enemy.mapNodeTooltip", "Floor {0}: {1}", floor, name);
        }

        private static string NodeGlyph(MapPointType type) => type switch {
            MapPointType.Monster => "M",
            MapPointType.Elite => "E",
            MapPointType.Boss => "B",
            MapPointType.Shop => "$",
            MapPointType.RestSite => "R",
            MapPointType.Treasure => "T",
            _ => "·",
        };

        private sealed partial class MapEdgeLayer : Control {
            private readonly List<(MapPoint from, MapPoint to)> _edges;
            private readonly int _minCol;
            private readonly int _maxRow;
            private readonly float _pad;
            private readonly float _cellW;
            private readonly float _cellH;

            public MapEdgeLayer(
                List<(MapPoint from, MapPoint to)> edges,
                int minCol,
                int maxRow,
                float pad,
                float cellW,
                float cellH) {
                _edges = edges;
                _minCol = minCol;
                _maxRow = maxRow;
                _pad = pad;
                _cellW = cellW;
                _cellH = cellH;
                MouseFilter = MouseFilterEnum.Ignore;
                SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            }

            public override void _Draw() {
                var lineColor = new Color(0.45f, 0.48f, 0.58f, 0.55f);
                foreach (var (from, to) in _edges) {
                    Vector2 a = NodePos(from.coord, _minCol, _maxRow);
                    Vector2 b = NodePos(to.coord, _minCol, _maxRow);
                    DrawLine(a, b, lineColor, 2f, true);
                }
            }
        }
    }
}
