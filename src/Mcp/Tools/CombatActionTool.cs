using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Snapshots;

namespace KitLib.Mcp.Tools;

internal sealed class CombatActionTool : IMcpTool {
    public string Name => "combat_action";
    public string Description =>
        "Execute a combat action in STS2: play a card from hand or end the turn. " +
        "play_card success includes afterState (playerPowers + enemies) unless queued in pseudo-coop.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "action": {
                "type": "string",
                "enum": ["play_card", "end_turn", "use_potion"],
                "description": "The combat action to perform."
            },
            "card_index": {
                "type": "integer",
                "description": "Index of the card in hand to play (for play_card)."
            },
            "target_index": {
                "type": "integer",
                "description": "Index of the enemy to target (for targeted cards). -1 for untargeted."
            }
        },
        "required": ["action"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        var actionStr = args["action"]?.GetValue<string>() ?? "";
        var cardIndex = args["card_index"]?.GetValue<int>() ?? 0;
        var targetIndex = args["target_index"]?.GetValue<int>() ?? -1;

        var gameAction = actionStr switch {
            "play_card" => new GameAction {
                Type = ActionType.PlayCard,
                TargetIndex = cardIndex,
                SecondaryIndex = targetIndex,
                Reason = "MCP tool call",
            },
            "end_turn" => new GameAction {
                Type = ActionType.EndTurn,
                Reason = "MCP tool call",
            },
            "use_potion" => new GameAction {
                Type = ActionType.UsePotion,
                TargetIndex = cardIndex,
                SecondaryIndex = targetIndex,
                Reason = "MCP tool call",
            },
            _ => null,
        };

        if (gameAction == null)
            return new JsonObject { ["error"] = $"Unknown action: {actionStr}" };

        var result = await AiPlayServices.ActionExecutor.ExecuteAsync(gameAction);

        var response = new JsonObject {
            ["success"] = result.Success,
            ["message"] = result.Message,
        };

        if (actionStr == "play_card" && result.Success) {
            if (result.Message?.Contains("Queued play", StringComparison.OrdinalIgnoreCase) == true) {
                response["queued"] = true;
            }
            else if (RunContext.TryGetRunAndPlayer(out _, out var player)) {
                response["afterState"] = GameSnapshot.CaptureCombatAfterState(player);
            }
        }

        return response;
    }
}
