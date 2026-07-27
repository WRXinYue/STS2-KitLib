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
        var combatObj = snapshot["combat"] as JsonObject;
        bool inCombat = phase == GamePhase.Combat
            || combatObj?["isPlayPhaseActive"]?.GetValue<bool>() == true;

        if (!inCombat)
            return new AiDecisionLiveDto(BuildNonCombat(snapshot, phase, action), false);

        return new AiDecisionLiveDto(BuildCombat(snapshot, phase, action), true);
    }

    static AiDecisionSnapshotDto BuildNonCombat(JsonObject snapshot, GamePhase phase, GameAction? action) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        int skipCost = DeckEvaluator.SkipOpportunityCost(metrics, plan, snapshot);

        var telemetry = new AiTelemetryDto(
            Summary: AiHudModel.PhaseShortLabel(phase),
            PlayerHp: snapshot["currentHp"]?.GetValue<int>() ?? 0,
            PlayerMaxHp: snapshot["maxHp"]?.GetValue<int>() ?? 0,
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
            CopyDecisionLog(),
            BuildCardOffers(snapshot, plan),
            skipCost,
            BuildFightOutlook(snapshot),
            BuildMacroInsights(snapshot, plan));
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
            CopyDecisionLog(),
            [],
            0,
            null,
            null);
    }

    static List<AiCardOfferDto> BuildCardOffers(JsonObject snapshot, DeckPlan plan) {
        var offered = snapshot["offeredCards"]?.AsArray();
        if (offered == null || offered.Count == 0)
            return [];

        int deckSize = snapshot["deck"]?.AsArray()?.Count ?? 0;
        var list = new List<AiCardOfferDto>(offered.Count);

        foreach (var node in offered) {
            if (node is not JsonObject card)
                continue;

            var breakdown = CardOfferScoring.ScoreBreakdown(card, plan, deckSize, snapshot);
            var role = MacroScoringInsights.ClassifyOffer(card, breakdown, snapshot, plan);
            list.Add(new AiCardOfferDto(
                card["index"]?.GetValue<int>() ?? list.Count,
                card["id"]?.GetValue<string>() ?? "",
                card["name"]?.GetValue<string>() ?? "?",
                breakdown.Total,
                breakdown.Marginal,
                breakdown.Synergy,
                breakdown.Option,
                breakdown.Dilution,
                breakdown.Early,
                breakdown.ExerciseProb,
                role.PrimaryRole,
                role.FightFuture,
                role.Reason,
                role.InRunScore,
                role.OutRunScore,
                role.ArchetypeIds));
        }

        list.Sort((a, b) => b.Total.CompareTo(a.Total));
        return list;
    }

    static AiFightOutlookDto? BuildFightOutlook(JsonObject snapshot) {
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        if (route.Count == 0)
            return null;

        var fight = route[0];
        var outcome = FightOutcomeEstimator.EstimateAverage(snapshot, null, fight);
        return new AiFightOutlookDto(
            fight.EncounterId,
            outcome.ExpectedRemainingHp,
            outcome.MinRemainingHp,
            outcome.ExpectedKillTurns,
            outcome.ExpectedChip,
            outcome.ExpectedFightChip,
            outcome.LethalSamples,
            outcome.SampleCount);
    }

    static AiMacroInsightsDto BuildMacroInsights(JsonObject snapshot, DeckPlan plan) {
        var resources = MacroScoringInsights.BuildResources(snapshot, plan);
        var weights = MacroScoringInsights.GetPhaseWeights(snapshot);
        var deckCombo = MacroScoringInsights.ScoreDeckComposition(snapshot, plan);

        var archetypes = deckCombo.Archetypes
            .Select(a => new AiDeckArchetypeDto(
                a.Id, a.Role, a.DeckPieces, a.RelicPieces, a.ScoreContribution))
            .ToList();

        string summary =
            $"Phase={weights.PhaseLabel}; simWt={weights.CurrentSimWeight:F2} optWt={weights.OptionWeight:F2}; "
            + $"route={deckCombo.RouteFightScore} quality={deckCombo.DeckQualityScore}";

        return new AiMacroInsightsDto(
            new AiMacroResourcesDto(
                resources.Hp,
                resources.MaxHp,
                resources.Gold,
                resources.DeckSize,
                resources.ActIndex,
                resources.TotalFloor,
                resources.RouteFightScore,
                resources.PhaseLabel),
            new AiScoringPhaseDto(
                weights.CurrentSimWeight,
                weights.OptionWeight,
                weights.DilutionWeight,
                weights.PhaseLabel,
                weights.Rationale),
            new AiDeckComboDto(
                deckCombo.RouteFightScore,
                deckCombo.DeckQualityScore,
                deckCombo.SurvivalGap,
                deckCombo.ThinGap,
                deckCombo.StarterBloat,
                archetypes),
            summary);
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
