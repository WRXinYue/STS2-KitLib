using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    private const string ExtensionWidthKey = "KitLibEnemySelect_ext";
    private const string DualMetaKey = "dm_dual_enemy_select";
    private const string CarrierNodeName = "EnemySelectDualCarrier";
    private const float DefaultMainWidth = 800f;
    private const float DefaultExtWidth = 480f;

    private static DevPanelUI.DualColumnOverlayHandle? _mainDual;
    private static VBoxContainer? _extensionHost;
    private static NGlobalUi? _mainGlobalUi;

    internal static bool IsMainPanelOpen => _mainDual != null;

    internal static void ClearExtensionHost() {
        if (_extensionHost == null)
            return;
        foreach (var child in _extensionHost.GetChildren())
            ((Node)child).QueueFree();
    }

    internal static void CloseExtensionPicker() {
        if (_mainDual == null || !GodotObject.IsInstanceValid(_mainDual.Root))
            return;

        var globalUi = _mainGlobalUi;
        var dual = _mainDual;
        _mainDual.CloseExtension(() => {
            ClearExtensionHost();
            if (globalUi != null && GodotObject.IsInstanceValid(dual.Root))
                DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
        });
    }

    internal static void ShowEncounterInExtension(
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onSelected,
        EncounterPickerOptions options) {
        if (_mainDual == null || _extensionHost == null) {
            ShowEncounterPickerModal(globalUi, filter, onSelected, options);
            return;
        }

        Callable.From(() => {
            bool alreadyOpen = _mainDual.ExtSlot.Visible;
            _mainDual.KillExtCloseTween();
            ClearExtensionHost();

            var builderOptions = new EncounterPickerOptions {
                CloseOnSelect = options.CloseOnSelect,
                ShowTitle = options.ShowTitle,
                PickerTitle = options.PickerTitle,
                Purpose = options.Purpose,
                OnMonsterSelected = options.OnMonsterSelected,
                OnFilterChanged = options.OnFilterChanged
                    ?? (nextFilter => ShowEncounterInExtension(
                        globalUi,
                        nextFilter,
                        onSelected,
                        options)),
            };

            BuildUnifiedEncounterPicker(
                _extensionHost,
                filter,
                enc => {
                    onSelected(enc);
                    if (options.CloseOnSelect)
                        CloseExtensionPicker();
                },
                mon => options.OnMonsterSelected?.Invoke(mon),
                builderOptions);

            if (!alreadyOpen) {
                _mainDual.PrepareExtensionVisible();
                _mainDual.AnimateExtensionSlideIn();
            }
            else {
                _mainDual.PrepareExtensionVisible();
            }

            GrabEncounterSearchFocus(_extensionHost);
            DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
        }).CallDeferred();
    }

    internal static void ShowCombatAddInExtension(
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onEncounterSelected,
        Action<MonsterModel> onMonsterSelected) {
        if (_mainDual == null || _extensionHost == null) {
            ShowCombatAddPickerModal(globalUi, filter, onEncounterSelected, onMonsterSelected);
            return;
        }

        Callable.From(() => {
            bool alreadyOpen = _mainDual.ExtSlot.Visible;
            _mainDual.KillExtCloseTween();
            ClearExtensionHost();

            BuildCombatAddPicker(
                _extensionHost,
                filter,
                onEncounterSelected,
                onMonsterSelected,
                nextFilter => ShowCombatAddInExtension(
                    globalUi,
                    nextFilter,
                    onEncounterSelected,
                    onMonsterSelected));

            if (!alreadyOpen) {
                _mainDual.PrepareExtensionVisible();
                _mainDual.AnimateExtensionSlideIn();
            }
            else {
                _mainDual.PrepareExtensionVisible();
            }

            GrabEncounterSearchFocus(_extensionHost);
            DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
        }).CallDeferred();
    }
}
