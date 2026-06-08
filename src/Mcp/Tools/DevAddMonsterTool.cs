using System.Text.Json.Nodes;
using System.Threading.Tasks;
using KitLib.Actions;
using KitLib.Multiplayer.Cheat;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Mcp.Tools;

internal sealed class DevAddMonsterTool : IMcpTool {
    public string Name => "dev_add_monster";
    public string Description =>
        "Add a monster to the current combat (same API as DevMode enemy browser / dmenemy spawn).";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "monster_id": {
                "type": "string",
                "description": "Monster model ID, e.g. OVICOPTER."
            }
        },
        "required": ["monster_id"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        if (!DevEnemyMcpHelper.TryRequireCombat(out var combatError))
            return combatError!;

        if (!args.TryGetPropertyValue("monster_id", out var idNode)
            || idNode?.GetValueKind() != System.Text.Json.JsonValueKind.String
            || string.IsNullOrWhiteSpace(idNode.GetValue<string>())) {
            return DevEnemyMcpHelper.Fail("Missing or invalid monster_id.");
        }

        var monsterId = idNode.GetValue<string>()!.Trim();
        var monster = DevEnemyMcpHelper.FindMonster(monsterId);
        if (monster == null)
            return DevEnemyMcpHelper.Fail($"Monster not found: '{monsterId}'.");

        if (MpCheatSession.InMultiplayerRun)
            return DevEnemyMcpHelper.Fail("Multiplayer add is not supported via MCP.");

        var canonicalId = ((AbstractModel)monster).Id.Entry;
        var creature = await CombatEnemyActions.AddMonster(monster);
        if (creature == null)
            return DevEnemyMcpHelper.Fail($"Failed to add monster '{canonicalId}' (combat may not be in progress).");

        return new JsonObject {
            ["ok"] = true,
            ["monsterId"] = canonicalId,
            ["hp"] = creature.CurrentHp,
            ["maxHp"] = creature.MaxHp,
            ["enemyCount"] = CombatEnemyActions.GetCurrentEnemies().Count,
        };
    }
}
