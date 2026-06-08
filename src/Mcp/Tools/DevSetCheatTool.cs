using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace KitLib.Mcp.Tools;

internal sealed class DevSetCheatTool : IMcpTool {
    public string Name => "dev_set_cheat";
    public string Description =>
        "Toggle patch/runtime cheats or set multiplier values (DevMode Cheats panel / dmcheat / dmruntime).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "cheat": {
                "type": "string",
                "description": "Cheat id, e.g. freeze_enemies, infinite_hp, damage_multiplier, god_mode."
            },
            "enabled": {
                "type": "boolean",
                "description": "For toggles. Omit to flip current state."
            },
            "value": {
                "type": "number",
                "description": "For multipliers (damage_multiplier, game_speed, ...) or extra_draw amount."
            }
        },
        "required": ["cheat"]
    }
    """;

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!DevCheatMcpHelper.TryRequireCheats(out var cheatError))
            return Task.FromResult<JsonNode>(cheatError!);

        if (!args.TryGetPropertyValue("cheat", out var cheatNode)
            || cheatNode?.GetValueKind() != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(cheatNode.GetValue<string>())) {
            return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail("Missing or invalid cheat."));
        }

        if (!DevCheatMcpHelper.TryParseCheatName(cheatNode.GetValue<string>(), out var cheat, out var nameError))
            return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail(nameError));

        var enabled = DevCheatMcpHelper.ParseOptionalBool(args, out var boolError);
        if (boolError != null)
            return Task.FromResult<JsonNode>(DevCheatMcpHelper.Fail(boolError));

        float? value = null;
        if (args.TryGetPropertyValue("value", out var valueNode)
            && valueNode?.GetValueKind() == System.Text.Json.JsonValueKind.Number)
            value = (float)valueNode.GetValue<double>();

        var result = DevCheatMcpHelper.ApplyCheat(cheat, enabled, value);
        return Task.FromResult<JsonNode>(result!);
    }
}
