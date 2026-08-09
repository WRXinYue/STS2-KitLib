using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;

namespace KitLib.Mcp.Tools;

internal sealed class DevListEnemiesTool : IMcpTool {
    public string Name => "dev_list_enemies";
    public string Description => "List enemies currently in combat (index, monsterId, HP).";
    public string InputSchemaJson => """{ "type": "object", "properties": {} }""";

    public Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!DevEnemyMcpHelper.TryRequireCombat(out var combatError))
            return Task.FromResult<JsonNode>(combatError!);

        var enemies = CombatEnemyActions.GetCurrentEnemies();
        return Task.FromResult<JsonNode>(new JsonObject {
            ["ok"] = true,
            ["count"] = enemies.Count,
            ["enemies"] = DevEnemyMcpHelper.SerializeEnemies(enemies),
        });
    }
}
