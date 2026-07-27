using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.AutoPlay.Scoring;
using KitLib.AI.Combat;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class CombatPlanner {
    static int _planTurnNumber = -1;
    static List<StablePlannedAction> _plannedRemainder = [];

    public static GameAction? PickBestMove(JsonObject snapshot) {
        var state = CombatState.FromSnapshot(snapshot);
        if (state.AliveEnemyCount == 0)
            return EndTurn("No enemies");

        InvalidatePlanIfNewTurn(state.TurnNumber);

        if (TryConsumePlannedMove(snapshot, state, out var plannedAction))
            return plannedAction;

        var playable = CombatCardCost.CountAffordable(state);
        var config = CombatBeamSearch.ForHand(playable, state.Potions.Count);
        var sw = Stopwatch.StartNew();

        BeamSearchResult? best = null;
        for (int depth = 5; depth <= config.MaxDepth; depth += 2) {
            if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                break;

            var result = CombatBeamSearch.Run(state, config with { MaxDepth = depth }, snapshot);
            if (result.HasResult)
                best = result;
        }

        if (best == null && sw.ElapsedMilliseconds < config.TimeBudgetMs) {
            var retry = CombatBeamSearch.Run(state, config, snapshot);
            if (retry.HasResult)
                best = retry;
        }

        if (best is { Path: { Count: > 0 } path, Score: var beamScore, Depth: var beamDepth }) {
            CachePlannedRemainder(state, path);
            var action = ToGameAction(path[0], state, $"Planner score={beamScore}");
            LogPick(snapshot, state, action, $"beam d={beamDepth} s={beamScore}", path);
            return action;
        }

        if (best is { Path: { Count: 0 }, Score: var endOnlyScore }) {
            ClearPlan();
            var action = EndTurn($"Planner score={endOnlyScore}");
            LogPick(snapshot, state, action, $"beam end s={endOnlyScore}", []);
            return action;
        }

        ClearPlan();
        var fallback = PickFallbackMove(snapshot, state);
        if (fallback != null)
            LogPick(snapshot, state, fallback, "fallback");
        return fallback;
    }

    static bool TryConsumePlannedMove(JsonObject snapshot, CombatState state, out GameAction? action) {
        action = null;
        if (_plannedRemainder.Count == 0)
            return false;

        var resolved = ResolveStableAction(state, _plannedRemainder[0]);
        if (resolved == null) {
            ClearPlan();
            return false;

        }

        _plannedRemainder.RemoveAt(0);
        action = ToGameAction(resolved, state, "Planner plan");
        LogPick(snapshot, state, action, "plan", BuildPathFromRemainder(state, resolved));
        return true;
    }

    static void InvalidatePlanIfNewTurn(int turnNumber) {
        if (_planTurnNumber != turnNumber) {
            ClearPlan();
            _planTurnNumber = turnNumber;
        }
    }

    static void ClearPlan() => _plannedRemainder.Clear();

    static void CachePlannedRemainder(CombatState root, IReadOnlyList<SimCombatAction> path) {
        ClearPlan();
        _planTurnNumber = root.TurnNumber;

        var sim = root;
        for (int i = 1; i < path.Count; i++) {
            _plannedRemainder.Add(StabilizeAction(sim, path[i]));
            sim = CombatSimulator.Apply(sim, path[i]);
        }
    }

    static List<SimCombatAction> BuildPathFromRemainder(CombatState state, SimCombatAction first) {
        var path = new List<SimCombatAction> { first };
        var sim = CombatSimulator.Apply(state, first);
        foreach (var planned in _plannedRemainder) {
            var resolved = ResolveStableAction(sim, planned);
            if (resolved == null)
                break;
            path.Add(resolved);
            sim = CombatSimulator.Apply(sim, resolved);
        }

        return path;
    }

    static StablePlannedAction StabilizeAction(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return new StablePlannedAction(SimActionKind.EndTurn, null, -1, -1, 0);

        if (action.Kind == SimActionKind.UsePotion) {
            var potion = state.Potions.FirstOrDefault(p => p.Slot == action.PotionSlot);
            return new StablePlannedAction(
                SimActionKind.UsePotion,
                potion?.Id,
                action.EnemyIndex,
                action.PotionSlot,
                action.McBranch);
        }

        var card = state.Hand[action.HandIndex];
        return new StablePlannedAction(
            SimActionKind.PlayCard,
            card.Id,
            action.EnemyIndex,
            -1,
            0);
    }

    static SimCombatAction? ResolveStableAction(CombatState state, StablePlannedAction planned) {
        if (planned.Kind == SimActionKind.EndTurn)
            return new SimCombatAction(SimActionKind.EndTurn);

        if (planned.Kind == SimActionKind.UsePotion) {
            if (state.PotionUsedThisTurn)
                return null;

            var potion = state.Potions.FirstOrDefault(p =>
                p.Slot == planned.PotionSlot
                || (planned.CardId != null && string.Equals(p.Id, planned.CardId, StringComparison.OrdinalIgnoreCase)));
            if (potion == null)
                return null;

            return new SimCombatAction(
                SimActionKind.UsePotion,
                -1,
                planned.EnemyIndex,
                potion.Slot,
                planned.McBranch);
        }

        if (planned.CardId == null)
            return null;

        SimCombatAction? best = null;
        int bestRank = int.MinValue;

        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!string.Equals(card.Id, planned.CardId, StringComparison.OrdinalIgnoreCase))
                continue;
            if (!CombatCardCost.CanAfford(card, state))
                continue;

            if (CombatTargetTypes.NeedsEnemyTarget(card)) {
                if (planned.EnemyIndex >= 0) {
                    var enemy = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == planned.EnemyIndex);
                    if (enemy == null || !ThreatModel.IsViableAttackTarget(state, enemy))
                        continue;

                    var action = new SimCombatAction(SimActionKind.PlayCard, i, planned.EnemyIndex);
                    int rank = CombatSetupEvaluator.RankPlayAction(state, action);
                    if (rank > bestRank) {
                        bestRank = rank;
                        best = action;
                    }

                    continue;
                }

                foreach (var enemy in state.Enemies.Where(e => ThreatModel.IsViableAttackTarget(state, e))) {
                    var action = new SimCombatAction(SimActionKind.PlayCard, i, enemy.Index);
                    int rank = CombatSetupEvaluator.RankPlayAction(state, action);
                    if (rank > bestRank) {
                        bestRank = rank;
                        best = action;
                    }
                }

                continue;
            }

            var selfAction = new SimCombatAction(SimActionKind.PlayCard, i, -1);
            int selfRank = CombatSetupEvaluator.RankPlayAction(state, selfAction);
            if (selfRank > bestRank) {
                bestRank = selfRank;
                best = selfAction;
            }
        }

        return best;
    }

    static GameAction? PickFallbackMove(JsonObject snapshot, CombatState state) {
        var ranked = CombatScorer.ScoreLegalMovesDetailed(snapshot)
            .OrderByDescending(x => x.Score)
            .ToList();

        foreach (var scored in ranked) {
            if (scored.Action.Type == ActionType.EndTurn)
                continue;
            if (scored.Action.Type == ActionType.PlayCard
                && IsPureBlockFallback(state, scored.Action))
                continue;

            return scored.Action;
        }

        return ranked.FirstOrDefault()?.Action;
    }

    static bool IsPureBlockFallback(CombatState state, GameAction action) {
        if (action.Type != ActionType.PlayCard
            || action.TargetIndex < 0
            || action.TargetIndex >= state.Hand.Count)
            return false;

        return BlockDefensePolicy.IsPureBlockCard(state.Hand[action.TargetIndex], state);
    }

    public static void LogPick(
        JsonObject snapshot,
        CombatState state,
        GameAction action,
        string note = "planner",
        IReadOnlyList<SimCombatAction>? beamPath = null) {
        if (!CombatDecisionLog.VerboseEnabled) return;
        var ranked = CombatScorer.ScoreLegalMovesDetailed(snapshot)
            .Concat(ScorePotionMoves(state, snapshot))
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();
        CombatDecisionLog.LogPick(snapshot, action, ranked, note, FormatBeamPath(state, beamPath));
    }

    static string FormatBeamPath(CombatState state, IReadOnlyList<SimCombatAction>? path) {
        if (path == null)
            return "";
        if (path.Count == 0)
            return "LINE=naked-end";

        var parts = new List<string>(Math.Min(path.Count, 5));
        var s = state;
        foreach (var action in path.Take(5)) {
            parts.Add(FormatBeamStep(s, action));
            s = CombatSimulator.Apply(s, action);
        }

        var line = string.Join(">", parts);
        if (path.Count > 5)
            line += $">...+{path.Count - 5}";
        return $"LINE={line}";
    }

    static string FormatBeamStep(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return "EndTurn";
        if (action.Kind == SimActionKind.UsePotion)
            return FormatPotionLabel(state, action);
        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return "?";

        var card = state.Hand[action.HandIndex];
        return action.EnemyIndex >= 0
            ? $"{card.Id}→e{action.EnemyIndex}"
            : card.Id;
    }

    static string FormatPotionLabel(CombatState state, SimCombatAction action) {
        var potion = state.Potions.FirstOrDefault(p => p.Slot == action.PotionSlot);
        var id = potion?.Id ?? "?";
        if (id.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase))
            id = id["POTION.".Length..];
        if (action.McBranch > 0)
            id += $"#{action.McBranch}";
        return action.EnemyIndex >= 0 ? $"{id}→e{action.EnemyIndex}" : id;
    }

    static GameAction ToGameAction(SimCombatAction action, CombatState state, string reason) {
        if (action.Kind == SimActionKind.EndTurn)
            return EndTurn(reason);

        if (action.Kind == SimActionKind.UsePotion) {
            var potion = state.Potions.FirstOrDefault(p => p.Slot == action.PotionSlot);
            var label = potion?.Id ?? "potion";
            if (label.StartsWith("POTION.", StringComparison.OrdinalIgnoreCase))
                label = label["POTION.".Length..];
            return new GameAction {
                Type = ActionType.UsePotion,
                TargetIndex = action.PotionSlot,
                SecondaryIndex = action.EnemyIndex,
                Reason = $"{label} score={reason}",
            };
        }

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return EndTurn($"Invalid hand index {action.HandIndex}");

        var card = state.Hand[action.HandIndex];
        return new GameAction {
            Type = ActionType.PlayCard,
            TargetIndex = action.HandIndex,
            SecondaryIndex = action.EnemyIndex,
            Reason = $"{card.Name} score={reason}",
        };
    }

    static GameAction EndTurn(string reason) => new() {
        Type = ActionType.EndTurn,
        Reason = reason,
    };

    static IEnumerable<CombatMoveScore> ScorePotionMoves(CombatState state, JsonObject snapshot) {
        foreach (var simAction in LegalActionGenerator.GenerateOrdered(state, maxActions: 16, snapshot)) {
            if (simAction.Kind != SimActionKind.UsePotion)
                continue;

            int score = CombatActionHeuristic.QuickScore(state, simAction, snapshot);
            if (score <= int.MinValue + 1)
                continue;

            var move = SimMoveScoring.ToGameAction(simAction, state);
            var label = FormatPotionLabel(state, simAction);
            yield return new CombatMoveScore(
                move with { Reason = $"{label} line:+{score}" },
                score,
                [$"line:+{score}"]);
        }
    }

    readonly record struct StablePlannedAction(
        SimActionKind Kind,
        string? CardId,
        int EnemyIndex,
        int PotionSlot,
        int McBranch);
}
