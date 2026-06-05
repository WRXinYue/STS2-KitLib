using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Combat;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.Combat.Simulation;

public static class CombatPlanner {
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

        var playable = state.Hand.Count(c => c.CanPlay && c.Cost <= state.Energy);
        var config = BeamConfig.ForHand(playable);
        var sw = Stopwatch.StartNew();

        BeamResult? best = null;
        for (int depth = 5; depth <= config.MaxDepth; depth += 2) {
            if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                break;

            var result = RunBeam(state, depth, config, sw);
            if (result.HasResult)
                best = result;
        }

        if (best is { Path: { Count: > 0 } path, Score: var beamScore, Depth: var beamDepth }) {
            var action = ToGameAction(path[0], state, $"Planner score={beamScore}");
            LogPick(snapshot, action, $"beam d={beamDepth} s={beamScore}");
            return action;
        }

        if (best is { Path: { Count: 0 }, Score: var endOnlyScore }) {
            var action = EndTurn($"Planner score={endOnlyScore}");
            LogPick(snapshot, action, $"beam end s={endOnlyScore}");
            return action;
        }

        var fallback = CombatScorer.PickBestCombatMove(snapshot);
        if (fallback != null)
            LogPick(snapshot, fallback, "fallback");
        return fallback;
    }

    static BeamResult RunBeam(CombatState root, int maxDepth, BeamConfig config, Stopwatch sw) {
        var beam = new List<BeamNode> {
            new(root, [], CombatEvaluator.EvaluateMidTurn(root)),
        };

        List<SimCombatAction>? bestPath = null;
        int bestScore = int.MinValue;
        int bestDepth = 0;

        for (int depth = 0; depth < maxDepth && sw.ElapsedMilliseconds < config.TimeBudgetMs; depth++) {
            var nextBeam = new List<BeamNode>();

            foreach (var node in beam) {
                if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                    break;

                foreach (var action in LegalActionGenerator.GenerateOrdered(node.State, config.MaxActionsPerNode)) {
                    if (action.Kind == SimActionKind.EndTurn) {
                        var afterTurn = CombatTurnResolver.ResolveEndTurn(node.State);
                        if (afterTurn.AliveEnemyCount == 0) {
                            int wipeScore = CombatEvaluator.EvaluateTerminal(afterTurn);
                            if (wipeScore > bestScore) {
                                bestScore = wipeScore;
                                bestPath = node.Path;
                                bestDepth = depth;
                            }
                            continue;
                        }

                        int postBudget = Math.Max(
                            120,
                            config.TimeBudgetMs - (int)sw.ElapsedMilliseconds);
                        int endScore = PostTurnSimulator.ScoreLine(afterTurn, postBudget, sw);
                        if (endScore > bestScore) {
                            bestScore = endScore;
                            bestPath = node.Path;
                            bestDepth = depth;
                        }
                        continue;
                    }

                    var next = CombatSimulator.Apply(node.State, action);
                    var newPath = node.Path.Append(action).ToList();

                    if (next.AliveEnemyCount == 0) {
                        int wipeScore = CombatEvaluator.EvaluateTerminal(next);
                        if (wipeScore > bestScore) {
                            bestScore = wipeScore;
                            bestPath = newPath;
                            bestDepth = depth + 1;
                        }
                        continue;
                    }

                    int score = depth + 1 >= maxDepth
                        ? CombatEvaluator.EvaluateTerminal(next)
                        : CombatEvaluator.EvaluateMidTurn(next);

                    if (depth + 1 >= maxDepth && score > bestScore) {
                        bestScore = score;
                        bestPath = newPath;
                        bestDepth = depth + 1;
                    }

                    nextBeam.Add(new BeamNode(next, newPath, score));
                }
            }

            if (nextBeam.Count == 0)
                break;

            beam = nextBeam
                .OrderByDescending(n => n.Score)
                .Take(config.BeamWidth)
                .ToList();

            var top = beam[0];
            if (top.Path.Count > 0 && top.Score > bestScore) {
                bestScore = top.Score;
                bestPath = top.Path;
                bestDepth = top.Path.Count;
            }
        }

        return new BeamResult(bestPath, bestScore, bestDepth);
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
            if (!card.IsAttack || !CombatCardCost.CanAfford(card, state)) continue;
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

    readonly record struct BeamNode(CombatState State, List<SimCombatAction> Path, int Score);

    readonly record struct BeamResult(List<SimCombatAction>? Path, int Score, int Depth) {
        public bool HasResult => Path != null && Score > int.MinValue;
    }

    readonly record struct BeamConfig(int MaxDepth, int BeamWidth, int TimeBudgetMs, int MaxActionsPerNode) {
        public static BeamConfig ForHand(int playableCards) {
            int depth = Math.Clamp(playableCards + 3, 10, 16);
            int width = Math.Clamp(playableCards * 4, 24, 48);
            int budget = playableCards >= 6 ? 650 : playableCards >= 4 ? 550 : 480;
            int actions = Math.Clamp(playableCards * 5, 20, 56);
            return new(depth, width, budget, actions);
        }
    }
}
