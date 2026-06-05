using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.AutoPlay.Scoring;
using DevMode.AI.Combat;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class CombatPlanner {
    public static GameAction? PickBestMove(JsonObject snapshot) {
        var state = CombatState.FromSnapshot(snapshot);
        if (state.AliveEnemyCount == 0)
            return EndTurn("No enemies");

        var playable = CombatCardCost.CountAffordable(state);
        var config = BeamConfig.ForHand(playable, state.Potions.Count);
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
            LogPick(snapshot, state, action, $"beam d={beamDepth} s={beamScore}", path);
            return action;
        }

        if (best is { Path: { Count: 0 }, Score: var endOnlyScore }) {
            var action = EndTurn($"Planner score={endOnlyScore}");
            LogPick(snapshot, state, action, $"beam end s={endOnlyScore}", []);
            return action;
        }

        var fallback = CombatScorer.PickBestCombatMove(snapshot);
        if (fallback != null)
            LogPick(snapshot, state, fallback, "fallback");
        return fallback;
    }

    static BeamResult RunBeam(CombatState root, int maxDepth, BeamConfig config, Stopwatch sw) {
        var beam = new List<BeamNode> {
            new(root, [], CombatEvaluator.EvaluateMidTurn(root)),
        };

        List<SimCombatAction>? bestPath = null;
        int bestScore = int.MinValue;
        int bestDepth = 0;

        int rootPostBudget = Math.Max(120, config.TimeBudgetMs - (int)sw.ElapsedMilliseconds);
        ConsiderLeaf(root, [], 0, rootPostBudget, sw, ref bestPath, ref bestScore, ref bestDepth);

        for (int depth = 0; depth < maxDepth && sw.ElapsedMilliseconds < config.TimeBudgetMs; depth++) {
            var nextBeam = new List<BeamNode>();
            int postBudget = Math.Max(120, config.TimeBudgetMs - (int)sw.ElapsedMilliseconds);

            foreach (var node in beam) {
                if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                    break;

                foreach (var action in LegalActionGenerator.GenerateOrdered(node.State, config.MaxActionsPerNode)) {
                    if (action.Kind == SimActionKind.EndTurn)
                        continue;

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

        return new BeamResult(bestPath, bestScore, bestDepth);
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
            int wipeScore = CombatEvaluator.EvaluateTerminal(afterTurn);
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

        if (prioritizeBlock && net > 0) {
            if (remainingNet > 0)
                return remainingNet * CombatEvalWeights.UnusedEnergyExposedNetPenalty + attackSetupValue;
            return attackSetupValue;
        }

        return blockValue + attackSetupValue;
    }

    static bool AppliesVulnerable(CombatHandCard card) =>
        card.Profile.AppliedVulnerable > 0
        || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable);

    public static void LogPick(
        JsonObject snapshot,
        CombatState state,
        GameAction action,
        string note = "planner",
        IReadOnlyList<SimCombatAction>? beamPath = null) {
        if (!CombatDecisionLog.VerboseEnabled) return;
        var ranked = CombatScorer.ScoreLegalMovesDetailed(snapshot)
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

        var parts = new List<string>(path.Count);
        foreach (var action in path.Take(5)) {
            if (action.Kind == SimActionKind.EndTurn) {
                parts.Add("EndTurn");
                continue;
            }

            if (action.Kind == SimActionKind.UsePotion) {
                var label = FormatPotionLabel(state, action);
                parts.Add(label);
                continue;
            }

            if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count) {
                parts.Add("?");
                continue;
            }

            var card = state.Hand[action.HandIndex];
            parts.Add(action.EnemyIndex >= 0
                ? $"{card.Id}→e{action.EnemyIndex}"
                : card.Id);
        }

        var line = string.Join(">", parts);
        if (path.Count > 5)
            line += $">...+{path.Count - 5}";
        return $"LINE={line}";
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
        public static BeamConfig ForHand(int playableCards, int potionCount = 0) {
            int depth = Math.Clamp(playableCards + 3, 10, 16);
            int width = Math.Clamp(playableCards * 4 + 4, 28, 52);
            int budget = playableCards >= 6 ? 730 : playableCards >= 4 ? 630 : 560;
            int potionActions = Math.Min(potionCount * (1 + LegalActionGenerator.MaxMcBranches), 12);
            int actions = Math.Clamp(playableCards * 5 + potionActions, 20, 56);
            return new(depth, width, budget, actions);
        }
    }
}
