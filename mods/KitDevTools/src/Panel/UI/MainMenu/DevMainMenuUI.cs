using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Godot;
using HarmonyLib;
using KitLib;
using KitLib.Actions;
using KitLib.Compat;
using KitLib.Feedback;
using KitLib.Replay;
using MegaCrit.Sts2.Core.ControllerInput;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.CommonUi;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.UI;

internal sealed class DevMainMenuActions {
    public required Action OnNewTest { get; init; }
}

internal static class DevMainMenuUI {
    private const string ButtonsContainerPath = "%MainMenuTextButtons";

    private static NMainMenu? _mainMenu;
    private static Control? _buttonsContainer;
    private static NMainMenuTextButton? _buttonTemplate;
    private static DevMainMenuActions? _actions;
    private static readonly List<NMainMenuTextButton> _addedButtons = new();
    private static readonly List<(Control control, bool wasVisible)> _hiddenControls = new();
    private static Control? _sessionContainer;
    private static DevMenuLevel _currentLevel;
    private static DevMainMenuInputForwarder? _inputForwarder;

    private enum DevMenuLevel {
        Root,
        Diagnostics,
    }

    // Runtime rows miss NMainMenu._Ready wiring; forward focus to the same handlers as stock buttons.
    private static readonly MethodInfo? MainMenuFocusedMethod =
        AccessTools.Method(typeof(NMainMenu), "MainMenuButtonFocused");
    private static readonly MethodInfo? MainMenuUnfocusedMethod =
        AccessTools.Method(typeof(NMainMenu), "MainMenuButtonUnfocused");

    public static void Show(NMainMenu mainMenu, DevMainMenuActions actions) {
        _mainMenu = mainMenu;
        _actions = actions;

        var container = mainMenu.GetNodeOrNull<Control>(ButtonsContainerPath);
        if (container == null) {
            MainFile.Logger.Warn("KitLib: Could not find MainMenuTextButtons container.");
            return;
        }

        var template = container.GetNodeOrNull<NMainMenuTextButton>("SettingsButton");
        if (template == null) {
            MainFile.Logger.Warn("KitLib: SettingsButton not found under MainMenuTextButtons.");
            return;
        }

        _buttonsContainer = container;
        _buttonTemplate = template;

        DismissOverlays(mainMenu.GetTree().Root);
        TakeOverContainer(container);
        EnsureInputForwarder(mainMenu);
        ShowRootMenu();
    }

    static void ShowRootMenu() {
        if (_mainMenu == null || _buttonsContainer == null || _buttonTemplate == null || _actions == null)
            return;

        ClearAddedButtons();
        var mainMenu = _mainMenu;
        var actions = _actions;
        var container = _buttonsContainer;
        var template = _buttonTemplate;

        AddButton((Control)container, template, I18N.T("devmenu.newTest", "New Test"), () => { Hide(); actions.OnNewTest(); });
        AddButton((Control)container, template, I18N.T("devmenu.newTestWithSeed", "New Test (Seed)"), () => {
            ShowSeedInputOverlay(mainMenu, actions.OnNewTest);
        });
        var autoSlayBtn = AddButton((Control)container, template, I18N.T("devmenu.autoslay", "AutoSlay"), () => {
            ShowAutoSlaySeedOverlay(mainMenu);
        });
        if (AutoSlayRunner.IsBlockedByMultiplayer || AutoSlayRunner.IsRunning)
            autoSlayBtn.SetEnabled(false);

        bool anySlot = SaveSlotManager.GetAllSlotIds().Count > 0;

        var loadBtn = AddButton(container, template, I18N.T("devmenu.loadSnapshot", "Load Save"), () => {
            SaveSlotUI.Show(mainMenu.GetTree().Root, saveMode: false, onConfirm: (slot, _) => {
                SaveSlotUI.Hide();
                Hide();
                SaveSlotManager.LoadFromSlot(slot);
            });
        });
        if (!anySlot)
            loadBtn.SetEnabled(false);

        AddButton((Control)container, template, I18N.T("devmenu.loadFeedback", "Load Feedback ZIP"), () => {
            OpenFeedbackZipPicker(mainMenu);
        });

        AddButton((Control)container, template, I18N.T("devmenu.replay", "Play Official Replay"), () => {
            OpenCombatReplayPicker(mainMenu);
        });

        AddButton((Control)container, template, I18N.T("devmenu.runReplay", "Play DevTools Replay"), () => {
            OpenRunReplayPicker(mainMenu);
        });

        AddButton((Control)container, template, I18N.T("devmenu.pseudocoop", "Pseudo Co-op Test (Host)"), () => {
            DevMainMenuPseudoCoopUI.Show(mainMenu, Hide);
        });

        AddButton((Control)container, template, I18N.T("devmenu.unlockAll", "Unlock All Progress"), () => {
            ShowUnlockAllConfirm(mainMenu);
        });

        AddButton((Control)container, template, I18N.T("devmenu.diagnostics", "Diagnostics"), ShowDiagnosticsMenu);

        AddButton((Control)container, template, I18N.T("devmenu.back", "Back"), Hide);
        FinishMenuBuild(DevMenuLevel.Root);
    }

    static void ShowDiagnosticsMenu() {
        if (_mainMenu == null || _buttonsContainer == null || _buttonTemplate == null)
            return;

        ClearAddedButtons();
        var mainMenu = _mainMenu;
        var container = _buttonsContainer;
        var template = _buttonTemplate;

        AddButton((Control)container, template, I18N.T("devmenu.logs", "Logs"), () => {
            LogViewerUI.ShowOnMainMenu(mainMenu);
        });

        AddButton((Control)container, template, I18N.T("devmenu.back", "Back"), ShowRootMenu);
        FinishMenuBuild(DevMenuLevel.Diagnostics);
    }

    static void ClearAddedButtons() {
        foreach (var btn in _addedButtons) {
            if (GodotObject.IsInstanceValid(btn))
                btn.QueueFree();
        }
        _addedButtons.Clear();
    }

    private static void WireMainMenuTextButton(NMainMenu mainMenu, NMainMenuTextButton button) {
        if (MainMenuFocusedMethod != null) {
            button.Connect(NClickableControl.SignalName.Focused, Callable.From<NMainMenuTextButton>(b => {
                Callable.From(() => {
                    if (GodotObject.IsInstanceValid(mainMenu) && GodotObject.IsInstanceValid(b))
                        MainMenuFocusedMethod.Invoke(mainMenu, [b]);
                }).CallDeferred();
            }));
        }
        if (MainMenuUnfocusedMethod != null) {
            button.Connect(NClickableControl.SignalName.Unfocused, Callable.From<NMainMenuTextButton>(b => {
                if (GodotObject.IsInstanceValid(mainMenu) && GodotObject.IsInstanceValid(b))
                    MainMenuUnfocusedMethod.Invoke(mainMenu, [b]);
            }));
        }
    }

    public static void Hide() {
        var mainMenu = _mainMenu;
        Node? root = null;
        if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu))
            root = mainMenu.GetTree().Root;

        DismissOverlays(root);
        ClearAddedButtons();
        RestoreStockButtons();
        if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu))
            RestoreStockMainMenuControllerFocus(mainMenu);
        ClearSessionState();
    }

    public static bool IsVisible => _mainMenu != null && GodotObject.IsInstanceValid(_mainMenu);

    public static void ReapplyHide() {
        foreach (var (ctrl, _) in _hiddenControls) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.Visible = false;
        }
    }

    internal static void NotifyOverlayOpened(Control overlayRoot, PanelContainer panel) {
        ReleaseDevMenuButtonFocus();
        DevMainMenuOverlay.FocusOverlayContentDeferred(overlayRoot, panel);
    }

    internal static void NotifyOverlayClosed() {
        if (!IsVisible)
            return;
        ReleaseDevMenuButtonFocus();
        RefreshControllerFocus();
    }

    private static void ReleaseDevMenuButtonFocus() {
        if (_mainMenu == null || !GodotObject.IsInstanceValid(_mainMenu))
            return;

        foreach (var btn in _addedButtons) {
            if (!GodotObject.IsInstanceValid(btn) || !btn.HasFocus())
                continue;
            if (MainMenuUnfocusedMethod != null)
                MainMenuUnfocusedMethod.Invoke(_mainMenu, [btn]);
            btn.ReleaseFocus();
        }

        _mainMenu.GetViewport()?.GuiReleaseFocus();
    }

    private static void TakeOverContainer(Control container) {
        if (_sessionContainer != null && _sessionContainer != container)
            RestoreStockButtons();

        if (_sessionContainer == container) {
            ReapplyHide();
            return;
        }

        _sessionContainer = container;
        _hiddenControls.Clear();
        foreach (var child in container.GetChildren()) {
            if (child is not Control ctrl || IsDevMenuAddedButton(ctrl))
                continue;
            _hiddenControls.Add((ctrl, ctrl.Visible));
            ctrl.Visible = false;
        }
    }

    private static void DismissOverlays(Node? attachRoot) {
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

    private static void RestoreStockButtons() {
        foreach (var (ctrl, wasVisible) in _hiddenControls) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.Visible = wasVisible;
        }
        _hiddenControls.Clear();
        _sessionContainer = null;
    }

    private static void ClearSessionState() {
        if (_inputForwarder != null && GodotObject.IsInstanceValid(_inputForwarder))
            _inputForwarder.QueueFree();
        _inputForwarder = null;
        _currentLevel = DevMenuLevel.Root;
        _mainMenu = null;
        _buttonsContainer = null;
        _buttonTemplate = null;
        _actions = null;
    }

    private static void FinishMenuBuild(DevMenuLevel level) {
        _currentLevel = level;
        WireAddedButtonFocusNeighbors();
        RefreshControllerFocus();
    }

    private static void WireAddedButtonFocusNeighbors() {
        var focusable = new List<NMainMenuTextButton>();
        foreach (var btn in _addedButtons) {
            if (IsFocusableMenuButton(btn))
                focusable.Add(btn);
        }

        for (var i = 0; i < focusable.Count; i++) {
            var btn = focusable[i];
            btn.FocusNeighborLeft = new NodePath(".");
            btn.FocusNeighborRight = new NodePath(".");
            btn.FocusNeighborTop = i > 0 ? focusable[i - 1].GetPath() : btn.GetPath();
            btn.FocusNeighborBottom = i < focusable.Count - 1
                ? focusable[i + 1].GetPath()
                : btn.GetPath();
        }
    }

    private static void RefreshControllerFocus() {
        if (!ShouldPreferControllerFocus())
            return;

        Callable.From(() => {
            foreach (var btn in _addedButtons) {
                if (!IsFocusableMenuButton(btn))
                    continue;
                btn.GrabFocus();
                return;
            }
        }).CallDeferred();
    }

    private static bool ShouldPreferControllerFocus() =>
        Sts2InputCompat.IsUsingController(NControllerManager.Instance)
        || Input.GetConnectedJoypads().Count > 0;

    private static void RestoreStockMainMenuControllerFocus(NMainMenu mainMenu) {
        Callable.From(() => {
            if (!GodotObject.IsInstanceValid(mainMenu))
                return;

            mainMenu.GetViewport()?.GuiReleaseFocus();
            WireStockButtonFocusNeighbors(mainMenu);

            if (ShouldPreferControllerFocus())
                GrabFirstVisibleStockButtonFocus(mainMenu);
        }).CallDeferred();
    }

    private static void WireStockButtonFocusNeighbors(NMainMenu mainMenu) {
        var container = mainMenu.GetNodeOrNull<Control>(ButtonsContainerPath)
            ?? mainMenu.GetNodeOrNull<Control>("MainMenuTextButtons");
        if (container == null)
            return;

        var focusable = new List<NMainMenuTextButton>();
        foreach (var child in container.GetChildren()) {
            if (child is not NMainMenuTextButton btn || !btn.Visible || !btn.IsEnabled)
                continue;
            if (btn.FocusMode == Control.FocusModeEnum.None)
                continue;
            focusable.Add(btn);
        }

        for (var i = 0; i < focusable.Count; i++) {
            var btn = focusable[i];
            btn.FocusNeighborLeft = new NodePath(".");
            btn.FocusNeighborRight = new NodePath(".");
            btn.FocusNeighborTop = i > 0 ? focusable[i - 1].GetPath() : btn.GetPath();
            btn.FocusNeighborBottom = i < focusable.Count - 1
                ? focusable[i + 1].GetPath()
                : btn.GetPath();
        }
    }

    private static void GrabFirstVisibleStockButtonFocus(NMainMenu mainMenu) {
        var container = mainMenu.GetNodeOrNull<Control>(ButtonsContainerPath)
            ?? mainMenu.GetNodeOrNull<Control>("MainMenuTextButtons");
        if (container == null)
            return;

        foreach (var child in container.GetChildren()) {
            if (child is not NMainMenuTextButton btn || !btn.Visible || !btn.IsEnabled)
                continue;
            if (btn.FocusMode == Control.FocusModeEnum.None)
                continue;
            btn.GrabFocus();
            return;
        }
    }

    private static bool IsFocusableMenuButton(NMainMenuTextButton btn) =>
        GodotObject.IsInstanceValid(btn)
        && btn.Visible
        && btn.FocusMode != Control.FocusModeEnum.None
        && btn.IsEnabled;

    private static void EnsureInputForwarder(NMainMenu mainMenu) {
        if (_inputForwarder != null && GodotObject.IsInstanceValid(_inputForwarder))
            return;

        _inputForwarder = new DevMainMenuInputForwarder { Name = "KitLibDevMenuInput" };
        mainMenu.AddChild(_inputForwarder);
    }

    private static void HandleCancelInput() {
        switch (_currentLevel) {
            case DevMenuLevel.Diagnostics:
                ShowRootMenu();
                break;
            default:
                Hide();
                break;
        }
    }

    private static bool HasBlockingOverlay() {
        var root = _mainMenu?.GetTree().Root;
        if (root == null)
            return false;

        foreach (var name in CancelBlockingOverlayNames) {
            if (root.FindChild(name, recursive: true, owned: false) != null)
                return true;
        }

        return false;
    }

    private static readonly string[] CancelBlockingOverlayNames = [
        SeedOverlayName,
        UnlockAllOverlayName,
        SaveSlotDialogRootId.NodeName,
        "KitLibLogViewer",
        "KitLibPseudoCoopLaunch",
    ];

    private sealed class DevMainMenuInputForwarder : Node {
        public override void _Ready() => SetProcessUnhandledInput(true);

        public override void _UnhandledInput(InputEvent @event) {
            if (!IsVisible || HasBlockingOverlay())
                return;

            if (@event is InputEventKey { Echo: false, Pressed: true } key
                && (key.Keycode == Key.Escape || key.PhysicalKeycode == Key.Escape)) {
                HandleCancelInput();
                GetViewport()?.SetInputAsHandled();
                return;
            }

            if (@event.IsActionPressed("ui_cancel")) {
                HandleCancelInput();
                GetViewport()?.SetInputAsHandled();
            }
        }
    }

    private static bool IsDevMenuAddedButton(Control ctrl) =>
        ctrl.Name.ToString().StartsWith("KitLibBtn_", StringComparison.Ordinal);

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
                Hide();
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
        Hide();
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
        Hide();
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
                Hide();
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

                Hide();
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

    private static NMainMenuTextButton AddButton(Control container, NMainMenuTextButton template, string text, Action action) {
        var btn = MainMenuTextButtonFactory.CreateFrom(
            template,
            container,
            name: $"KitLibBtn_{text.Replace(" ", "")}",
            text: text,
            onReleased: _ => action());

        if (_mainMenu != null)
            WireMainMenuTextButton(_mainMenu, btn);

        _addedButtons.Add(btn);
        return btn;
    }
}
