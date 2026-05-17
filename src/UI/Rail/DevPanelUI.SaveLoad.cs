using System;
using DevMode;
using DevMode.Icons;
using DevMode.Panels;
using DevMode.Presets;
using DevMode.Settings;
using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Runs;

namespace DevMode.UI;

internal static partial class DevPanelUI {
    internal static void ShowSaveLoadOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        const float defaultExtWidth = 600f;
        const float extSlideOutSec = 0.28f;

        var root = CreateAndSetupRoot(globalUi, SaveLoadRootName, 1250);
        root.SetMeta("dm_dual_save_load", true);

        void FallbackClose() => ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();
        root.AddChild(CreateBrowserBackdrop(() => RequestCloseBrowserOverlay(globalUi, SaveLoadRootName, FallbackClose)));

        float mainW = ResolveBrowserPanelWidth(SaveLoadRootName, 520f, (Node)globalUi);
        float extW = ResolveBrowserPanelWidth(SaveLoadExtensionWidthKey, defaultExtWidth, (Node)globalUi);

        var clipHost = CreateBrowserPanelClipHost();

        var mover = new Control {
            Name = "SaveLoadDualCarrier",
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipContents = true,
        };
        mover.AnchorLeft = 0;
        mover.AnchorRight = 0;
        // Same vertical band as `Rail` and `CreateBrowserPanel` (0.15–0.85 of clip host).
        // Full-height mover + FullRect inner panels made columns taller than the rail.
        mover.AnchorTop = 0.15f;
        mover.AnchorBottom = 0.85f;
        mover.OffsetTop = 0;
        mover.OffsetBottom = 0;

        var row = new HBoxContainer {
            Name = "SaveLoadDualRow",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };
        row.AddThemeConstantOverride("separation", 0);
        // Parent `mover` is a plain Control; without full-rect anchors the row only uses minimum
        // height and sits at the top — middle column looks detached / "floating" upward.
        row.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var mainSlot = new Control {
            CustomMinimumSize = new Vector2(mainW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var mainPanel = CreateBrowserPanelInner(mainW, joinFlushOnRight: true);
        mainPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mainSlot.AddChild(mainPanel);

        var mainVbox = mainPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException("Save/Load main panel missing Content");

        AddBrowserNavTab(mainVbox, I18N.T("panel.section.save", "Save / Load"));

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

        mainVbox.AddChild(menuHost);

        var extSlot = new Control {
            CustomMinimumSize = new Vector2(extW, 0),
            SizeFlagsHorizontal = Control.SizeFlags.ShrinkBegin,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            Visible = false,
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        var extPanel = CreateBrowserPanelInner(extW);
        extPanel.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extSlot.AddChild(extPanel);

        var extVbox = extPanel.GetNodeOrNull<VBoxContainer>("Content")
            ?? throw new InvalidOperationException("Save/Load extension panel missing Content");

        var slotHost = new Control {
            Name = "SaveLoadSlotHost",
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        slotHost.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        extVbox.AddChild(slotHost);

        row.AddChild(mainSlot);
        row.AddChild(extSlot);
        mover.AddChild(row);
        clipHost.AddChild(mover);
        root.AddChild(clipHost);

        Tween? extCloseTween = null;

        void KillExtCloseTween() {
            extCloseTween?.Kill();
            extCloseTween = null;
        }

        void SyncMoverWidth() {
            float totalW = mainSlot.CustomMinimumSize.X + (extSlot.Visible ? extSlot.CustomMinimumSize.X : 0f);
            mover.OffsetLeft = 0;
            mover.OffsetRight = Mathf.Max(1f, totalW);
        }

        void CloseExtensionPanel() {
            if (!extSlot.Visible) return;
            KillExtCloseTween();
            float w = Mathf.Max(1f, extPanel.GetRect().Size.X);
            extCloseTween = extPanel.CreateTween();
            extCloseTween.SetTrans(Tween.TransitionType.Cubic);
            extCloseTween.SetEase(Tween.EaseType.In);
            extCloseTween.TweenProperty(extPanel, "position:x", w, extSlideOutSec);
            extCloseTween.TweenCallback(Callable.From(() => {
                extCloseTween = null;
                SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                extPanel.Position = Vector2.Zero;
                extSlot.Visible = false;
                SyncMoverWidth();
            }));
        }

        void OpenPicker(bool saveMode) {
            Callable.From(() => {
                KillExtCloseTween();
                if (extSlot.Visible)
                    SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                extPanel.Position = Vector2.Zero;
                extSlot.Visible = true;
                SyncMoverWidth();
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
                            KillExtCloseTween();
                            SaveSlotUI.TearDownEmbeddedInDevPanel(slotHost);
                            extPanel.Position = Vector2.Zero;
                            extSlot.Visible = false;
                            SyncMoverWidth();
                            RequestCloseBrowserOverlay(globalUi, SaveLoadRootName, FallbackClose);
                        });
                Callable.From(() => PlayBrowserPanelOpenFromLeft(extPanel)).CallDeferred();
            }).CallDeferred();
        }

        saveBtn.Pressed += () => OpenPicker(saveMode: true);
        loadBtn.Pressed += () => OpenPicker(saveMode: false);

        clipHost.Resized += () => SyncMoverWidth();

        bool opened = false;
        clipHost.TreeEntered += () => {
            if (opened) return;
            opened = true;
            Callable.From(() => {
                SyncMoverWidth();
                PlaySubPanelSlideOpenFromLeft(mover);
            }).CallDeferred();
        };

        ((Node)globalUi).AddChild(root);
    }

    internal static void ShowRestartSeedOverlay(NGlobalUi globalUi, DevPanelActions actions) {
        ((Node)globalUi).GetNodeOrNull<Control>(RestartSeedRootName)?.QueueFree();
        ((Node)globalUi).GetNodeOrNull<Control>(SaveLoadRootName)?.QueueFree();

        var (root, _, vbox) = CreateOverlayRoot(globalUi, RestartSeedRootName, 520f);

        AddBrowserNavTab(vbox, I18N.T("restart.title", "Restart with Seed"));

        var inner = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        inner.AddThemeConstantOverride("separation", 12);

        // ── Seed input ──
        var seedSection = new VBoxContainer();
        seedSection.AddThemeConstantOverride("separation", 4);

        var seedLbl = new Label { Text = I18N.T("restart.seed.label", "Seed (leave empty for random):") };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
        seedSection.AddChild(seedLbl);

        var seedInput = new LineEdit {
            PlaceholderText = I18N.T("restart.seed.placeholder", "e.g. DEADBEEF"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        seedSection.AddChild(seedInput);
        inner.AddChild(seedSection);

        // ── Divider ──
        inner.AddChild(new ColorRect {
            Color = DevModeTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill
        });

        // ── Carry-over scope ──
        var carryLbl = new Label { Text = I18N.T("restart.carry.label", "Carry over from current run:") };
        carryLbl.AddThemeFontSizeOverride("font_size", 12);
        carryLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
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
            noRunLbl.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
            inner.AddChild(noRunLbl);
        }

        inner.AddChild(new Control { SizeFlagsVertical = Control.SizeFlags.ExpandFill });

        // ── Status label ──
        var statusLbl = new Label { HorizontalAlignment = HorizontalAlignment.Center };
        statusLbl.AddThemeFontSizeOverride("font_size", 11);
        statusLbl.AddThemeColorOverride("font_color", DevModeTheme.Subtle);
        inner.AddChild(statusLbl);

        // ── Action buttons ──
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

            // Capture carry-over state from current run
            var scope = PresetContents.None;
            if (cardsToggle.ButtonPressed) scope |= PresetContents.Cards;
            if (relicsToggle.ButtonPressed) scope |= PresetContents.Relics;

            if (scope != PresetContents.None && hasRun) {
                var preset = PresetManager.CaptureFromRun(scope);
                if (preset != null) {
                    DevModeState.PendingRestartPreset = preset;
                    DevModeState.PendingRestartScope = scope;
                    MainFile.Logger.Info($"[DevMode] RestartWithSeed: captured preset scope={scope}.");
                }
            }

            if (goldToggle.ButtonPressed && hasRun && RunContext.TryGetRunAndPlayer(out _, out var player)) {
                DevModeState.PendingRestartGold = player.Gold;
                MainFile.Logger.Info($"[DevMode] RestartWithSeed: captured gold={player.Gold}.");
            }

            // Store seed for SeedInjectPatch to inject into NGame.StartNewSingleplayerRun.
            // (NGame.DebugSeedOverride is unreliable — NCharacterSelectScreen clears it before the run.)
            if (!string.IsNullOrEmpty(seed)) {
                DevModeState.PendingRestartSeed = seed;
                MainFile.Logger.Info($"[DevMode] RestartWithSeed: seed override set to '{seed}'.");
            }

            // Signal MainMenuPatch to skip the Dev menu and go straight to character select
            DevModeState.AutoProceedToCharSelect = true;

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
