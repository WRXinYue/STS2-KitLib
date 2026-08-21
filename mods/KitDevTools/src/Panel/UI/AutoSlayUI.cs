using Godot;
using MegaCrit.Sts2.Core.Nodes.CommonUi;

namespace KitLib.UI;

internal static class AutoSlayUI {
    private const string RootName = "KitLibAutoSlay";
    private const float PanelW = 480f;

    public static void Show(NGlobalUi globalUi) {
        Remove(globalUi);

        var dual = DevPanelUI.CreateMainOnlyDualOverlay(
            globalUi, RootName, PanelW, () => Remove(globalUi), contentSeparation: 10);
        var root = dual.Root;
        var vbox = dual.MainContent;

        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(I18N.T("autoslay.title", "AutoSlay")));
        var subtitle = new Label {
            Text = I18N.T("autoslay.hint",
                "Official smoke bot: main menu through floor 49. Single-player only."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        var seedLabel = new Label { Text = I18N.T("autoslay.seed", "Seed") };
        seedLabel.AddThemeFontSizeOverride("font_size", 12);
        seedLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(seedLabel);

        var seedInput = new LineEdit {
            Text = AutoSlayRunner.DraftSeed,
            PlaceholderText = I18N.T("autoslay.seedPlaceholder", "Leave empty for a random seed"),
            ClearButtonEnabled = true,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        DevModeFormChrome.ApplyLineEdit(seedInput);
        seedInput.TextChanged += text => AutoSlayRunner.DraftSeed = text;
        vbox.AddChild(seedInput);

        var status = new Label { AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 11);
        status.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        vbox.AddChild(status);

        var buttons = new HBoxContainer();
        buttons.AddThemeConstantOverride("separation", 8);

        var startBtn = new Button {
            Text = I18N.T("autoslay.start", "Start"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        startBtn.AddThemeFontSizeOverride("font_size", 13);
        DevModeFormChrome.ApplyAccentPillButton(startBtn);

        var stopBtn = new Button {
            Text = I18N.T("autoslay.stop", "Stop"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        stopBtn.AddThemeFontSizeOverride("font_size", 13);

        startBtn.Pressed += () => {
            AutoSlayRunner.TryStart(seedInput.Text, out _);
            RefreshChrome(seedInput, startBtn, stopBtn, status);
        };
        stopBtn.Pressed += () => {
            AutoSlayRunner.Stop();
            RefreshChrome(seedInput, startBtn, stopBtn, status);
        };

        buttons.AddChild(startBtn);
        buttons.AddChild(stopBtn);
        vbox.AddChild(buttons);

        var timer = new Godot.Timer {
            WaitTime = 0.5,
            Autostart = true,
            ProcessMode = Node.ProcessModeEnum.Always,
        };
        timer.Timeout += () => {
            if (!GodotObject.IsInstanceValid(root))
                return;
            RefreshChrome(seedInput, startBtn, stopBtn, status);
        };
        root.AddChild(timer);

        RefreshChrome(seedInput, startBtn, stopBtn, status);
        dual.AttachToScene();
    }

    public static void Remove(NGlobalUi globalUi) {
        ((Node)globalUi).GetNodeOrNull<Control>(RootName)?.QueueFree();
    }

    static void RefreshChrome(LineEdit seedInput, Button startBtn, Button stopBtn, Label status) {
        var mp = AutoSlayRunner.IsBlockedByMultiplayer;
        var running = AutoSlayRunner.IsRunning;
        seedInput.Editable = !running && !mp;
        startBtn.Disabled = running || mp;
        stopBtn.Disabled = !running;

        if (mp) {
            status.Text = I18N.T("autoslay.mpWarning", "Not available in multiplayer.");
            return;
        }

        if (running) {
            status.Text = I18N.T("autoslay.status.running", "Running seed {0}",
                AutoSlayRunner.LastSeed ?? "");
            return;
        }

        status.Text = I18N.T("autoslay.status.idle", "Idle");
    }
}
