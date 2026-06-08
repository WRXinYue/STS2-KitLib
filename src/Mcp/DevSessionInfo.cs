using System.Text.Json.Nodes;
using KitLib.AI;
using KitLib.AI.Core;
using KitLib.AI.Core.Schema;
using KitLib.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Mcp;

internal static class DevSessionInfo {
    public static JsonObject Capture() {
        var provider = AiPlayServices.StateProvider;
        var runActive = provider.IsRunActive;
        var phase = ResolvePhase(provider, runActive);

        var prompts = new JsonArray();
        if (CrashRecoveryPromptUI.IsVisible)
            prompts.Add("CrashRecovery");
        if (ProgressLossPromptUI.IsVisible)
            prompts.Add("ProgressLoss");

        return new JsonObject {
            ["runActive"] = runActive,
            ["phase"] = phase,
            ["inDevRun"] = KitLibState.InDevRun,
            ["blockingPrompts"] = prompts,
        };
    }

    private static string ResolvePhase(IGameStateProvider provider, bool runActive) {
        if (runActive)
            return provider.CurrentPhase.ToString();

        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu != null && GodotObject.IsInstanceValid(mainMenu)) {
            if (IsCharacterSelectOpen(mainMenu))
                return "CharacterSelect";
            return "MainMenu";
        }

        return GamePhase.None.ToString();
    }

    private static bool IsCharacterSelectOpen(NMainMenu mainMenu) {
        var charSelect = mainMenu.SubmenuStack?.GetSubmenuType<NCharacterSelectScreen>();
        return charSelect != null && charSelect.IsVisibleInTree() && charSelect.Visible;
    }
}
