using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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

    public static int RunBestScore(CombatState root, BeamSearchOptions options) {
        var sw = Stopwatch.StartNew();
        var result = RunBeam(root, options.MaxDepth, options, sw);
        return result.HasResult ? result.Score : int.MinValue;
    }

    public static BeamSearchResult Run(CombatState root, BeamSearchOptions options) {
        var sw = Stopwatch.StartNew();
        return RunBeam(root, options.MaxDepth, options, sw);
    }

    static BeamSearchResult RunBeam(CombatState root, int maxDepth, BeamSearchOptions config, Stopwatch sw) {
        var beam = new List<BeamNode> {
            new(root, [], CombatEvaluator.EvaluateMidTurn(root)),
        };

        List<SimCombatAction>? bestPath = null;
        int bestScore = int.MinValue;
        int bestDepth = 0;

        int rootPostBudget = Math.Max(40, config.TimeBudgetMs - (int)sw.ElapsedMilliseconds);
        ConsiderLeaf(root, [], 0, rootPostBudget, sw, ref bestPath, ref bestScore, ref bestDepth);

        for (int depth = 0; depth < maxDepth && sw.ElapsedMilliseconds < config.TimeBudgetMs; depth++) {
            var nextBeam = new List<BeamNode>();
            int postBudget = Math.Max(40, config.TimeBudgetMs - (int)sw.ElapsedMilliseconds);

            foreach (var node in beam) {
                if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                    break;

                foreach (var action in LegalActionGenerator.GenerateOrdered(node.State, config.MaxActionsPerNode)) {
                    if (action.Kind == SimActionKind.EndTurn)
                        continue;

                    var next = CombatSimulator.Apply(node.State, action);
                    var newPath = node.Path.Append(action).ToList();

                    if (next.AliveEnemyCount == 0) {
                        int wipeScore = CombatEvaluator.EvaluateTerminal(next)
                            + CombatEvalWeights.CombatWipePriorityBonus;
                        if (wipeScore > bestScore) {
                            bestScore = wipeScore;
                            bestPath = newPath;
                            bestDepth = depth + 1;
                        }
                        continue;
                    }

                    ConsiderLeaf(next, newPath, depth + 1, postBudget, sw,
                        ref bestPath, ref bestScore, ref bestDepth);

                    bool exhausted = next.Energy <= 0 || !CombatCardCost.HasAffordablePlay(next);
                    if (exhausted || depth + 1 >= maxDepth)
                        continue;

                    nextBeam.Add(new BeamNode(next, newPath, CombatEvaluator.EvaluateMidTurn(next)));
                }
            }

            if (nextBeam.Count == 0)
                break;

            beam = nextBeam
                .OrderByDescending(n => n.Score)
                .Take(config.BeamWidth)
                .ToList();
        }

        return new BeamSearchResult(bestPath, bestScore, bestDepth);
    }

    static void ConsiderLeaf(
        CombatState state,
        List<SimCombatAction> path,
        int depth,
        int postBudget,
        Stopwatch sw,
        ref List<SimCombatAction>? bestPath,
        ref int bestScore,
        ref int bestDepth) {
        var afterTurn = CombatTurnResolver.ResolveEndTurn(state);
        if (afterTurn.AliveEnemyCount == 0) {
            int wipeScore = CombatEvaluator.EvaluateTerminal(afterTurn)
                + CombatEvalWeights.CombatWipePriorityBonus;
            if (wipeScore > bestScore) {
                bestScore = wipeScore;
                bestPath = path;
                bestDepth = depth;
            }

            return;
        }

        int endScore = PostTurnSimulator.ScoreLine(afterTurn, postBudget, sw);
        endScore -= UnusedEnergyPenalty(state);

        var net = ThreatModel.NetDamageAfterBlock(state);
        var incoming = ThreatModel.IncomingDamage(state);
        if (net <= 0 && incoming > 0)
            endScore += CombatEvalWeights.TerminalFullBlockBonus / 2;

        if (endScore > bestScore) {
            bestScore = endScore;
            bestPath = path;
            bestDepth = depth;
        }
    }

    static int UnusedEnergyPenalty(CombatState state) {
        if (state.Energy <= 0 || !CombatCardCost.HasAffordablePlay(state))
            return 0;
        if (RelicCombatRules.RetainsEnergyOnTurnStart(state.RelicIds, state.TurnNumber + 1))
            return 0;

        var net = ThreatModel.NetDamageAfterBlock(state);
        if (net <= 0)
            return GreedyRemainingPlayValue(state, prioritizeBlock: false);

        return net * CombatEvalWeights.UnusedEnergyExposedNetPenalty
            + GreedyRemainingPlayValue(state, prioritizeBlock: true);
    }

    static int GreedyRemainingPlayValue(CombatState state, bool prioritizeBlock) {
        var net = ThreatModel.NetDamageAfterBlock(state);
        var primary = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);
        var primaryTarget = state.Enemies.FirstOrDefault(e => e.IsAlive && e.Index == primary);

        var blockOptions = new List<(int Cost, int Value)>();
        var attackOptions = new List<(int Cost, int Value)>();

        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;

            int cost = CombatCardCost.EffectiveCost(card, state.Modifiers);

            if (card.Block > 0 && net > 0) {
                int block = Math.Min(CombatDamageCalc.OutgoingBlock(card, state), net);
                if (block > 0)
                    blockOptions.Add((cost, block));
            }

            if (card.IsAttack && card.Damage > 0) {
                var vuln = primaryTarget?.Vulnerable ?? 0;
                int value = CombatDamageCalc.OutgoingDamage(card, state, vuln);
                if (value > 0)
                    attackOptions.Add((cost, value));
            }
        }

        var setupOptions = new List<(int Cost, int Value)>();
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (primaryTarget == null || primaryTarget.Vulnerable > 0)
                continue;
            if (!AppliesVulnerable(card)) continue;

            int cost = CombatCardCost.EffectiveCost(card, state.Modifiers);
            int setup = CombatSetupEvaluator.ComputeVulnerableSetupValue(state, i, primary);
            if (setup > 0)
                setupOptions.Add((cost, setup));
        }

        blockOptions.Sort((a, b) => b.Value.CompareTo(a.Value));
        attackOptions.Sort((a, b) => b.Value.CompareTo(a.Value));
        setupOptions.Sort((a, b) => b.Value.CompareTo(a.Value));

        int energy = state.Energy;
        int remainingNet = net;
        int blockValue = 0;

        if (prioritizeBlock && remainingNet > 0) {
            foreach (var (cost, value) in blockOptions) {
                if (cost > energy || remainingNet <= 0) continue;
                energy -= cost;
                remainingNet = Math.Max(0, remainingNet - value);
                blockValue += value;
            }
        }

        int attackSetupValue = 0;
        foreach (var options in new[] { attackOptions, setupOptions }) {
            foreach (var (cost, value) in options) {
                if (cost > energy) continue;
                energy -= cost;
                attackSetupValue += value;
            }
        }

        int junkReliefValue = 0;
        if (DeckPollutionEvaluator.HandJunkCount(state) > 0) {
            foreach (var card in state.Hand) {
                if (!CombatCardCost.CanAfford(card, state)) continue;
                int relief = DeckPollutionEvaluator.JunkReliefScore(state, card);
                if (relief > 0) {
                    int cost = CombatCardCost.EffectiveCost(card, state.Modifiers);
                    if (cost > energy) continue;
                    energy -= cost;
                    junkReliefValue += relief;
                    continue;
                }
                int emergency = DeckPollutionEvaluator.EmergencyJunkPlayScore(state, card);
                if (emergency <= int.MinValue + 1) continue;
                int emergencyCost = CombatCardCost.EffectiveCost(card, state.Modifiers);
                if (emergencyCost > energy) continue;
                energy -= emergencyCost;
                junkReliefValue += emergency;
            }
        }

        if (prioritizeBlock && net > 0) {
            if (remainingNet > 0)
                return remainingNet * CombatEvalWeights.UnusedEnergyExposedNetPenalty + attackSetupValue + junkReliefValue;
            return attackSetupValue + junkReliefValue;
        }

        return blockValue + attackSetupValue + junkReliefValue;
    }

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0
        || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

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
