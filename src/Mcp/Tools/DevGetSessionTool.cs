using System.Text.Json.Nodes;
using System.Threading.Tasks;
using DevMode.Mcp;

namespace DevMode.Mcp.Tools;

internal sealed class DevGetSessionTool : IMcpTool {
    public string Name => "dev_get_session";
    public string Description =>
        "Get DevMode session state: run active, game phase, dev-run flag, and blocking startup prompts.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {},
        "required": []
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) =>
        Task.FromResult<JsonNode>(DevSessionInfo.Capture());
}
