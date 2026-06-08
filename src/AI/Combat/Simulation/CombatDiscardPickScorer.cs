using System;
using System.Collections.Generic;
using KitLib.AI.Combat;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatDiscardPickScorer {
    public static int TopPickScore(CombatState state, CombatPileCard card) {
        if (card.IsStatus)
            return -200;

        if (CardMechanicIndex.TryGet(card.Id, out var profile)
            && MechanicCombatBonus.IsSetupSkill(profile))
            return 120;

        int score = 0;
        bool isAttack = string.Equals(card.CardType, "Attack", StringComparison.OrdinalIgnoreCase)
            || card.Damage > 0;
        var incoming = ThreatModel.IncomingDamage(state);

        if (isAttack) {
            score += card.Damage * 2;
            if (incoming > 0)
                score += 8;
        }

        score += CombatCardCost.EffectiveBlock(card.Block, state.Modifiers);

        if (CombatCardCost.EffectiveCost(card, state.Modifiers) <= state.Energy)
            score += 4;

        return score;
    }

    public static bool IsTopDeckPickFromDiscard(IReadOnlyList<CardModel> options, int selectCount) {
        if (selectCount != 1 || options.Count == 0)
            return false;

        foreach (var card in options) {
            if (card.Pile?.Type != PileType.Discard)
                return false;
        }

        return true;
    }
}
