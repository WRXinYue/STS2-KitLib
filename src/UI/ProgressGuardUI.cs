using System;
using System.Globalization;
using System.Linq;
using System.Text;
using KitLib.Modding;
using KitLib.Progress;
using Godot;
using MegaCrit.Sts2.Core.Saves;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ProgressGuardUI {
    private const string RootName = "KitLibProgressGuard";
    private const string RestoreConfirmName = "KitLibProgressRestoreConfirm";
    private const float PanelW = 560f;

    private static NMainMenu? _mainMenu;

    public static void ShowOnMainMenu(NMainMenu mainMenu) {
        _mainMenu = mainMenu;
        var parent = mainMenu.GetTree().Root;
        HideAnywhere();
        Action close = HideAnywhere;
        var (_, vbox) = DevMainMenuOverlay.Create(parent, RootName, PanelW, close, contentSeparation: 12);
        BuildPanel(vbox, mainMenu);
    }

    public static void HideAnywhere() {
        ProgressGuardBackupDetailUI.HideAnywhere();
        DevMainMenuOverlay.RemoveAnywhere(RestoreConfirmName);
        DevMainMenuOverlay.RemoveAnywhere(RootName);
        _mainMenu = null;
    }

    internal static void FinishRestore(
        NMainMenu mainMenu,
        string backupDir,
        string directoryName,
        int profileId) {
        DevMainMenuOverlay.RemoveAnywhere(RestoreConfirmName);

        var result = ProfileProgressBackupService.TryRestoreProgress(backupDir, profileId);
        MainFile.Logger.Info($"[ProgressGuard] Restore finished: {result}");

        switch (result) {
            case ProgressRestoreResult.Success:
                MainFile.Logger.Info(I18N.T("progressGuard.restore.success",
                    "Progress restored from {0}.", directoryName));
                ModCharacterProgressLossDetector.ClearPending();
                if (GodotObject.IsInstanceValid(mainMenu))
                    mainMenu.RefreshButtons();
                HideAnywhere();
                break;
            case ProgressRestoreResult.BlockedRunInProgress:
                MainFile.Logger.Warn(I18N.T("progressGuard.restore.blockedRun",
                    "Cannot restore progress while a run is in progress. Return to the main menu first."));
                break;
            default:
                MainFile.Logger.Warn(I18N.T("progressGuard.restore.failed",
                    "Failed to restore progress from {0}.", directoryName));
                break;
        }
    }

    private static void Remove(Node attachRoot) => HideAnywhere();

    private static void BuildPanel(VBoxContainer vbox, NMainMenu mainMenu) {
        var titleBox = new VBoxContainer();
        titleBox.AddThemeConstantOverride("separation", 4);
        titleBox.AddChild(DevPanelUI.CreatePanelTitle(
            I18N.T("settings.section.progressGuard", "Progress protection")));
        var subtitle = new Label {
            Text = I18N.T("progressGuard.subtitle",
                "Backs up progress.save when the loaded mod set changes, before vanilla save filtering can wipe mod unlocks."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        titleBox.AddChild(subtitle);
        vbox.AddChild(titleBox);
        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());

        ProgressGuardPanelBuilder.AddToggleSection(vbox, includeSectionHeader: false);

        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        BuildStatusSection(vbox);

        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        BuildBackupsSection(vbox, mainMenu);
    }

    private static void BuildStatusSection(VBoxContainer vbox) {
        vbox.AddChild(DevPanelUI.CreateSectionHeader(I18N.T("progressGuard.status.title", "Status")));

        var mods = ModRuntime.Catalog.GetSnapshot();
        var currentHash = ModSetFingerprintStore.ComputeHash(mods);
        var stored = ModSetFingerprintStore.Load();

        vbox.AddChild(MakeStatusLabel(I18N.T("progressGuard.status.mods", "Loaded mods: {0}", mods.Count)));

        if (stored == null) {
            vbox.AddChild(MakeStatusLabel(I18N.T("progressGuard.status.none", "No fingerprint saved yet")));
            return;
        }

        var hashShort = stored.Hash.Length >= 8 ? stored.Hash[..8] : stored.Hash;
        var stamp = stored.UtcTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        vbox.AddChild(MakeStatusLabel(
            I18N.T("progressGuard.status.fingerprint", "Last fingerprint: {0} ({1})", stamp, hashShort)));

        var matches = string.Equals(stored.Hash, currentHash, StringComparison.Ordinal);
        var matchKey = matches
            ? "progressGuard.status.match"
            : "progressGuard.status.changed";
        var matchFallback = matches
            ? "Matches current mod set"
            : "Mod set changed since last launch";
        var matchLabel = MakeStatusLabel(I18N.T(matchKey, matchFallback));
        matchLabel.AddThemeColorOverride("font_color", matches ? KitLibTheme.Subtle : KitLibTheme.Accent);
        vbox.AddChild(matchLabel);
    }

    private static void BuildBackupsSection(VBoxContainer vbox, NMainMenu mainMenu) {
        int profileId;
        try {
            profileId = SaveManager.Instance.CurrentProfileId;
        }
        catch {
            profileId = 1;
        }

        vbox.AddChild(DevPanelUI.CreateSectionHeader(
            I18N.T("progressGuard.backups.title", "Recent backups (profile {0})", profileId)));

        var backups = ProfileProgressBackupService.ListRecentBackups(profileId);
        if (backups.Count == 0) {
            vbox.AddChild(MakeStatusLabel(I18N.T("progressGuard.backups.empty", "No backups yet")));
            return;
        }

        foreach (var backup in backups)
            vbox.AddChild(BuildBackupRow(backup, profileId, mainMenu));
    }

    private static Control BuildBackupRow(ProfileBackupSummary backup, int profileId, NMainMenu mainMenu) {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);
        row.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;

        var time = backup.UtcTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        var suffix = backup.HasProgressSave ? "" : " (no progress.save)";

        var label = new Label {
            Text = I18N.T("progressGuard.backups.item", "{0} — {1}", backup.DirectoryName, time + suffix),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        row.AddChild(label);

        var detailsBtn = new Button {
            Text = I18N.T("progressGuard.detail.button", "Details"),
            FocusMode = Control.FocusModeEnum.None,
        };
        detailsBtn.Disabled = !backup.HasProgressSave;
        detailsBtn.Pressed += () => ProgressGuardBackupDetailUI.Show(mainMenu, backup, profileId);
        row.AddChild(detailsBtn);

        var restoreBtn = new Button {
            Text = I18N.T("progressGuard.restore.button", "Restore"),
            FocusMode = Control.FocusModeEnum.None,
        };
        restoreBtn.Disabled = !backup.HasProgressSave;
        restoreBtn.Pressed += () => ShowRestoreConfirm(mainMenu, backup, profileId);
        row.AddChild(restoreBtn);

        return row;
    }

    internal static void ShowRestoreConfirm(NMainMenu mainMenu, ProfileBackupSummary backup, int profileId) {
        var root = mainMenu.GetTree().Root;
        DevMainMenuOverlay.RemoveAnywhere(RestoreConfirmName);

        var overlay = new Control {
            Name = RestoreConfirmName,
            MouseFilter = Control.MouseFilterEnum.Stop,
            ZIndex = 2100,
        };
        overlay.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        var backdrop = new ColorRect {
            Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop,
        };
        backdrop.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        backdrop.GuiInput += e => {
            if (e is InputEventMouseButton { Pressed: true })
                Callable.From(() => DevMainMenuOverlay.RemoveAnywhere(RestoreConfirmName)).CallDeferred();
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
            Text = I18N.T("progressGuard.restore.confirmTitle", "Restore progress.save?"),
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

        var meta = ProfileProgressBackupService.TryLoadMeta(backup.BackupDirectory);
        var currentHash = ModSetFingerprintStore.ComputeHash(ModRuntime.Catalog.GetSnapshot());
        var time = backup.UtcTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);

        var body = new StringBuilder();
        body.AppendLine(I18N.T("progressGuard.restore.confirmBody",
            "This replaces the active profile's progress.save and reloads progress in memory. An in-progress run is not affected."));
        body.AppendLine();
        body.AppendLine(I18N.T("progressGuard.restore.confirmBackup",
            "Backup: {0} (profile {1}, {2})", backup.DirectoryName, profileId, time));

        if (meta != null
            && !string.IsNullOrEmpty(meta.FingerprintHash)
            && !string.Equals(meta.FingerprintHash, currentHash, StringComparison.Ordinal)) {
            body.AppendLine();
            body.AppendLine(I18N.T("progressGuard.restore.confirmWarnMods",
                "Warning: the current mod set differs from when this backup was taken. Restore matching mods first or mod progress may be filtered again on load."));
            if (meta.Mods.Count > 0) {
                body.AppendLine();
                foreach (var mod in meta.Mods.Take(12))
                    body.AppendLine($"  • {mod.Id} v{mod.Version}");
                if (meta.Mods.Count > 12)
                    body.AppendLine($"  … +{meta.Mods.Count - 12}");
            }
        }

        var bodyLabel = new Label {
            Text = body.ToString().TrimEnd(),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        bodyLabel.AddThemeFontSizeOverride("font_size", 12);
        bodyLabel.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        vbox.AddChild(bodyLabel);

        var btnRow = new HBoxContainer();
        btnRow.AddThemeConstantOverride("separation", 10);

        var cancelBtn = new Button {
            Text = I18N.T("restart.cancel", "Cancel"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        cancelBtn.Pressed += () =>
            Callable.From(() => DevMainMenuOverlay.RemoveAnywhere(RestoreConfirmName)).CallDeferred();
        btnRow.AddChild(cancelBtn);

        var confirmBtn = new Button {
            Text = I18N.T("progressGuard.restore.button", "Restore"),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            FocusMode = Control.FocusModeEnum.None,
        };
        confirmBtn.Pressed += () => {
            confirmBtn.Disabled = true;
            cancelBtn.Disabled = true;
            var backupDir = backup.BackupDirectory;
            var dirName = backup.DirectoryName;
            Callable.From(() => FinishRestore(mainMenu, backupDir, dirName, profileId)).CallDeferred();
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

    private static Label MakeStatusLabel(string text) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        return label;
    }
}
