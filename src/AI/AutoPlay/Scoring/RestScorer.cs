using System;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using KitLib.AI.Sts2;
using MegaCrit.Sts2.Core.Map;

namespace KitLib.AI.AutoPlay.Scoring;

public static class RestScorer {
    public static GameAction PickBest(JsonObject snapshot) {
        if (snapshot["restProceedReady"]?.GetValue<bool>() == true)
            return new GameAction { Type = ActionType.Proceed, Reason = "Leave rest site" };

        var options = snapshot["restOptions"]?.AsArray();
        if (IsRestChoiceConsumed(options))
            return new GameAction { Type = ActionType.Proceed, Reason = "Leave rest site (action used)" };
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var hpRatio = maxHp > 0 ? (float)hp / maxHp : 1f;
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        var upgradeScore = MapUpgradeEvaluator.BestDeckUpgradeScore(snapshot, plan);
        var eliteAhead = NextNodeIsElite();
        var pathPressure = HasPathSurvivalPressure(metrics);

        if (options == null || options.Count == 0)
            return new GameAction { Type = ActionType.Proceed, Reason = "Leave rest site (no options)" };

        int healIdx = FindOption(options, "HEAL", "REST");
        int smithIdx = FindOption(options, "SMITH", "UPGRADE");

        if (healIdx < 0)
            return new GameAction { Type = ActionType.Proceed, Reason = "Leave rest site (heal used)" };

        var urgentHealThreshold = pathPressure ? 0.65f : 0.55f;
        if (hpRatio < urgentHealThreshold || (hpRatio < 0.7f && eliteAhead)) {
            return new GameAction {
                Type = ActionType.Rest,
                TargetIndex = healIdx,
                Reason = pathPressure
                    ? $"Rest heal (path pressure) HP {hp}/{maxHp}"
                    : $"Rest heal HP {hp}/{maxHp}",
            };
        }

        var prior = CodexPriorCatalog.GetPreferredRestChoice(snapshot);
        if (!string.IsNullOrEmpty(prior)) {
            int priorIdx = FindOption(options, prior);
            var isSmithPrior = prior.Contains("SMITH", StringComparison.OrdinalIgnoreCase)
                || prior.Contains("UPGRADE", StringComparison.OrdinalIgnoreCase);
            if (priorIdx >= 0 && hpRatio is >= 0.45f and <= 0.85f) {
                if (!(isSmithPrior && hpRatio < 0.75f && eliteAhead)) {
                    var actionType = isSmithPrior ? ActionType.UpgradeCard : ActionType.Rest;
                    return new GameAction {
                        Type = actionType,
                        TargetIndex = priorIdx,
                        Reason = $"Rest prior {prior} HP {hp}/{maxHp}",
                    };
                }
            }
        }

        if (smithIdx >= 0 && ShouldSmith(snapshot, plan, hpRatio, eliteAhead, upgradeScore)) {
            return new GameAction {
                Type = ActionType.UpgradeCard,
                TargetIndex = smithIdx,
                Reason = upgradeScore >= MapUpgradeEvaluator.StrongUpgradeThreshold
                    ? $"Smith priority upgradeScore={upgradeScore} HP {hp}/{maxHp}"
                    : "Open smith to upgrade",
            };
        }

        var mildHealThreshold = pathPressure ? 0.65f : 0.75f;
        if (healIdx >= 0 && hpRatio < mildHealThreshold) {
            return new GameAction {
                Type = ActionType.Rest,
                TargetIndex = healIdx,
                Reason = $"Rest (HP {hp}/{maxHp})",
            };
        }

        if (smithIdx >= 0) {
            return new GameAction {
                Type = ActionType.UpgradeCard,
                TargetIndex = smithIdx,
                Reason = "Smith (HP healthy)",
            };
        }

        return new GameAction { Type = ActionType.Proceed, Reason = "Leave rest site" };
    }

    static bool HasPathSurvivalPressure(DeckMetrics metrics) {
        var cached = MapPathPlanner.CachedPlan;
        if (cached == null) return false;
        return cached.CombatsToRestAtNext >= 3f && metrics.BlockDeficit >= 1;
    }

    static bool NextNodeIsElite() {
        var cached = MapPathPlanner.CachedPlan;
        if (cached == null) return false;
        if (!AiPlayServices.StateProvider.TryGetRunAndPlayer(out var state, out _))
            return false;

        var point = state.Map?.GetPoint(cached.NextCoord);
        return point?.PointType == MapPointType.Elite;
    }

    static bool IsRestChoiceConsumed(JsonArray? options) {
        if (options == null) return false;

        foreach (var node in options) {
            if (node is not JsonObject opt) continue;
            if (opt["enabled"]?.GetValue<bool>() != false) continue;
            var id = opt["optionId"]?.GetValue<string>() ?? "";
            if (IsRestActionId(id))
                return true;
        }

        return false;
    }

    static bool IsRestActionId(string id) {
        var upper = id.ToUpperInvariant();
        return upper.Contains("HEAL", StringComparison.Ordinal)
            || upper.Contains("REST", StringComparison.Ordinal)
            || upper.Contains("SMITH", StringComparison.Ordinal)
            || upper.Contains("UPGRADE", StringComparison.Ordinal);
    }

    static int FindOption(JsonArray options, params string[] ids) {
        for (int i = 0; i < options.Count; i++) {
            if (options[i] is not JsonObject opt) continue;
            if (opt["enabled"]?.GetValue<bool>() == false) continue;
            var optId = opt["optionId"]?.GetValue<string>() ?? "";
            if (ids.Any(id => optId.Contains(id, StringComparison.OrdinalIgnoreCase)))
                return opt["index"]?.GetValue<int>() ?? i;
        }
        return -1;
    }

    static bool ShouldSmith(
        JsonObject snapshot,
        DeckPlan plan,
        float hpRatio,
        bool eliteAhead,
        int upgradeScore) {
        if (!HasUpgradeTarget(snapshot, plan))
            return false;

        if (upgradeScore >= MapUpgradeEvaluator.CriticalUpgradeThreshold)
            return hpRatio >= 0.6f && (!eliteAhead || hpRatio >= 0.8f);

        if (upgradeScore >= MapUpgradeEvaluator.StrongUpgradeThreshold)
            return hpRatio >= 0.65f && (!eliteAhead || hpRatio >= 0.85f);

        return hpRatio >= 0.75f && !eliteAhead;
    }

    static bool HasUpgradeTarget(JsonObject snapshot, DeckPlan plan) {
        var deck = snapshot["deck"]?.AsArray();
        if (deck == null) return false;

        foreach (var node in deck) {
            if (node is not JsonObject card) continue;
            var upgrade = card["upgradeLevel"]?.GetValue<int>() ?? 0;
            var maxUpgrade = card["maxUpgradeLevel"]?.GetValue<int>() ?? 1;
            if (upgrade >= maxUpgrade) continue;

            var tags = CardCatalog.ResolveTags(
                card["id"]?.GetValue<string>(),
                card["cardType"]?.GetValue<string>(),
                card["keywords"]?.AsArray());
            if (DeckPlanInferer.ScoreTags(tags, plan) >= 1f)
                return true;
        }
        return false;
    }
}
