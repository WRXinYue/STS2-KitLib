using System;
using System.Collections.Generic;
using KitLib.AI.Combat;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatCardPlayEffects {
    public static void ApplyIteration(
        CombatHandCard card,
        CombatState state,
        int enemyIndex,
        int skillsInHand,
        PlayerBuffState buffs,
        ref int playerHp,
        ref int block,
        ref List<CombatEnemy> enemies) {
        if (card.Profile.HpLoss > 0)
            PlayerPowerSimulator.ApplyPlayerHpLoss(
                card.Profile.HpLoss, buffs, ref playerHp, ref enemies);

        if (card.IsAttack && card.Damage > 0) {
            if (card.IsAoe || CombatTargetTypes.IsAllEnemies(card.TargetType)) {
                var aoeDamage = CombatDamageCalc.OutgoingDamage(card, state, vulnerableOnTarget: 0, skillsInHand);
                CombatEffectApplier.ApplyAoeDamage(enemies, aoeDamage);
            } else {
                var targetIndex = enemyIndex;
                if (targetIndex < 0)
                    targetIndex = CombatSetupEvaluator.PrimaryAttackTargetIndex(state with {
                        PlayerHp = playerHp,
                        PlayerBlock = block,
                        Enemies = enemies,
                    });

                if (targetIndex >= 0) {
                    var target = FindEnemy(enemies, targetIndex);
                    var damage = CombatDamageCalc.OutgoingDamage(
                        card,
                        state with { PlayerHp = playerHp, PlayerBlock = block, Enemies = enemies },
                        target?.Vulnerable ?? 0,
                        skillsInHand);
                    CombatEffectApplier.ApplySingleDamage(enemies, targetIndex, damage);
                }
            }
        }

        if (card.Profile.AppliedVulnerable > 0)
            CombatEffectApplier.ApplyDebuff(enemies, enemyIndex, card.IsAoe, "VULNERABLE", card.Profile.AppliedVulnerable);

        if (card.Profile.AppliedWeak > 0)
            CombatEffectApplier.ApplyDebuff(enemies, enemyIndex, card.IsAoe, "WEAK", card.Profile.AppliedWeak);

        if (card.IsSkill) {
            if (card.Block > 0)
                block += CombatDamageCalc.OutgoingBlock(card, state with { PlayerBlock = block });
            else if (!MechanicCombatBonus.IsSetupSkill(card.Profile))
                block += 5;
        }
    }

    public static bool TryPlayTopOfDrawExhaust(
        CombatState state,
        int enemyIndex,
        ref List<CombatPileCard> draw,
        ref List<CombatPileCard> discard,
        ref List<CombatPileCard> exhaust,
        ref int playerHp,
        ref int block,
        ref List<CombatEnemy> enemies,
        ref List<PlayerCombatModifier> modifiers,
        ref PlayerBuffState buffs,
        ref int rngCounter) {
        (draw, discard, rngCounter) = CombatPileSimulator.ReshuffleIfNeeded(
            draw, discard, state.ShuffleRngSeed, rngCounter);
        if (draw.Count == 0)
            return false;

        var top = draw[0];
        draw.RemoveAt(0);

        var profile = CardMechanicIndex.TryGet(top.Id, out var indexed)
            ? indexed
            : CardMechanicIndex.InferFromSnapshot(top.ToJson());
        var topCard = PileToHand(top, profile);
        int skillsInHand = CountSkillsInHand(state.Hand);

        ApplyIteration(topCard, state, enemyIndex, skillsInHand, buffs, ref playerHp, ref block, ref enemies);
        PlayerPowerSimulator.InstallFromCard(topCard, ref modifiers, ref buffs);
        exhaust = CombatPileSimulator.AddToBottom(exhaust, top);
        return true;
    }

    public static CombatHandCard PileToHand(CombatPileCard pile, CardMechanicProfile profile, int handIndex = 0) {
        var isAoe = profile.Flags.HasFlag(CardMechanicFlags.Aoe);
        return new CombatHandCard(
            handIndex,
            pile.Id,
            pile.Name,
            0,
            pile.Damage,
            pile.Block,
            pile.CardType,
            isAoe ? "AllEnemies" : "AnyEnemy",
            true,
            profile,
            isAoe,
            pile.HasRetain,
            true,
            profile.AttackHitCount);
    }

    public static int CountSkillsInHand(IReadOnlyList<CombatHandCard> hand) {
        int count = 0;
        foreach (var card in hand) {
            if (card.IsSkill)
                count++;
        }

        return count;
    }

    public static int CountStatusCards(CombatState state) {
        int count = 0;
        foreach (var card in state.Hand) {
            if (DeckPollutionEvaluator.IsHandJunk(card))
                count++;
        }

        foreach (var card in state.DrawPile) {
            if (card.IsStatus)
                count++;
        }

        foreach (var card in state.DiscardPile) {
            if (card.IsStatus)
                count++;
        }

        return count;
    }

    static CombatEnemy? FindEnemy(List<CombatEnemy> enemies, int targetIndex) {
        foreach (var enemy in enemies) {
            if (!enemy.IsAlive) continue;
            if (enemy.Index == targetIndex)
                return enemy;
        }

        return null;
    }
}
