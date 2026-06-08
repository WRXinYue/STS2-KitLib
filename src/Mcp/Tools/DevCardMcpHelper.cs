using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.Actions;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Runs;

namespace KitLib.Mcp.Tools;

internal static class DevCardMcpHelper {
    public static JsonObject Fail(string error) => new() {
        ["ok"] = false,
        ["error"] = error,
    };

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

    public static bool TryRequireRun(out RunState state, out Player player, out JsonObject? error) {
        error = null;
        if (RunContext.TryGetRunAndPlayer(out state, out player))
            return true;
        error = Fail("No active run.");
        return false;
    }
}
