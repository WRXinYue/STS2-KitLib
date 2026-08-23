using System.Globalization;
using Godot;
using KitLib.Abstractions.Host;
using KitLib.Abstractions.Modding;
using KitLib.Modding;
using KitLib.Replay;

namespace KitLib.UI;

internal static class DevToolsModSettingsPage {
    internal static void Register() {
        KitLibModSettingsRegistry.Register(new KitLibModSettingsPageRegistration {
            ModId = KitLibProductIds.KitDevTools,
            PageId = "general",
            Title = "General",
            TitleKey = "devtools.settings.page.general",
            SortOrder = 0,
            BuildBody = Build,
        });
    }

    static object Build() {
        if (!ModSettingsUi.IsAvailable) {
            return new Label {
                Text = I18N.T("devtools.settings.modPanelRequired",
                    "KitModPanel is required for these settings."),
                AutowrapMode = TextServer.AutowrapMode.WordSmart,
            };
        }

        var root = ModSettingsUi.CreatePageStack();
        root.AddChild(ModSettingsUi.CreateStringField(
            I18N.T("devtools.settings.runReplayKeep.title", "DevTools replays to keep"),
            I18N.T("devtools.settings.runReplayKeep.desc",
                "How many newest DevTools replay files to keep. Older files under KitLib/run-replays are deleted. Default 5."),
            () => DevToolsSettings.RunReplayKeepCount.ToString(CultureInfo.InvariantCulture),
            ApplyKeepCount));
        return root;
    }

    static void ApplyKeepCount(string text) {
        if (!int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int count))
            return;
        DevToolsSettings.SetRunReplayKeepCount(count);
    }
}
