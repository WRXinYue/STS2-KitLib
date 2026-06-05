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
    static readonly Regex MarginalRegex = new(@"marginal=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex NextFightRegex = new(@"nextFight=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public static string BuildDeckProfileLine(JsonObject snapshot) {
        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);
        var style = AiHudRunForecast.StyleLabel(profile.Style);

        if (AiHudRunForecast.MeetsOfficialBig(profile))
            return I18N.T(
                "ai.hud.deck.profile.big",
                "Deck: {0} · {1} cards (≥{2}) · mean {3:0.#}",
                style,
                profile.DeckSize,
                AiHudRunForecast.OfficialBigDeckMin,
                profile.MeanValue);

        if (AiHudRunForecast.MeetsOfficialSmall(profile))
            return I18N.T(
                "ai.hud.deck.profile.small",
                "Deck: {0} · {1} cards (≤{2}) · mean {3:0.#}",
                style,
                profile.DeckSize,
                AiHudRunForecast.OfficialSmallDeckMax,
                profile.MeanValue);

        return I18N.T(
            "ai.hud.deck.profile.mid",
            "Deck: {0} · {1} cards (21–39) · mean {2:0.#}",
            style,
            profile.DeckSize,
            profile.MeanValue);
    }

    public static string BuildForecastLine(JsonObject snapshot, GamePhase phase) {
        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);
        var prognosis = AiHudRunForecast.AnalyzeRun(snapshot, profile);
        var winPct = (int)Math.Round(prognosis.WinRate * 100f);

        if (phase == GamePhase.CardReward && prognosis.NextFightScore != 0) {
            var lethalHint = prognosis.NextFightLethal
                ? I18N.T("ai.hud.forecast.lethal", "T1 lethal")
                : I18N.T("ai.hud.forecast.noLethal", "no T1 lethal");
            return I18N.T(
                "ai.hud.forecast.cardReward",
                "Forecast: ~{0}% win · route {1} nodes · next-fight EV {2} (IN {3}, {4})",
                winPct,
                prognosis.RouteNodes,
                prognosis.NextFightScore,
                prognosis.NextFightIncoming,
                lethalHint);
        }

        if (phase == GamePhase.Combat) {
            return I18N.T(
                "ai.hud.forecast.combat",
                "Forecast: ~{0}% win · beam depth full · route risk {1}",
                winPct,
                prognosis.PathRisk);
        }

        if (prognosis.CombatsToRest > 0f) {
            return I18N.T(
                "ai.hud.forecast.route",
                "Forecast: ~{0}% win · {1} nodes to boss · {2:0.#} fights to rest · risk {3}",
                winPct,
                prognosis.RouteNodes,
                prognosis.CombatsToRest,
                prognosis.PathRisk);
        }

        return I18N.T(
            "ai.hud.forecast.default",
            "Forecast: ~{0}% win · {1} nodes on route · risk {2}",
            winPct,
            prognosis.RouteNodes,
            prognosis.PathRisk);
    }

    public static string BuildStrategyLine(JsonObject snapshot, GamePhase phase) {
        return phase switch {
            GamePhase.Combat => BuildCombatStrategy(snapshot),
            GamePhase.MapSelection => BuildMapStrategy(snapshot),
            GamePhase.CardReward => BuildCardRewardStrategy(snapshot),
            GamePhase.Shop => BuildShopStrategy(snapshot),
            GamePhase.RestSite => BuildRestStrategy(snapshot),
            GamePhase.EventChoice => BuildEventStrategy(snapshot),
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

        var scoreHint = BuildDecisionScoreHint(decision);
        if (!string.IsNullOrWhiteSpace(scoreHint))
            line += $" ({scoreHint})";

        if (!string.IsNullOrWhiteSpace(reason))
            line += $" — {reason}";

        return I18N.T("ai.hud.next.prefix", "Next: {0}", line);
    }

    public static string? BuildParamStrip(JsonObject snapshot, GamePhase phase) {
        if (phase == GamePhase.Combat)
            return BuildCombatParams(snapshot);

        if (phase == GamePhase.CardReward)
            return BuildCardRewardParams(snapshot);

        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        return I18N.T("ai.hud.params.run", "F{0} G={1} HP={2}/{3}", floor, gold, hp, maxHp);
    }

    public static string? BuildScoreTerms(AiHudDecision? decision, GamePhase phase) {
        if (decision == null || string.IsNullOrWhiteSpace(decision.Reason))
            return null;

        if (phase == GamePhase.CardReward) {
            var marginal = MarginalRegex.Match(decision.Reason);
            var nextFight = NextFightRegex.Match(decision.Reason);
            if (marginal.Success || nextFight.Success) {
                var sb = new StringBuilder();
                if (marginal.Success)
                    sb.Append($"marginal:{marginal.Groups[1].Value}");
                if (nextFight.Success) {
                    if (sb.Length > 0) sb.Append(", ");
                    sb.Append($"nextFight:{nextFight.Groups[1].Value}");
                }
                return sb.ToString();
            }
        }

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
            return I18N.T("ai.hud.strategy.combat.safe", "No incoming damage — beam search for lethal and setup");

        if (IntentCalculator.IsFatalIfUnblocked(snapshot))
            return I18N.T("ai.hud.strategy.combat.fatal", "Fatal incoming {0} — block first, then beam", net);

        if (BlockThreatEvaluator.ShouldScoreBlock(snapshot))
            return I18N.T("ai.hud.strategy.combat.block", "Enemy deals {0} ({1} after block) — beam favors block", incoming, net);

        return I18N.T("ai.hud.strategy.combat.light", "Light threat {0} — full-depth beam balances kill and block", incoming);
    }

    static string BuildMapStrategy(JsonObject snapshot) {
        var plan = MapPathPlanner.CachedPlan;
        if (plan == null && AiPlayServices.StateProvider.TryGetRunAndPlayer(out var state, out var player))
            plan = MapPathPlanner.Plan(state, player, forceRefresh: false);

        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);

        if (plan == null)
            return I18N.T("ai.hud.strategy.map.none", "Pick the best reachable node for {0}", AiHudRunForecast.StyleLabel(profile.Style));

        var nextType = snapshot["mapNodes"]?[plan.NextChildIndex]?["pointType"]?.GetValue<string>() ?? "?";
        return I18N.T(
            "ai.hud.strategy.map.plan",
            "Route {0} → next {1} (risk {2}, {3:0.#} fights to rest)",
            plan.Summary,
            nextType,
            plan.PathRiskAtNext,
            plan.CombatsToRestAtNext);
    }

    static string BuildCardRewardStrategy(JsonObject snapshot) {
        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);
        var plan = DeckPlanInferer.Infer(snapshot);
        var preview = snapshot["nextFightPreview"]?.AsArray();
        var fightHint = preview != null && preview.Count > 0
            ? preview[0]?["roomType"]?.GetValue<string>() ?? "?"
            : null;
        var routeScore = NextFightDeckEvaluator.GetBaselineRouteScore(snapshot, plan);

        if (AiHudRunForecast.MeetsOfficialBig(profile))
            return I18N.T(
                "ai.hud.strategy.reward.big",
                "Big deck (≥40) — skip unless marginal+next-fight > 0 (route EV {0})",
                routeScore);

        if (fightHint != null)
            return I18N.T(
                "ai.hud.strategy.reward.nextFight",
                "MC×8 + beam d=3 vs {0} fights; pick only when total > 0 (route EV {1})",
                fightHint,
                routeScore);

        if (profile.ThinGap < 0 || AiHudRunForecast.MeetsOfficialSmall(profile))
            return I18N.T(
                "ai.hud.strategy.reward.small",
                "Small deck (≤20) — take high marginal picks (route EV {0})",
                routeScore);

        return I18N.T(
            "ai.hud.strategy.reward.default",
            "Marginal deck quality + next-fight sim; skip when score <= 0 (route EV {0})",
            routeScore);
    }

    static string BuildShopStrategy(JsonObject snapshot) {
        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        if (metrics.RemovalUplift >= DeckEvaluator.MinRemovalUplift)
            return I18N.T(
                "ai.hud.strategy.shop.remove",
                "Removal uplift {0} — thin {1} deck at shop",
                metrics.RemovalUplift,
                AiHudRunForecast.StyleLabel(profile.Style));
        return I18N.T(
            "ai.hud.strategy.shop.buy",
            "Buy relic/card/potion for {0}; skip bloat",
            AiHudRunForecast.StyleLabel(profile.Style));
    }

    static string BuildRestStrategy(JsonObject snapshot) {
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var ratio = maxHp > 0 ? (float)hp / maxHp : 1f;
        var prognosis = AiHudRunForecast.AnalyzeRun(snapshot);

        if (ratio < 0.55f)
            return I18N.T(
                "ai.hud.strategy.rest.heal",
                "Low HP ({0}/{1}) — rest heal (route risk {2})",
                hp,
                maxHp,
                prognosis.PathRisk);

        return I18N.T("ai.hud.strategy.rest.upgrade", "HP healthy — smith upgrade on core cards");
    }

    static string BuildEventStrategy(JsonObject snapshot) {
        var profile = AiHudRunForecast.AnalyzeDeck(snapshot);
        if (AiHudRunForecast.MeetsOfficialBig(profile))
            return I18N.T("ai.hud.strategy.event.big", "Big deck (≥40) — favor remove/transform; avoid bloat");
        if (profile.IsExhaustFocused)
            return I18N.T("ai.hud.strategy.event.small", "Small deck — favor exhaust synergies and removal");
        return I18N.T("ai.hud.strategy.event", "Evaluate options by deck synergy and codex priors");
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

    static string BuildCardRewardParams(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        return I18N.T(
            "ai.hud.params.cardReward",
            "F{0} G={1} HP={2}/{3} · sim MC×8 beam d=3",
            floor, gold, hp, maxHp);
    }

    static string? BuildDecisionScoreHint(AiHudDecision decision) {
        if (decision.Action is not (ActionType.PickCardReward or ActionType.SkipCardReward))
            return null;

        var marginal = MarginalRegex.Match(decision.Reason);
        var nextFight = NextFightRegex.Match(decision.Reason);
        if (!marginal.Success && !nextFight.Success)
            return null;

        var sb = new StringBuilder();
        if (marginal.Success)
            sb.Append($"Δdeck {marginal.Groups[1].Value}");
        if (nextFight.Success) {
            if (sb.Length > 0) sb.Append(", ");
            sb.Append($"Δfight {nextFight.Groups[1].Value}");
        }
        return sb.ToString();
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

        var marginalIdx = text.IndexOf(" marginal=", StringComparison.OrdinalIgnoreCase);
        if (marginalIdx >= 0)
            text = text[..marginalIdx];

        return text.Trim().TrimEnd('—', '-', ' ');
    }
}
