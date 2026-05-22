using System;
using System.Collections.Generic;
using DevMode.Actions;
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

    internal static void ShowMonsterInExtension(NGlobalUi globalUi, Action<MonsterModel> onSelected) {
        if (_mainDual == null || _extensionHost == null) {
            ShowMonsterSpawnOverlay(globalUi, onSelected);
            return;
        }

        Callable.From(() => {
            bool alreadyOpen = _mainDual.ExtSlot.Visible;
            _mainDual.KillExtCloseTween();
            ClearExtensionHost();

            BuildMonsterPicker(
                _extensionHost,
                onSelected,
                closeOnSelect: true,
                onBack: CloseExtensionPicker);

            if (!alreadyOpen) {
                _mainDual.PrepareExtensionVisible();
                _mainDual.AnimateExtensionSlideIn();
            }
            else {
                _mainDual.PrepareExtensionVisible();
            }

            DevPanelUI.NotifyBrowserContextLayoutChanged(globalUi);
        }).CallDeferred();
    }

    private static void BuildMonsterPicker(
        VBoxContainer vbox,
        Action<MonsterModel> onSelected,
        bool closeOnSelect,
        Action? onBack) {
        var monsters = EnemyActions.GetAllMonsters();
        if (monsters.Count == 0) {
            vbox.AddChild(new Label {
                Text = I18N.T("enemy.emptyMonsters", "No monsters found."),
                HorizontalAlignment = HorizontalAlignment.Center,
            });
            return;
        }

        if (onBack != null) {
            var backBtn = new Button {
                Text = I18N.T("enemy.pickerBack", "Back"),
                CustomMinimumSize = new Vector2(0, 32),
                FocusMode = Control.FocusModeEnum.None,
            };
            backBtn.Pressed += onBack;
            vbox.AddChild(backBtn);
        }

        vbox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("enemy.pickMonster", "Add monster to combat")));
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        var (searchRowCtrl, searchBox) = DevPanelUI.CreateSearchRow(
            I18N.T("enemy.searchMonsterPlaceholder", "Search monsters..."));
        vbox.AddChild(searchRowCtrl);

        var listBox = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        listBox.AddThemeConstantOverride("separation", 2);
        vbox.AddChild(listBox);

        var rows = new List<(Control cell, string searchKey)>();
        foreach (var monster in monsters) {
            var id = ((AbstractModel)monster).Id.Entry;
            var title = monster.Title?.GetFormattedText();
            var displayName = !string.IsNullOrEmpty(title) ? title : id;

            var btn = DevPanelUI.CreateListItemButton(displayName);
            var captured = monster;
            btn.Pressed += () => {
                onSelected(captured);
                if (closeOnSelect)
                    CloseExtensionPicker();
            };
            listBox.AddChild(btn);
            rows.Add((btn, $"{displayName} {id}".ToLowerInvariant()));
        }

        searchBox.TextChanged += text => {
            var query = text.Trim().ToLowerInvariant();
            foreach (var (cell, key) in rows)
                cell.Visible = string.IsNullOrEmpty(query) || key.Contains(query);
        };

        searchBox.GrabFocus();
    }
}
