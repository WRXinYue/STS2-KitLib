using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.Sts2.Mcp;

namespace KitLib.Mcp.Tools;

internal sealed class GetSelectionStateTool : IMcpTool {
    public string Name => "get_selection_state";
    public string Description =>
        "Get the active in-combat card selection UI (discard/draw/exhaust pile pick, hand select, etc.). " +
        "Returns options with index, card id, name, and cost when a selection screen is open.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) =>
        Task.FromResult<JsonNode>(McpCardSelectionHelper.CaptureState());
}
