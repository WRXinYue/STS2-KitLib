using System.Linq;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.AI.Knowledge;

namespace KitLib.Mcp.Tools;

internal sealed class DevDumpMonsterMechanicsTool : IMcpTool {
    public string Name => "dev_dump_monster_mechanics";
    public string Description => "Dump MonsterMechanicIndex profiles (flags, moves, spawn hints) for validation.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "prefix": {
                "type": "string",
                "description": "Optional monsterId prefix filter."
            }
        }
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        string? prefix = null;
        if (args.TryGetPropertyValue("prefix", out var prefixNode)
            && prefixNode?.GetValueKind() == System.Text.Json.JsonValueKind.String)
            prefix = prefixNode.GetValue<string>()?.Trim();

        var profiles = MonsterMechanicIndex.AllProfiles();
        var arr = new JsonArray();
        foreach (var profile in profiles) {
            if (!string.IsNullOrWhiteSpace(prefix)
                && !profile.MonsterId.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                continue;

            arr.Add(new JsonObject {
                ["monsterId"] = profile.MonsterId,
                ["flags"] = profile.Flags.ToString(),
                ["spawnedMonsterIds"] = new JsonArray(
                    profile.SpawnedMonsterIds.Select(id => JsonValue.Create(id)).ToArray()),
                ["moveCount"] = profile.Moves.Count,
            });
        }

        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["count"] = arr.Count,
            ["monsters"] = arr,
        });
    }
}
