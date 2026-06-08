using KitLib.Icons;
using Godot;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class RoomSelectUI {
    internal readonly record struct RoomEntry(
        RoomType Type,
        string NameKey,
        string NameFallback,
        string DescKey,
        string DescFallback,
        Color Accent,
        MdiIcon Icon);

    internal readonly record struct MainPanelHandle(
        VBoxContainer ListHost,
        Label WarnLabel,
        Label StatusLabel);

    private static readonly RoomEntry[] Rooms =
    {
        new(RoomType.Shop,
            "room.type.shop",     "Shop",
            "room.desc.shop",     "Visit the merchant — buy and remove cards.",
            new Color(0.88f, 0.72f, 0.22f),
            MdiIcon.Star),

        new(RoomType.RestSite,
            "room.type.rest",     "Rest Site",
            "room.desc.rest",     "Rest or smith — recover HP or upgrade a card.",
            new Color(0.35f, 0.78f, 0.52f),
            MdiIcon.Heart),

        new(RoomType.Treasure,
            "room.type.treasure", "Treasure",
            "room.desc.treasure", "Open a chest — gain a relic.",
            new Color(0.90f, 0.65f, 0.20f),
            MdiIcon.TreasureChest),

        new(RoomType.Map,
            "room.type.map",      "Map",
            "room.desc.map",      "Return to the map screen.",
            new Color(0.42f, 0.68f, 0.92f),
            MdiIcon.Map),
    };

    private static MainPanelHandle BuildMainPanel(VBoxContainer mainVbox) {
        BuildNavTab(mainVbox, I18N.T("room.nav.title", "Room Teleport"));

        var warnLabel = new Label {
            Text = I18N.T("room.noRun", "No active run — start a run first."),
            HorizontalAlignment = HorizontalAlignment.Center,
            Visible = false,
        };
        warnLabel.AddThemeFontSizeOverride("font_size", 11);
        warnLabel.AddThemeColorOverride("font_color", new Color(0.88f, 0.55f, 0.35f));
        mainVbox.AddChild(warnLabel);

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };
        var list = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        mainVbox.AddChild(scroll);

        var statusLabel = new Label { Text = "", HorizontalAlignment = HorizontalAlignment.Center };
        statusLabel.AddThemeFontSizeOverride("font_size", 11);
        statusLabel.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        mainVbox.AddChild(statusLabel);

        foreach (var entry in Rooms)
            list.AddChild(BuildRoomCard(entry, warnLabel, statusLabel));

        return new MainPanelHandle(list, warnLabel, statusLabel);
    }
}
