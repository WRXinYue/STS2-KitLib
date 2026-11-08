using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Godot;
using KitLib.Game;
using KitLib.Host;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.Mcp.Tools;

/// <summary>Plays a whole combat turn in one call so the caller pays one round trip per turn
/// instead of one per card.
/// 一次调用执行完整回合，避免每张牌一次往返。</summary>
internal sealed class DevCombatTurnTool : IMcpTool {
    private const int DefaultTimeoutMs = 8000;
    private const int MaxTimeoutMs = 30000;

    public string Name => "dev_combat_turn";
    public string Description =>
        "Play a full turn in one call: play cards in order (index/target refer to the hand at call " +
        "time), optionally end the turn, wait for resolution, and return the resulting state. " +
        "Stops after the first failing card.";
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
                        "target": { "type": "integer", "description": "Enemy index, -1 for untargeted." },
                        "selection_card_id": { "type": "string", "description": "If this card opens a selection screen, auto-pick the first option with this card id." },
                        "selection_index": { "type": "integer", "description": "If this card opens a selection screen, auto-pick this option index." }
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

        // A selection overlay owns input until it is resolved; starting a turn on top of it used to
        // queue an end turn and burn the whole timeout before failing.
        if (CardSelectionUi.IsActive()) {
            return new JsonObject {
                ["ok"] = false,
                ["pendingSelection"] = true,
                ["selectionState"] = KitLibGameOps.SelectionState(),
                ["message"] = "A card selection screen is open. Resolve it with selection_action before playing a turn.",
                ["played"] = new JsonArray(),
                ["endTurn"] = false,
            };
        }

        var played = new JsonArray();
        bool ok = true;
        bool pendingSelection = false;

        if (args["cards"] is JsonArray cards) {
            RunContext.TryGetRunAndPlayer(out _, out var player);
            var hand = player?.PlayerCombatState?.Hand?.Cards?.ToList();

            // Hand indexes shift after each play, so the requested cards are re-resolved by identity.
            int LiveIndexOf(CardModel model) {
                var live = player?.PlayerCombatState?.Hand?.Cards;
                if (live == null)
                    return -1;
                int i = 0;
                foreach (var current in live) {
                    if (ReferenceEquals(current, model))
                        return i;
                    i++;
                }
                return -1;
            }

            foreach (var node in cards) {
                if (node is not JsonObject card)
                    continue;

                int requested = card["index"]?.GetValue<int>() ?? -1;
                int target = card["target"]?.GetValue<int>() ?? -1;
                CardModel? model = hand != null && requested >= 0 && requested < hand.Count
                    ? hand[requested]
                    : null;
                int index = model != null ? LiveIndexOf(model) : requested;

                if (model != null && index < 0) {
                    played.Add(new JsonObject {
                        ["index"] = requested,
                        ["target"] = target,
                        ["success"] = false,
                        ["message"] = "Card is no longer in hand.",
                    });
                    ok = false;
                    // "Stops after the first failing card" also means no surprise end-turn; the caller
                    // can still end the turn explicitly after inspecting the failure.
                    endTurn = false;
                    break;
                }

                var hint = SelectionHintFrom(card);
                var result = await KitLibGameOps.Execute(new GameAction {
                    Type = ActionType.PlayCard,
                    TargetIndex = index,
                    SecondaryIndex = target,
                    Reason = "MCP dev_combat_turn",
                }, hint);

                played.Add(new JsonObject {
                    ["index"] = index,
                    ["requested"] = requested,
                    ["target"] = target,
                    ["success"] = result.Success,
                    ["message"] = result.Message,
                });

                if (!result.Success) {
                    ok = false;
                    if (result.Message == "pending_selection" || CardSelectionUi.IsActive()) {
                        pendingSelection = true;
                    }
                    else {
                        // Stop the turn on a failed card instead of silently ending it afterwards.
                        endTurn = false;
                    }
                    break;
                }

                // A successful play can still leave a selection screen open (patched cards); stop instead
                // of playing the next card on top of the overlay.
                if (CardSelectionUi.IsActive()) {
                    pendingSelection = true;
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

        if (pendingSelection || CardSelectionUi.IsActive()) {
            response["pendingSelection"] = true;
            response["selectionState"] = KitLibGameOps.SelectionState();
            endTurn = false;
        }

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

    /// <summary>Optional per-card selection auto-pick, mirroring combat_action's selection hint.</summary>
    private static SelectionHint? SelectionHintFrom(JsonObject card) {
        var cardId = card["selection_card_id"]?.GetValue<string>()?.Trim();
        int? selectionIndex = null;
        if (card.TryGetPropertyValue("selection_index", out var indexNode)
            && indexNode?.GetValueKind() == JsonValueKind.Number) {
            selectionIndex = indexNode.GetValue<int>();
        }

        if (string.IsNullOrWhiteSpace(cardId) && selectionIndex == null)
            return null;

        return new SelectionHint {
            CardId = string.IsNullOrWhiteSpace(cardId) ? null : cardId,
            CardIndex = selectionIndex,
        };
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
