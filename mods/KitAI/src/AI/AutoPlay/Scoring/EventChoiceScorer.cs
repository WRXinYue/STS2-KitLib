using System;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

/// <summary>Scores event-room options (Neow and generic events) from snapshot data.</summary>
public static class EventChoiceScorer {
    const int CodexPrimaryBaseline = 10;
    const int CodexPrimarySampleThreshold = 20;

    public static GameAction PickBest(JsonObject snapshot) {
        var options = snapshot["eventOptions"]?.AsArray();
        if (options == null || options.Count == 0) {
            return new GameAction {
                Type = ActionType.SelectEventChoice,
                TargetIndex = 0,
                Reason = "No event options in snapshot — pick first",
            };
        }

        var eventId = snapshot["eventId"]?.GetValue<string>() ?? "";
        var isNeow = eventId.Contains("NEOW", StringComparison.OrdinalIgnoreCase)
            || InferNeowFromOptions(options);
        var plan = DeckPlanInferer.Infer(snapshot);

        int bestIdx = -1;
        int bestScore = int.MinValue;
        EventOptionBreakdown? bestBreakdown = null;

        for (int i = 0; i < options.Count; i++) {
            if (options[i] is not JsonObject opt)
                continue;
            if (opt["locked"]?.GetValue<bool>() == true)
                continue;

            var breakdown = ScoreEventOption(opt, plan, snapshot, isNeow);
            if (bestIdx < 0 || breakdown.Total > bestScore) {
                bestIdx = i;
                bestScore = breakdown.Total;
                bestBreakdown = breakdown;
            }
        }

        if (bestIdx < 0)
            bestIdx = 0;

        var title = options[bestIdx]?["title"]?.GetValue<string>() ?? $"option {bestIdx}";
        var reason = FormatReason(isNeow, title, bestBreakdown, bestScore);
        return new GameAction {
            Type = ActionType.SelectEventChoice,
            TargetIndex = bestIdx,
            Reason = reason,
        };
    }

    static string FormatReason(bool isNeow, string title, EventOptionBreakdown? breakdown, int score) {
        var prefix = isNeow ? "Neow pick" : "Event pick";
        if (breakdown is not EventOptionBreakdown b)
            return $"{prefix} [{title}] (score {score})";

        var keyPart = string.IsNullOrEmpty(b.OptionKey) ? "" : $" key={b.OptionKey}";
        var codexPart = b.Codex != 0 ? $" codex={(b.Codex >= 0 ? "+" : "")}{b.Codex}" : "";
        var synergyPart = b.Synergy != 0 ? $" synergy={(b.Synergy >= 0 ? "+" : "")}{b.Synergy}" : "";
        var modePart = b.CodexPrimary ? " codex_primary" : "";
        return $"{prefix} [{title}] score={score}{keyPart}{codexPart}{synergyPart}{modePart}";
    }

    static bool InferNeowFromOptions(JsonArray options) {
        foreach (var node in options) {
            if (node is not JsonObject opt) continue;
            var key = opt["textKey"]?.GetValue<string>() ?? "";
            if (key.Contains("NEOW", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    static EventOptionBreakdown ScoreEventOption(JsonObject opt, DeckPlan plan, JsonObject snapshot, bool isNeow) {
        var characterId = snapshot["characterId"]?.GetValue<string>();
        var eventId = snapshot["eventId"]?.GetValue<string>() ?? "";
        var optionKey = EventOptionInfer.OptionKey(opt);
        var relicId = EventOptionInfer.RelicId(opt);

        var keyword = isNeow ? NeowKeywordScore(opt, plan) : GenericKeywordScore(opt);

        var codex = 0;
        var codexN = 0;
        if (!string.IsNullOrEmpty(optionKey))
            codex = CodexPriorCatalog.GetEventOptionBonus(characterId, eventId, optionKey, out codexN);
        if (codex == 0 && !string.IsNullOrEmpty(relicId))
            codex = CodexPriorCatalog.GetRelicBonus(characterId, relicId, "event");

        var synergy = ScoreOptionSynergy(opt, relicId, plan, snapshot);

        var codexPrimary = codexN >= CodexPrimarySampleThreshold && codex != 0;
        if (codexPrimary)
            codex = (int)Math.Round(codex * 1.5f);

        var total = codexPrimary
            ? CodexPrimaryBaseline + codex + synergy
            : keyword + codex + synergy;

        return new EventOptionBreakdown(total, keyword, codex, synergy, optionKey, codexPrimary);
    }

    static int ScoreOptionSynergy(JsonObject opt, string? relicId, DeckPlan plan, JsonObject snapshot) {
        if (!string.IsNullOrEmpty(relicId))
            return DeckSynergyEvaluator.ScoreRelic(relicId, plan, snapshot)
                + DeckSynergyEvaluator.RelicTagPlanScore(relicId, plan);

        var cardLike = new JsonObject {
            ["id"] = opt["modelId"]?.GetValue<string>() ?? opt["optionKey"]?.GetValue<string>() ?? "",
            ["cardType"] = opt["cardType"]?.GetValue<string>() ?? "",
            ["cost"] = opt["cost"]?.GetValue<int>() ?? 0,
        };
        if (string.IsNullOrWhiteSpace(cardLike["id"]?.GetValue<string>()))
            return 0;
        return DeckSynergyEvaluator.ScoreCard(cardLike, plan, snapshot);
    }

    static int NeowKeywordScore(JsonObject opt, DeckPlan plan) {
        var key = (opt["textKey"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var title = (opt["title"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var text = $"{key} {title}";

        int score = 0;
        if (text.Contains("RELIC", StringComparison.Ordinal)
            || text.Contains("CAPSULE", StringComparison.Ordinal))
            score += 12;
        if (text.Contains("CARD", StringComparison.Ordinal)
            || text.Contains("COFFER", StringComparison.Ordinal)
            || text.Contains("COLORLESS", StringComparison.Ordinal)
            || text.Contains("PAPERWEIGHT", StringComparison.Ordinal))
            score += 10;
        if (text.Contains("POTION", StringComparison.Ordinal)
            || text.Contains("PHIAL", StringComparison.Ordinal)
            || text.Contains("SLOT", StringComparison.Ordinal))
            score += 8;
        if (text.Contains("GOLD", StringComparison.Ordinal) || text.Contains("PEARL", StringComparison.Ordinal))
            score += 5;
        if (text.Contains("REMOVE", StringComparison.Ordinal)
            || text.Contains("TRANSFORM", StringComparison.Ordinal))
            score += (int)Math.Round(plan.ThinPreference * 12f);
        if (text.Contains("CURSE", StringComparison.Ordinal)
            || text.Contains("TORMENT", StringComparison.Ordinal)
            || text.Contains("PAIN", StringComparison.Ordinal))
            score -= 40;
        if (text.Contains("MAXHP", StringComparison.Ordinal) || text.Contains("LAVA", StringComparison.Ordinal))
            score -= 5;

        return score;
    }

    static int GenericKeywordScore(JsonObject opt) {
        var title = (opt["title"]?.GetValue<string>() ?? "").ToUpperInvariant();
        int score = 5;
        if (title.Contains("RELIC")) score += 8;
        if (title.Contains("HEAL")) score += 8;
        if (title.Contains("GOLD")) score += 5;
        if (title.Contains("CURSE")) score -= 30;
        return score;
    }

    readonly record struct EventOptionBreakdown(
        int Total,
        int Keyword,
        int Codex,
        int Synergy,
        string? OptionKey,
        bool CodexPrimary);
}
