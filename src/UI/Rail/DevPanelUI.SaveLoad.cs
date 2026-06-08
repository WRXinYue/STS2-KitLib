using System;
using KitLib;
using KitLib.Cheat;
using KitLib.Icons;
using KitLib.Panels;
using KitLib.Presets;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.UI;

internal static partial class DevPanelUI {
    internal static void ShowSaveLoadOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        const float defaultExtWidth = 600f;

        void FallbackClose() => ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        var dual = CreateDualColumnOverlay(new DualColumnOverlayOptions {
            GlobalUi = globalUi,
            RootName = SaveLoadRootName,
            DualMetaKey = "dm_dual_save_load",
            CarrierNodeName = "SaveLoadDualCarrier",
            MainWidthKey = SaveLoadRootName,
            ExtWidthKey = SaveLoadExtensionWidthKey,
            MainDefaultWidth = 520f,
            ExtDefaultWidth = defaultExtWidth,
            FallbackClose = FallbackClose,
        });

        AddBrowserNavTab(dual.MainContent, I18N.T("panel.section.save", "Save / Load"));

        var menuHost = new VBoxContainer {
            Name = SaveLoadMenuHostName,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        menuHost.AddThemeConstantOverride("separation", 6);

        var newTestBtn = CreateListItemButton(I18N.T("panel.newTest", "New Test"));
        newTestBtn.Icon = MdiIcon.Plus.Texture(16);
        newTestBtn.Alignment = HorizontalAlignment.Left;
        newTestBtn.Pressed += () => { ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree(); actions.OnNewTest(); };
        menuHost.AddChild(newTestBtn);

        var restartSeedBtn = CreateListItemButton(I18N.T("panel.restartWithSeed", "Restart with Seed"));
        restartSeedBtn.Icon = MdiIcon.Refresh.Texture(16);
        restartSeedBtn.Alignment = HorizontalAlignment.Left;
        restartSeedBtn.Pressed += () => ShowRestartSeedOverlay(globalUi, actions);
        menuHost.AddChild(restartSeedBtn);

        var saveBtn = CreateListItemButton(I18N.T("panel.save", "Save"));
        saveBtn.Icon = MdiIcon.ContentSave.Texture(16);
        saveBtn.Alignment = HorizontalAlignment.Left;
        menuHost.AddChild(saveBtn);

        var loadBtn = CreateListItemButton(I18N.T("panel.load", "Load"));
        loadBtn.Icon = MdiIcon.FolderOpen.Texture(16);
        loadBtn.Alignment = HorizontalAlignment.Left;
        menuHost.AddChild(loadBtn);

        dual.MainContent.AddChild(menuHost);

        var slotHost = new Control {
            Name = "SaveLoadSlotHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        slotHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        dual.ExtContent.AddChild(slotHost);

        void CloseExtensionPanel() {
            dual.CloseExtension(() => SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost));
        }

        void OpenPicker(bool saveMode) {
            Callable.From(() => {
                dual.KillExtCloseTween();
                if (dual.ExtSlot.Visible)
                    SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                dual.PrepareExtensionVisible();
                SaveSlotUI.Show(
                    slotHost,
                    saveMode: saveMode,
                    onConfirm: slot => {
                        if (saveMode)
                            SaveSlotManager.SaveToSlot(slot);
                        else
                            SaveSlotManager.LoadFromSlot(slot);
                    },
                    host: SaveSlotUiHost.EmbeddedInDevPanel,
                    onEmbeddedCancel: CloseExtensionPanel,
                    onEmbeddedAfterLoadClose: saveMode
                        ? null
                        : () => {
                            dual.KillExtCloseTween();
                            SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                            dual.ExtPanel.Position = Vector2.Zero;
                            dual.ExtSlot.Visible = false;
                            dual.SyncMoverWidth();
                            RequestCloseBrowserOverlay(globalUi, SaveLoadRootName, FallbackClose);
                        });
                dual.AnimateExtensionSlideIn();
            }).CallDeferred();
        }

        saveBtn.Pressed += () => OpenPicker(saveMode: true);
        loadBtn.Pressed += () => OpenPicker(saveMode: false);

        dual.AttachToScene();
    }

    internal static void ShowRestartSeedOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        var (root, _, vbox) = CreateOverlayRoot(globalUi, RestartSeedRootName, 520f);

        AddBrowserNavTab(vbox, I18N.T("restart.title", "Restart with Seed"));

        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 12);

        var seedSection = new VBoxContainer();
        seedSection.AddThemeConstantOverride("separation", 4);

        var seedLbl = new Label { Text = I18N.T("restart.seed.label", "Seed (leave empty for random):") };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        seedSection.AddChild(seedLbl);

        var seedInput = new LineEdit {
            PlaceholderText = I18N.T("restart.seed.placeholder", "e.g. DEADBEEF"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        seedSection.AddChild(seedInput);
        inner.AddChild(seedSection);

        inner.AddChild(new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        var carryLbl = new Label { Text = I18N.T("restart.carry.label", "Carry over from current run:") };
        carryLbl.AddThemeFontSizeOverride("font_size", 12);
        carryLbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        inner.AddChild(carryLbl);

        bool hasRun = RunContext.TryGetRunAndPlayer(out _, out _);

        var cardsToggle = new CheckButton {
            Text = I18N.T("preset.scope.cards", "Cards"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        cardsToggle.AddThemeFontSizeOverride("font_size", 13);
        cardsToggle.AddThemeColorOverride("font_color", new Color(0.35f, 0.58f, 0.95f));
        inner.AddChild(cardsToggle);

        var relicsToggle = new CheckButton {
            Text = I18N.T("preset.scope.relics", "Relics"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        relicsToggle.AddThemeFontSizeOverride("font_size", 13);
        relicsToggle.AddThemeColorOverride("font_color", new Color(0.88f, 0.72f, 0.22f));
        inner.AddChild(relicsToggle);

        var goldToggle = new CheckButton {
            Text = I18N.T("restart.carry.gold", "Gold"),
            ButtonPressed = false,
            Disabled = !hasRun,
            FocusMode = Control.FocusModeEnum.None,
        };
        goldToggle.AddThemeFontSizeOverride("font_size", 13);
        goldToggle.AddThemeColorOverride("font_color", new Color(0.32f, 0.76f, 0.50f));
        inner.AddChild(goldToggle);

        if (!hasRun) {
            var noRunLbl = new Label { Text = I18N.T("restart.noRun", "(No active run — carry-over unavailable)") };
            noRunLbl.AddThemeFontSizeOverride("font_size", 11);
            noRunLbl.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
            inner.AddChild(noRunLbl);
        }

        inner.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        var statusLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        statusLbl.AddThemeFontSizeOverride("font_size", 11);
        statusLbl.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        inner.AddChild(statusLbl);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 8);

        var cancelBtn = CreateListItemButton(I18N.T("restart.cancel", "Cancel"));
        cancelBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        cancelBtn.Pressed += () => ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
        btnRow.AddChild(cancelBtn);

        var restartBtn = CreateListItemButton(I18N.T("restart.go", "Restart"));
        restartBtn.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        restartBtn.Icon = MdiIcon.Refresh.Texture(16);
        restartBtn.Alignment = HorizontalAlignment.Center;
        restartBtn.Pressed += () => {
            var seed = seedInput.Text?.Trim();

            var scope = PresetContents.None;
            if (cardsToggle.ButtonPressed) scope |= PresetContents.Cards;
            if (relicsToggle.ButtonPressed) scope |= PresetContents.Relics;

            if (scope != PresetContents.None && hasRun) {
                var preset = PresetManager.CaptureFromRun(scope);
                if (preset != null) {
                    CheatRestartState.PendingRestartPreset = preset;
                    CheatRestartState.PendingRestartScope = scope;
                    MainFile.Logger.Info($"[KitLib] RestartWithSeed: captured preset scope={scope}.");
                }
            }

            if (goldToggle.ButtonPressed && hasRun && RunContext.TryGetRunAndPlayer(out _, out var player)) {
                KitLibState.PendingRestartGold = player.Gold;
                MainFile.Logger.Info($"[KitLib] RestartWithSeed: captured gold={player.Gold}.");
            }

            if (!string.IsNullOrEmpty(seed)) {
                KitLibState.PendingRestartSeed = seed;
                MainFile.Logger.Info($"[KitLib] RestartWithSeed: seed override set to '{seed}'.");
            }

            KitLibState.AutoProceedToCharSelect = true;

            ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
            actions.OnNewTest();
        };
        btnRow.AddChild(restartBtn);

        inner.AddChild(btnRow);

        vbox.AddChild(inner);
        ((Node)globalUi).AddChild(root);
        seedInput.GrabFocus();
    }
}