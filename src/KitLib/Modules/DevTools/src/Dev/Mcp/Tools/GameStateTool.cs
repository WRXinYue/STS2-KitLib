using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Host;

namespace KitLib.Mcp.Tools;

internal sealed class GameStateTool : IMcpTool {
    public string Name => "get_game_state";
    public string Description =>
        "Get the current STS2 game state: phase, HP, gold, combat (hand, energy, enemies) and the " +
        "phase-specific panels (mapNodes, offeredCards, shopOffers, restOptions, ...). " +
        "deck/relics/potions are omitted unless requested with sections, which keeps each poll small.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "sections": {
                "type": "string",
                "description": "Extra sections to include: deck, relics, potions (comma-separated), or 'full' for all."
            }
        },
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        var snapshot = KitLibGameOps.Snapshot();
        if (snapshot is not { Count: > 0 }) {
            return Task.FromResult<JsonNode>(new JsonObject {
                ["error"] = "No active run.",
            });
        }

        TrimSections(snapshot, args["sections"]?.GetValue<string>());
        return Task.FromResult<JsonNode>(snapshot);
    }

    // The deck alone is twenty cards of JSON, and state is polled far more often than the deck changes;
    // callers that need it ask for it, so every other call carries a fraction of the payload.
    private static void TrimSections(JsonObject snapshot, string? raw) {
        var sections = (raw ?? "").Trim().ToLowerInvariant();
        if (sections is "full" or "*")
            return;

        var keep = new HashSet<string>(
            sections.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
        foreach (var section in new[] { "deck", "relics", "potions" }) {
            if (!keep.Contains(section))
                snapshot.Remove(section);
        }
    }
}
