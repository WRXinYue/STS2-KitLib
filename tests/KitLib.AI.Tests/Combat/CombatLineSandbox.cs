using System.Collections.Generic;
using System.Linq;
using System.Text;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Exhaustive turn simulator: enumerates complete play lines via DFS and ranks leaves with
/// <see cref="CombatSetupEvaluator.CompareLines"/> (same lex order as beam search).
/// </summary>
public static class CombatLineSandbox {
    public sealed class RankedLine {
        internal RankedLine(
            string label,
            IReadOnlyList<SimCombatAction> path,
            CombatSetupEvaluator.CombatLineOutcome outcome,
            int packedScore) {
            Label = label;
            Path = path;
            Outcome = outcome;
            PackedScore = packedScore;
        }

        public string Label { get; }
        public IReadOnlyList<SimCombatAction> Path { get; }
        internal CombatSetupEvaluator.CombatLineOutcome Outcome { get; }
        public int PackedScore { get; }
    }

    public static IReadOnlyList<RankedLine> RankCompleteLines(CombatState root, int maxPlaysCap = 8) {
        var results = new List<RankedLine>();
        var path = new List<SimCombatAction>();
        int maxPlays = Math.Min(maxPlaysCap, root.Energy + 1);
        Search(root, root, path, 0, maxPlays, results);

        results.Sort((a, b) => {
            int cmp = CombatSetupEvaluator.CompareLines(a.Outcome, b.Outcome);
            if (cmp != 0)
                return cmp;
            return b.PackedScore.CompareTo(a.PackedScore);
        });

        return results;
    }

    static void Search(
        CombatState root,
        CombatState state,
        List<SimCombatAction> path,
        int depth,
        int maxPlays,
        List<RankedLine> results) {
        if (state.AliveEnemyCount == 0) {
            Record(root, state, path, results);
            return;
        }

        if (!CombatCardCost.HasAffordablePlay(state) || depth >= maxPlays) {
            Record(root, state, path, results);
            return;
        }

        int branches = 0;
        foreach (var action in LegalActionGenerator.GenerateUnpruned(state)) {
            if (action.Kind == SimActionKind.EndTurn)
                continue;

            branches++;
            if (branches > 32)
                break;

            path.Add(action);
            Search(root, CombatSimulator.Apply(state, action), path, depth + 1, maxPlays, results);
            path.RemoveAt(path.Count - 1);
        }
    }

    static void Record(
        CombatState root,
        CombatState leaf,
        List<SimCombatAction> path,
        List<RankedLine> results) {
        var outcome = CombatSetupEvaluator.EvaluateTerminalLine(leaf, root);
        results.Add(new RankedLine(
            FormatLine(root, path),
            path.ToList(),
            outcome,
            CombatSetupEvaluator.PackLineScore(outcome)));
    }

    public static string FormatLine(CombatState root, IReadOnlyList<SimCombatAction> path) {
        if (path.Count == 0)
            return "EndTurn";

        var parts = new List<string>(path.Count);
        var sim = root;
        foreach (var action in path) {
            parts.Add(FormatStep(sim, action));
            sim = CombatSimulator.Apply(sim, action);
        }

        return string.Join(">", parts);
    }

    static string FormatStep(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return "EndTurn";

        if (action.Kind == SimActionKind.UsePotion)
            return $"POTION#{action.PotionSlot}";

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return "?";

        var card = state.Hand[action.HandIndex];
        return action.EnemyIndex >= 0
            ? $"{card.Id}→e{action.EnemyIndex}"
            : card.Id;
    }

    public static string FormatRankingReport(IEnumerable<RankedLine> ranked, int take = 12) {
        var sb = new StringBuilder();
        int rank = 1;
        foreach (var line in ranked.Take(take)) {
            sb.Append(rank).Append(". ")
                .Append(line.Label)
                .Append("  IN=").Append(line.Outcome.Incoming)
                .Append(" AV=").Append(line.Outcome.AvoidableIncoming)
                .Append(" F1=").Append(line.Outcome.FutureIncoming1)
                .Append(" focus=").Append(line.Outcome.FocusHp)
                .Append(" vuln=").Append(line.Outcome.VulnerableOutlook)
                .Append(" pack=").Append(line.PackedScore)
                .AppendLine();
            rank++;
        }

        return sb.ToString();
    }

    public static int Compare(RankedLine baseline, RankedLine candidate) =>
        CombatSetupEvaluator.CompareLines(baseline.Outcome, candidate.Outcome);

    public static RankedLine? FindByLabel(IReadOnlyList<RankedLine> ranked, string labelPrefix) =>
        ranked.FirstOrDefault(l => l.Label.StartsWith(labelPrefix, System.StringComparison.Ordinal)
            || l.Label == labelPrefix);

    public static CombatState ApplyPath(CombatState state, IReadOnlyList<SimCombatAction> path) {
        var sim = state;
        foreach (var action in path)
            sim = CombatSimulator.Apply(sim, action);
        return sim;
    }

    public static List<SimCombatAction> BuildPlayPath(CombatState state, params string[] cardIdsInOrder) {
        var path = new List<SimCombatAction>(cardIdsInOrder.Length);
        var sim = state;
        foreach (var cardId in cardIdsInOrder) {
            int handIndex = -1;
            for (int i = 0; i < sim.Hand.Count; i++) {
                if (string.Equals(sim.Hand[i].Id, cardId, StringComparison.OrdinalIgnoreCase)) {
                    handIndex = i;
                    break;
                }
            }

            if (handIndex < 0)
                throw new InvalidOperationException($"Card '{cardId}' not in hand: {FormatHand(sim)}");

            var action = LegalActionGenerator.GenerateUnpruned(sim)
                .FirstOrDefault(a => a.Kind == SimActionKind.PlayCard && a.HandIndex == handIndex);
            if (action == null)
                throw new InvalidOperationException($"Cannot play '{cardId}' at index {handIndex}");

            path.Add(action);
            sim = CombatSimulator.Apply(sim, action);
        }

        return path;
    }

    static string FormatHand(CombatState state) =>
        string.Join(",", state.Hand.Select(c => c.Id));
}
