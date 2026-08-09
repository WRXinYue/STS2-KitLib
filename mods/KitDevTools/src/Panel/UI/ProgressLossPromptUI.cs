using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using KitLib.Progress;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ProgressLossPromptUI {
    private const string PromptName = "KitLibProgressLossPrompt";

    private static bool DismissedForSession { get; set; }

    public static bool IsVisible =>
        GodotObject.IsInstanceValid(
            Engine.GetMainLoop() is SceneTree tree
                ? tree.Root?.FindChild(PromptName, true, false)
                : null);

    public static bool TryShowStartupPrompt(NMainMenu mainMenu) {
        if (DismissedForSession)
            return false;

        if (!SettingsStore.Current.PromptOnModCharacterProgressLoss)
            return false;

        if (ModCharacterProgressLossDetector.Pending == null)
            ModCharacterProgressLossDetector.DetectAfterProgressLoad();

        var pending = ModCharacterProgressLossDetector.Pending;
        if (pending == null)
            return false;

        Show(mainMenu, pending);
        return true;
    }

    public static void HideAnywhere() => DevMainMenuOverlay.RemoveAnywhere(PromptName);

    private static void Show(NMainMenu mainMenu, ModCharacterProgressLossResult loss) {
        if (DismissedForSession || !GodotObject.IsInstanceValid(mainMenu))
            return;

        var root = mainMenu.GetTree().Root;
        HideAnywhere();

        int profileId;
        try {
            profileId = MegaCrit.Sts2.Core.Saves.SaveManager.Instance.CurrentProfileId;
        }
        catch {
            profileId = 1;
        }

        var overlay = new Control {
            Name = PromptName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2050,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                Callable.From(Dismiss).CallDeferred();
        };
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
            Text = I18N.T("progressGuard.lossPrompt.title", "Mod character progress may have been lost"),
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

        var time = loss.Backup.UtcTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        var body = new StringBuilder();
        body.AppendLine(I18N.T("progressGuard.lossPrompt.body",
            "Vanilla save loading may have filtered mod character stats that are still present in a recent backup."));
        body.AppendLine();
        body.AppendLine(FormatCharacterList(loss.LostCharacterNames));
        body.AppendLine();
        body.AppendLine(I18N.T("progressGuard.restore.confirmBackup",
            "Backup: {0} (profile {1}, {2})", loss.Backup.DirectoryName, profileId, time));

        var bodyLabel = new Label {
            Text = body.ToString().TrimEnd(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        bodyLabel.AddThemeFontSizeOverride("font_size", 12);
        bodyLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(bodyLabel);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var dismissBtn = new Button {
            Text = I18N.T("progressGuard.lossPrompt.notNow", "Not now"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        dismissBtn.Pressed += () => Callable.From(Dismiss).CallDeferred();
        btnRow.AddChild(dismissBtn);

        var restoreBtn = new Button {
            Text = I18N.T("progressGuard.restore.button", "Restore"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        restoreBtn.Pressed += () => {
            restoreBtn.Disabled = true;
            dismissBtn.Disabled = true;
            var backup = loss.Backup;
            var menu = mainMenu;
            Callable.From(() => {
                HideAnywhere();
                ProgressGuardUI.ShowRestoreConfirm(menu, backup, profileId);
            }).CallDeferred();
        };
        btnRow.AddChild(restoreBtn);

        vbox.AddChild(btnRow);
        panel.AddChild(vbox);

        root.AddChild(overlay);
        dismissBtn.GrabFocus();
    }

    private static void Dismiss() {
        DismissedForSession = true;
        ModCharacterProgressLossDetector.ClearPending();
        HideAnywhere();
    }

    private static string FormatCharacterList(IReadOnlyList<string> names) {
        const int maxShown = 8;
        var shown = names.Take(maxShown).ToList();
        var lines = shown.Select(n => $"  • {n}").ToList();
        if (names.Count > maxShown)
            lines.Add(I18N.T("progressGuard.lossPrompt.charactersMore", "  … +{0} more", names.Count - maxShown));

        var header = I18N.T("progressGuard.lossPrompt.characters", "Affected characters:");
        return header + "\n" + string.Join("\n", lines);
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
}
