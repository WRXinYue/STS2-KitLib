using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class CombatSimulator {
    public static CombatState Apply(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return CombatTurnResolver.ResolveEndTurn(state);

        if (action.Kind == SimActionKind.UsePotion)
            return PotionSimulator.Apply(state, action.PotionSlot, action.EnemyIndex, action.McBranch);

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return state;

        var card = state.Hand[action.HandIndex];
        if (!CombatCardCost.CanAfford(card, state))
            return state;

        var waive = state.NextPlayCostWaive;
        bool consumedWaive = CardPlayCostEffect.ConsumesWaive(card, waive);
        var energy = state.Energy - CombatCardCost.EffectiveCost(card, state);
        var hand = state.Hand.ToList();
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var exhaust = state.ExhaustPile.ToList();
        var enemies = state.Enemies.ToList();
        var hp = state.PlayerHp;
        var block = state.PlayerBlock;
        var rngCounter = state.ShuffleRngCounter;
        var attacksPlayed = state.AttacksPlayedThisTurn;
        var modifiers = state.Modifiers.ToList();
        var buffs = state.Buffs;
        var pileEffects = CardPileEffectResolver.ResolveAll(card.Id);
        var exhaustHand = CardPileEffectResolver.ExhaustHandCount(card.Id);

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile)) {
            hand = ApplyHandTransform(hand, action.HandIndex);
            return state.WithPlayer(state.PlayerHp, block, energy).WithHand(hand);
        }

        int skillsInHand = CombatCardPlayEffects.CountSkillsInHand(hand);
        hand.RemoveAt(action.HandIndex);

        if (card.Profile.Flags.HasFlag(CardMechanicFlags.PlaysTopOfDrawExhaust)) {
            CombatCardPlayEffects.TryPlayTopOfDrawExhaust(
                state with { Energy = energy, Hand = hand, DrawPile = draw, DiscardPile = discard, ExhaustPile = exhaust, Enemies = enemies },
                action.EnemyIndex,
                ref draw,
                ref discard,
                ref exhaust,
                ref hp,
                ref block,
                ref enemies,
                ref modifiers,
                ref buffs,
                ref rngCounter);
        } else {
            int playCount = 1 + Math.Max(0, card.Profile.ReplayCount);
            for (int i = 0; i < playCount; i++) {
                CombatCardPlayEffects.ApplyIteration(
                    card,
                    state with { Energy = energy, Hand = hand, Enemies = enemies, AttacksPlayedThisTurn = attacksPlayed },
                    action.EnemyIndex,
                    skillsInHand,
                    buffs,
                    ref hp,
                    ref block,
                    ref enemies);

                if (card.IsAttack && card.Damage > 0)
                    attacksPlayed++;
            }
        }

        PlayerPowerSimulator.InstallFromCard(card, ref modifiers, ref buffs);

        var pileCard = CombatPileSimulator.HandToPile(card);
        if (card.HasExhaust || card.Profile.Flags.HasFlag(CardMechanicFlags.Exhaust))
            exhaust = CombatPileSimulator.AddToBottom(exhaust, pileCard);
        else
            discard = CombatPileSimulator.AddToBottom(discard, pileCard);

        if (exhaustHand > 0)
            (hand, exhaust) = ExhaustFromHand(hand, exhaust, exhaustHand, state);
        else if (pileEffects.Discard > 0)
            (hand, discard) = DiscardFromHand(hand, discard, pileEffects.Discard, state);

        if (pileEffects.Draw > 0) {
            (hand, draw, discard, rngCounter) = CombatPileSimulator.DrawCards(
                hand, draw, discard, pileEffects.Draw,
                state.ShuffleRngSeed, rngCounter);
        } else {
            hand = ReindexHand(hand);
        }

        if (pileEffects.Scry > 0)
            draw = ApplyScry(draw, pileEffects.Scry);

        (draw, discard) = CombatPileManipulator.ApplyOnPlay(state, card.Id, draw, discard);

        int drawOnEmpty = RelicCombatRules.DrawOnHandEmptyCount(state.RelicIds);
        if (drawOnEmpty > 0 && hand.Count == 0) {
            (hand, draw, discard, rngCounter) = CombatPileSimulator.DrawCards(
                hand, draw, discard, drawOnEmpty,
                state.ShuffleRngSeed, rngCounter);
        }

        var nextWaive = CardPlayCostEffect.GrantOnPlay(card.Id)
            ?? (consumedWaive ? NextPlayCostWaive.None : waive);

        return state
            .WithPlayer(hp, block, energy)
            .WithHand(hand)
            .WithEnemies(enemies)
            .WithPiles(draw, discard, exhaust)
            .WithShuffleRng(state.ShuffleRngSeed, rngCounter)
            .WithModifiers(modifiers)
            .WithNextPlayCostWaive(nextWaive) with {
                AttacksPlayedThisTurn = attacksPlayed,
                Buffs = buffs,
            };
    }

    static (List<CombatHandCard> hand, List<CombatPileCard> exhaust) ExhaustFromHand(
        List<CombatHandCard> hand,
        List<CombatPileCard> exhaust,
        int count,
        CombatState state) {
        for (int i = 0; i < count && hand.Count > 0; i++) {
            int idx = PickWorstHandIndex(hand, state);
            if (idx < 0) break;
            exhaust = CombatPileSimulator.AddToBottom(exhaust, CombatPileSimulator.HandToPile(hand[idx]));
            hand.RemoveAt(idx);
        }

        return (ReindexHand(hand), exhaust);
    }

    static (List<CombatHandCard> hand, List<CombatPileCard> discard) DiscardFromHand(
        List<CombatHandCard> hand,
        List<CombatPileCard> discard,
        int count,
        CombatState state) {
        for (int i = 0; i < count && hand.Count > 0; i++) {
            int idx = PickWorstHandIndex(hand, state);
            if (idx < 0) break;
            discard = CombatPileSimulator.AddToBottom(discard, CombatPileSimulator.HandToPile(hand[idx]));
            hand.RemoveAt(idx);
        }

        return (ReindexHand(hand), discard);
    }

    static int PickWorstHandIndex(List<CombatHandCard> hand, CombatState state) {
        int worst = -1;
        int worstScore = int.MaxValue;
        var incoming = ThreatModel.IncomingDamage(state);

        for (int i = 0; i < hand.Count; i++) {
            var c = hand[i];
            if (c.HasRetain) continue;

            int score = 0;
            if (DeckPollutionEvaluator.IsHandJunk(c))
                score -= 500;
            if (c.IsAttack && c.Damage > 0)
                score += c.Damage * 2 + (incoming > 0 ? 6 : 0);
            score += c.Block;
            if (c.Cost <= state.Energy)
                score += 3;

            if (score < worstScore) {
                worstScore = score;
                worst = i;
            }
        }

        return worst >= 0 ? worst : hand.Count > 0 ? 0 : -1;
    }

    static List<CombatPileCard> ApplyScry(List<CombatPileCard> draw, int scryCount) {
        if (scryCount <= 0 || draw.Count == 0)
            return draw;

        var pile = draw.ToList();
        int window = Math.Min(scryCount, pile.Count);
        int worstIdx = 0;
        int worstScore = int.MaxValue;

        for (int i = 0; i < window; i++) {
            int score = PileCardUtility(pile[i]);
            if (score < worstScore) {
                worstScore = score;
                worstIdx = i;
            }
        }

        var bottom = pile[worstIdx];
        pile.RemoveAt(worstIdx);
        pile.Add(bottom);
        return pile;
    }

    static int PileCardUtility(CombatPileCard card) {
        if (card.IsStatus) return -20;
        return card.Damage + card.Block - card.Cost;
    }

    static List<CombatHandCard> ReindexHand(List<CombatHandCard> hand) {
        var result = new List<CombatHandCard>(hand.Count);
        for (int i = 0; i < hand.Count; i++)
            result.Add(hand[i] with { HandIndex = i });
        return result;
    }

    static List<CombatHandCard> ApplyHandTransform(List<CombatHandCard> hand, int skillIndex) {
        var skill = hand[skillIndex];
        var upgraded = (skill.ToJson()["upgradeLevel"]?.GetValue<int>() ?? 0) > 0;
        var rockDamage = CombatTransformSimulator.GiantRockDamage(upgraded);
        var result = new List<CombatHandCard>();

        for (int i = 0; i < hand.Count; i++) {
            if (i == skillIndex)
                continue;

            var c = hand[i];
            if (!CombatTransformSimulator.IsTransformableAttack(c.ToJson()))
                result.Add(c);
            else
                result.Add(c with {
                    Id = "GIANT_ROCK",
                    Name = "Giant Rock",
                    Damage = rockDamage,
                    TargetType = "AnyEnemy",
                });
        }

        return ReindexHand(result);
    }
}
