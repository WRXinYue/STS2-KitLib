using System.Linq;
using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Actions;
using KitLib.UI;
using MegaCrit.Sts2.Core.Map;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Monsters;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.Map;
using MegaCrit.Sts2.Core.Rooms;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Patches.Map;

/// <summary>
/// Map node hover preview when map prediction cheat is enabled.
/// </summary>
[HarmonyPatch(typeof(NMapPoint), "OnFocus")]
public static class MapPointHoverPatch {
    private const string TooltipName = "KitLibMapTooltip";

    public static void Postfix(NMapPoint __instance) {
        if (!KitLibState.CheatsInRun || !KitLibState.MapCheats.MapPredictionEnabled)
            return;

        if (__instance.State == MapPointState.Traveled)
            return;

        var point = __instance.Point;
        if (point == null || !MapEncounterPreview.IsPreviewableNode(point.PointType))
            return;

        var state = RunManager.Instance?.DebugOnlyGetState();
        if (state == null) return;

        var preview = MapEncounterPreview.Build(state, point);
        if (preview == null) return;

        ShowTooltip(__instance, preview);
    }

    private static void ShowTooltip(NMapPoint mapPoint, MapEncounterPreview.Preview preview) {
        RemoveTooltip(mapPoint);

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

        var (typeTag, tagColor) = GetRoomTag(preview.CombatRoomType);
        var headerLabel = new Label {
            Text = preview.IsApproximate
                ? I18N.T("map.tooltipHeaderApprox", "[{0}] Floor {1} ~", typeTag, preview.Floor)
                : I18N.T("map.tooltipHeader", "[{0}] Floor {1}", typeTag, preview.Floor),
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        headerLabel.AddThemeColorOverride("font_color", tagColor);
        headerLabel.AddThemeFontSizeOverride("font_size", 13);
        vbox.AddChild(headerLabel);

        if (preview.Event is { } eventModel) {
            string eventName = eventModel.Title?.GetFormattedText()
                ?? ((AbstractModel)eventModel).Id.Entry;
            vbox.AddChild(MakePrimaryLabel(eventName));
            string eventId = ((AbstractModel)eventModel).Id.Entry;
            if (eventId != eventName)
                vbox.AddChild(MakeSubtleLabel(eventId));
        }
        else if (preview.Encounter is { } encounter) {
            string encounterName = encounter.Title?.GetFormattedText()
                ?? ((AbstractModel)encounter).Id.Entry;
            var encId = ((AbstractModel)encounter).Id.Entry;

            vbox.AddChild(MakePrimaryLabel(
                preview.IsOverride ? I18N.T("map.override", "{0} (Override)", encounterName) : encounterName));

            if (encId != encounterName)
                vbox.AddChild(MakeSubtleLabel(encId));

            var monsters = encounter.AllPossibleMonsters?.ToList();
            if (monsters is { Count: > 0 }) {
                vbox.AddChild(BuildMonsterRow(monsters));
                vbox.AddChild(MakeSubtleLabel(string.Join(", ",
                    monsters.Select(m => m.Title?.GetFormattedText() ?? ((AbstractModel)m).Id.Entry).Distinct())));
            }
        }
        else if (preview.CombatRoomType == RoomType.Unassigned) {
            vbox.AddChild(MakeSubtleLabel(
                I18N.T("map.predictUnknown", "Unknown node — prediction unavailable (RNG); visit to reveal.")));
        }

        if (!preview.IsCurrentRoom && preview.IsCombatNode) {
            var hintLabel = MakeSubtleLabel(I18N.T("map.editHint", "Edit in Dev panel"));
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

    static (string tag, Color color) GetRoomTag(RoomType roomType) => roomType switch {
        RoomType.Monster => (I18N.T("map.roomNormal", "Normal"), new Color(0.53f, 0.8f, 0.53f)),
        RoomType.Elite => (I18N.T("map.roomElite", "Elite"), new Color(1f, 0.8f, 0.27f)),
        RoomType.Boss => (I18N.T("map.roomBoss", "Boss"), new Color(1f, 0.27f, 0.27f)),
        RoomType.Event => (I18N.T("map.roomEvent", "Event"), new Color(0.7f, 0.65f, 1f)),
        RoomType.Shop => (I18N.T("map.roomShop", "Shop"), new Color(0.9f, 0.75f, 0.4f)),
        RoomType.Treasure => (I18N.T("map.roomTreasure", "Treasure"), new Color(1f, 0.85f, 0.35f)),
        RoomType.RestSite => (I18N.T("map.roomRest", "Rest Site"), new Color(0.55f, 0.85f, 0.95f)),
        _ => ("?", Colors.White),
    };

    static Label MakePrimaryLabel(string text) {
        var label = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color(0.95f, 0.92f, 0.85f));
        return label;
    }

    static Label MakeSubtleLabel(string text) {
        var label = new Label {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            CustomMinimumSize = new Vector2(160, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore
        };
        label.AddThemeColorOverride("font_color", new Color(0.5f, 0.5f, 0.6f));
        label.AddThemeFontSizeOverride("font_size", 11);
        return label;
    }

    static Control BuildMonsterRow(System.Collections.Generic.List<MonsterModel> monsters) {
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

        return visualsContainer;
    }

    private static void RemoveTooltip(NMapPoint mapPoint) {
        mapPoint.GetNodeOrNull<Control>(TooltipName)?.QueueFree();
    }
}

[HarmonyPatch(typeof(NMapPoint), "OnUnfocus")]
public static class MapPointUnhoverPatch {
    public static void Postfix(NMapPoint __instance) {
        if (!KitLibState.CheatsInRun || !KitLibState.MapCheats.MapPredictionEnabled)
            return;
        __instance.GetNodeOrNull<Control>("KitLibMapTooltip")?.QueueFree();
    }
}
