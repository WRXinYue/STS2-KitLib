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
        var config = CombatBeamSearch.ForHand(playable, state.Potions.Count);
        var sw = Stopwatch.StartNew();

        BeamSearchResult? best = null;
        for (int depth = 5; depth <= config.MaxDepth; depth += 2) {
            if (sw.ElapsedMilliseconds >= config.TimeBudgetMs)
                break;

            var result = CombatBeamSearch.Run(state, config with { MaxDepth = depth });
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
}
