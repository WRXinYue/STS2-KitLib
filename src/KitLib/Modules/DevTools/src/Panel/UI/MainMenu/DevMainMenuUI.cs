using System;
using System.IO;
using Godot;
using KitLib;
using KitLib.Actions;
using KitLib.Compat;
using KitLib.Feedback;
using KitLib.Replay;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.UI;

internal static class DevMainMenuUI {
    // ── Rail actions (invoked directly from the main-menu left rail) ──────

    internal static void StartNewTest(NMainMenu mainMenu) {
        DismissOverlays(mainMenu.GetTree()?.Root);
        KitLibState.InDevRun = true;
        var charSelect = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
        charSelect.InitializeSingleplayer();
        mainMenu.SubmenuStack.Push(charSelect);
    }

    internal static void StartNewTestWithSeed(NMainMenu mainMenu) =>
        ShowSeedInputOverlay(mainMenu, () => StartNewTest(mainMenu));

    internal static void StartAutoSlay(NMainMenu mainMenu) => ShowAutoSlaySeedOverlay(mainMenu);

    internal static void LoadSave(NMainMenu mainMenu) => SaveSlotUI.Show(
        mainMenu.GetTree().Root,
        saveMode: false,
        onConfirm: (slot, _) => {
            SaveSlotUI.Hide();
            DismissOverlays(mainMenu.GetTree()?.Root);
            SaveSlotManager.LoadFromSlot(slot);
        });

    internal static void LoadFeedbackZip(NMainMenu mainMenu) => OpenFeedbackZipPicker(mainMenu);

    internal static void PlayOfficialReplay(NMainMenu mainMenu) => OpenCombatReplayPicker(mainMenu);

    internal static void PlayDevToolsReplay(NMainMenu mainMenu) => OpenRunReplayPicker(mainMenu);

    internal static void StartPseudoCoop(NMainMenu mainMenu) =>
        DevMainMenuPseudoCoopUI.Show(mainMenu, () => DismissOverlays(mainMenu.GetTree()?.Root));

    internal static void UnlockAllProgress(NMainMenu mainMenu) => ShowUnlockAllConfirm(mainMenu);

    internal static void ShowLogs(NMainMenu mainMenu) => LogViewerUI.ShowOnMainMenu(mainMenu);

    internal static bool AutoSlayAvailable =>
        !AutoSlayRunner.IsBlockedByMultiplayer && !AutoSlayRunner.IsRunning;

    internal static bool HasAnySaveSlot => SaveSlotManager.GetAllSlotIds().Count > 0;

    /// <summary>Close every overlay this module can leave on top of the main menu.</summary>
    internal static void DismissOverlays(Node? attachRoot) {
        SaveSlotUI.Hide();
        LogViewerUI.HideAnywhere();
        ProgressGuardUI.HideAnywhere();
        ProgressLossPromptUI.HideAnywhere();
        DevMainMenuPseudoCoopUI.HideAnywhere();

        var root = attachRoot ?? (Engine.GetMainLoop() as SceneTree)?.Root;
        DevMainMenuOverlay.RemoveAnywhere(SeedOverlayName);
        DevMainMenuOverlay.RemoveAnywhere(UnlockAllOverlayName);
        DevMainMenuOverlay.RemoveAnywhere(FeedbackOverlayName);
        CombatReplayBarUI.Hide();
    }

    private const string SeedOverlayName = "KitLibSeedInput";
    private const string UnlockAllOverlayName = "KitLibUnlockAllConfirm";
    private const string FeedbackOverlayName = "KitLibFeedbackImport";

    private static void OpenFeedbackZipPicker(NMainMenu mainMenu) {
        var reports = FeedbackReportBuilder.ReportsDirectory;
        var dlg = new FileDialog {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = I18N.T("devmenu.loadFeedback", "Load Feedback ZIP"),
            CurrentDir = Directory.Exists(reports) ? reports : OS.GetUserDataDir()
        };
        dlg.AddFilter("*.zip", I18N.T("devmenu.loadFeedback.filter", "Feedback ZIP"));
        mainMenu.AddChild(dlg);
        dlg.FileSelected += path => {
            dlg.QueueFree();
            if (!FeedbackImport.TryImport(path, out var imported, out var error)) {
                ShowFeedbackImportMessage(mainMenu, error);
                return;
            }
            ShowFeedbackImportConfirm(mainMenu, imported);
        };
        dlg.Canceled += () => dlg.QueueFree();
        dlg.PopupCentered();
    }

    private static void ShowFeedbackImportConfirm(NMainMenu mainMenu, FeedbackImport.Result imported) {
        var body = I18N.T("devmenu.loadFeedback.confirmBody",
            "Enter this run from the ZIP. Your live save slot is not overwritten.\n\nRun save: {0}\nCombat replay (.mcr): {1}\nCombat checkpoint: {2}{3}",
            Path.GetFileName(imported.RunSavePath),
            imported.ReplayMcrPath != null
                ? I18N.T("devmenu.loadFeedback.yes", "Yes")
                : I18N.T("devmenu.loadFeedback.no", "No"),
            imported.HasCheckpoint
                ? I18N.T("devmenu.loadFeedback.yes", "Yes")
                : I18N.T("devmenu.loadFeedback.no", "No"),
            imported.CompatibilityNote);

        ShowFeedbackImportOverlay(
            mainMenu,
            I18N.T("devmenu.loadFeedback.confirmTitle", "Load Feedback Run?"),
            body,
            confirmText: I18N.T("devmenu.loadFeedback.enter", "Enter run"),
            onConfirm: () => {
                DismissOverlays(mainMenu.GetTree()?.Root);
                if (!SaveSlotManager.LoadFromFile(imported.RunSavePath)) {
                    MainFile.Logger.Warn("Feedback import: failed to load run save.");
                    ShowFeedbackImportMessage(mainMenu,
                        I18N.T("devmenu.loadFeedback.err.loadFailed", "Failed to load the run save."));
                }
            },
            extraText: imported.ReplayMcrPath == null
                ? null
                : I18N.T("devmenu.loadFeedback.playReplay", "Play official replay"),
            onExtra: imported.ReplayMcrPath == null
                ? null
                : () => StartCombatReplay(mainMenu, imported.ReplayMcrPath!));
    }

    private static void OpenCombatReplayPicker(NMainMenu mainMenu) {
        var dlg = new FileDialog {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = I18N.T("devmenu.replay", "Play Official Replay"),
            CurrentDir = DefaultReplayDirectory()
        };
        dlg.AddFilter("*.mcr", I18N.T("devmenu.replay.filter", "Official replay"));
        mainMenu.AddChild(dlg);
        dlg.FileSelected += path => {
            dlg.QueueFree();
            StartCombatReplay(mainMenu, path);
        };
        dlg.Canceled += () => dlg.QueueFree();
        dlg.PopupCentered();
    }

    private static void OpenRunReplayPicker(NMainMenu mainMenu) {
        var dlg = new FileDialog {
            FileMode = FileDialog.FileModeEnum.OpenFile,
            Access = FileDialog.AccessEnum.Filesystem,
            UseNativeDialog = true,
            Title = I18N.T("devmenu.runReplay", "Play DevTools Replay"),
            CurrentDir = CombatReplayPlayback.DefaultRunLogsDirectory(),
        };
        dlg.AddFilter("*" + CombatReplayPlayback.RunReplayExtension, I18N.T("devmenu.runReplay.filter", "DevTools replay"));
        dlg.AddFilter("*" + CombatReplayPlayback.LegacyRunReplayExtension, I18N.T("devmenu.runReplay.filterLegacy", "DevTools replay (legacy)"));
        mainMenu.AddChild(dlg);
        dlg.FileSelected += path => {
            dlg.QueueFree();
            StartRunReplay(mainMenu, path);
        };
        dlg.Canceled += () => dlg.QueueFree();
        dlg.PopupCentered();
    }

    private static string DefaultReplayDirectory() {
        try {
            var rel = SaveManager.Instance.GetProfileScopedPath("replays");
            var abs = ProjectSettings.GlobalizePath(rel);
            if (Directory.Exists(abs))
                return abs;
        }
        catch (Exception) {
        }
        return OS.GetUserDataDir();
    }

    private static void StartCombatReplay(NMainMenu mainMenu, string path) {
        var tree = mainMenu.GetTree();
        if (!CombatReplayPlayback.TryPlay(path, tree, out var error)) {
            ShowFeedbackImportOverlay(
                mainMenu,
                I18N.T("devmenu.replay", "Play Official Replay"),
                error,
                confirmText: null,
                onConfirm: null);
            return;
        }
        DismissOverlays(mainMenu.GetTree()?.Root);
        CombatReplayBarUI.Show(tree);
    }

    private static void StartRunReplay(NMainMenu mainMenu, string path) {
        var tree = mainMenu.GetTree();
        if (!CombatReplayPlayback.TryPlay(path, tree, out var error)) {
            ShowFeedbackImportOverlay(
                mainMenu,
                I18N.T("devmenu.runReplay", "Play DevTools Replay"),
                error,
                confirmText: null,
                onConfirm: null);
            return;
        }
        DismissOverlays(mainMenu.GetTree()?.Root);
        CombatReplayBarUI.Show(tree);
    }

    private static void ShowFeedbackImportMessage(NMainMenu mainMenu, string error) {
        ShowFeedbackImportOverlay(
            mainMenu,
            I18N.T("devmenu.loadFeedback", "Load Feedback ZIP"),
            error,
            confirmText: null,
            onConfirm: null);
    }

    private static void ShowFeedbackImportOverlay(
        NMainMenu mainMenu,
        string titleText,
        string bodyText,
        string? confirmText,
        Action? onConfirm,
        string? extraText = null,
        Action? onExtra = null) {
        var root = mainMenu.GetTree().Root;
        root.GetNodeOrNull<Control>(FeedbackOverlayName)?.QueueFree();

        var overlay = new Control {
            Name = FeedbackOverlayName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2000,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(backdrop);

        var wrapper = new CenterContainer();
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(wrapper);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(extraText == null ? 480 : 560, 0) };
        panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label {
            Text = titleText,
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(title);

        var body = new Label {
            Text = bodyText,
            AutowrapMode = TextServer.AutowrapMode.WordSmart
        };
        body.AddThemeFontSizeOverride("font_size", 12);
        body.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(body);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var closeBtn = new Button {
            Text = confirmText == null
                ? I18N.T("devmenu.loadFeedback.ok", "OK")
                : I18N.T("restart.cancel", "Cancel"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        closeBtn.Pressed += () => overlay.QueueFree();
        btnRow.AddChild(closeBtn);

        if (confirmText != null && onConfirm != null) {
            var confirmBtn = new Button {
                Text = confirmText,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
            };
            confirmBtn.Pressed += () => {
                overlay.QueueFree();
                onConfirm();
            };
            btnRow.AddChild(confirmBtn);
        }

        if (extraText != null && onExtra != null) {
            var extraBtn = new Button {
                Text = extraText,
                SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
                FocusMode = Control.FocusModeEnum.None,
            };
            extraBtn.Pressed += () => {
                overlay.QueueFree();
                onExtra();
            };
            btnRow.AddChild(extraBtn);
        }

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);
        root.AddChild(overlay);
        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                overlay.QueueFree();
        };
    }

    private static void ShowUnlockAllConfirm(NMainMenu mainMenu) {
        var root = mainMenu.GetTree().Root;
        root.GetNodeOrNull<Control>(UnlockAllOverlayName)?.QueueFree();

        var overlay = new Control {
            Name = UnlockAllOverlayName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2000,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(backdrop);

        var wrapper = new CenterContainer();
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(wrapper);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(480, 0) };
        panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label {
            Text = I18N.T("devmenu.unlockAll.confirmTitle", "Unlock All Progress?"),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(title);

        vbox.AddChild(new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        var body = new Label {
            Text = I18N.T("devmenu.unlockAll.confirmBody",
                "Reveals all timeline epochs, ascension levels (A10), and compendium entries (cards, relics, potions, events, monsters, acts). This permanently modifies your save file."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        body.AddThemeFontSizeOverride("font_size", 12);
        body.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(body);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var cancelBtn = new Button {
            Text = I18N.T("restart.cancel", "Cancel"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        cancelBtn.Pressed += () => overlay.QueueFree();
        btnRow.AddChild(cancelBtn);

        var confirmBtn = new Button {
            Text = I18N.T("devmenu.unlockAll.confirm", "Unlock"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        confirmBtn.Pressed += () => {
            ProgressUnlockActions.UnlockAll();
            mainMenu.RefreshButtons();
            overlay.QueueFree();
        };
        btnRow.AddChild(confirmBtn);

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);

        root.AddChild(overlay);
        cancelBtn.GrabFocus();
    }

    private static StyleBoxFlat CreateOverlayPanelStyle() => new() {
        BgColor = new Color(0.12f, 0.12f, 0.15f, 0.98f),
        CornerRadiusTopLeft = 8,
        CornerRadiusTopRight = 8,
        CornerRadiusBottomLeft = 8,
        CornerRadiusBottomRight = 8,
        ContentMarginLeft = 24,
        ContentMarginRight = 24,
        ContentMarginTop = 20,
        ContentMarginBottom = 20,
        BorderWidthTop = 1,
        BorderWidthBottom = 1,
        BorderWidthLeft = 1,
        BorderWidthRight = 1,
        BorderColor = new Color(0.35f, 0.35f, 0.45f, 0.7f),
    };

    private static void ShowAutoSlaySeedOverlay(NMainMenu mainMenu) {
        ShowSeedInputOverlay(
            mainMenu,
            I18N.T("autoslay.title", "AutoSlay"),
            I18N.T("restart.seed.label", "Seed (leave empty for random):"),
            I18N.T("autoslay.seedPlaceholder", "Leave empty for a random seed"),
            seed => {
                DismissOverlays(mainMenu.GetTree()?.Root);
                Callable.From(() => {
                    if (!AutoSlayRunner.TryStart(seed, out var message))
                        MainFile.Logger.Warn($"AutoSlay did not start: {message}");
                }).CallDeferred();
            });
    }

    private static void ShowSeedInputOverlay(NMainMenu mainMenu, Action onNewTest) {
        ShowSeedInputOverlay(
            mainMenu,
            I18N.T("restart.title", "Restart with Seed"),
            I18N.T("restart.seed.label", "Seed (leave empty for random):"),
            I18N.T("restart.seed.placeholder", "e.g. DEADBEEF"),
            seed => {
                if (!string.IsNullOrEmpty(seed)) {
                    KitLibState.PendingRestartSeed = SeedHelper.CanonicalizeSeed(seed);
                    KitLog.Info($"MainMenu seed input: '{KitLibState.PendingRestartSeed}'.");
                }

                onNewTest();
            });
    }

    private static void ShowSeedInputOverlay(
        NMainMenu mainMenu,
        string titleText,
        string seedLabelText,
        string placeholder,
        Action<string> onConfirm) {
        var root = mainMenu.GetTree().Root;
        root.GetNodeOrNull<Control>(SeedOverlayName)?.QueueFree();

        var overlay = new Control {
            Name = SeedOverlayName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2000,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(backdrop);

        var wrapper = new CenterContainer();
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(wrapper);

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(440, 0) };
        panel.AddThemeStyleboxOverride("panel", CreateOverlayPanelStyle());
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label { Text = titleText, HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(title);

        vbox.AddChild(new ColorRect { Color = KitLibTheme.Separator, CustomMinimumSize = new Vector2(0, 1), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var seedLbl = new Label { Text = seedLabelText };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(seedLbl);

        var seedInput = new LineEdit {
            PlaceholderText = placeholder,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        seedInput.AddThemeFontSizeOverride("font_size", 14);
        vbox.AddChild(seedInput);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var cancelBtn = new Button {
            Text = I18N.T("restart.cancel", "Cancel"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        cancelBtn.Pressed += () => overlay.QueueFree();
        btnRow.AddChild(cancelBtn);

        var startBtn = new Button {
            Text = I18N.T("restart.go", "Start"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        startBtn.Pressed += () => {
            var seed = seedInput.Text?.Trim() ?? "";
            overlay.QueueFree();
            onConfirm(seed);
        };
        btnRow.AddChild(startBtn);

        seedInput.TextSubmitted += _ => startBtn.EmitSignal(Button.SignalName.Pressed);

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);

        root.AddChild(overlay);
        seedInput.GrabFocus();
    }
}
