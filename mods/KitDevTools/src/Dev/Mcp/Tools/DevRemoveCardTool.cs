using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

namespace KitLib.Mcp.Tools;

internal sealed class DevRemoveCardTool : IMcpTool {
    public string Name => "dev_remove_card";
    public string Description =>
        "Remove a card from a pile by card_id or pile_index (DevMode card browser remove).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "target": {
                "type": "string",
                "description": "deck, hand, draw, discard, or exhaust (default hand).",
                "default": "hand"
            },
            "card_id": {
                "type": "string",
                "description": "Card model ID in the pile (first match)."
            },
            "pile_index": {
                "type": "integer",
                "description": "Index within the pile (from dev_list_cards)."
            },
            "permanent": {
                "type": "boolean",
                "description": "Also remove from run deck when deleting from combat piles (default true).",
                "default": true
            }
        },
        "required": []
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        var rawTarget = args.TryGetPropertyValue("target", out var targetNode)
            && targetNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? targetNode.GetValue<string>()
            : "hand";
        if (!DevCardMcpHelper.TryParseApiPile(rawTarget, out var pile, out var targetError))
            return DevCardMcpHelper.Fail(targetError);

        string? cardId = null;
        if (args.TryGetPropertyValue("card_id", out var idNode)
            && idNode?.GetValueKind() == System.Text.Json.JsonValueKind.String) {
            cardId = idNode.GetValue<string>()?.Trim();
        }

        int? pileIndex = null;
        if (args.TryGetPropertyValue("pile_index", out var indexNode)
            && indexNode?.GetValueKind() == System.Text.Json.JsonValueKind.Number) {
            pileIndex = indexNode.GetValue<int>();
        }

        var permanent = true;
        if (args.TryGetPropertyValue("permanent", out var permNode)
            && permNode?.GetValueKind() is System.Text.Json.JsonValueKind.True
                or System.Text.Json.JsonValueKind.False) {
            permanent = permNode.GetValue<bool>();
        }

        var result = await RunInventoryBridge.TryRemoveCard(new KitLibRemoveCardRequest(
            pile, cardId, pileIndex, permanent));
        if (!result.Ok)
            return DevCardMcpHelper.Fail(result.Error ?? "Remove card failed.");

        return new JsonObject {
            ["ok"] = true,
            ["cardId"] = result.ItemId ?? cardId ?? "",
            ["target"] = rawTarget!.Trim().ToLowerInvariant(),
            ["permanent"] = permanent,
        };
    }
}
