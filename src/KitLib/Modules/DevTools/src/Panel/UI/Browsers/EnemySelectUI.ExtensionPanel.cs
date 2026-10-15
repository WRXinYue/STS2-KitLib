using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.UI;

internal static partial class EnemySelectUI {
    private const string DualMetaKey = "dm_dual_enemy_select";
    private const string CarrierNodeName = "EnemySelectDualCarrier";
    private const float DefaultMainWidth = 600f;
    private const float DefaultExtWidth = 440f;

    private static DevPanelUI.DualColumnOverlayHandle? _mainDual;
    private static ScrollContainer? _mapDetailScroll;
    private static VBoxContainer? _mapDetailHost;
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

        ClearExtensionHost();
        ShowMapDetailPanel();
    }

    private static void ShowMapDetailPanel() {
        if (_extensionHost != null)
            _extensionHost.Visible = false;
        if (_mapDetailScroll != null)
            _mapDetailScroll.Visible = true;
    }

    private static void ShowPickerPanel() {
        if (_mapDetailScroll != null)
            _mapDetailScroll.Visible = false;
        if (_extensionHost != null)
            _extensionHost.Visible = true;
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
            ShowPickerPanel();

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
            ShowPickerPanel();

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
        }).CallDeferred();
    }
}
