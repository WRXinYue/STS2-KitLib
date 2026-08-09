using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Core.Schema;

namespace KitLib.AI.Combat;

public sealed record CombatMoveScore(
    GameAction Action,
    int Score,
    IReadOnlyList<string> Terms) {
    public string FormatTerms() => Terms.Count == 0 ? "" : string.Join(", ", Terms);

    public static string FormatMoveLabel(GameAction action) {
        if (action.Type == ActionType.EndTurn)
            return "EndTurn";
        if (action.Type == ActionType.UsePotion) {
            var target = action.SecondaryIndex >= 0 ? $"→e{action.SecondaryIndex}" : "";
            return $"Potion#{action.TargetIndex}{target}";
        }
        if (action.Type != ActionType.PlayCard)
            return action.Type.ToString();

        var playTarget = action.SecondaryIndex >= 0 ? $"→e{action.SecondaryIndex}" : "";
        return $"Play#{action.TargetIndex}{playTarget}";
    }
}

internal sealed class ScoreBuilder {
    readonly List<string> _terms = [];
    int _total;

    public int Total => _total;

    public string FormatTerms() => _terms.Count == 0 ? "" : string.Join(", ", _terms);

    public void Add(string term, int delta) {
        if (delta == 0) return;
        _total += delta;
        _terms.Add($"{term}:{delta:+0;-0;0}");
    }

    public CombatMoveScore Build(GameAction action) =>
        new(action, _total, _terms.ToArray());
}
