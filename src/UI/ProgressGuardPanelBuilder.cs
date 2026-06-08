using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal static class ProgressGuardPanelBuilder {
    internal static void AddToggleSection(VBoxContainer parent, bool includeSectionHeader) {
        if (includeSectionHeader)
            parent.AddChild(DevPanelUI.CreateSectionHeader(
                I18N.T("settings.section.progressGuard", "Progress protection")));

        parent.AddChild(DevPanelUI.CreateCheatToggle(
            I18N.T("settings.autoBackupProgress", "Auto-backup progress on mod change"),
            I18N.T("settings.autoBackupProgress.desc",
                "When the loaded mod set changes, copy progress.save before the game can write"),
            () => SettingsStore.Current.AutoBackupProgressOnModChange,
            SettingsStore.SetAutoBackupProgressOnModChange));

        parent.AddChild(DevPanelUI.CreateCheatToggle(
            I18N.T("settings.warnRemovedModProgress", "Warn on removed-mod progress residue"),
            I18N.T("settings.warnRemovedModProgress.desc",
                "Log a warning if progress.save still references mods that are no longer loaded"),
            () => SettingsStore.Current.WarnOnRemovedModProgressResidue,
            SettingsStore.SetWarnOnRemovedModProgressResidue));

        parent.AddChild(DevPanelUI.CreateCheatToggle(
            I18N.T("settings.promptModCharacterProgressLoss", "Prompt on mod character progress loss"),
            I18N.T("settings.promptModCharacterProgressLoss.desc",
                "On startup, offer to restore from backup when mod character stats were filtered on load"),
            () => SettingsStore.Current.PromptOnModCharacterProgressLoss,
            SettingsStore.SetPromptOnModCharacterProgressLoss));
    }
}
