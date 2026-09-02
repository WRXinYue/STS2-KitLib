using System.Text.Json.Nodes;
using Godot;
using KitLib.Host;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Mcp;

internal static class DevSessionInfo {
    public static JsonObject Capture() {
        var run = RunManager.Instance;
        var runActive = run?.IsInProgress == true;

        var prompts = new JsonArray();
        if (KitLibPanelOps.IsProgressLossPromptVisible?.Invoke() == true)
            prompts.Add("ProgressLoss");

        return new JsonObject {
            ["runActive"] = runActive,
            ["phase"] = ResolvePhase(runActive),
            ["inDevRun"] = KitLibState.InDevRun,
            ["blockingPrompts"] = prompts,
        };
    }

    static string ResolvePhase(bool runActive) {
        if (runActive)
            return "Run";

        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu)) {
            if (IsCharacterSelectOpen(mainMenu))
                return "CharacterSelect";
            return "MainMenu";
        }

        return "None";
    }

    static bool IsCharacterSelectOpen(NMainMenu mainMenu) {
        var charSelect = mainMenu.SubmenuStack?.GetSubmenuType<NCharacterSelectScreen>();
        return charSelect != null && charSelect.IsVisibleInTree() && charSelect.Visible;
    }
}
