using System;
using System.Collections.Generic;
using System.Linq;
using KitLib;
using KitLib.Multiplayer.PseudoCoop;
using KitLib.Settings;
using Godot;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

/// <summary>Unified pseudo-coop panel: options, character, seed, one-click host start.</summary>
internal static class DevMainMenuPseudoCoopUI {
    private const string OverlayName = "KitLibPseudoCoopLaunch";

    static readonly List<CharacterModel> Characters = ModelDb.AllCharacters.ToList();

    public static void Show(NMainMenu mainMenu, Action onBeforeLaunch) {
        var root = mainMenu.GetTree().Root;
        root.GetNodeOrNull<Control>(OverlayName)?.QueueFree();

        var overlay = new Control {
            Name = OverlayName,
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

        var panel = new PanelContainer { CustomMinimumSize = new Vector2(520, 0) };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        wrapper.AddChild(panel);

        var vbox = new VBoxContainer();
        vbox.AddThemeConstantOverride("separation", 10);

        vbox.AddChild(CreateTitle());
        vbox.AddChild(Separator());

        var desc = CreateSecondaryLabel(I18N.T(
            "devmenu.pseudocoop.desc",
            "Host a multiplayer run from KitLib. You play locally; AI drives simulated teammates in combat."));
        vbox.AddChild(desc);

        var charRow = new HBoxContainer();
        charRow.AddThemeConstantOverride("separation", 8);
        charRow.AddChild(CreateFieldLabel(I18N.T("devmenu.pseudocoop.character", "Character")));

        var charPicker = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        foreach (var c in Characters)
            charPicker.AddItem(c.Title.GetFormattedText());
        charPicker.Selected = Math.Max(0, Characters.FindIndex(c => c.Id.Entry == "ironclad"));
        charRow.AddChild(charPicker);
        vbox.AddChild(charRow);

        var seedRow = new HBoxContainer();
        seedRow.AddThemeConstantOverride("separation", 8);
        seedRow.AddChild(CreateFieldLabel(I18N.T("devmenu.pseudocoop.seed", "Seed")));
        var seedEdit = new LineEdit {
            PlaceholderText = I18N.T("devmenu.pseudocoop.seedPlaceholder", "Random if empty"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        seedRow.AddChild(seedEdit);
        vbox.AddChild(seedRow);

        vbox.AddChild(Separator());

        var chkMpCheat = CreateCheckBox(
            I18N.T("devmenu.pseudocoop.mpCheat", "Enable multiplayer cheat opt-in"),
            SettingsStore.Current.MultiplayerCheatOptIn);
        var chkSyncBot = CreateCheckBox(
            I18N.T("devmenu.pseudocoop.syncBot", "SyncBot (simulate remote ACKs)"),
            true);
        var chkPhantom = CreateCheckBox(
            I18N.T("devmenu.pseudocoop.phantom", "Spawn phantom player (NetId 1001)"),
            true);
        var chkTeammate = CreateCheckBox(
            I18N.T("devmenu.pseudocoop.teammate", "AI teammate in combat"),
            true);
        var chkAutoEndTurn = CreateCheckBox(
            I18N.T("devmenu.pseudocoop.autoEndTurn", "SyncBot auto end-turn for remotes"),
            SettingsStore.Current.SyncBotAutoEndTurn);
        var chkAutoPresetLaunch = CreateCheckBox(
            I18N.T("pseudocoop.autoPreset", "Auto preset on host launch"),
            SettingsStore.Current.PseudoCoopAutoPresetOnLaunch);

        vbox.AddChild(chkMpCheat);
        vbox.AddChild(chkSyncBot);
        vbox.AddChild(chkPhantom);
        vbox.AddChild(chkTeammate);
        vbox.AddChild(chkAutoEndTurn);
        vbox.AddChild(chkAutoPresetLaunch);

        var statusLbl = new Label {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        statusLbl.AddThemeFontSizeOverride("font_size", 12);
        statusLbl.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        vbox.AddChild(statusLbl);

        var errorLbl = new Label {
            Visible = false,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        errorLbl.AddThemeFontSizeOverride("font_size", 12);
        errorLbl.AddThemeColorOverride("font_color", new Color(1f, 0.45f, 0.45f));
        vbox.AddChild(errorLbl);

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
            Text = I18N.T("devmenu.pseudocoop.start", "Start pseudo co-op"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.All,
        };
        startBtn.Pressed += () => {
            errorLbl.Visible = false;
            statusLbl.Text = I18N.T("devmenu.pseudocoop.starting", "Hosting…");
            statusLbl.Visible = true;
            startBtn.Disabled = true;
            cancelBtn.Disabled = true;

            var idx = charPicker.Selected;
            if (idx < 0 || idx >= Characters.Count) {
                errorLbl.Text = I18N.T("devmenu.pseudocoop.noCharacter", "Invalid character.");
                errorLbl.Visible = true;
                statusLbl.Visible = false;
                startBtn.Disabled = false;
                cancelBtn.Disabled = false;
                return;
            }

            var options = new PseudoCoopLobbyHost.LaunchOptions {
                Character = Characters[idx],
                Seed = string.IsNullOrWhiteSpace(seedEdit.Text) ? null : seedEdit.Text.Trim(),
                EnableMpCheatOptIn = chkMpCheat.ButtonPressed,
                SyncBotEnabled = chkSyncBot.ButtonPressed,
                SpawnPhantomPlayer = chkPhantom.ButtonPressed,
                SyncBotAutoEndTurn = chkAutoEndTurn.ButtonPressed,
                MpAiTeammateEnabled = chkTeammate.ButtonPressed,
                AutoPresetOnLaunch = chkAutoPresetLaunch.ButtonPressed,
            };

            _ = StartAsync(overlay, onBeforeLaunch, options, statusLbl, errorLbl, startBtn, cancelBtn);
        };
        btnRow.AddChild(startBtn);
        vbox.AddChild(btnRow);

        panel.AddChild(vbox);
        root.AddChild(overlay);
    }

    static async System.Threading.Tasks.Task StartAsync(
        Control overlay,
        Action onBeforeLaunch,
        PseudoCoopLobbyHost.LaunchOptions options,
        Label statusLbl,
        Label errorLbl,
        Button startBtn,
        Button cancelBtn) {
        overlay.Visible = false;
        overlay.MouseFilter = Control.MouseFilterEnum.Ignore;
        onBeforeLaunch();
        await System.Threading.Tasks.Task.Yield();

        var (ok, err) = await PseudoCoopLobbyHost.TryStartAsync(options);
        if (!ok) {
            overlay.Visible = true;
            overlay.MouseFilter = Control.MouseFilterEnum.Stop;
            statusLbl.Visible = false;
            errorLbl.Text = err;
            errorLbl.Visible = true;
            startBtn.Disabled = false;
            cancelBtn.Disabled = false;
            return;
        }

        overlay.QueueFree();
    }

    static Label CreateTitle() {
        var title = new Label {
            Text = I18N.T("devmenu.pseudocoop.title", "Pseudo Co-op (Host)"),
            HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        return title;
    }

    static Label CreateFieldLabel(string text) {
        var lbl = new Label {
            Text = text,
            CustomMinimumSize = new Vector2(88, 0),
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        return lbl;
    }

    static Label CreateSecondaryLabel(string text) {
        var lbl = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        lbl.AddThemeFontSizeOverride("font_size", 12);
        lbl.AddThemeColorOverride("font_color", KitLibTheme.TextSecondary);
        return lbl;
    }

    static ColorRect Separator() => new() {
        Color = KitLibTheme.Separator,
        CustomMinimumSize = new Vector2(0, 1),
        SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
    };

    static StyleBoxFlat CreatePanelStyle() => new() {
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

    static CheckBox CreateCheckBox(string text, bool pressed) {
        var chk = new CheckBox {
            Text = text,
            ButtonPressed = pressed,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        chk.AddThemeFontSizeOverride("font_size", 12);
        return chk;
    }

    public static void HideAnywhere() => DevMainMenuOverlay.RemoveAnywhere(OverlayName);
}
