using System;
using System.Collections.Generic;
using System.Reflection;
using DevMode;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Helpers;
using MegaCrit.Sts2.Core.Nodes.GodotExtensions;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace DevMode.UI;

internal sealed class DevMainMenuActions {
    public required Action OnNewTest { get; init; }
    public required Action OnCardLibrary { get; init; }
    public required Action OnRelicCollection { get; init; }
}

internal static class DevMainMenuUI {
    private const string ButtonsContainerPath = "%MainMenuTextButtons";

    private static NMainMenu? _mainMenu;
    private static readonly List<NMainMenuTextButton> _addedButtons = new();
    private static readonly List<(Control control, bool wasVisible)> _hiddenControls = new();

    // Runtime rows miss NMainMenu._Ready wiring; forward focus to the same handlers as stock buttons.
    private static readonly MethodInfo? MainMenuFocusedMethod =
        AccessTools.Method(typeof(NMainMenu), "MainMenuButtonFocused");
    private static readonly MethodInfo? MainMenuUnfocusedMethod =
        AccessTools.Method(typeof(NMainMenu), "MainMenuButtonUnfocused");

    public static void Show(NMainMenu mainMenu, DevMainMenuActions actions) {
        _mainMenu = mainMenu;

        var container = mainMenu.GetNodeOrNull<Control>(ButtonsContainerPath);
        if (container == null) {
            MainFile.Logger.Warn("DevMode: Could not find MainMenuTextButtons container.");
            return;
        }

        var template = container.GetNodeOrNull<NMainMenuTextButton>("SettingsButton");
        if (template == null) {
            MainFile.Logger.Warn("DevMode: SettingsButton not found under MainMenuTextButtons.");
            return;
        }

        _hiddenControls.Clear();
        foreach (var child in container.GetChildren()) {
            if (child is not Control ctrl) continue;
            _hiddenControls.Add((ctrl, ctrl.Visible));
            ctrl.Visible = false;
        }

        _addedButtons.Clear();
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

        AddButton(container, template, I18N.T("devmenu.cardLibrary", "Card Library"), () => { Hide(); actions.OnCardLibrary(); });
        AddButton(container, template, I18N.T("devmenu.relicCollection", "Relic Collection"), () => { Hide(); actions.OnRelicCollection(); });

        NMainMenuTextButton? persistNormalRunBtn = null;
        persistNormalRunBtn = AddButton(container, template, GetPersistNormalRunModeLabel(), () => {
            AdvanceNormalRunMode();
            if (persistNormalRunBtn?.label != null)
                persistNormalRunBtn.label.Text = GetPersistNormalRunModeLabel();
        });

        AddButton(container, template, I18N.T("devmenu.back", "Back"), Hide);
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
        if (_mainMenu == null || !GodotObject.IsInstanceValid(_mainMenu)) return;

        SaveSlotUI.Hide();

        foreach (var btn in _addedButtons) {
            if (GodotObject.IsInstanceValid(btn))
                btn.QueueFree();
        }
        _addedButtons.Clear();

        RestoreButtons();
        _mainMenu = null;
    }

    public static bool IsVisible => _mainMenu != null && GodotObject.IsInstanceValid(_mainMenu);

    public static void ReapplyHide() {
        foreach (var (ctrl, _) in _hiddenControls) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.Visible = false;
        }
    }

    private static void RestoreButtons() {
        foreach (var (ctrl, wasVisible) in _hiddenControls) {
            if (GodotObject.IsInstanceValid(ctrl))
                ctrl.Visible = wasVisible;
        }
        _hiddenControls.Clear();
    }

    private const string SeedOverlayName = "DevModeSeedInput";

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
        var panelStyle = new StyleBoxFlat {
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
        panel.AddThemeStyleboxOverride("panel", panelStyle);
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 14);

        var title = new Label { Text = I18N.T("restart.title", "Restart with Seed"), HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", DevModeTheme.Accent);
        vbox.AddChild(title);

        vbox.AddChild(new ColorRect { Color = DevModeTheme.Separator, CustomMinimumSize = new Vector2(0, 1), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });

        var seedLbl = new Label { Text = I18N.T("restart.seed.label", "Seed (leave empty for random):") };
        seedLbl.AddThemeFontSizeOverride("font_size", 12);
        seedLbl.AddThemeColorOverride("font_color", DevModeTheme.TextPrimary);
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
                DevModeState.PendingRestartSeed = SeedHelper.CanonicalizeSeed(seed);
                MainFile.Logger.Info($"[DevMode] MainMenu seed input: '{DevModeState.PendingRestartSeed}'.");
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
        DevModeState.DebugMode = DevModeState.DebugMode switch {
            DebugMode.Off => DebugMode.Panel,
            DebugMode.Panel => DebugMode.Full,
            DebugMode.Full => DebugMode.Off,
            _ => DebugMode.Off,
        };
    }

    private static string GetPersistNormalRunModeLabel() {
        return DevModeState.DebugMode switch {
            DebugMode.Off => I18N.T("devmenu.persistNormalRun.disabled", "Normal run: disabled"),
            DebugMode.Panel => I18N.T("devmenu.persistNormalRun.devMode", "Normal run: Dev Mode"),
            DebugMode.Full => I18N.T("devmenu.persistNormalRun.cheatMode", "Normal run: Cheat Mode"),
            _ => "",
        };
    }

    private static NMainMenuTextButton AddButton(Control container, NMainMenuTextButton template, string text, Action action) {
        var btn = MainMenuTextButtonFactory.CreateFrom(
            template,
            container,
            name: $"DevModeBtn_{text.Replace(" ", "")}",
            text: text,
            onReleased: _ => action());

        if (_mainMenu != null)
            WireMainMenuTextButton(_mainMenu, btn);

        _addedButtons.Add(btn);
        return btn;
    }
}
