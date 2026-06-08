using System;
using System.Globalization;
using System.Linq;
using System.Text;
using KitLib.Modding;
using KitLib.Progress;
using Godot;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.UI;

internal static class ProgressGuardBackupDetailUI {
    private const string RootName = "KitLibProgressBackupDetail";

    public static void Show(NMainMenu mainMenu, ProfileBackupSummary backup, int profileId) {
        var root = mainMenu.GetTree().Root;
        HideAnywhere();

        var details = ProgressBackupInspector.Inspect(backup.BackupDirectory);

        var overlay = new Control {
            Name = RootName,
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
                Callable.From(HideAnywhere).CallDeferred();
        };
        overlay.AddChild(backdrop);

        var wrapper = new CenterContainer();
        wrapper.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        overlay.AddChild(wrapper);

        var panel = new PanelContainer {
            CustomMinimumSize = new Vector2(640, 0),
        };
        panel.AddThemeStyleboxOverride("panel", CreatePanelStyle());
        wrapper.AddChild(panel);

        var outer = new VBoxContainer();
        outer.AddThemeConstantOverride("separation", 10);

        var title = new Label {
            Text = I18N.T("progressGuard.detail.title", "Backup details"),
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        title.AddThemeFontSizeOverride("font_size", 16);
        title.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        outer.AddChild(title);

        var time = backup.UtcTimestamp.ToUniversalTime().ToString("u", CultureInfo.InvariantCulture);
        var subtitle = new Label {
            Text = I18N.T("progressGuard.detail.subtitle", "{0} — profile {1}, {2}",
                backup.DirectoryName, profileId, time),
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        subtitle.AddThemeFontSizeOverride("font_size", 11);
        subtitle.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        outer.AddChild(subtitle);

        outer.AddChild(new ColorRect {
            Color = KitLibTheme.Separator,
            CustomMinimumSize = new Vector2(0, 1),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        });

        var scroll = new ScrollContainer {
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            CustomMinimumSize = new Vector2(0, 420),
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
        };

        var content = new VBoxContainer {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };
        content.AddThemeConstantOverride("separation", 6);
        BuildDetailContent(content, details, backup, profileId, mainMenu);
        scroll.AddChild(content);
        outer.AddChild(scroll);

        var closeBtn = new Button {
            Text = I18N.T("progressGuard.detail.close", "Close"),
            FocusMode = Control.FocusModeEnum.None,
        };
        closeBtn.Pressed += () => Callable.From(HideAnywhere).CallDeferred();
        outer.AddChild(closeBtn);

        panel.AddChild(outer);
        root.AddChild(overlay);
        closeBtn.GrabFocus();
    }

    public static void HideAnywhere() => DevMainMenuOverlay.RemoveAnywhere(RootName);

    private static void BuildDetailContent(
        VBoxContainer content,
        ProgressBackupDetails details,
        ProfileBackupSummary backup,
        int profileId,
        NMainMenu mainMenu) {
        if (details.LoadFailed) {
            content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.loadFailed",
                "Could not read progress.save: {0}", details.LoadError ?? "?")));
            AppendMetaSection(content, details.Meta);
            return;
        }

        AppendMetaSection(content, details.Meta);

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.account", "Account progress")));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.uniqueId",
            "Save ID: {0}", string.IsNullOrEmpty(details.UniqueId) ? "?" : details.UniqueId)));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.schema",
            "Schema version: {0}", details.SchemaVersion)));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.playtime",
            "Total playtime: {0}", ProgressBackupInspector.FormatPlaytime(details.TotalPlaytime))));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.unlocks",
            "Total unlocks: {0} | Score: {1} | Floors: {2}",
            details.TotalUnlocks, details.CurrentScore, details.FloorsClimbed)));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.runs",
            "Runs: {0}W / {1}L | Max co-op ascension: {2}",
            details.TotalWins, details.TotalLosses, details.MaxMultiplayerAscension)));

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.characters", "Characters")));
        if (details.Characters.Count == 0) {
            content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.characters.empty", "No character stats.")));
        }
        else {
            foreach (var ch in details.Characters) {
                content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.character.line",
                    "{0}: A{1} (pref A{2}), {3}W/{4}L, playtime {5}, streak {6}/{7}",
                    ch.CharacterName,
                    ch.MaxAscension,
                    ch.PreferredAscension,
                    ch.Wins,
                    ch.Losses,
                    ProgressBackupInspector.FormatPlaytime(ch.Playtime),
                    ch.CurrentWinStreak,
                    ch.BestWinStreak)));
            }
        }

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.timeline", "Timeline (epochs)")));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.epochs.summary",
            "Total: {0} | Revealed: {1} | Obtained: {2}",
            details.EpochTotal, details.EpochRevealed, details.EpochObtained)));

        if (details.ModEpochIds.Count > 0) {
            content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.epochs.mod",
                "Mod-related epochs ({0}):", details.ModEpochIds.Count)));
            foreach (var line in details.ModEpochIds)
                content.AddChild(MakeBodyLabel($"  • {line}"));
        }

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.compendium", "Compendium")));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.compendium",
            "Cards {0} | Relics {1} | Potions {2} | Events {3} | Acts {4}",
            details.DiscoveredCards,
            details.DiscoveredRelics,
            details.DiscoveredPotions,
            details.DiscoveredEvents,
            details.DiscoveredActs)));

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.files", "Bundled files")));
        var files = new StringBuilder();
        files.Append("progress.save");
        if (details.HasPrefs) files.Append(", prefs.save");
        if (details.HasCurrentRun) files.Append(", current_run.save");
        content.AddChild(MakeBodyLabel(files.ToString()));

        var restoreBtn = new Button {
            Text = I18N.T("progressGuard.restore.button", "Restore"),
            FocusMode = Control.FocusModeEnum.None,
            Disabled = !backup.HasProgressSave,
        };
        restoreBtn.Pressed += () => {
            Callable.From(() => {
                HideAnywhere();
                ProgressGuardUI.ShowRestoreConfirm(mainMenu, backup, profileId);
            }).CallDeferred();
        };
        content.AddChild(restoreBtn);
    }

    private static void AppendMetaSection(VBoxContainer content, ProfileBackupMeta? meta) {
        if (meta == null)
            return;

        content.AddChild(MakeSectionHeader(I18N.T("progressGuard.detail.section.meta", "Backup metadata")));
        content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.meta.trigger",
            "Trigger: {0}", meta.TriggerReason)));
        if (!string.IsNullOrEmpty(meta.FingerprintHash)) {
            var hashShort = meta.FingerprintHash.Length >= 8
                ? meta.FingerprintHash[..8]
                : meta.FingerprintHash;
            content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.meta.fingerprint",
                "Mod fingerprint: {0}", hashShort)));
        }

        if (meta.Mods.Count > 0) {
            content.AddChild(MakeBodyLabel(I18N.T("progressGuard.detail.meta.mods",
                "Mods at backup ({0}):", meta.Mods.Count)));
            foreach (var mod in meta.Mods.Take(16))
                content.AddChild(MakeBodyLabel($"  • {mod.Id} v{mod.Version}"));
            if (meta.Mods.Count > 16)
                content.AddChild(MakeBodyLabel($"  … +{meta.Mods.Count - 16}"));
        }
    }

    private static Label MakeSectionHeader(string text) {
        var label = new Label { Text = text };
        label.AddThemeFontSizeOverride("font_size", 12);
        label.AddThemeColorOverride("font_color", KitLibTheme.Accent);
        return label;
    }

    private static Label MakeBodyLabel(string text) {
        var label = new Label {
            Text = text,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", KitLibTheme.TextPrimary);
        return label;
    }

    private static StyleBoxFlat CreatePanelStyle() => new() {
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
