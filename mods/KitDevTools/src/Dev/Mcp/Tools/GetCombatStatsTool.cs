using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.CombatStats;

namespace KitLib.Mcp.Tools;

internal sealed class GetCombatStatsTool : IMcpTool {
    public string Name => "get_combat_stats";
    public string Description =>
        "Get combat stats timeline for the current or last fight: events with source attribution, " +
        "creature snapshots. Use since_sequence after combat_action to read only new events.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "since_sequence": {
                "type": "integer",
                "description": "Return events with sequence greater than this (0 = from start)."
            },
            "max_events": {
                "type": "integer",
                "description": "Max events to return (default 200)."
            },
            "turn": {
                "type": "integer",
                "description": "Optional turn filter."
            }
        },
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        int sinceSequence = args["since_sequence"]?.GetValue<int>() ?? 0;
        int maxEvents = args["max_events"]?.GetValue<int>() ?? 200;
        int? turn = args["turn"] != null ? args["turn"]!.GetValue<int>() : null;

        return Task.FromResult<JsonNode>(CombatStatsMcpExport.Capture(sinceSequence, maxEvents, turn));
    }
}
