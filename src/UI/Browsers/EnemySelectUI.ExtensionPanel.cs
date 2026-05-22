using System;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Rooms;

namespace DevMode.UI;

internal static partial class EnemySelectUI {
    private const string ExtensionWidthKey = "DevModeEnemySelect_ext";
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
        if (_mainDual == null)
            return;
        var globalUi = _mainGlobalUi;
        _mainDual.CloseExtension(() => {
            ClearExtensionHost();
            if (globalUi != null)
                DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
        });
    }

    internal static void ShowEncounterInExtension(
        NGlobalUi globalUi,
        RoomType? filter,
        Action<EncounterModel> onSelected,
        EncounterPickerOptions options) {
        if (_mainDual == null || _extensionHost == null) {
            ShowEncounterOverlay(globalUi, filter, onSelected);
            return;
        }

        Callable.From(() => {
            bool alreadyOpen = _mainDual.ExtSlot.Visible;
            _mainDual.KillExtCloseTween();
            ClearExtensionHost();

            BuildEncounterPicker(
                _extensionHost,
                globalUi,
                filter,
                enc => {
                    onSelected(enc);
                    if (options.CloseOnSelect)
                        CloseExtensionPicker();
                },
                new EncounterPickerOptions {
                    CloseOnSelect = options.CloseOnSelect,
                    ShowTitle = options.ShowTitle,
                    CompactEmbedded = true,
                    PickerTitle = options.PickerTitle,
                    OnBack = options.OnBack ?? CloseExtensionPicker,
                    OnFilterChanged = options.OnFilterChanged
                        ?? (nextFilter => ShowEncounterInExtension(
                            globalUi,
                            nextFilter,
                            onSelected,
                            options)),
                });

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
