using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.Sts2.Mcp;

namespace KitLib.Mcp.Tools;

internal sealed class SelectionActionTool : IMcpTool {
    public string Name => "selection_action";
    public string Description =>
        "Pick card(s) on the active selection screen. Use get_selection_state to list options first. " +
        "Supports combat pile picks (e.g. Renew and Replace choosing from discard), hand multi-select, and confirm.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "card_index": {
                "type": "integer",
                "description": "Single option index from get_selection_state.options."
            },
            "card_indices": {
                "type": "array",
                "items": { "type": "integer" },
                "description": "Multiple option indices for multi-select prompts."
            },
            "card_id": {
                "type": "string",
                "description": "Pick the first visible option matching this card model id."
            },
            "confirm": {
                "type": "boolean",
                "description": "Click confirm/proceed when enabled (default true)."
            }
        },
        "required": []
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) =>
        await McpCardSelectionHelper.PickAsync(args);
}
