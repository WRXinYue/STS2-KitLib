using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Game;

namespace KitLib.Mcp.Tools;

/// <summary>Waits for a target phase on the main thread, replacing the poll loops callers used after
/// each action.</summary>
internal sealed class DevWaitPhaseTool : IMcpTool {
    private const int DefaultTimeoutMs = 5000;
    private const int MaxTimeoutMs = 30000;

    public string Name => "dev_wait_phase";
    public string Description =>
        "Block until the game reaches a phase (Combat, MapSelection, RewardScreen, CardReward, " +
        "RelicSelection, RestSite, Shop, EventChoice, TreasureRoom, GameOver), or 'not_combat'. " +
        "Use it after an action instead of re-polling get_game_state.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "phase": {
                "type": "string",
                "description": "Target GamePhase name, or 'not_combat'."
            },
            "timeout_ms": {
                "type": "integer",
                "description": "Give up after this many milliseconds (default 5000, max 30000)."
            }
        },
        "required": ["phase"]
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        var target = args["phase"]?.GetValue<string>()?.Trim() ?? "";
        if (string.IsNullOrEmpty(target))
            return new JsonObject { ["ok"] = false, ["error"] = "phase is required." };

        int timeoutMs = Math.Clamp(args["timeout_ms"]?.GetValue<int>() ?? DefaultTimeoutMs, 100, MaxTimeoutMs);
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return new JsonObject { ["ok"] = false, ["error"] = "Scene tree unavailable." };

        ulong start = Time.GetTicksMsec();
        while (true) {
            var current = LeanGamePhase.Current();
            ulong elapsed = Time.GetTicksMsec() - start;
            if (Matches(current, target)) {
                return new JsonObject {
                    ["ok"] = true,
                    ["phase"] = current.ToString(),
                    ["waitedMs"] = elapsed,
                };
            }

            if (elapsed >= (ulong)timeoutMs) {
                return new JsonObject {
                    ["ok"] = false,
                    ["timedOut"] = true,
                    ["phase"] = current.ToString(),
                    ["waitedMs"] = elapsed,
                };
            }

            // Yielding a frame keeps the UI animating while we wait; blocking here would freeze the wait.
            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }
    }

    private static bool Matches(GamePhase current, string target) {
        if (target.Equals("not_combat", StringComparison.OrdinalIgnoreCase))
            return current != GamePhase.Combat;
        return Enum.TryParse<GamePhase>(target, ignoreCase: true, out var parsed) && current == parsed;
    }
}
