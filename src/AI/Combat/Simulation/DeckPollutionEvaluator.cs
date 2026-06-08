using System;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class DeckPollutionEvaluator {
    public static bool IsHandJunk(CombatHandCard card) =>
        CombatJunkCard.IsJunkId(card.Id)
        || string.Equals(card.CardType, "Status", StringComparison.OrdinalIgnoreCase);

    public static int HandJunkCount(CombatState state) =>
        state.Hand.Count(IsHandJunk);

    public static bool HasAffordableJunkRelief(CombatState state) {
        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (card.Profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand))
                return true;
            if (CardPileEffectResolver.ExhaustHandCount(card.Id) > 0)
                return true;
        }

        return false;
    }

    public static int PollutionReliefDelta(CombatState state, int handIndex, int enemyIndex = -1) {
        if (handIndex < 0 || handIndex >= state.Hand.Count)
            return 0;

        int before = EffectivePollutionBurden(state);
        var after = CombatSimulator.Apply(
            state,
            new SimCombatAction(SimActionKind.PlayCard, handIndex, enemyIndex));
        return before - EffectivePollutionBurden(after);
    }

    public static int JunkReliefScore(CombatState state, CombatHandCard card, int handIndex) {
        if (HandJunkCount(state) <= 0)
            return 0;

        if (!card.Profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand)
            && CardPileEffectResolver.ExhaustHandCount(card.Id) <= 0)
            return 0;

        return Math.Max(0, PollutionReliefDelta(state, handIndex));
    }

    public static bool SelfExhaustsOnPlay(CombatHandCard card) =>
        card.HasExhaust
        || card.Profile.Flags.HasFlag(CardMechanicFlags.Exhaust);

    /// <summary>Status with Exhaust (Slimed) — playable to remove when no relief skill in hand.</summary>
    public static bool HasAffordableEmergencyJunkClear(CombatState state) {
        if (HasAffordableJunkRelief(state))
            return false;

        foreach (var card in state.Hand) {
            if (!CombatCardCost.CanAfford(card, state)) continue;
            if (!IsHandJunk(card) || !SelfExhaustsOnPlay(card)) continue;
            return true;
        }

        return false;
    }

    public static int EmergencyJunkPlayScore(CombatState state, CombatHandCard card, int handIndex) {
        if (!IsHandJunk(card) || !SelfExhaustsOnPlay(card))
            return int.MinValue;
        if (HasAffordableJunkRelief(state))
            return int.MinValue;

        // Beam picks by terminal score, not QuickScore — prune while attacks/blocks remain.
        if (ExpectedPlayableDamage(state) > 0)
            return int.MinValue;
        if (ExpectedPlayableBlock(state) > 0 && ThreatModel.IncomingDamage(state) > 0)
            return int.MinValue;

        int delta = PollutionReliefDelta(state, handIndex);
        return delta > 0 ? delta : int.MinValue;
    }

    public static int JunkCount(CombatState state) =>
        HandJunkCount(state)
        + state.DrawPile.Count(c => c.IsStatus)
        + state.DiscardPile.Count(c => c.IsStatus);

    /// <summary>
    /// Pollution after sim: each remaining junk/status card costs <see cref="CombatJunkCard.DefaultJunkValue"/>.
    /// Clearable cards (Slimed) are handled by playing them in <c>SimulateGreedyJunkClear</c>, not by discounts here.
    /// </summary>
    public static int EffectivePollutionBurden(CombatState state) =>
        JunkCount(state) * CombatJunkCard.DefaultJunkValue;

    public static int ImmediatePollutionCost(CombatState state) =>
        EffectivePollutionBurden(state);

    public static int ProjectedPollutionCost(CombatState state, int horizonTurns = 2) {
        double total = ImmediatePollutionCost(state);

        foreach (var enemy in state.Enemies.Where(e => e.IsAlive)) {
            for (int i = 0; i < Math.Min(horizonTurns, enemy.IntentSteps.Length); i++) {
                var step = enemy.IntentSteps[i];
                var effects = MoveEffectIndex.GetEffects(enemy.MonsterId, step.MoveId);
                foreach (var effect in effects) {
                    if (effect.Kind != MonsterMoveEffectKind.StatusInject
                        || effect.IsNonDeterministic
                        || effect.Count <= 0)
                        continue;

                    total += effect.Count * CombatJunkCard.DefaultJunkValue * DrawProbability(state);
                }
            }
        }

        return (int)Math.Round(total);
    }

    public static int ExpectedPlayableDamage(CombatState state) {
        int vuln = ThreatModel.FocusVulnerableStacks(state);
        int total = 0;
        foreach (var card in state.Hand) {
            if (!card.IsAttack || card.Damage <= 0) continue;
            if (!CombatCardCost.CanAfford(card, state)) continue;

            total += CombatDamageCalc.OutgoingDamage(card, state, vuln);
        }

        return total;
    }

    public static int ExpectedPlayableBlock(CombatState state) {
        int total = 0;
        foreach (var card in state.Hand) {
            if (!card.IsSkill || card.Block <= 0) continue;

            if (CombatCardCost.EffectiveCost(card, state) > state.Energy) continue;
            total += CombatDamageCalc.OutgoingBlock(card, state);
        }

        return total;
    }

    static double DrawProbability(CombatState state) {
        int deck = state.DrawPile.Count + state.DiscardPile.Count + state.Hand.Count;
        if (deck <= 0) return 0.5;
        return Math.Min(1.0, CombatPileSimulator.BaseHandDrawCount / (double)Math.Max(1, deck));
    }
}
