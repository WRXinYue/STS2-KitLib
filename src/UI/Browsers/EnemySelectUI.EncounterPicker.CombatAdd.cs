using System;
using System.Linq;
using KitLib.Actions;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal static void BuildCombatAddPicker(
        VBoxContainer vbox,
        RoomType? filter,
        Action<EncounterModel> onEncounterSelected,
        Action<MonsterModel> onMonsterSelected,
        Action<RoomType?> onFilterChanged) {
        var layout = CombatAddLayout;

        BuildCombatAddTitle(vbox);

        var contentTabBar = new HBoxContainer();
        contentTabBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(contentTabBar);

        var roomFilterBar = new HBoxContainer();
        roomFilterBar.AddThemeConstantOverride("separation", 4);
        vbox.AddChild(roomFilterBar);

        var filterOptions = new EncounterPickerOptions {
            Purpose = EncounterPickerPurpose.CombatAdd,
            OnFilterChanged = onFilterChanged,
        };
        BuildRoomFilterBar(roomFilterBar, filter, filterOptions);

        var (searchRow, searchBox) = DevPanelUI.CreateSearchRow(
            I18N.T(layout.SearchPlaceholderKey, layout.SearchPlaceholderDefault));
        vbox.AddChild(searchRow);

        var preview = BuildTopBandPreview(bandHeight: 72f);
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
            EnemyActions.GetAllMonsters().ToList(),
            roomFilterBar);

        controller.BindContentTabs(contentTabBar);
        controller.BindSearch();
        controller.Rebuild();
    }
}
