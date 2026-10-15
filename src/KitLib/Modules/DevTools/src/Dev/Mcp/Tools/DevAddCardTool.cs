using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Abstractions.Host;
using KitLib.RunInventory;

namespace KitLib.Mcp.Tools;

internal sealed class DevAddCardTool : IMcpTool {
    public string Name => "dev_add_card";
    public string Description =>
        "Add a card to deck or a combat pile (same API as DevMode card browser).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "card_id": {
                "type": "string",
                "description": "Card model ID, e.g. IRONCLAD_CARD_STRIKE."
            },
            "target": {
                "type": "string",
                "description": "deck, hand, draw, discard, or exhaust (default hand).",
                "default": "hand"
            },
            "duration": {
                "type": "string",
                "description": "perm or temp (default perm). Combat piles only.",
                "default": "perm"
            },
            "upgrade_levels": {
                "type": "integer",
                "description": "Upgrade levels to apply on spawn (default 0).",
                "default": 0
            }
        },
        "required": ["card_id"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!args.TryGetPropertyValue("card_id", out var idNode)
            || idNode?.GetValueKind() != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(idNode.GetValue<string>())) {
            return DevCardMcpHelper.Fail("Missing or invalid card_id.");
        }

        var cardId = idNode.GetValue<string>()!.Trim();
        var rawTarget = args.TryGetPropertyValue("target", out var targetNode)
            && targetNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? targetNode.GetValue<string>()
            : "hand";
        if (!DevCardMcpHelper.TryParseApiPile(rawTarget, out var pile, out var targetError))
            return DevCardMcpHelper.Fail(targetError);

        var rawDuration = args.TryGetPropertyValue("duration", out var durationNode)
            && durationNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? durationNode.GetValue<string>()
            : "perm";
        if (!DevCardMcpHelper.TryParseApiDuration(rawDuration, out var duration, out var durationError))
            return DevCardMcpHelper.Fail(durationError);

        var upgradeLevels = 0;
        if (args.TryGetPropertyValue("upgrade_levels", out var upgradeNode)
            && upgradeNode?.GetValueKind() == System.Text.Json.JsonValueKind.Number) {
            upgradeLevels = System.Math.Max(0, upgradeNode.GetValue<int>());
        }

        var result = await RunInventoryBridge.TryAddCard(new KitLibAddCardRequest(
            cardId, pile, duration, upgradeLevels));
        if (!result.Ok)
            return DevCardMcpHelper.Fail(result.Error ?? "Add card failed.");

        return new JsonObject {
            ["ok"] = true,
            ["cardId"] = result.ItemId ?? cardId,
            ["target"] = rawTarget!.Trim().ToLowerInvariant(),
            ["duration"] = duration == KitLibCardDuration.Permanent ? "perm" : "temp",
            ["upgradeLevels"] = upgradeLevels,
        };
    }
}
