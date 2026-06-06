using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class CombatBeamSearch {
    public static readonly BeamSearchOptions ShallowMacro = new(3, 12, 12, 18);

    public static BeamSearchOptions ForHand(int playableCards, int potionCount = 0) {
        int depth = Math.Clamp(playableCards + 3, 10, 16);
        int width = Math.Clamp(playableCards * 4 + 4, 28, 52);
        int budget = playableCards >= 6 ? 730 : playableCards >= 4 ? 630 : 560;
        int potionActions = Math.Min(potionCount * (1 + LegalActionGenerator.MaxMcBranches), 12);
        int actions = Math.Clamp(playableCards * 5 + potionActions, 20, 56);
        return new BeamSearchOptions(depth, width, budget, actions);
    }

    public static int RunBestScore(
        CombatState root,
        BeamSearchOptions options,
        JsonObject? rootSnapshot = null) {
        var result = Run(root, options, rootSnapshot);
        return result.HasResult ? result.Score : int.MinValue;
    }

    public static BeamSearchResult Run(
        CombatState root,
        BeamSearchOptions options,
        JsonObject? rootSnapshot = null) {
        var sw = Stopwatch.StartNew();
        return RunBeam(root, options.MaxDepth, options, sw, rootSnapshot);
    }

    static BeamSearchResult RunBeam(
        CombatState root,
        int maxDepth,
        BeamSearchOptions config,
        Stopwatch sw,
        JsonObject? rootSnapshot) {
        var beam = new List<BeamNode> {
            new(root, [], RankLine(root)),
        };

        List<SimCombatAction>? bestPath = null;
        CombatSetupEvaluator.CombatLineOutcome? bestOutcome = null;
        int bestDepth = 0;

        for (int depth = 0; depth < maxDepth && sw.ElapsedMilliseconds < config.TimeBudgetMs; depth++) {
            var nextBeam = new List<BeamNode>();
            foreach (var node in beam) {
                if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                    break;

                foreach (var action in LegalActionGenerator.GenerateOrdered(
                    node.State, config.MaxActionsPerNode, rootSnapshot)) {
                    if (action.Kind == SimActionKind.EndTurn)
                        continue;

                    var next = CombatSimulator.Apply(node.State, action);
                    var newPath = node.Path.Append(action).ToList();

                    if (next.AliveEnemyCount == 0) {
                        ConsiderLeaf(root, next, newPath, depth + 1, rootSnapshot,
                            ref bestPath, ref bestOutcome, ref bestDepth);
                        continue;
                    }

                    ConsiderLeaf(root, next, newPath, depth + 1, rootSnapshot,
                        ref bestPath, ref bestOutcome, ref bestDepth);

                    bool exhausted = next.Energy <= 0 || !CombatCardCost.HasAffordablePlay(next);
                    if (exhausted || depth + 1 >= maxDepth)
                        continue;

                    nextBeam.Add(new BeamNode(next, newPath, RankLine(next)));
                }
            }

            if (depth == 0 && nextBeam.Count > 0) {
                CombatDebugTrace.LogBeamDepthCandidates(
                    root,
                    nextBeam.OrderByDescending(n => n.Score)
                        .Select(n => ((IReadOnlyList<SimCombatAction>)n.Path, n.Score)),
                    depth + 1);
            }

            if (nextBeam.Count == 0)
                break;

            beam = nextBeam
                .OrderByDescending(n => n.Score)
                .Take(config.BeamWidth)
                .ToList();
        }

        int score = int.MinValue;
        if (bestOutcome.HasValue) {
            score = CombatSetupEvaluator.PackLineScore(bestOutcome.Value);
            if (bestPath is { Count: > 0 })
                score += SimMoveScoring.OpeningModifierBonus(root, bestPath[0], rootSnapshot);
        }

        return new BeamSearchResult(bestPath, score, bestDepth);
    }

    static void ConsiderLeaf(
        CombatState root,
        CombatState state,
        List<SimCombatAction> path,
        int depth,
        JsonObject? rootSnapshot,
        ref List<SimCombatAction>? bestPath,
        ref CombatSetupEvaluator.CombatLineOutcome? bestOutcome,
        ref int bestDepth) {
        CombatSetupEvaluator.CombatLineOutcome outcome = state.AliveEnemyCount == 0
            ? CombatSetupEvaluator.WipeOutcome(state)
            : CombatSetupEvaluator.EvaluateLine(state);

        if (IsBetterLineOutcome(bestOutcome, bestPath, outcome, path)) {
            bestOutcome = outcome;
            bestPath = path;
            bestDepth = depth;
            int rank = CombatSetupEvaluator.PackLineScore(outcome);
            if (path.Count > 0)
                rank += SimMoveScoring.OpeningModifierBonus(root, path[0], rootSnapshot);
            CombatDebugTrace.LogBeamLeafUpdate(
                root, state, path, rank, depth, "line outcome");
        }
    }

    static bool IsBetterLineOutcome(
        CombatSetupEvaluator.CombatLineOutcome? currentBest,
        List<SimCombatAction>? currentPath,
        CombatSetupEvaluator.CombatLineOutcome candidate,
        List<SimCombatAction> candidatePath) {
        if (currentBest == null)
            return true;

        int cmp = CombatSetupEvaluator.CompareLines(currentBest.Value, candidate);
        if (cmp > 0)
            return true;
        if (cmp < 0)
            return false;

        int currentLen = currentPath?.Count ?? 0;
        return candidatePath.Count < currentLen;
    }

    static int RankLine(CombatState state) {
        if (state.AliveEnemyCount == 0)
            return CombatSetupEvaluator.PackLineScore(CombatSetupEvaluator.WipeOutcome(state));
        return CombatSetupEvaluator.PackLineScore(CombatSetupEvaluator.EvaluateLine(state));
    }

    readonly record struct BeamNode(CombatState State, List<SimCombatAction> Path, int Score);
}

public readonly record struct BeamSearchOptions(
    int MaxDepth,
    int BeamWidth,
    int TimeBudgetMs,
    int MaxActionsPerNode);

public readonly record struct BeamSearchResult(
    IReadOnlyList<SimCombatAction>? Path,
    int Score,
    int Depth) {
    public bool HasResult => Path != null && Score > int.MinValue;
}
