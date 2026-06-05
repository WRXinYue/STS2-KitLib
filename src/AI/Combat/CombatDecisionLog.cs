using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using DevMode.AI.Combat.Simulation;
using DevMode.AI.Core.Schema;
using DevMode.AI.Knowledge;
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
        var threatLine = FormatEnemyThreat(snapshot);

        var sb = new StringBuilder();
        sb.Append("Combat pick ");
        sb.Append(FormatPicked(picked, ranked));
        sb.Append($" | E={energy} hand={handCount}");
        if (!string.IsNullOrWhiteSpace(threatLine))
            sb.Append(' ').Append(threatLine);
        int enemyIndex = picked.Type == ActionType.PlayCard && picked.SecondaryIndex >= 0
            ? picked.SecondaryIndex
            : -1;
        if (enemyIndex >= 0)
            sb.Append(FormatTargetBias(combat?["enemies"]?.AsArray(), enemyIndex));
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

    static string FormatEnemyThreat(JsonObject snapshot) {
        var state = CombatState.FromSnapshot(snapshot);
        var incoming = ThreatModel.IncomingDamage(state);
        var nonDamage = state.Enemies.Where(e => e.IsAlive).Sum(e => e.NonDamageThreat);
        var next = ThreatModel.NextTurnIncoming(state);
        return $"IN={incoming} ND={nonDamage} NXT={next}";
    }

    static string FormatTargetBias(JsonArray? enemies, int targetIndex) {
        if (enemies == null || targetIndex < 0 || targetIndex >= enemies.Count)
            return "";

        var target = enemies[targetIndex]?.AsObject();
        var id = target?["monsterId"]?.GetValue<string>() ?? "?";
        var bias = MinionEngagementPolicy.TargetBias(enemies, targetIndex);
        var flags = EnemyMechanicResolver.ResolveFlags(target);
        return $" tgt={id} bias={bias} flags={flags}";
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
