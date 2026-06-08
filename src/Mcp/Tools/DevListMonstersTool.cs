using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;

namespace KitLib.Mcp.Tools;

internal sealed class DevListMonstersTool : IMcpTool {
    public string Name => "dev_list_monsters";
    public string Description => "List known monster model IDs (for dev_add_monster).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "prefix": {
                "type": "string",
                "description": "Optional ID prefix filter, e.g. OVICO."
            }
        }
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        string? prefix = null;
        if (args.TryGetPropertyValue("prefix", out var prefixNode)
            && prefixNode?.GetValueKind() == System.Text.Json.JsonValueKind.String)
            prefix = prefixNode.GetValue<string>()?.Trim();

        var monsters = EnemyActions.GetAllMonsters();
        var arr = DevEnemyMcpHelper.SerializeMonsters(monsters, prefix);
        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["count"] = arr.Count,
            ["monsters"] = arr,
        });
    }
}
