using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;

namespace KitLib.Mcp.Tools;

internal sealed class DevListCardsTool : IMcpTool {
    public string Name => "dev_list_cards";
    public string Description =>
        "List cards in a DevMode pile (deck, hand, draw, discard, exhaust) or all piles.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "target": {
                "type": "string",
                "description": "Pile to list: deck, hand, draw, discard, exhaust, or all (default all).",
                "default": "all"
            }
        },
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!DevCardMcpHelper.TryRequireRun(out var state, out var player, out var runError))
            return Task.FromResult<JsonNode>(runError!);

        var rawTarget = args.TryGetPropertyValue("target", out var targetNode)
            && targetNode?.GetValueKind() == System.Text.Json.JsonValueKind.String
            ? targetNode.GetValue<string>()
            : "all";

        if (string.Equals(rawTarget, "all", System.StringComparison.OrdinalIgnoreCase)) {
            var piles = new JsonObject();
            foreach (var (name, pile) in AllPiles()) {
                var cards = CardActions.GetCardsForTarget(player, pile);
                piles[name] = DevCardMcpHelper.SerializeCards(cards);
            }
            return Task.FromResult<JsonNode>(new JsonObject {
                ["ok"] = true,
                ["piles"] = piles,
            });
        }

        if (!DevCardMcpHelper.TryParseTarget(rawTarget, out var target, out var error))
            return Task.FromResult<JsonNode>(DevCardMcpHelper.Fail(error));

        var list = CardActions.GetCardsForTarget(player, target);
        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["target"] = rawTarget!.Trim().ToLowerInvariant(),
            ["cards"] = DevCardMcpHelper.SerializeCards(list),
        });
    }

    private static IEnumerable<(string Name, CardTarget Target)> AllPiles() {
        yield return ("deck", CardTarget.Deck);
        yield return ("hand", CardTarget.Hand);
        yield return ("draw", CardTarget.DrawPile);
        yield return ("discard", CardTarget.DiscardPile);
        yield return ("exhaust", CardTarget.ExhaustPile);
    }
}
