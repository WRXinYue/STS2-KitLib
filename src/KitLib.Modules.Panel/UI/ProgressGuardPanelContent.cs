using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Godot;
using KitLib.Integration;
using KitLib.Modding;
using KitLib.Progress;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Saves;

namespace KitLib.UI;

internal static class ProgressGuardPanelContent {
    internal static void BuildPanel(VBoxContainer vbox, NMainMenu? mainMenu) {
        vbox.AddChild(CreateSubtitleLabel());

        ProgressGuardPanelBuilder.AddToggleSection(vbox, includeSectionHeader: false);

        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        BuildStatusSection(vbox);

        vbox.AddChild(DevPanelUI.CreateOverlaySeparator());
        BuildBackupsSection(vbox, mainMenu);
    }

    static Label CreateSubtitleLabel() {
        var subtitle = new Label {
            Text = I18N.T("progressGuard.subtitle",
                "Backs up progress.save when the loaded mod set changes, before vanilla save filtering can wipe mod unlocks."),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return subtitle;
    }

    static void BuildStatusSection(VBoxContainer vbox) {
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

    static void BuildBackupsSection(VBoxContainer vbox, NMainMenu? mainMenu) {
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

    static Control BuildBackupRow(ProfileBackupSummary backup, int profileId, NMainMenu? mainMenu) {
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

        var canAct = backup.HasProgressSave && mainMenu != null;

        var detailsBtn = new Button {
            Text = I18N.T("progressGuard.detail.button", "Details"),
            FocusMode = Control.FocusModeEnum.All,
            Disabled = !canAct,
        };
        ModSettingsRitsuFormDevTheme.ApplyActionButton(detailsBtn);
        if (canAct)
            detailsBtn.Pressed += () => ProgressGuardBackupDetailUI.Show(mainMenu!, backup, profileId);
        row.AddChild(detailsBtn);

        var restoreBtn = new Button {
            Text = I18N.T("progressGuard.restore.button", "Restore"),
            FocusMode = Control.FocusModeEnum.All,
            Disabled = !canAct,
        };
        ModSettingsRitsuFormDevTheme.ApplyActionButton(restoreBtn);
        if (canAct)
            restoreBtn.Pressed += () => ProgressGuardUI.ShowRestoreConfirm(mainMenu!, backup, profileId);
        row.AddChild(restoreBtn);

        return row;
    }

    static Label MakeStatusLabel(string text) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        return label;
    }
}
