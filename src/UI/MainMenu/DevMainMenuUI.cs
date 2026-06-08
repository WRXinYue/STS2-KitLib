using System;
using System.Collections.Generic;
using System.Reflection;
using KitLib;
using KitLib.Actions;
using KitLib.Multiplayer.LanTest;
using KitLib.Settings;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Debug.Multiplayer;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

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

        AddButton(container, template, I18N.T("devmenu.newTest", "New Test"), () => { Hide(); actions.OnNewTest(); });
        AddButton(container, template, I18N.T("devmenu.newTestWithSeed", "New Test (Seed)"), () => {
            ShowSeedInputOverlay(mainMenu, actions.OnNewTest);
        });

        bool anySlot = SaveSlotManager.GetAllSlotIds().Count > 0;

        var loadBtn = AddButton(container, template, I18N.T("devmenu.loadSnapshot", "Load Save"), () => {
            SaveSlotUI.Show(mainMenu.GetTree().Root, saveMode: false, onConfirm: slot => {
                SaveSlotUI.Hide();
                Hide();
                SaveSlotManager.LoadFromSlot(slot);
            });
        });
        if (!anySlot)
            loadBtn.SetEnabled(false);

        NMainMenuTextButton? persistNormalRunBtn = null;
        persistNormalRunBtn = AddButton(container, template, GetPersistNormalRunModeLabel(), () => {
            AdvanceNormalRunMode();
            if (persistNormalRunBtn?.label != null)
                persistNormalRunBtn.label.Text = GetPersistNormalRunModeLabel();
        });

        AddButton(container, template, I18N.T("devmenu.multiplayer", "Multiplayer"), ShowMultiplayerMenu);

        AddButton(container, template, I18N.T("devmenu.unlockAll", "Unlock All Progress"), () => {
            ShowUnlockAllConfirm(mainMenu);
        });

        AddButton(container, template, I18N.T("devmenu.diagnostics", "Diagnostics"), ShowDiagnosticsMenu);

        AddButton(container, template, I18N.T("devmenu.progressGuard", "Progress protection"), () => {
            ProgressGuardUI.ShowOnMainMenu(mainMenu);
        });

        AddButton(container, template, I18N.T("devmenu.back", "Back"), Hide);
    }

    static void ShowDiagnosticsMenu() {
        if (_mainMenu == null || _buttonsContainer == null || _buttonTemplate == null)
            return;

        ClearAddedButtons();
        var mainMenu = _mainMenu;
        var container = _buttonsContainer;
        var template = _buttonTemplate;

        AddButton(container, template, I18N.T("devmenu.logs", "Logs"), () => {
            LogViewerUI.ShowOnMainMenu(mainMenu);
        });

        AddButton(container, template, I18N.T("devmenu.feedback", "Mod Feedback"), () => {
            FeedbackReportUI.ShowOnMainMenu(mainMenu);
        });

        AddButton(container, template, I18N.T("devmenu.back", "Back"), ShowRootMenu);
    }

    static void ShowMultiplayerMenu() {
        if (_mainMenu == null || _buttonsContainer == null || _buttonTemplate == null)
            return;

        ClearAddedButtons();
        var mainMenu = _mainMenu;
        var container = _buttonsContainer;
        var template = _buttonTemplate;

        NMainMenuTextButton? mpCheatBtn = null;
        mpCheatBtn = AddButton(container, template, GetMultiplayerCheatOptInLabel(), () => {
            SettingsStore.SetMultiplayerCheatOptIn(!SettingsStore.Current.MultiplayerCheatOptIn);
            if (mpCheatBtn?.label != null)
                mpCheatBtn.label.Text = GetMultiplayerCheatOptInLabel();
        });

        AddButton(container, template, I18N.T("devmenu.pseudocoop", "Pseudo Co-op Test (Host)"), () => {
            DevMainMenuPseudoCoopUI.Show(mainMenu, Hide);
        });

        AddButton(container, template, I18N.T("devmenu.lanMultiplayer", "LAN Multiplayer"), () => {
            OpenLanMultiplayer(mainMenu);
        });

        AddButton(container, template, I18N.T("devmenu.back", "Back"), ShowRootMenu);
    }

    static void OpenLanMultiplayer(NMainMenu mainMenu) {
        DualInstanceTestBootstrap.EnsureMultiplayerDevActive("lan_multiplayer_open");
        Hide();
        var game = mainMenu.GetTree().Root.GetNodeOrNull<NGame>("Game")
            ?? NGame.Instance;
        if (game == null) {
            MainFile.Logger.Warn("KitLib: NGame not found; cannot open LAN multiplayer test scene.");
            return;
        }

        var testScene = SceneHelper.Instantiate<NMultiplayerTest>("debug/multiplayer_test");
        game.RootSceneContainer.SetCurrentScene(testScene);
        _ = TaskHelper.RunSafely(game.Transition.FadeIn());
        MainFile.Logger.Info("KitLib: Opened LAN multiplayer test scene (debug/multiplayer_test).");
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
        Node? root = null;
        if (_mainMenu != null && GodotObject.IsInstanceValid(_mainMenu))
            root = _mainMenu.GetTree().Root;

        DismissOverlays(root);
        ClearAddedButtons();
        RestoreStockButtons();
        ClearSessionState();
    }

    public static bool IsVisible => _mainMenu != null && GodotObject.IsInstanceValid(_mainMenu);

    public static void ReapplyHide() {
        foreach (var (ctrl, _) in _hiddenControls) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.Visible = false;
        }
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
        FeedbackReportUI.HideAnywhere();
        LogViewerUI.HideAnywhere();
        ProgressGuardUI.HideAnywhere();
        ProgressLossPromptUI.HideAnywhere();
        ErrorFeedbackPromptUI.HideAnywhere();
        CrashRecoveryPromptUI.HideAnywhere();
        DevMainMenuPseudoCoopUI.HideAnywhere();

        var root = attachRoot ?? (Engine.GetMainLoop() as SceneTree)?.Root;
        DevMainMenuOverlay.RemoveAnywhere(SeedOverlayName);
        DevMainMenuOverlay.RemoveAnywhere(UnlockAllOverlayName);
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
        _mainMenu = null;
        _buttonsContainer = null;
        _buttonTemplate = null;
        _actions = null;
    }

    private static bool IsDevMenuAddedButton(Control ctrl) =>
        ctrl.Name.ToString().StartsWith("KitLibBtn_", StringComparison.Ordinal);

    private const string SeedOverlayName = "KitLibSeedInput";
    private const string UnlockAllOverlayName = "KitLibUnlockAllConfirm";

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

    private static void ShowSeedInputOverlay(NMainMenu mainMenu, Action onNewTest) {
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

        var title = new Label { Text = I18N.T("restart.title", "Restart with Seed"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        vbox.AddChild(title);

        vbox.AddChild(new ColorRect { Color = KitLibTheme.Separator, CustomMinimumSize = new Vector2(0, 1), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var seedLbl = new Label { Text = I18N.T("restart.seed.label", "Seed (leave empty for random):") };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(seedLbl);

        var seedInput = new LineEdit {
            PlaceholderText = I18N.T("restart.seed.placeholder", "e.g. DEADBEEF"),
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
            var seed = seedInput.Text?.Trim();
            if (!string.IsNullOrEmpty(seed)) {
                KitLibState.PendingRestartSeed = SeedHelper.CanonicalizeSeed(seed);
                MainFile.Logger.Info($"[KitLib] MainMenu seed input: '{KitLibState.PendingRestartSeed}'.");
            }

            overlay.QueueFree();
            Hide();
            onNewTest();
        };
        btnRow.AddChild(startBtn);

        seedInput.TextSubmitted += _ => startBtn.EmitSignal(Button.SignalName.Pressed);

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);

        root.AddChild(overlay);
        seedInput.GrabFocus();
    }

    private static void AdvanceNormalRunMode() {
        var next = KitLibState.NormalRunMode switch {
            NormalRunMode.Disabled => NormalRunMode.DevPanel,
            NormalRunMode.DevPanel => NormalRunMode.Cheat,
            NormalRunMode.Cheat   => NormalRunMode.Disabled,
            _                     => NormalRunMode.Disabled,
        };
        SettingsStore.SetNormalRunMode(next);
    }

    private static string GetMultiplayerCheatOptInLabel() =>
        SettingsStore.Current.MultiplayerCheatOptIn
            ? I18N.T("devmenu.mpCheat.on", "Multiplayer cheat: ON")
            : I18N.T("devmenu.mpCheat.off", "Multiplayer cheat: OFF");

    private static string GetPersistNormalRunModeLabel() {
        return KitLibState.NormalRunMode switch {
            NormalRunMode.Disabled => I18N.T("devmenu.persistNormalRun.disabled", "Normal run: disabled"),
            NormalRunMode.DevPanel => I18N.T("devmenu.persistNormalRun.devMode", "Normal run: Dev Mode"),
            NormalRunMode.Cheat   => I18N.T("devmenu.persistNormalRun.cheatMode", "Normal run: Cheat Mode"),
            _                     => "",
        };
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
