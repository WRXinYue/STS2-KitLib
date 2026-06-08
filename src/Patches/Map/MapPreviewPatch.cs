using System;
using System.Linq;
using KitLib;
using KitLib.Actions;
using KitLib.UI;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches.Map;

/// <summary>
/// Adds encounter preview tooltip on map node hover.
/// </summary>
[HarmonyPatch(typeof(NMapPoint), "OnFocus")]
public static class MapPointHoverPatch {
    private const string TooltipName = "KitLibMapTooltip";

    public static void Postfix(NMapPoint __instance) {
        if (!KitLibState.InDevRun) return;

        var point = __instance.Point;
        if (point == null) return;

        if (!MapEncounterPreview.IsCombatNode(point.PointType))
            return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return;

        var preview = MapEncounterPreview.Build(state, point);
        if (preview?.Encounter == null) return;

        ShowTooltip(__instance, preview);
    }

    private static void ShowTooltip(NMapPoint mapPoint, MapEncounterPreview.Preview preview) {
        RemoveTooltip(mapPoint);

        var encounter = preview.Encounter!;
        var encounterName = encounter.Title?.GetFormattedText()
            ?? ((AbstractModel)encounter).Id.Entry;
        var encId = ((AbstractModel)encounter).Id.Entry;

        var typeTag = preview.CombatRoomType switch {
            RoomType.Monster => I18N.T("map.roomNormal", "Normal"),
            RoomType.Elite => I18N.T("map.roomElite", "Elite"),
            RoomType.Boss => I18N.T("map.roomBoss", "Boss"),
            _ => ""
        };

        var tagColor = preview.CombatRoomType switch {
            RoomType.Elite => new Color(1f, 0.8f, 0.27f),
            RoomType.Boss => new Color(1f, 0.27f, 0.27f),
            _ => new Color(0.53f, 0.8f, 0.53f)
        };

        var panel = new PanelContainer {
            Name = TooltipName,
            ZIndex = 1500,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        var style = new StyleBoxFlat {
            BgColor = new Color(0.06f, 0.06f, 0.1f, 0.95f),
            ContentMarginLeft = 10,
            ContentMarginRight = 10,
            ContentMarginTop = 8,
            ContentMarginBottom = 8,
            CornerRadiusTopLeft = 6,
            CornerRadiusTopRight = 6,
            CornerRadiusBottomLeft = 6,
            CornerRadiusBottomRight = 6,
            BorderWidthTop = 1,
            BorderWidthBottom = 1,
            BorderWidthLeft = 1,
            BorderWidthRight = 1,
            BorderColor = new Color(0.4f, 0.4f, 0.55f, 0.7f)
        };
        panel.AddThemeStyleboxOverride("panel", style);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 4);
        vbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        var headerLabel = new Label {
            Text = I18N.T("map.tooltipHeader", "[{0}] Floor {1}", typeTag, preview.Floor),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        headerLabel.AddThemeColorOverride("font_color", tagColor);
        headerLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(headerLabel);

        var nameLabel = new Label {
            Text = preview.IsOverride ? I18N.T("map.override", "{0} (Override)", encounterName) : encounterName,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        nameLabel.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.85f));
        vbox.AddChild(nameLabel);

        if (encId != encounterName) {
            var idLabel = new Label {
                Text = encId,
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            idLabel.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
            idLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(idLabel);
        }

        var monsters = encounter.AllPossibleMonsters?.ToList();
        if (monsters != null && monsters.Count > 0) {
            var visualsContainer = new SubViewportContainer {
                CustomMinimumSize = new Vector2(Math.Min(monsters.Count * 80, 240), 100),
                StretchShrink = 1,
                Stretch = true,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            var subViewport = new SubViewport {
                Size = new Vector2I(Math.Min(monsters.Count * 80, 240), 100),
                TransparentBg = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always
            };
            visualsContainer.AddChild(subViewport);

            float spacing = Math.Min(80f, 240f / monsters.Count);
            for (int i = 0; i < Math.Min(monsters.Count, 3); i++) {
                var visuals = EnemySelectUI.TryCreateVisualsPublic(monsters[i]);
                if (visuals != null) {
                    float scale = 0.3f;
                    visuals.Scale = new Vector2(scale, scale);
                    visuals.Position = new Vector2(spacing * i + spacing / 2, 80);
                    subViewport.AddChild(visuals);
                }
            }

            vbox.AddChild(visualsContainer);
        }

        if (monsters != null && monsters.Count > 0) {
            var monsterNames = monsters
                .Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry)
                .Distinct();
            var monstersLabel = new Label {
                Text = string.Join(", ", monsterNames),
                HorizontalAlignment = HorizontalAlignment.Center,
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
                CustomMinimumSize = new Vector2(160, 0),
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            monstersLabel.AddThemeColorOverride("font_color", new Color(0.7f, 0.75f, 0.85f));
            monstersLabel.AddThemeFontSizeOverride("font_size", 11);
            vbox.AddChild(monstersLabel);
        }

        if (!preview.IsCurrentRoom) {
            var hintLabel = new Label {
                Text = I18N.T("map.editHint", "Edit in Dev panel"),
                HorizontalAlignment = HorizontalAlignment.Center,
                MouseFilter = Control.MouseFilterEnum.Ignore
            };
            hintLabel.AddThemeColorOverride("font_color", new Color(0.45f, 0.45f, 0.55f));
            hintLabel.AddThemeFontSizeOverride("font_size", 10);
            vbox.AddChild(hintLabel);
        }

        panel.AddChild(vbox);

        mapPoint.AddChild(panel);
        panel.Position = new Vector2(-120, -panel.Size.Y - 20);

        Callable.From(() => {
            if (GodotObject.IsInstanceValid(panel))
                panel.Position = new Vector2(-120, -panel.Size.Y - 20);
        }).CallDeferred();
    }

    private static void RemoveTooltip(NMapPoint mapPoint) {
        mapPoint.GetNodeOrNull<Control>(TooltipName)?.QueueFree();
    }
}

[HarmonyPatch(typeof(NMapPoint), "OnUnfocus")]
public static class MapPointUnhoverPatch {
    public static void Postfix(NMapPoint __instance) {
        if (!KitLibState.InDevRun) return;
        __instance.GetNodeOrNull<Control>("KitLibMapTooltip")?.QueueFree();
    }
}
