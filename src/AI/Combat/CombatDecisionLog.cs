using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using DevMode.AI.Core.Schema;
using DevMode.Settings;

namespace DevMode.AI.Combat;

/// <summary>Verbose combat pick logging for AutoPlay terminal / godot.log.</summary>
public static class CombatDecisionLog {
    const int MaxAlternatives = 4;

    public static bool VerboseEnabled =>
        SettingsStore.Current.AiCombatVerboseLog;

    public static void LogPick(
        JsonObject snapshot,
        GameAction picked,
        IReadOnlyList<CombatMoveScore> ranked,
        string? searchNote = null) {
        if (!VerboseEnabled || ranked.Count == 0)
            return;

        var combat = snapshot["combat"]?.AsObject();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var handCount = combat?["hand"]?.AsArray()?.Count ?? 0;

        var sb = new StringBuilder();
        sb.Append("Combat pick ");
        sb.Append(FormatPicked(picked, ranked));
        sb.Append($" | E={energy} hand={handCount}");
        if (!string.IsNullOrWhiteSpace(searchNote))
            sb.Append(' ').Append(searchNote);

        var alts = ranked
            .Where(x => x.Action.Type != picked.Type
                || x.Action.TargetIndex != picked.TargetIndex
                || x.Action.SecondaryIndex != picked.SecondaryIndex)
            .Take(MaxAlternatives);

        foreach (var alt in alts) {
            sb.Append(" || ");
            sb.Append(CombatMoveScore.FormatMoveLabel(alt.Action));
            sb.Append('=').Append(alt.Score);
            var terms = alt.FormatTerms();
            if (!string.IsNullOrWhiteSpace(terms))
                sb.Append(" [").Append(terms).Append(']');
        }

        AiDecisionLog.Record("AutoPlay", sb.ToString());
    }

    static string FormatPicked(GameAction picked, IReadOnlyList<CombatMoveScore> ranked) {
        var match = ranked.FirstOrDefault(x =>
            x.Action.Type == picked.Type
            && x.Action.TargetIndex == picked.TargetIndex
            && x.Action.SecondaryIndex == picked.SecondaryIndex);

        if (match == null)
            return $"{CombatMoveScore.FormatMoveLabel(picked)} (search)";

        var terms = match.FormatTerms();
        return terms.Length > 0
            ? $"{CombatMoveScore.FormatMoveLabel(picked)} score={match.Score} [{terms}]"
            : $"{CombatMoveScore.FormatMoveLabel(picked)} score={match.Score}";
    }
}
