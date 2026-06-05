using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace DevMode.AI.Combat.Simulation;

/// <summary>Full next-turn mini-beam: play until energy is exhausted before scoring EndTurn lines.</summary>
public static class PostTurnSimulator {
    const int MinBeamWidth = 16;
    const int MaxBeamWidth = 32;

    public static int ScoreLine(CombatState afterTurn, int timeBudgetMs, Stopwatch sw) {
        if (afterTurn.AliveEnemyCount == 0)
            return CombatEvaluator.EvaluateTerminal(afterTurn);

        int baseline = CombatEvaluator.EvaluateTerminal(afterTurn);
        var playable = CombatCardCost.CountAffordable(afterTurn);
        if (playable == 0)
            return baseline;

        int maxDepth = Math.Clamp(playable + 2, 4, 12);
        int width = Math.Clamp(playable * 3, MinBeamWidth, MaxBeamWidth);
        int maxActions = Math.Clamp(playable * 5, 16, 48);

        var beam = new List<(CombatState State, int Score)> {
            (afterTurn, CombatEvaluator.EvaluateMidTurn(afterTurn)),
        };
        int best = baseline;

        for (int depth = 0; depth < maxDepth; depth++) {
            if (sw.ElapsedMilliseconds >= timeBudgetMs)
                break;

            var nextBeam = new List<(CombatState State, int Score)>();

            foreach (var (state, _) in beam) {
                if (sw.ElapsedMilliseconds >= timeBudgetMs)
                    break;

                if (state.Energy <= 0 || !CombatCardCost.HasAffordablePlay(state)) {
                    int terminal = CombatEvaluator.EvaluateTerminal(state);
                    best = Math.Max(best, terminal);
                    continue;
                }

                foreach (var action in LegalActionGenerator.GenerateOrdered(state, maxActions)) {
                    if (action.Kind == SimActionKind.EndTurn)
                        continue;

                    var next = CombatSimulator.Apply(state, action);
                    if (next.AliveEnemyCount == 0) {
                        best = Math.Max(best, CombatEvaluator.EvaluateTerminal(next));
                        continue;
                    }

                    bool exhausted = next.Energy <= 0 || !CombatCardCost.HasAffordablePlay(next);
                    int score = exhausted
                        ? CombatEvaluator.EvaluateTerminal(next)
                        : CombatEvaluator.EvaluateMidTurn(next);
                    best = Math.Max(best, score);

                    if (!exhausted)
                        nextBeam.Add((next, score));
                }
            }

            if (nextBeam.Count == 0)
                break;

            beam = nextBeam
                .OrderByDescending(x => x.Score)
                .Take(width)
                .ToList();
        }

        return best;
    }
}
