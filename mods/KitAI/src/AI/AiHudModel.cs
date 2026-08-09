using System;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using KitLib.AI.Sts2;

namespace KitLib.AI;

/// <summary>Player-facing HUD text from snapshots and last decision (no re-decide).</summary>
public static class AiHudModel {
    static readonly Regex ScoreBracketRegex = new(@"\s*\[[^\]]*\]", RegexOptions.Compiled);
    static readonly Regex MarginalRegex = new(@"marginal=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex NextFightRegex = new(@"nextFight=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex CardScoreRegex = new(@"score=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    static readonly Regex MapScoreRegex = new(@"score=(-?\d+)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    public static string BuildTelemetryLine(JsonObject snapshot, GamePhase phase) => phase switch {
        GamePhase.Combat => BuildCombatTelemetryLine(snapshot),
        GamePhase.MapSelection => BuildMapTelemetryLine(snapshot),
        GamePhase.CardReward => BuildCardRewardTelemetryLine(snapshot),
        _ => BuildRunTelemetryLine(snapshot),
    };

    public static string BuildNextActionLine(AiHudDecision? decision, JsonObject snapshot) {
        if (decision == null)
            return I18N.T("ai.hud.next.waiting", "Next: waiting for decision");

        if (decision.Phase == GamePhase.Combat)
            return BuildCombatNextActionLine(decision, snapshot);

        return BuildNonCombatNextActionLine(decision, snapshot);
    }

    public static string? BuildAuxLine(AiHudDecision? decision, GamePhase phase) {
        if (decision == null)
            return null;

        if (phase == GamePhase.Combat)
            return BuildCombatAuxLine(decision);

        if (phase == GamePhase.CardReward)
            return BuildCardRewardAuxLine(decision);

        if (phase == GamePhase.MapSelection)
            return BuildMapAuxLine(decision);

        return null;
    }

    /// <summary>Sim-derived combat metrics aligned with <see cref="CombatDecisionLog"/>.</summary>
    public static string BuildCombatTelemetryLine(JsonObject snapshot) {
        var state = CombatState.FromSnapshot(snapshot);
        int incoming = ThreatModel.IncomingDamage(state);
        int net = ThreatModel.NetDamageAfterBlock(state);
        int nonDamage = ThreatModel.TotalNonDamageThreat(state);
        int nextTurn = ThreatModel.NextTurnIncoming(state);
        int junk = DeckPollutionEvaluator.JunkCount(state);
        int pollution = DeckPollutionEvaluator.EffectivePollutionBurden(state);
        int playDamage = DeckPollutionEvaluator.ExpectedPlayableDamage(state);
        int playBlock = DeckPollutionEvaluator.ExpectedPlayableBlock(state);
        int setup = CombatSetupEvaluator.ComputeSetupDebt(state);

        return I18N.T(
            "ai.hud.telemetry.combat",
            "HP {0}/{1} BLK {2} E {3} · IN {4} NET {5} ND {6} NXT {7} · JUNK {8} POLL {9} PLAY {10}/{11} SETUP {12}",
            state.PlayerHp,
            state.PlayerMaxHp,
            state.PlayerBlock,
            state.Energy,
            incoming,
            net,
            nonDamage,
            nextTurn,
            junk,
            pollution,
            playDamage,
            playBlock,
            setup);
    }

    public static string? BuildCombatAuxLine(AiHudDecision? decision) {
        if (decision == null || string.IsNullOrWhiteSpace(decision.Reason))
            return null;

        var beam = Regex.Match(decision.Reason, @"beam d=(\d+)", RegexOptions.IgnoreCase);
        var score = Regex.Match(decision.Reason, @"Planner score=(-?\d+)", RegexOptions.IgnoreCase);
        if (!beam.Success && !score.Success)
            return null;

        if (beam.Success && score.Success)
            return I18N.T("ai.hud.beam.meta", "beam d={0} · score {1}", beam.Groups[1].Value, score.Groups[1].Value);
        if (score.Success)
            return I18N.T("ai.hud.beam.score", "score {0}", score.Groups[1].Value);
        return I18N.T("ai.hud.beam.depth", "beam d={0}", beam.Groups[1].Value);
    }

    static string BuildRunTelemetryLine(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var plan = MapPathPlanner.CachedPlan;
        int pathRisk = plan?.PathRiskAtNext ?? 0;
        float restIn = plan?.CombatsToRestAtNext ?? 0f;

        return I18N.T(
            "ai.hud.telemetry.run",
            "F{0} G={1} HP={2}/{3} deck={4} · risk {5} restIn {6:0.#}",
            floor, gold, hp, maxHp, deckSize, pathRisk, restIn);
    }

    static string BuildMapTelemetryLine(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var plan = MapPathPlanner.CachedPlan;

        if (plan == null)
            return BuildRunTelemetryLine(snapshot);

        var nextType = snapshot["mapNodes"]?[plan.NextChildIndex]?["pointType"]?.GetValue<string>() ?? "?";
        return I18N.T(
            "ai.hud.telemetry.map",
            "F{0} G={1} HP={2}/{3} deck={4} · {5} → {6} · risk {7} restIn {8:0.#}",
            floor, gold, hp, maxHp, deckSize,
            plan.Summary, nextType, plan.PathRiskAtNext, plan.CombatsToRestAtNext);
    }

    static string BuildCardRewardTelemetryLine(JsonObject snapshot) {
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 0;
        var deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var preview = snapshot["nextFightPreview"]?.AsArray();
        string room = "?";
        int incoming = 0;
        if (preview != null && preview.Count > 0 && preview[0] is JsonObject fight) {
            room = fight["roomType"]?.GetValue<string>() ?? "?";
            incoming = fight["incomingTurn1"]?.GetValue<int>() ?? 0;
        }

        return I18N.T(
            "ai.hud.telemetry.cardReward",
            "F{0} G={1} HP={2}/{3} deck={4} · next {5} IN {6}",
            floor, gold, hp, maxHp, deckSize, room, incoming);
    }

    static string? BuildCardRewardAuxLine(AiHudDecision decision) {
        if (string.IsNullOrWhiteSpace(decision.Reason))
            return null;

        var score = CardScoreRegex.Match(decision.Reason);
        var marginal = MarginalRegex.Match(decision.Reason);
        var nextFight = NextFightRegex.Match(decision.Reason);
        if (!score.Success && !marginal.Success && !nextFight.Success)
            return null;

        var scoreVal = score.Success ? score.Groups[1].Value : "—";
        var marginalVal = marginal.Success ? marginal.Groups[1].Value : "—";
        var nextFightVal = nextFight.Success ? nextFight.Groups[1].Value : "—";
        return I18N.T(
            "ai.hud.aux.cardReward",
            "score {0} · Δdeck {1} · Δfight {2}",
            scoreVal, marginalVal, nextFightVal);
    }

    static string? BuildMapAuxLine(AiHudDecision decision) {
        if (string.IsNullOrWhiteSpace(decision.Reason))
            return null;

        var score = MapScoreRegex.Match(decision.Reason);
        if (!score.Success)
            return null;

        return I18N.T("ai.hud.aux.map", "pathScore {0}", score.Groups[1].Value);
    }

    static string BuildCombatNextActionLine(AiHudDecision decision, JsonObject snapshot) {
        if (decision.Action == ActionType.EndTurn)
            return I18N.T("ai.hud.next.endTurn", "Next: End turn");

        if (decision.Action == ActionType.UsePotion) {
            var slot = decision.TargetIndex;
            var target = decision.SecondaryIndex >= 0 ? $"→e{decision.SecondaryIndex}" : "";
            return I18N.T("ai.hud.next.potion", "Next: Potion #{0}{1}", slot, target);
        }

        if (decision.Action == ActionType.PlayCard) {
            var hand = snapshot["combat"]?["hand"]?.AsArray();
            string card = "?";
            if (hand != null && decision.TargetIndex >= 0 && decision.TargetIndex < hand.Count)
                card = hand[decision.TargetIndex]?["name"]?.GetValue<string>()
                    ?? hand[decision.TargetIndex]?["id"]?.GetValue<string>()
                    ?? "?";

            if (decision.SecondaryIndex >= 0) {
                var enemies = snapshot["combat"]?["enemies"]?.AsArray();
                var enemy = EnemyIndexResolver.FindByCombatIndex(enemies, decision.SecondaryIndex);
                var monsterId = enemy?["monsterId"]?.GetValue<string>();
                var target = !string.IsNullOrWhiteSpace(monsterId)
                    ? $"→{monsterId}"
                    : $"→e{decision.SecondaryIndex}";
                return I18N.T("ai.hud.next.playTarget", "Next: {0} {1}", card, target);
            }

            return I18N.T("ai.hud.next.play", "Next: {0}", card);
        }

        return I18N.T("ai.hud.next.prefix", "Next: {0}", ActionVerb(decision.Action));
    }

    static string BuildNonCombatNextActionLine(AiHudDecision decision, JsonObject snapshot) {
        if (decision.Action == ActionType.SelectMapNode) {
            var node = ExtractMapTarget(decision.Reason);
            if (!string.IsNullOrWhiteSpace(node))
                return I18N.T("ai.hud.next.map", "Next: → {0}", node);
        }

        if (decision.Action == ActionType.PickCardReward) {
            var name = ExtractBracketName(decision.Reason);
            if (!string.IsNullOrWhiteSpace(name))
                return I18N.T("ai.hud.next.pickCard", "Next: Pick {0}", name);
            return I18N.T("ai.hud.action.pickCard", "Pick card");
        }

        if (decision.Action == ActionType.SkipCardReward)
            return I18N.T("ai.hud.next.skipCard", "Next: Skip");

        if (decision.Action == ActionType.UpgradeCard) {
            var name = ExtractBracketName(decision.Reason);
            if (!string.IsNullOrWhiteSpace(name))
                return I18N.T("ai.hud.next.upgrade", "Next: Upgrade {0}", name);
        }

        if (decision.Action == ActionType.RemoveCardAtShop) {
            var name = ExtractBracketName(decision.Reason);
            if (!string.IsNullOrWhiteSpace(name))
                return I18N.T("ai.hud.next.remove", "Next: Remove {0}", name);
        }

        if (decision.Action == ActionType.PickRelic) {
            var name = ExtractBracketName(decision.Reason);
            if (!string.IsNullOrWhiteSpace(name))
                return I18N.T("ai.hud.next.relic", "Next: Relic {0}", name);
        }

        if (decision.Action == ActionType.SelectEventChoice) {
            var name = ExtractBracketName(decision.Reason);
            if (!string.IsNullOrWhiteSpace(name))
                return I18N.T("ai.hud.next.event", "Next: {0}", name);
        }

        if (decision.Action == ActionType.PurchaseShopItem) {
            var detail = ExtractShopPurchase(decision.Reason);
            if (!string.IsNullOrWhiteSpace(detail))
                return I18N.T("ai.hud.next.buy", "Next: Buy {0}", detail);
        }

        if (decision.Action == ActionType.Rest)
            return I18N.T("ai.hud.next.rest", "Next: Rest");

        if (decision.Action == ActionType.Proceed)
            return I18N.T("ai.hud.next.proceed", "Next: Proceed");

        var verb = ActionVerb(decision.Action);
        var detail2 = ActionDetail(decision, snapshot);
        var line = string.IsNullOrWhiteSpace(detail2) ? verb : $"{verb} · {detail2}";
        return I18N.T("ai.hud.next.prefix", "Next: {0}", line);
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

    static string? ExtractBracketName(string? reason) {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var cardPick = Regex.Match(reason, @"Card pick \[(.+?)\]");
        if (cardPick.Success)
            return cardPick.Groups[1].Value.Trim();

        var pick = Regex.Match(reason, @"\[(.+?)\]");
        if (pick.Success)
            return pick.Groups[1].Value.Trim();

        return null;
    }

    static string? ExtractMapTarget(string? reason) {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var map = Regex.Match(reason, @"Map → (\S+)");
        return map.Success ? map.Groups[1].Value.Trim() : null;
    }

    static string? ExtractShopPurchase(string? reason) {
        if (string.IsNullOrWhiteSpace(reason))
            return null;

        var buy = Regex.Match(reason, @"Buy (\S+)");
        if (buy.Success)
            return buy.Groups[1].Value.Trim();

        var potion = Regex.Match(reason, @"potion \[(.+?)\]");
        if (potion.Success)
            return potion.Groups[1].Value.Trim();

        return ExtractBracketName(reason);
    }
}
