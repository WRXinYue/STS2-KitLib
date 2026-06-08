using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI;

namespace KitLib.Mcp.Tools;

internal sealed class GameStateTool : IMcpTool {
    public string Name => "get_game_state";
    public string Description =>
        "Get the current STS2 game state including HP, gold, deck, relics, and combat info. " +
        "In combat, includes combat.playerPowers (id, modelId, amount), combat.phase, and enemies with index.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        var snapshot = AiPlayServices.StateProvider.TakeSnapshot();
        if (snapshot.Count == 0) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["error"] = "No active run.",
            });
        }
        return Task.FromResult<JsonNode>(snapshot);
    }
}
