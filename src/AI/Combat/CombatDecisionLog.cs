using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.Settings;

namespace KitLib.AI.Combat;

/// <summary>Verbose combat pick logging for AutoPlay terminal / godot.log.</summary>
public static class CombatDecisionLog {
    const int MaxScorerAlternatives = 4;

    public static bool VerboseEnabled =>
        SettingsStore.Current.AiCombatVerboseLog;

    public static void LogPick(
        JsonObject snapshot,
        GameAction picked,
        IReadOnlyList<CombatMoveScore> ranked,
        string? searchNote = null,
        string? beamPathSummary = null) {
        if (!VerboseEnabled || ranked.Count == 0)
            return;

        var state = CombatState.FromSnapshot(snapshot);
        var combat = snapshot["combat"]?.AsObject();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var handCount = combat?["hand"]?.AsArray()?.Count ?? 0;

        var sb = new StringBuilder();
        sb.Append("Combat pick ");
        sb.Append(FormatPicked(picked, ranked, searchNote));
        sb.Append($" | E={energy} hand={handCount}");
        sb.Append(' ').Append(FormatEnemyThreat(state));
        sb.Append(' ').Append(FormatPlanContext(state));

        int enemyIndex = picked.Type == ActionType.PlayCard && picked.SecondaryIndex >= 0
            ? picked.SecondaryIndex
            : picked.Type == ActionType.UsePotion && picked.SecondaryIndex >= 0
                ? picked.SecondaryIndex
                : -1;
        if (enemyIndex >= 0)
            sb.Append(FormatTargetDetail(combat?["enemies"]?.AsArray(), enemyIndex));

        if (!string.IsNullOrWhiteSpace(beamPathSummary))
            sb.Append(' ').Append(beamPathSummary);

        if (picked.Type == ActionType.EndTurn) {
            var afterTurn = CombatTurnResolver.ResolveEndTurn(state);
            sb.Append(' ').Append(FormatPostEndTurnPreview(afterTurn));
        }

        sb.Append(" | scorer-alts:");
        var alts = ranked
            .Where(x => x.Action.Type != picked.Type
                || x.Action.TargetIndex != picked.TargetIndex
                || x.Action.SecondaryIndex != picked.SecondaryIndex)
            .Take(MaxScorerAlternatives);

        bool anyAlt = false;
        foreach (var alt in alts) {
            anyAlt = true;
            sb.Append(' ');
            sb.Append(CombatMoveScore.FormatMoveLabel(alt.Action));
            sb.Append('=').Append(alt.Score);
            var terms = alt.FormatTerms();
            if (!string.IsNullOrWhiteSpace(terms))
                sb.Append(" [").Append(terms).Append(']');
        }

        if (!anyAlt)
            sb.Append(" (none)");

        AiDecisionLog.Record("AutoPlay", sb.ToString());
    }

    static string FormatPlanContext(CombatState state) {
        int vulnPlays = 0;
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (card.Profile.AppliedVulnerable > 0
                || card.Profile.Flags.HasFlag(CardMechanicFlags.AppliesVulnerable))
                vulnPlays++;
        }

        bool retainsEnergy = RelicCombatRules.RetainsEnergyOnTurnStart(
            state.RelicIds, state.TurnNumber + 1);

        return $"SETUP={CombatSetupEvaluator.ComputeSetupDebt(state)} INFERNO={CombatSetupEvaluator.ComputeInfernoComboDebt(state)} INF_RET={state.Buffs.InfernoRetaliation} VULN={vulnPlays} ICE={(retainsEnergy ? 1 : 0)}";
    }

    static string FormatEnemyThreat(CombatState state) {
        var incoming = ThreatModel.IncomingDamage(state);
        var nonDamage = ThreatModel.TotalNonDamageThreat(state);
        var next = ThreatModel.NextTurnIncoming(state);
        var junk = DeckPollutionEvaluator.JunkCount(state);
        var poll = DeckPollutionEvaluator.ProjectedPollutionCost(state);
        var play = DeckPollutionEvaluator.ExpectedPlayableDamage(state);
        var peek = DrawPlanner.FormatPeekSummary(state);
        var reshuf = DrawPlanner.WillReshuffle(state, RelicCombatRules.PlannedHandDraw(state)) ? 1 : 0;
        var outlook = PileRhythmEvaluator.DrawPileOutlook(state);
        var vulnEv = VulnerableOutlookEvaluator.Estimate(state);
        var weakEv = WeakMitigationEvaluator.Estimate(state);
        return $"IN={incoming} ND={nonDamage} NXT={next} JUNK={junk} POLL={poll} PLAY={play} {peek} RESHUF={reshuf} VULN_EV={vulnEv} WEAK_EV={weakEv} OUTLOOK={outlook}";
    }

    internal static string FormatPostEndTurnPreview(CombatState afterTurn) =>
        $"POST_PLAY={DeckPollutionEvaluator.ExpectedPlayableDamage(afterTurn)} POST_BLK={DeckPollutionEvaluator.ExpectedPlayableBlock(afterTurn)}";

    static string FormatTargetDetail(JsonArray? enemies, int combatIndex) {
        var target = EnemyIndexResolver.FindByCombatIndex(enemies, combatIndex);
        if (target == null)
            return "";

        var arraySlot = EnemyIndexResolver.ArraySlot(enemies, combatIndex);
        var id = target["monsterId"]?.GetValue<string>() ?? "?";
        var bias = arraySlot >= 0
            ? MinionEngagementPolicy.TargetBias(enemies, arraySlot)
            : 0;
        var flags = EnemyMechanicResolver.ResolveFlags(target);
        var vuln = CombatPowerReader.GetVulnerable(target);
        var hp = target["currentHp"]?.GetValue<int>() ?? 0;
        return $" tgt={id} hp={hp} vuln={vuln} bias={bias} flags={flags}";
    }

    static string FormatPicked(
        GameAction picked,
        IReadOnlyList<CombatMoveScore> ranked,
        string? searchNote) {
        var label = CombatMoveScore.FormatMoveLabel(picked);
        var match = ranked.FirstOrDefault(x =>
            x.Action.Type == picked.Type
            && x.Action.TargetIndex == picked.TargetIndex
            && x.Action.SecondaryIndex == picked.SecondaryIndex);

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(searchNote))
            sb.Append('[').Append(searchNote).Append("] ");

        sb.Append(label);

        if (match == null) {
            sb.Append(" scorer=—");
            return sb.ToString();
        }

        sb.Append(" scorer=").Append(match.Score);
        var terms = match.FormatTerms();
        if (!string.IsNullOrWhiteSpace(terms))
            sb.Append(" [").Append(terms).Append(']');

        return sb.ToString();
    }
}
