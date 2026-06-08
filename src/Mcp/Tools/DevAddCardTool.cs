using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Mcp.Tools;

internal sealed class DevAddCardTool : IMcpTool {
    public string Name => "dev_add_card";
    public string Description =>
        "Add a card to deck or a combat pile (same API as DevMode card browser / dmcard console).";
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
        if (!DevCardMcpHelper.TryRequireRun(out var state, out var player, out var runError))
            return runError!;

        if (!args.TryGetPropertyValue("card_id", out var idNode)
            || idNode?.GetValueKind() != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(idNode.GetValue<string>())) {
            return DevCardMcpHelper.Fail("Missing or invalid card_id.");
        }

        var cardId = idNode.GetValue<string>()!.Trim();
        var card = CardActions.FindCardById(cardId);
        if (card == null)
            return DevCardMcpHelper.Fail($"Card not found: '{cardId}'.");

        var rawTarget = args.TryGetPropertyValue("target", out var targetNode)
            && targetNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? targetNode.GetValue<string>()
            : "hand";
        if (!DevCardMcpHelper.TryParseTarget(rawTarget, out var target, out var targetError))
            return DevCardMcpHelper.Fail(targetError);

        var rawDuration = args.TryGetPropertyValue("duration", out var durationNode)
            && durationNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? durationNode.GetValue<string>()
            : "perm";
        if (!DevCardMcpHelper.TryParseDuration(rawDuration, out var duration, out var durationError))
            return DevCardMcpHelper.Fail(durationError);

        var upgradeLevels = 0;
        if (args.TryGetPropertyValue("upgrade_levels", out var upgradeNode)
            && upgradeNode?.GetValueKind() == System.Text.Json.JsonValueKind.Number) {
            upgradeLevels = System.Math.Max(0, upgradeNode.GetValue<int>());
        }

        var request = new AddCardRequest {
            Target = target,
            Duration = duration,
            UpgradeLevelsToApply = upgradeLevels,
        };
        if (!CardActions.TryValidateAdd(state, player, card, request, out var validateError))
            return DevCardMcpHelper.Fail(validateError);

        if (MpCheatSession.InMultiplayerRun)
            return DevCardMcpHelper.Fail("Multiplayer add is not supported via MCP.");

        await CardActions.Add(state, player, card)
            .Target(target)
            .Duration(duration)
            .UpgradeLevels(upgradeLevels)
            .RunAsync();

        return new JsonObject {
            ["ok"] = true,
            ["cardId"] = ((AbstractModel)card).Id.Entry,
            ["target"] = rawTarget!.Trim().ToLowerInvariant(),
            ["duration"] = duration == EffectDuration.Permanent ? "perm" : "temp",
            ["upgradeLevels"] = upgradeLevels,
        };
    }
}
