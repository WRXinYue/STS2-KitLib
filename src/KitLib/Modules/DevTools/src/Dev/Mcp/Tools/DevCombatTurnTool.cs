using System;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Game;
using KitLib.Host;

namespace KitLib.Mcp.Tools;

/// <summary>Plays a whole combat turn in one call so the caller pays one round trip per turn
/// instead of one per card.
/// 一次调用执行完整回合，避免每张牌一次往返。</summary>
internal sealed class DevCombatTurnTool : IMcpTool {
    private const int DefaultTimeoutMs = 8000;
    private const int MaxTimeoutMs = 30000;

    public string Name => "dev_combat_turn";
    public string Description =>
        "Play a full turn in one call: play cards in order (index/target), optionally end the turn, " +
        "wait for resolution, and return the resulting state. Stops after the first failing card.";
    public string InputSchemaJson => """
    {
        "type": "object",
        "properties": {
            "cards": {
                "type": "array",
                "description": "Cards to play in order.",
                "items": {
                    "type": "object",
                    "properties": {
                        "index": { "type": "integer", "description": "Hand index." },
                        "target": { "type": "integer", "description": "Enemy index, -1 for untargeted." }
                    },
                    "required": ["index"]
                }
            },
            "end_turn": {
                "type": "boolean",
                "description": "End the turn after the cards resolve (default true)."
            },
            "timeout_ms": {
                "type": "integer",
                "description": "Per-step wait budget in milliseconds (default 8000, max 30000)."
            }
        }
    }
    """;

    public async Task<JsonNode> ExecuteAsync(JsonObject args) {
        ulong started = Time.GetTicksMsec();
        int timeoutMs = Math.Clamp(args["timeout_ms"]?.GetValue<int>() ?? DefaultTimeoutMs, 1000, MaxTimeoutMs);
        bool endTurn = args["end_turn"]?.GetValue<bool>() ?? true;
        var wait = TimeSpan.FromMilliseconds(timeoutMs);

        var played = new JsonArray();
        bool ok = true;

        if (args["cards"] is JsonArray cards) {
            foreach (var node in cards) {
                if (node is not JsonObject card)
                    continue;

                int index = card["index"]?.GetValue<int>() ?? -1;
                int target = card["target"]?.GetValue<int>() ?? -1;
                var result = await KitLibGameOps.Execute(new GameAction {
                    Type = ActionType.PlayCard,
                    TargetIndex = index,
                    SecondaryIndex = target,
                    Reason = "MCP dev_combat_turn",
                });

                played.Add(new JsonObject {
                    ["index"] = index,
                    ["target"] = target,
                    ["success"] = result.Success,
                    ["message"] = result.Message,
                });

                if (!result.Success) {
                    ok = false;
                    break;
                }

                // Let the card's animations and triggers finish before the next play.
                await GameWait.ActionsSettled(wait);
                if (LeanGamePhase.Current() != GamePhase.Combat) {
                    endTurn = false;
                    break;
                }
            }
        }

        var response = new JsonObject {
            ["ok"] = ok,
            ["played"] = played,
        };

        if (endTurn && Sts2CombatCompat.IsCombatPlayPhaseActive()) {
            var endResult = await KitLibGameOps.Execute(new GameAction {
                Type = ActionType.EndTurn,
                Reason = "MCP dev_combat_turn",
            });
            response["endTurn"] = endResult.Success;
            response["endTurnMessage"] = endResult.Message;
            if (endResult.Success)
                response["timedOut"] = !await WaitForNextTurn(timeoutMs);
        }
        else {
            response["endTurn"] = false;
        }

        // The caller needs the post-turn state; fetching it separately would cost another round trip.
        if (DevCardMcpHelper.PostActionState() is { } state)
            response["state"] = state;

        response["phase"] = LeanGamePhase.Current().ToString();
        response["waitedMs"] = Time.GetTicksMsec() - started;
        return response;
    }

    // EndTurn is queued, so the play phase still reads active for a frame. Wait for the enemy turn to
    // take over, then for the next play phase or for combat to end.
    private static async Task<bool> WaitForNextTurn(int timeoutMs) {
        var tree = Engine.GetMainLoop() as SceneTree;
        if (tree == null)
            return false;

        ulong start = Time.GetTicksMsec();
        bool leftPlayPhase = false;

        while (Time.GetTicksMsec() - start < (ulong)timeoutMs) {
            if (!Sts2CombatCompat.IsCombatPlayPhaseActive())
                leftPlayPhase = true;
            else if (leftPlayPhase)
                return true;

            if (LeanGamePhase.Current() != GamePhase.Combat)
                return true;

            await tree.ToSignal(tree, SceneTree.SignalName.ProcessFrame);
        }

        return false;
    }
}
