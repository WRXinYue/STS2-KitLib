using KitLib.Settings;
using Godot;

namespace KitLib.UI;

internal static class CrashRecoveryPanelBuilder {
    internal static void AddToggleSection(VBoxContainer parent, bool includeSectionHeader) {
        if (includeSectionHeader)
            parent.AddChild(DevPanelUI.CreateSectionHeader(
                I18N.T("settings.section.crashRecovery", "Crash recovery")));

        parent.AddChild(DevPanelUI.CreateCheatToggle(
            I18N.T("crashRecovery.settings.prompt", "Prompt to export feedback on crash"),
            I18N.T("crashRecovery.settings.prompt.desc",
                "Show a dialog when an unhandled error occurs or the previous session exited abnormally"),
            () => SettingsStore.Current.PromptOnCrashFeedback,
            SettingsStore.SetPromptOnCrashFeedback));
    }
}
