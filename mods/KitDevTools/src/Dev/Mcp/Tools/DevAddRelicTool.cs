using System.Text.Json;
using System.Text.Json.Nodes;
using KitLib.Actions;
using KitLib.RunInventory;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Mcp.Tools;

/// <summary>
/// Grants a relic to the local player — the relic counterpart of <see cref="DevAddCardTool"/>.
/// </summary>
/// <remarks>
/// <para>
/// Without this there was no way to build a scenario around a SPECIFIC relic. The bridge could set gold, HP,
/// energy and cheats and add any card, but a relic could only be obtained by playing until one dropped, which
/// is seed-dependent and cannot be aimed at a chosen relic. So every "what does this relic do next" behaviour
/// — reward rerolls, sacrifices, campfire draws — was unreachable without a human at the keyboard.
/// </para>
/// </remarks>
internal sealed class DevAddRelicTool : IMcpTool {
    public string Name => "dev_add_relic";

    public string Description =>
        "Grant a relic to the local player by model id (same API as the DevMode relic browser). "
        + "Pass 'search' instead to look an id up.";

    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "relic_id": {
                "type": "string",
                "description": "Relic model ID, e.g. DRIFTWOOD, PAELS_WING, DREAM_CATCHER."
            },
            "search": {
                "type": "string",
                "description": "List relic ids and rarities containing this text (case-insensitive) instead of granting. Empty string lists every relic."
            }
        }
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        // Lookup mode reads the model database only, so an id can be resolved from the main menu with no run.
        if (args.TryGetPropertyValue("search", out var searchNode)
            && searchNode?.GetValueKind() == JsonValueKind.String) {
            return Search(searchNode.GetValue<string>() ?? string.Empty);
        }

        if (!args.TryGetPropertyValue("relic_id", out var idNode)
            || idNode?.GetValueKind() != JsonValueKind.String
            || string.IsNullOrWhiteSpace(idNode.GetValue<string>())) {
            return DevCardMcpHelper.Fail("Missing or invalid relic_id (pass 'search' to look one up).");
        }

        var relicId = idNode.GetValue<string>()!.Trim();
        var result = await RunInventoryBridge.TryAddRelic(relicId);
        if (!result.Ok)
            return DevCardMcpHelper.Fail(result.Error ?? "Add relic failed.");

        var rarity = RelicActions.FindRelicById(result.ItemId ?? relicId)?.Rarity.ToString() ?? "";
        return new JsonObject {
            ["ok"] = true,
            ["relicId"] = result.ItemId ?? relicId,
            ["rarity"] = rarity,
        };
    }

    private static JsonNode Search(string needle) {
        var matches = new JsonArray();
        foreach (var relic in ModelDb.AllRelics) {
            var entry = ((AbstractModel)relic).Id.Entry;
            if (needle.Length > 0 && entry.IndexOf(needle, System.StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            matches.Add(new JsonObject {
                ["id"] = entry,
                ["rarity"] = relic.Rarity.ToString(),
            });
        }

        return new JsonObject {
            ["ok"] = true,
            ["count"] = matches.Count,
            ["matches"] = matches,
        };
    }
}
