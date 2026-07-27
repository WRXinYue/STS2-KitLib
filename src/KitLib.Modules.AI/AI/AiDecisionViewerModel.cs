using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Core.Schema;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;

namespace KitLib.AI;

/// <summary>Builds structured AI decision snapshots for Dev Viewer and diagnostics.</summary>
public static class AiDecisionViewerModel {
    public static AiDecisionLiveDto BuildLive(JsonObject snapshot, GamePhase phase, GameAction? action) {
        bool inCombat = phase == GamePhase.Combat
            || snapshot["combat"]?["isPlayPhaseActive"]?.GetValue<bool>() == true;

        if (!inCombat)
            return new AiDecisionLiveDto(BuildNonCombat(phase, action), false);

        return new AiDecisionLiveDto(BuildCombat(snapshot, phase, action), true);
    }

    static AiDecisionSnapshotDto BuildNonCombat(GamePhase phase, GameAction? action) {
        var telemetry = new AiTelemetryDto(
            Summary: AiHudModel.PhaseShortLabel(phase),
            PlayerHp: 0,
            PlayerMaxHp: 0,
            PlayerBlock: 0,
            Energy: 0,
            Incoming: 0,
            NetDamage: 0,
            NonDamageThreat: 0,
            NextTurnIncoming: 0,
            Junk: 0,
            Pollution: 0,
            PlayDamage: 0,
            PlayBlock: 0,
            SetupDebt: 0,
            InfernoDebt: 0,
            PeekSummary: "",
            Outlook: 0);

        return new AiDecisionSnapshotDto(
            phase.ToString(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            telemetry,
            BuildLastAction(action),
            [],
            new AiPileOutlookDto(0, 0, 0, false, ""),
            new AiBlockPolicyDto(false, false, false, false, 0, 0, 0),
            CopyDecisionLog());
    }

    static AiDecisionSnapshotDto BuildCombat(JsonObject snapshot, GamePhase phase, GameAction? action) {
        var state = CombatState.FromSnapshot(snapshot);
        int incoming = ThreatModel.IncomingDamage(state);
        int net = ThreatModel.NetDamageAfterBlock(state);
        int nonDamage = ThreatModel.TotalNonDamageThreat(state);
        int next = ThreatModel.NextTurnIncoming(state);
        int junk = DeckPollutionEvaluator.JunkCount(state);
        int pollution = DeckPollutionEvaluator.EffectivePollutionBurden(state);
        int playDamage = DeckPollutionEvaluator.ExpectedPlayableDamage(state);
        int playBlock = DeckPollutionEvaluator.ExpectedPlayableBlock(state);
        int setup = CombatSetupEvaluator.ComputeSetupDebt(state);
        int inferno = CombatSetupEvaluator.ComputeInfernoComboDebt(state);
        string peek = DrawPlanner.FormatPeekSummary(state);
        int outlook = PileRhythmEvaluator.DrawPileOutlook(state);

        var telemetry = new AiTelemetryDto(
            Summary: AiHudModel.BuildCombatTelemetryLine(snapshot),
            PlayerHp: state.PlayerHp,
            PlayerMaxHp: state.PlayerMaxHp,
            PlayerBlock: state.PlayerBlock,
            Energy: state.Energy,
            Incoming: incoming,
            NetDamage: net,
            NonDamageThreat: nonDamage,
            NextTurnIncoming: next,
            Junk: junk,
            Pollution: pollution,
            PlayDamage: playDamage,
            PlayBlock: playBlock,
            SetupDebt: setup,
            InfernoDebt: inferno,
            PeekSummary: peek,
            Outlook: outlook);

        int focusEnemy = CombatSetupEvaluator.PrimaryAttackTargetIndex(state);
        var hand = BuildHand(state, focusEnemy);

        var combat = snapshot["combat"]?.AsObject();
        var piles = new AiPileOutlookDto(
            combat?["drawPileCount"]?.GetValue<int>() ?? state.DrawPile.Count,
            combat?["discardPileCount"]?.GetValue<int>() ?? state.DiscardPile.Count,
            state.ExhaustPile.Count,
            DrawPlanner.WillReshuffle(state, RelicCombatRules.PlannedHandDraw(state)),
            peek);

        var blockPolicy = new AiBlockPolicyDto(
            BlockDefensePolicy.NeedsBlock(state),
            BlockDefensePolicy.CanSkipBlockForKill(state),
            BlockDefensePolicy.ShouldPrioritizeBlock(state),
            BlockDefensePolicy.HasAffordablePureBlock(state),
            BlockDefensePolicy.BlockScalingAttackEnergyReserve(state),
            BlockDefensePolicy.NetDamage(state),
            BlockDefensePolicy.AffordableBlockTotal(state));

        return new AiDecisionSnapshotDto(
            phase.ToString(),
            DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            telemetry,
            BuildLastAction(action),
            hand,
            piles,
            blockPolicy,
            CopyDecisionLog());
    }

    static List<AiHandCardDto> BuildHand(CombatState state, int focusEnemy) {
        var hand = new List<AiHandCardDto>(state.Hand.Count);
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            bool canPlay = card.CanPlay && CombatCardCost.CanAfford(card, state);
            int rank = 0;
            if (canPlay) {
                var sim = new SimCombatAction(SimActionKind.PlayCard, i, focusEnemy);
                rank = CombatSetupEvaluator.RankPlayAction(state, sim);
            }

            bool blockScaling = BlockDefensePolicy.IsBlockScalingAttack(card);
            bool pureBlock = BlockDefensePolicy.IsPureBlockCard(card, state);
            bool defer = blockScaling
                && BlockDefensePolicy.ShouldDeferBlockScalingAttack(state, card, i, focusEnemy);
            string deferReason = blockScaling
                ? BlockScalingDecisionDiag.ExplainDeferPublic(state, card, i, focusEnemy)
                : "";

            hand.Add(new AiHandCardDto(
                i,
                card.Id,
                card.Name,
                card.Cost,
                card.Damage,
                card.Block,
                card.CardType,
                canPlay,
                rank,
                blockScaling,
                pureBlock,
                defer,
                deferReason));
        }

        hand.Sort((a, b) => b.RankScore.CompareTo(a.RankScore));
        return hand;
    }

    static AiLastActionDto? BuildLastAction(GameAction? action) {
        if (action == null)
            return null;

        string label = action.Reason ?? action.Type.ToString();
        if (!string.IsNullOrWhiteSpace(action.Reason))
            label = action.Reason;

        return new AiLastActionDto(
            action.Type.ToString(),
            label,
            action.Reason ?? "",
            action.TargetIndex,
            action.SecondaryIndex);
    }

    static List<string> CopyDecisionLog() {
        var lines = AiDecisionLog.Snapshot();
        if (lines.Count <= 48)
            return [.. lines];

        return lines.Skip(lines.Count - 48).ToList();
    }
}
