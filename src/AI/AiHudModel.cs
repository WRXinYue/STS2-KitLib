using System;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using DevMode.AI.Combat;
using DevMode.AI.Core.Schema;
using DevMode.AI.Planning;
using DevMode.AI.Sts2;

namespace DevMode.AI;

/// <summary>Player-facing HUD text from snapshots and last decision (no re-decide).</summary>
public static class AiHudModel {
    static readonly Regex ScoreBracketRegex = new(@"\s*\[[^\]]*\]", RegexOptions.Compiled);

    public static string PhaseShortLabel(GamePhase phase) => phase switch {
        GamePhase.Combat => I18N.T("ai.hud.phase.combat", "Combat"),
        GamePhase.MapSelection => I18N.T("ai.hud.phase.map", "Map"),
        GamePhase.CardReward => I18N.T("ai.hud.phase.cardReward", "Card reward"),
        GamePhase.EventChoice => I18N.T("ai.hud.phase.event", "Event"),
        GamePhase.Shop => I18N.T("ai.hud.phase.shop", "Shop"),
        GamePhase.RestSite => I18N.T("ai.hud.phase.rest", "Rest"),
        GamePhase.RewardScreen => I18N.T("ai.hud.phase.rewards", "Rewards"),
        GamePhase.RelicSelection => I18N.T("ai.hud.phase.relic", "Relic"),
        GamePhase.PostCombatTransition => I18N.T("ai.hud.phase.postCombat", "Post-combat"),
        GamePhase.TreasureRoom => I18N.T("ai.hud.phase.treasure", "Treasure"),
        _ => I18N.T("ai.hud.phase.other", "Other"),
    };

    public static string BuildStrategyLine(JsonObject snapshot, GamePhase phase) {
        return phase switch {
            GamePhase.Combat => BuildCombatStrategy(snapshot),
            GamePhase.MapSelection => BuildMapStrategy(snapshot),
            GamePhase.CardReward => BuildCardRewardStrategy(snapshot),
            GamePhase.Shop => BuildShopStrategy(snapshot),
            GamePhase.RestSite => BuildRestStrategy(snapshot),
            GamePhase.EventChoice => I18N.T("ai.hud.strategy.event", "Evaluate event options by deck synergy and codex priors"),
            _ => I18N.T("ai.hud.strategy.default", "Follow StrongStrategy for current phase"),
        };
    }

    public static string BuildNextActionLine(AiHudDecision? decision, JsonObject snapshot) {
        if (decision == null)
            return I18N.T("ai.hud.next.waiting", "Next: waiting for decision");

        var verb = ActionVerb(decision.Action);
        var detail = ActionDetail(decision, snapshot);
        var reason = SanitizeReason(decision.Reason);
        var line = string.IsNullOrWhiteSpace(detail)
            ? $"{verb}"
            : $"{verb} · {detail}";

        if (!string.IsNullOrWhiteSpace(reason))
            line += $" — {reason}";

        return I18N.T("ai.hud.next.prefix", "Next: {0}", line);
    }

    public static string? BuildParamStrip(JsonObject snapshot, GamePhase phase) {
        if (phase == GamePhase.Combat)
            return BuildCombatParams(snapshot);

        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        return I18N.T("ai.hud.params.run", "F{0} G={1} HP={2}/{3}", floor, gold, hp, maxHp);
    }

    public static string? BuildScoreTerms(AiHudDecision? decision) {
        if (decision == null || string.IsNullOrWhiteSpace(decision.Reason))
            return null;

        var match = Regex.Match(decision.Reason, @"\[(.+)\]\s*$");
        if (!match.Success)
            return null;

        var inner = match.Groups[1].Value;
        return inner.Contains("block:", StringComparison.Ordinal)
            || inner.Contains("mechanic:", StringComparison.Ordinal)
            || inner.Contains("attack:", StringComparison.Ordinal)
            ? inner
            : null;
    }

    static string BuildCombatStrategy(JsonObject snapshot) {
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var net = IntentCalculator.NetDamageAfterBlock(snapshot);

        if (incoming <= 0)
            return I18N.T("ai.hud.strategy.combat.safe", "No incoming damage — prioritize damage and setup");

        if (IntentCalculator.IsFatalIfUnblocked(snapshot))
            return I18N.T("ai.hud.strategy.combat.fatal", "Fatal incoming {0} — block first", net);

        if (BlockThreatEvaluator.ShouldScoreBlock(snapshot))
            return I18N.T("ai.hud.strategy.combat.block", "Enemy deals {0} ({1} after block) — favor block", incoming, net);

        return I18N.T("ai.hud.strategy.combat.light", "Light threat {0} — balance offense and defense", incoming);
    }

    static string BuildMapStrategy(JsonObject snapshot) {
        var plan = MapPathPlanner.CachedPlan;
        if (plan == null && AiPlayServices.StateProvider.TryGetRunAndPlayer(out var state, out var player))
            plan = MapPathPlanner.Plan(state, player, forceRefresh: false);

        if (plan == null)
            return I18N.T("ai.hud.strategy.map.none", "Pick the best reachable map node");

        var nextType = snapshot["mapNodes"]?[plan.NextChildIndex]?["pointType"]?.GetValue<string>() ?? "?";
        return I18N.T(
            "ai.hud.strategy.map.plan",
            "Route {0} → next {1} (risk {2})",
            plan.Summary,
            nextType,
            plan.PathRiskAtNext);
    }

    static string BuildCardRewardStrategy(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        var preview = snapshot["nextFightPreview"]?.AsArray();
        var fightHint = preview != null && preview.Count > 0
            ? preview[0]?["roomType"]?.GetValue<string>() ?? "?"
            : null;

        if (fightHint != null)
            return I18N.T(
                "ai.hud.strategy.reward.nextFight",
                "Score offers vs next fights ({0}); pick only when total > 0",
                fightHint);

        if (metrics.BlockDeficit >= 2)
            return I18N.T("ai.hud.strategy.reward.block", "Low block sources — favor transitional defense");

        return I18N.T(
            "ai.hud.strategy.reward.default",
            "Marginal deck quality + next-fight sim; skip when score <= 0 (mean {0:0.#})",
            metrics.MeanValue);
    }

    static string BuildShopStrategy(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        if (metrics.RemovalUplift >= DeckEvaluator.MinRemovalUplift)
            return I18N.T("ai.hud.strategy.shop.remove", "Removal uplift {0} — consider shop remove", metrics.RemovalUplift);
        return I18N.T("ai.hud.strategy.shop.buy", "Spend gold on relic/card/potion that fits plan");
    }

    static string BuildRestStrategy(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var ratio = maxHp > 0 ? (float)hp / maxHp : 1f;
        if (ratio < 0.55f)
            return I18N.T("ai.hud.strategy.rest.heal", "Low HP ({0}/{1}) — favor rest heal", hp, maxHp);
        return I18N.T("ai.hud.strategy.rest.upgrade", "HP healthy — favor smith upgrade");
    }

    static string BuildCombatParams(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        var block = combat?["playerBlock"]?.GetValue<int>() ?? 0;
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        return I18N.T(
            "ai.hud.params.combat",
            "HP={0}/{1} BLK={2} IN={3} E={4}",
            hp, maxHp, block, incoming, energy);
    }

    static string ActionVerb(ActionType type) => type switch {
        ActionType.PlayCard => I18N.T("ai.hud.action.playCard", "Play card"),
        ActionType.EndTurn => I18N.T("ai.hud.action.endTurn", "End turn"),
        ActionType.SelectMapNode => I18N.T("ai.hud.action.map", "Select node"),
        ActionType.PickCardReward => I18N.T("ai.hud.action.pickCard", "Pick card"),
        ActionType.SkipCardReward => I18N.T("ai.hud.action.skipCard", "Skip card"),
        ActionType.SelectEventChoice => I18N.T("ai.hud.action.event", "Event choice"),
        ActionType.UsePotion => I18N.T("ai.hud.action.potion", "Use potion"),
        ActionType.CollectReward => I18N.T("ai.hud.action.collect", "Collect reward"),
        ActionType.Proceed => I18N.T("ai.hud.action.proceed", "Proceed"),
        ActionType.PickRelic => I18N.T("ai.hud.action.relic", "Pick relic"),
        ActionType.Rest => I18N.T("ai.hud.action.rest", "Rest"),
        ActionType.UpgradeCard => I18N.T("ai.hud.action.upgrade", "Upgrade"),
        ActionType.PurchaseShopItem => I18N.T("ai.hud.action.buy", "Buy"),
        ActionType.RemoveCardAtShop => I18N.T("ai.hud.action.remove", "Remove card"),
        ActionType.LeaveShop => I18N.T("ai.hud.action.leaveShop", "Leave shop"),
        ActionType.HandleTreasureRoom => I18N.T("ai.hud.action.treasure", "Treasure room"),
        _ => type.ToString(),
    };

    static string ActionDetail(AiHudDecision decision, JsonObject snapshot) {
        if (decision.Action == ActionType.PlayCard) {
            var hand = snapshot["combat"]?["hand"]?.AsArray();
            if (hand != null && decision.TargetIndex >= 0 && decision.TargetIndex < hand.Count) {
                var card = hand[decision.TargetIndex]?.AsObject();
                var name = card?["name"]?.GetValue<string>();
                if (!string.IsNullOrWhiteSpace(name))
                    return name;
            }
        }

        return ExtractBracketName(decision.Reason) ?? "";
    }

    static string? ExtractBracketName(string reason) {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var pick = Regex.Match(reason, @"\[(.+?)\]");
        if (pick.Success)
            return pick.Groups[1].Value.Trim();

        var cardPick = Regex.Match(reason, @"Card pick \[(.+?)\]");
        if (cardPick.Success)
            return cardPick.Groups[1].Value.Trim();

        var map = Regex.Match(reason, @"Map → (\S+)");
        if (map.Success)
            return map.Groups[1].Value.Trim();

        return null;
    }

    static string SanitizeReason(string? reason) {
        if (string.IsNullOrWhiteSpace(reason))
            return "";

        var text = reason;
        text = ScoreBracketRegex.Replace(text, "");
        var scoreIdx = text.IndexOf(" score=", StringComparison.OrdinalIgnoreCase);
        if (scoreIdx >= 0)
            text = text[..scoreIdx];

        return text.Trim().TrimEnd('—', '-', ' ');
    }
}
