using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI;
using KitLib.AI.Core.Schema;

namespace KitLib.Mcp.Tools;

internal sealed class MapActionTool : IMcpTool {
    public string Name => "map_action";
    public string Description =>
        "Execute a non-combat action: select map node, pick card reward, choose event option, interact with shop, rest, or proceed.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "action": {
                "type": "string",
                "enum": [
                    "select_map_node", "pick_card_reward", "skip_card_reward",
                    "select_event_choice", "purchase_shop_item", "remove_card_at_shop",
                    "leave_shop", "rest", "upgrade_card", "collect_reward",
                    "dismiss_rewards", "proceed"
                ],
                "description": "The action to perform."
            },
            "target_index": {
                "type": "integer",
                "description": "Index of the target (node, card, item, etc.)."
            }
        },
        "required": ["action"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        var actionStr = args["action"]?.GetValue<string>() ?? "";
        var targetIndex = args["target_index"]?.GetValue<int>() ?? 0;

        ActionType? actionType = actionStr switch {
            "select_map_node" => ActionType.SelectMapNode,
            "pick_card_reward" => ActionType.PickCardReward,
            "skip_card_reward" => ActionType.SkipCardReward,
            "select_event_choice" => ActionType.SelectEventChoice,
            "purchase_shop_item" => ActionType.PurchaseShopItem,
            "remove_card_at_shop" => ActionType.RemoveCardAtShop,
            "leave_shop" => ActionType.LeaveShop,
            "rest" => ActionType.Rest,
            "upgrade_card" => ActionType.UpgradeCard,
            "collect_reward" => ActionType.CollectReward,
            "dismiss_rewards" => ActionType.DismissRewards,
            "proceed" => ActionType.Proceed,
            _ => null,
        };

        if (actionType == null)
            return new JsonObject { ["error"] = $"Unknown action: {actionStr}" };

        var gameAction = new GameAction {
            Type = actionType.Value,
            TargetIndex = targetIndex,
            Reason = "MCP tool call",
        };

        var result = await AiPlayServices.ActionExecutor.ExecuteAsync(gameAction);
        return new JsonObject {
            ["success"] = result.Success,
            ["message"] = result.Message,
        };
    }
}
