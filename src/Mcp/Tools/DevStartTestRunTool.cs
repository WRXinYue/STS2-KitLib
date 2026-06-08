using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.UI;
using Godot;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Nodes.Screens.CharacterSelect;
using MegaCrit.Sts2.Core.Nodes.Screens.MainMenu;

namespace KitLib.Mcp.Tools;

internal sealed class DevStartTestRunTool : IMcpTool {
    public string Name => "dev_start_test_run";
    public string Description =>
        "Start a new DevMode test run from the main menu (opens character select). Optional seed override.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "seed": {
                "type": "string",
                "description": "Optional run seed override."
            }
        },
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        var mainMenu = NGame.Instance?.MainMenu;
        if (mainMenu == null || !GodotObject.IsInstanceValid(mainMenu)) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = false,
                ["error"] = "Main menu is not active. Return to the title screen first.",
            });
        }

        if (args.TryGetPropertyValue("seed", out var seedNode)
            && seedNode?.GetValueKind() == System.Text.Json.JsonValueKind.String) {
            var seed = seedNode.GetValue<string>()?.Trim();
            KitLibState.PendingRestartSeed = string.IsNullOrEmpty(seed) ? null : seed;
        }
        else {
            KitLibState.PendingRestartSeed = null;
        }

        KitLibState.InDevRun = true;

        if (DevMainMenuUI.IsVisible)
            DevMainMenuUI.Hide();

        var charSelect = mainMenu.SubmenuStack.GetSubmenuType<NCharacterSelectScreen>();
        charSelect.InitializeSingleplayer();
        mainMenu.SubmenuStack.Push(charSelect);

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["status"] = "character_select",
        });
    }
}
