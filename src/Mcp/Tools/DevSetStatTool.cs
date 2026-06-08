using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class DevSetStatTool : IMcpTool {
    public string Name => "dev_set_stat";
    public string Description =>
        "Set run/combat stats or enable stat locks (DevMode Cheats panel / dmruntime locks).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "stat": {
                "type": "string",
                "description": "gold, current_hp, max_hp, current_energy, max_energy, stars, orb_slots, potion_slots."
            },
            "value": {
                "type": "integer",
                "description": "Target value."
            },
            "lock": {
                "type": "boolean",
                "description": "When true, enable continuous lock at value. When false, disable lock. Omit for one-shot set."
            }
        },
        "required": ["stat", "value"]
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!DevCheatMcpHelper.TryRequireCheats(out var cheatError))
            return Task.FromResult<JsonNode>(cheatError!);

        if (!args.TryGetPropertyValue("stat", out var statNode)
            || statNode?.GetValueKind() != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(statNode.GetValue<string>())) {
            return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail("Missing or invalid stat."));
        }

        if (!args.TryGetPropertyValue("value", out var valueNode)
            || valueNode?.GetValueKind() != System.Text.Json.JsonValueKind.Number) {
            return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail("Missing or invalid value."));
        }

        var stat = statNode.GetValue<string>()!.Trim().ToLowerInvariant().Replace('-', '_');
        var value = valueNode.GetValue<int>();
        bool? lockEnabled = null;
        if (args.TryGetPropertyValue("lock", out var lockNode)) {
            if (lockNode?.GetValueKind() == System.Text.Json.JsonValueKind.True)
                lockEnabled = true;
            else if (lockNode?.GetValueKind() == System.Text.Json.JsonValueKind.False)
                lockEnabled = false;
            else
                return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail("Invalid lock value. Use true/false."));
        }

        var result = DevCheatMcpHelper.ApplyStat(stat, value, lockEnabled);
        return Task.FromResult<JsonNode>(result!);
    }
}
