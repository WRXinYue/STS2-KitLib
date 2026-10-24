using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Abstractions.Host;
using KitLib.Actions;
using KitLib.Host;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Mcp.Tools;

internal static class DevCardMcpHelper {
    public static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };

    public static bool TryParseApiPile(string? raw, out KitLibCardPile pile, out string error) {
        pile = KitLibCardPile.Hand;
        error = "";
        switch ((raw ?? "hand").Trim().ToLowerInvariant()) {
            case "deck":
                pile = KitLibCardPile.Deck;
                return true;
            case "hand":
                pile = KitLibCardPile.Hand;
                return true;
            case "draw":
            case "draw_pile":
                pile = KitLibCardPile.Draw;
                return true;
            case "discard":
            case "discard_pile":
                pile = KitLibCardPile.Discard;
                return true;
            case "exhaust":
            case "exhaust_pile":
                pile = KitLibCardPile.Exhaust;
                return true;
            default:
                error = $"Unknown target '{raw}'. Use deck, hand, draw, discard, or exhaust.";
                return false;
        }
    }

    public static bool TryParseApiDuration(string? raw, out KitLibCardDuration duration, out string error) {
        duration = KitLibCardDuration.Permanent;
        error = "";
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        switch (raw.Trim().ToLowerInvariant()) {
            case "perm":
            case "permanent":
                duration = KitLibCardDuration.Permanent;
                return true;
            case "temp":
            case "temporary":
                duration = KitLibCardDuration.Temporary;
                return true;
            default:
                error = $"Unknown duration '{raw}'. Use perm or temp.";
                return false;
        }
    }

    public static bool TryParseTarget(string? raw, out CardTarget target, out string error) {
        target = CardTarget.Hand;
        error = "";
        switch ((raw ?? "hand").Trim().ToLowerInvariant()) {
            case "deck":
                target = CardTarget.Deck;
                return true;
            case "hand":
                target = CardTarget.Hand;
                return true;
            case "draw":
            case "draw_pile":
                target = CardTarget.DrawPile;
                return true;
            case "discard":
            case "discard_pile":
                target = CardTarget.DiscardPile;
                return true;
            case "exhaust":
            case "exhaust_pile":
                target = CardTarget.ExhaustPile;
                return true;
            default:
                error = $"Unknown target '{raw}'. Use deck, hand, draw, discard, or exhaust.";
                return false;
        }
    }

    public static bool TryParseDuration(string? raw, out EffectDuration duration, out string error) {
        duration = EffectDuration.Permanent;
        error = "";
        if (string.IsNullOrWhiteSpace(raw))
            return true;

        switch (raw.Trim().ToLowerInvariant()) {
            case "perm":
            case "permanent":
                duration = EffectDuration.Permanent;
                return true;
            case "temp":
            case "temporary":
                duration = EffectDuration.Temporary;
                return true;
            default:
                error = $"Unknown duration '{raw}'. Use perm or temp.";
                return false;
        }
    }

    public static JsonArray SerializeCards(IReadOnlyList<CardModel> cards) {
        var arr = new JsonArray();
        for (var i = 0; i < cards.Count; i++) {
            var card = cards[i];
            var entry = new JsonObject {
                ["index"] = i,
                ["cardId"] = ((AbstractModel)card).Id.Entry,
            };
            try {
                entry["upgradeLevel"] = card.CurrentUpgradeLevel;
            }
            catch {
                entry["upgradeLevel"] = 0;
            }
            arr.Add(entry);
        }
        return arr;
    }

    public static CardModel? ResolveCardInPile(
        IReadOnlyList<CardModel> cards,
        string? cardId,
        int? pileIndex,
        out string error) {
        error = "";
        if (pileIndex.HasValue) {
            if (pileIndex.Value < 0 || pileIndex.Value >= cards.Count) {
                error = $"pile_index {pileIndex.Value} out of range (count {cards.Count}).";
                return null;
            }
            return cards[pileIndex.Value];
        }

        if (string.IsNullOrWhiteSpace(cardId)) {
            error = "Provide card_id or pile_index.";
            return null;
        }

        var match = cards.FirstOrDefault(c =>
            string.Equals(((AbstractModel)c).Id.Entry, cardId.Trim(), System.StringComparison.OrdinalIgnoreCase));
        if (match == null)
            error = $"Card '{cardId}' not found in target pile.";
        return match;
    }

    // Post-action digest: what a caller needs right after acting is phase plus the live combat/map
    // essentials, so the bulky parts of a full snapshot stay out of every action response.
    public static JsonObject? PostActionState() {
        var snapshot = KitLibGameOps.Snapshot();
        if (snapshot == null)
            return null;

        var state = new JsonObject {
            ["phase"] = snapshot["phase"]?.DeepClone(),
            ["currentHp"] = snapshot["currentHp"]?.DeepClone(),
            ["gold"] = snapshot["gold"]?.DeepClone(),
        };

        foreach (var key in PhaseKeys) {
            if (snapshot[key] is { } node)
                state[key] = node.DeepClone();
        }

        if (snapshot["combat"] is JsonObject combat)
            state["combat"] = CompactCombat(combat);

        return state;
    }

    private static readonly string[] PhaseKeys = [
        "roomType", "mapNodes", "rewardsHaveCollectable", "offeredCards",
        "eventOptions", "shopOffers", "restOptions", "offeredRelics",
    ];

    private static JsonObject CompactCombat(JsonObject combat) {
        var enemies = new JsonArray();
        if (combat["enemies"] is JsonArray raw) {
            foreach (var node in raw) {
                if (node is not JsonObject enemy)
                    continue;
                var entry = new JsonObject {
                    ["index"] = enemy["index"]?.DeepClone(),
                    ["currentHp"] = enemy["currentHp"]?.DeepClone(),
                    ["maxHp"] = enemy["maxHp"]?.DeepClone(),
                    ["block"] = enemy["block"]?.DeepClone(),
                    ["isAlive"] = enemy["isAlive"]?.DeepClone(),
                };
                if (enemy["name"] is { } name)
                    entry["name"] = name.DeepClone();
                if (enemy["intentDamage"] is { } intent)
                    entry["intentDamage"] = intent.DeepClone();
                enemies.Add(entry);
            }
        }

        return new JsonObject {
            ["phase"] = combat["phase"]?.DeepClone(),
            ["currentEnergy"] = combat["currentEnergy"]?.DeepClone(),
            ["turnNumber"] = combat["turnNumber"]?.DeepClone(),
            ["handCount"] = (combat["hand"] as JsonArray)?.Count ?? 0,
            ["enemies"] = enemies,
        };
    }

    public static bool TryRequireRun(out RunState state, out Player player, out JsonObject? error) {
        error = null;
        if (RunContext.TryGetRunAndPlayer(out state, out player))
            return true;
        error = Fail("No active run.");
        return false;
    }
}
