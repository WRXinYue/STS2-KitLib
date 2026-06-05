using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Combat.Simulation;

public static class CombatPlanner {
    const int TimeBudgetMs = 160;
    const int MaxDepth = 4;
    const int BeamWidth = 10;

    public static GameAction? PickBestMove(JsonObject snapshot) {
        var state = CombatState.FromSnapshot(snapshot);
        if (state.AliveEnemyCount == 0)
            return EndTurn("No enemies");

        var mustBlock = ThreatModel.IsFatalIfUnblocked(state)
            && !BlockThreatEvaluator.ShouldSuppressTransform(snapshot);

        if (!mustBlock && !BlockThreatEvaluator.ShouldSuppressTransform(snapshot)) {
            if (SimLethalChecker.CanLethalAfterTransform(state, out var tTarget, out var tIdx)) {
                var action = ToGameAction(
                    new SimCombatAction(SimActionKind.PlayCard, tIdx, -1),
                    state,
                    $"Lethal setup: transform → enemy {tTarget}");
                LogPick(snapshot, action, "lethal-transform");
                return action;
            }

            var aoe = AoeDamageEstimator.FindBestAoeLethalAction(state);
            if (aoe != null) {
                var action = ToGameAction(
                    aoe,
                    state,
                    $"AOE lethal x{AoeDamageEstimator.EstimateAoeKills(state, state.Hand[aoe.HandIndex].Damage)}");
                LogPick(snapshot, action, "aoe-lethal");
                return action;
            }

            if (SimLethalChecker.CanLethal(state, out var lethalTarget)) {
                var move = FindLethalPlay(state, lethalTarget);
                if (move != null) {
                    LogPick(snapshot, move, "lethal");
                    return move;
                }
            }
        }

        var sw = Stopwatch.StartNew();
        var beam = new List<(CombatState State, List<SimCombatAction> Path, int Score)> {
            (state, [], CombatEvaluator.Evaluate(state)),
        };

        List<SimCombatAction>? bestPath = null;
        int bestScore = int.MinValue;

        for (int depth = 0; depth < MaxDepth && sw.ElapsedMilliseconds < TimeBudgetMs; depth++) {
            var nextBeam = new List<(CombatState, List<SimCombatAction>, int)>();

            foreach (var (node, path, _) in beam) {
                if (sw.ElapsedMilliseconds >= TimeBudgetMs) break;

                foreach (var action in LegalActionGenerator.Generate(node)) {
                    if (action.Kind == SimActionKind.EndTurn) {
                        int endScore = CombatEvaluator.Evaluate(node);
                        if (endScore > bestScore) {
                            bestScore = endScore;
                            bestPath = path;
                        }
                        continue;
                    }

                    var next = CombatSimulator.Apply(node, action);
                    var newPath = path.Append(action).ToList();
                    int score = CombatEvaluator.Evaluate(next) + depth * 2;

                    if (next.AliveEnemyCount == 0) {
                        if (score > bestScore) {
                            bestScore = score;
                            bestPath = newPath;
                        }
                        continue;
                    }

                    nextBeam.Add((next, newPath, score));
                }
            }

            if (nextBeam.Count == 0) break;

            beam = nextBeam
                .OrderByDescending(x => x.Item3)
                .Take(BeamWidth)
                .ToList();

            var top = beam[0];
            if (top.Item3 > bestScore && top.Item2.Count > 0) {
                bestScore = top.Item3;
                bestPath = top.Item2;
            }
        }

        if (bestPath is { Count: > 0 }) {
            var action = ToGameAction(bestPath[0], state, $"Planner score={bestScore}");
            LogPick(snapshot, action, $"beam={bestScore}");
            return action;
        }

        var fallback = CombatScorer.PickBestCombatMove(snapshot);
        if (fallback != null)
            LogPick(snapshot, fallback, "fallback");
        return fallback;
    }

    public static void LogPick(JsonObject snapshot, GameAction action, string note = "planner") {
        if (!CombatDecisionLog.VerboseEnabled) return;
        var ranked = CombatScorer.ScoreLegalMovesDetailed(snapshot)
            .OrderByDescending(x => x.Score)
            .Take(5)
            .ToList();
        CombatDecisionLog.LogPick(snapshot, action, ranked, note);
    }

    static GameAction? FindLethalPlay(CombatState state, int targetIndex) {
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!card.CanPlay || !card.IsAttack || card.Cost > state.Energy) continue;
            return ToGameAction(new SimCombatAction(SimActionKind.PlayCard, i, targetIndex), state, "Lethal attack");
        }
        return null;
    }

    static GameAction ToGameAction(SimCombatAction action, CombatState state, string reason) {
        if (action.Kind == SimActionKind.EndTurn)
            return EndTurn(reason);

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
}
