using System;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

/// <summary>
/// Estimates when playing exhaust/burn enablers (Havoc, Burning Pact) threatens key cards in hand or on draw top.
/// </summary>
public static class KeyCardBurnRiskEvaluator {
    public const int MinPeekBlockValue = 4;
    public const int MinKeyCardValue = 25;

    public static bool IsBurnEnabler(CombatHandCard card) =>
        card.Profile.Flags.HasFlag(CardMechanicFlags.HasExhaustFromHand)
        || card.Profile.Flags.HasFlag(CardMechanicFlags.PlaysTopOfDrawExhaust)
        || CardPileEffectResolver.ExhaustHandCount(card.Id) > 0;

    public static bool BurnsDrawTopOnPlay(CombatHandCard card) =>
        card.Profile.Flags.HasFlag(CardMechanicFlags.PlaysTopOfDrawExhaust);

    public static bool HasAffordableBurnEnabler(CombatState state, int excludeHandIndex = -1) {
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;
            var card = state.Hand[i];
            if (!IsBurnEnabler(card))
                continue;
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            return true;
        }

        return false;
    }

    public static bool HasAffordableDrawTopBurnEnabler(CombatState state, int excludeHandIndex = -1) {
        for (int i = 0; i < state.Hand.Count; i++) {
            if (i == excludeHandIndex)
                continue;
            var card = state.Hand[i];
            if (!BurnsDrawTopOnPlay(card))
                continue;
            if (!CombatCardCost.CanAfford(card, state))
                continue;
            return true;
        }

        return false;
    }

    /// <summary>Peeked draw-top block that Havoc-style plays would destroy before it can be drawn.</summary>
    public static int PeekTopPureBlockAtBurnRisk(CombatState state, int excludeHandIndex = -1) {
        if (!HasAffordableDrawTopBurnEnabler(state, excludeHandIndex))
            return 0;

        var peek = DrawPlanner.PeekTop(state, 1);
        if (peek.Count == 0)
            return 0;

        var top = peek[0];
        if (!IsPureBlockPileCard(top))
            return 0;

        int block = CombatDamageCalc.OutgoingBlock(top, state.Modifiers);
        return block >= MinPeekBlockValue ? block : 0;
    }

    public static int TopDrawKeyCardValue(CombatState state) {
        var peek = DrawPlanner.PeekTop(state, 1);
        if (peek.Count == 0)
            return 0;

        return KeyPileCardValue(peek[0], state);
    }

    public static bool ShouldPruneBurnEnablerOpener(CombatState state, int handIndex) {
        if (handIndex < 0 || handIndex >= state.Hand.Count)
            return false;

        var card = state.Hand[handIndex];
        if (!BurnsDrawTopOnPlay(card))
            return false;
        if (!CombatCardCost.CanAfford(card, state))
            return false;

        int topValue = TopDrawKeyCardValue(state);
        if (topValue < MinKeyCardValue)
            return false;

        int peekBlock = PeekTopPureBlockAtBurnRisk(state, handIndex);
        if (peekBlock > 0)
            return true;

        return topValue >= MinKeyCardValue + 15;
    }

    public static int KeyHandCardValue(CombatHandCard card, CombatState state) {
        if (DeckPollutionEvaluator.IsHandJunk(card))
            return -200;

        int score = 0;
        int incoming = ThreatModel.IncomingDamage(state);

        if (BlockDefensePolicy.IsBlockScalingAttack(card))
            score += 80 + CombatDamageCalc.OutgoingDamage(card, state);

        if (MechanicCombatBonus.IsSetupSkill(card.Profile))
            score += 90;

        if (card.IsAttack && CombatDamageCalc.DealsAttackDamage(card))
            score += CombatDamageCalc.OutgoingDamage(card, state) * 2 + (incoming > 0 ? 6 : 0);

        score += CombatDamageCalc.OutgoingBlock(card, state);
        if (card.Cost <= state.Energy)
            score += 3;

        return score;
    }

    static int KeyPileCardValue(CombatPileCard card, CombatState state) {
        if (card.IsStatus || CombatJunkCard.IsJunkId(card.Id))
            return -200;

        int score = 0;
        int incoming = ThreatModel.IncomingDamage(state);

        if (BlockDefensePolicy.IsBlockScalingCardId(card.Id))
            score += 70 + CombatDamageCalc.OutgoingDamage(card, state.Modifiers);

        if (CardMechanicIndex.TryGet(card.Id, out var profile)
            && MechanicCombatBonus.IsSetupSkill(profile))
            score += 90;

        bool isAttack = string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase)
            || card.Damage > 0;
        if (isAttack)
            score += CombatDamageCalc.OutgoingDamage(card, state.Modifiers) * 2 + (incoming > 0 ? 6 : 0);

        score += CombatDamageCalc.OutgoingBlock(card, state.Modifiers);
        int cost = CombatCardCost.EffectiveCost(card, state.Modifiers);
        if (cost <= state.Energy)
            score += 3;

        return score;
    }

    static bool IsPureBlockPileCard(CombatPileCard card) {
        if (card.Block <= 0)
            return false;

        if (string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase))
            return false;

        if (card.Damage > 0)
            return false;

        if (CardMechanicIndex.TryGet(card.Id, out var profile)) {
            if (profile.AppliedVulnerable > 0 || profile.AppliedWeak > 0)
                return false;
        }

        return true;
    }
}
