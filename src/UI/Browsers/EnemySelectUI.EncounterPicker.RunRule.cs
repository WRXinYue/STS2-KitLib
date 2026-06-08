using System;
using System.Linq;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal static void BuildUnifiedEncounterPicker(
        VBoxContainer vbox,
        RoomType? filter,
        Action<EncounterModel> onEncounterSelected,
        Action<MonsterModel>? onMonsterSelected,
        EncounterPickerOptions options) {
        var layout = RunRuleLayout;

        if (layout.ShowNavTab)
            BuildPickerNavTab(vbox, ResolvePickerTitle(filter, options));

        var roomFilterBar = new HBoxContainer();
        roomFilterBar.AddThemeConstantOverride("separation", 5);
        BuildRoomFilterBar(roomFilterBar, filter, options);
        vbox.AddChild(roomFilterBar);

        var (searchRow, searchBox) = DevPanelUI.CreateSearchRow(
            I18N.T(layout.SearchPlaceholderKey, layout.SearchPlaceholderDefault));
        vbox.AddChild(searchRow);

        var preview = BuildPickerPreview(layout.PreviewLayout);
        vbox.AddChild(preview.Root);

        var (listRegion, _, list, statusLabel) = CreatePickerListSection(layout.ListSeparation);
        vbox.AddChild(listRegion);
        vbox.AddChild(statusLabel);
        BindPickerListRegionLayout(vbox, listRegion);

        var controller = new EncounterPickerListController(
            filter,
            layout.CellStyle,
            preview,
            list,
            statusLabel,
            searchBox,
            onEncounterSelected,
            onMonsterSelected,
            EnemyActions.GetAllEncounters(filter).ToList(),
            monsters: null,
            roomFilterBar: null);

        controller.BindSearch();
        controller.Rebuild();
    }
}
