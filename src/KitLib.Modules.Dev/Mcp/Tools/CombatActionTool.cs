using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.Core.Schema;
using KitLib.AI.Sts2.Mcp;
using KitLib.AI.Sts2.Snapshots;
using KitLib.Host;

namespace KitLib.Mcp.Tools;

internal sealed class CombatActionTool : IMcpTool {
    public string Name => "combat_action";
    public string Description =>
        "Execute a combat action in STS2: play a card from hand or end the turn. " +
        "play_card may return pendingSelection + selectionState when a pile/hand picker opens; " +
        "follow with selection_action or pass selection_card_id on play_card for one-shot auto-pick.";
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
            },
            "selection_card_id": {
                "type": "string",
                "description": "For play_card: auto-pick this card id when a selection screen opens during the same call."
            },
            "selection_index": {
                "type": "integer",
                "description": "For play_card: auto-pick this option index when a selection screen opens."
            }
        },
        "required": ["action"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        var actionStr = args["action"]?.GetValue<string>() ?? "";
        var cardIndex = args["card_index"]?.GetValue<int>() ?? 0;
        var targetIndex = args["target_index"]?.GetValue<int>() ?? -1;

        McpPlayContext.Clear();
        if (actionStr == "play_card") {
            McpPlayContext.SelectionCardId = args["selection_card_id"]?.GetValue<string>()?.Trim();
            if (args.TryGetPropertyValue("selection_index", out var selectionIndexNode))
                McpPlayContext.SelectionIndex = selectionIndexNode?.GetValue<int>();
        }

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

        ActionResult result;
        try {
            result = await AiPlayServices.ActionExecutor.ExecuteAsync(gameAction);
        }
        finally {
            McpPlayContext.Clear();
        }

        var response = new JsonObject {
            ["success"] = result.Success,
            ["message"] = result.Message,
        };

        McpCombatActionExtensions.ApplyPendingSelection(response, actionStr, result);

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
