using System;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    internal enum EncounterPickerPurpose {
        RunRule,
        CombatAdd,
    }

    private enum PickerContentTab {
        Encounters,
        Monsters,
    }

    private enum PickerCellStyle {
        Full,
        Compact,
    }

    private enum PickerPreviewLayout {
        TopBand,
    }

    internal sealed class EncounterPickerOptions {
        public bool CloseOnSelect { get; init; } = true;
        public bool ShowTitle { get; init; } = true;
        public string? PickerTitle { get; init; }
        public Action<RoomType?>? OnFilterChanged { get; init; }
        public EncounterPickerPurpose Purpose { get; init; } = EncounterPickerPurpose.RunRule;
        public Action<MonsterModel>? OnMonsterSelected { get; init; }
    }

    private sealed record PickerLayoutConfig(
        PickerPreviewLayout PreviewLayout,
        PickerCellStyle CellStyle,
        bool ShowContentTabs,
        bool ShowNavTab,
        string SearchPlaceholderKey,
        string SearchPlaceholderDefault,
        int ListSeparation,
        int VboxSeparation);

    private const float ModalDefaultWidth = 840f;
    private const float ModalDefaultHeight = 560f;
    private const float CombatAddPanelWidth = 392f;
    private const float CombatAddPanelHeight = 500f;
    private const float CombatAddCellHeight = 34f;

    private static readonly PickerLayoutConfig RunRuleLayout = new(
        PickerPreviewLayout.TopBand,
        PickerCellStyle.Full,
        ShowContentTabs: false,
        ShowNavTab: true,
        "enemy.searchPlaceholder",
        "Search encounters...",
        ListSeparation: 6,
        VboxSeparation: 8);

    private static readonly PickerLayoutConfig CombatAddLayout = new(
        PickerPreviewLayout.TopBand,
        PickerCellStyle.Compact,
        ShowContentTabs: true,
        ShowNavTab: false,
        "enemy.searchCombatPlaceholder",
        "Search encounters or monsters...",
        ListSeparation: 4,
        VboxSeparation: 6);
}
