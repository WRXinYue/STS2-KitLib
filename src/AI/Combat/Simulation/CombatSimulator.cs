using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class CombatSimulator {
    public static CombatState Apply(CombatState state, SimCombatAction action) {
        if (action.Kind == SimActionKind.EndTurn)
            return CombatTurnResolver.ResolveEndTurn(state);

        if (action.HandIndex < 0 || action.HandIndex >= state.Hand.Count)
            return state;

        var card = state.Hand[action.HandIndex];
        if (!CombatCardCost.CanAfford(card, state))
            return state;

        var energy = state.Energy - CombatCardCost.EffectiveCost(card, state.Modifiers);
        var hand = state.Hand.ToList();
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var exhaust = state.ExhaustPile.ToList();
        var enemies = state.Enemies.ToList();
        var block = state.PlayerBlock;
        var rngCounter = state.ShuffleRngCounter;
        var pileEffects = CardPileEffectResolver.ResolveAll(card.Id);

        if (CombatTransformSimulator.IsHandAttackTransform(card.Profile)) {
            hand = ApplyHandTransform(hand, action.HandIndex);
            return state.WithPlayer(state.PlayerHp, block, energy).WithHand(hand);
        }

        hand.RemoveAt(action.HandIndex);

        if (card.IsAttack && card.Damage > 0) {
            if (card.IsAoe || card.TargetType is "AllEnemy")
                ApplyAoeDamage(enemies, card.Damage);
            else if (action.EnemyIndex >= 0)
                ApplySingleDamage(enemies, action.EnemyIndex, card.Damage);
        }

        if (card.Profile.AppliedVulnerable > 0)
            ApplyDebuff(enemies, action, card.IsAoe, "VULNERABLE", card.Profile.AppliedVulnerable);

        if (card.Profile.AppliedWeak > 0)
            ApplyDebuff(enemies, action, card.IsAoe, "WEAK", card.Profile.AppliedWeak);

        if (card.IsSkill) {
            if (card.Block > 0)
                block += CombatCardCost.EffectiveBlock(card.Block, state.Modifiers);
            else if (!MechanicCombatBonus.IsSetupSkill(card.Profile))
                block += 5;
        }

        var pileCard = CombatPileSimulator.HandToPile(card);
        if (card.HasExhaust || card.Profile.Flags.HasFlag(CardMechanicFlags.Exhaust))
            exhaust = CombatPileSimulator.AddToBottom(exhaust, pileCard);
        else
            discard = CombatPileSimulator.AddToBottom(discard, pileCard);

        if (pileEffects.Discard > 0)
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

        return state
            .WithPlayer(state.PlayerHp, block, energy)
            .WithHand(hand)
            .WithEnemies(enemies)
            .WithPiles(draw, discard, exhaust)
            .WithShuffleRng(state.ShuffleRngSeed, rngCounter);
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

    static void ApplyAoeDamage(List<CombatEnemy> enemies, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
        }
    }

    static void ApplySingleDamage(List<CombatEnemy> enemies, int targetIndex, int damage) {
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != targetIndex && i != targetIndex) continue;
            if (!enemies[i].IsAlive) continue;
            var before = enemies[i];
            enemies[i] = ApplyDamageToEnemy(enemies[i], damage);
            if (before.IsAlive && !enemies[i].IsAlive && !before.IsMinion)
                ThreatModel.OnPrimaryEnemyKilled(enemies, i);
            return;
        }
    }

    static CombatEnemy ApplyDamageToEnemy(CombatEnemy enemy, int damage) {
        var scaled = (int)Math.Round(damage * (enemy.Vulnerable > 0 ? 1.5f : 1f));
        var remaining = Math.Max(0, scaled - enemy.Block);
        var newBlock = Math.Max(0, enemy.Block - scaled);
        var newHp = Math.Max(0, enemy.CurrentHp - remaining);
        return enemy.WithHp(newHp, newBlock, newHp > 0);
    }

    static void ApplyDebuff(
        List<CombatEnemy> enemies,
        SimCombatAction action,
        bool isAoe,
        string token,
        int amount) {
        if (isAoe) {
            for (int i = 0; i < enemies.Count; i++) {
                if (!enemies[i].IsAlive) continue;
                enemies[i] = token == "VULNERABLE"
                    ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                    : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            }
            return;
        }

        if (action.EnemyIndex < 0) return;
        for (int i = 0; i < enemies.Count; i++) {
            if (enemies[i].Index != action.EnemyIndex && i != action.EnemyIndex) continue;
            enemies[i] = token == "VULNERABLE"
                ? enemies[i].WithPowers(enemies[i].Vulnerable + amount, enemies[i].Weak)
                : enemies[i].WithPowers(enemies[i].Vulnerable, enemies[i].Weak + amount);
            return;
        }
    }
}
