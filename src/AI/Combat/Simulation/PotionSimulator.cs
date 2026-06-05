using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

internal static class PotionSimulator {
    public static CombatState Apply(CombatState state, int slot, int enemyIndex, int mcBranch) {
        if (state.PotionUsedThisTurn || state.Potions.Count == 0)
            return state;

        var potion = state.Potions.FirstOrDefault(p => p.Slot == slot);
        if (potion == null)
            return state;

        if (!PotionCombatEffectData.TryGetProfile(potion.Id, out var profile) || !profile.Simulatable)
            return state;

        if (profile.Random != null && mcBranch <= 0)
            return state;

        if (NeedsEnemyTarget(profile.TargetType) && enemyIndex < 0)
            return state;

        var hp = state.PlayerHp;
        var block = state.PlayerBlock;
        var energy = state.Energy;
        var hand = state.Hand.ToList();
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var exhaust = state.ExhaustPile.ToList();
        var enemies = state.Enemies.ToList();
        var modifiers = state.Modifiers.ToList();
        var rngCounter = state.ShuffleRngCounter;

        if (profile.Random != null) {
            for (int i = 0; i < profile.Random.PickCount; i++) {
                if (hand.Count >= CombatPileSimulator.MaxHandSize)
                    break;
                var card = PotionRandomPools.SampleCard(profile.Random.Pool, state, slot, mcBranch);
                hand.Add(card with { HandIndex = hand.Count });
            }
        } else {
            foreach (var effect in profile.Effects)
                ApplyEffect(effect, profile.TargetType, ref hp, ref block, ref energy, ref hand, ref draw, ref discard, ref exhaust, ref enemies, ref modifiers, ref rngCounter, state, enemyIndex);
        }

        var remaining = state.Potions.Where(p => p.Slot != slot).ToList();
        hand = ReindexHand(hand);

        return state
            .WithPlayer(hp, block, energy)
            .WithHand(hand)
            .WithEnemies(enemies)
            .WithPiles(draw, discard, exhaust)
            .WithModifiers(modifiers)
            .WithShuffleRng(state.ShuffleRngSeed, rngCounter)
            .WithPotions(remaining, potionUsedThisTurn: true);
    }

    static void ApplyEffect(
        PotionCombatEffect effect,
        string targetType,
        ref int hp,
        ref int block,
        ref int energy,
        ref List<CombatHandCard> hand,
        ref List<CombatPileCard> draw,
        ref List<CombatPileCard> discard,
        ref List<CombatPileCard> exhaust,
        ref List<CombatEnemy> enemies,
        ref List<PlayerCombatModifier> modifiers,
        ref int rngCounter,
        CombatState state,
        int enemyIndex) {
        switch (effect.Kind) {
            case PotionCombatEffectKind.GainBlock:
                block += CombatDamageCalc.OutgoingBlock(effect.Amount, modifiers);
                break;

            case PotionCombatEffectKind.GainEnergy:
                energy += effect.Amount;
                break;

            case PotionCombatEffectKind.DrawCards:
                (hand, draw, discard, rngCounter) = CombatPileSimulator.DrawCards(
                    hand, draw, discard, effect.Amount,
                    state.ShuffleRngSeed, rngCounter);
                break;

            case PotionCombatEffectKind.GainStrength:
                modifiers = CombatEffectApplier.AddModifier(modifiers, PlayerCombatModifier.Strength(effect.Amount));
                break;

            case PotionCombatEffectKind.GainDexterity:
                modifiers = CombatEffectApplier.AddModifier(modifiers, PlayerCombatModifier.Dexterity(effect.Amount));
                break;

            case PotionCombatEffectKind.ApplyWeak:
                CombatEffectApplier.ApplyDebuff(enemies, enemyIndex, IsAllEnemies(targetType), "WEAK", effect.Amount);
                break;

            case PotionCombatEffectKind.ApplyVulnerable:
                CombatEffectApplier.ApplyDebuff(enemies, enemyIndex, IsAllEnemies(targetType), "VULNERABLE", effect.Amount);
                break;

            case PotionCombatEffectKind.DamageSingle:
                CombatEffectApplier.ApplySingleDamage(enemies, enemyIndex, effect.Amount);
                break;

            case PotionCombatEffectKind.DamageAll:
                CombatEffectApplier.ApplyAoeDamage(enemies, effect.Amount);
                break;
        }
    }

    public static bool NeedsEnemyTarget(string? targetType) {
        if (string.IsNullOrEmpty(targetType)) return false;
        return targetType.Contains("Enemy", StringComparison.OrdinalIgnoreCase);
    }

    static bool IsAllEnemies(string targetType) =>
        targetType.Contains("AllEnemies", StringComparison.OrdinalIgnoreCase);

    static List<CombatHandCard> ReindexHand(List<CombatHandCard> hand) {
        var result = new List<CombatHandCard>(hand.Count);
        for (int i = 0; i < hand.Count; i++)
            result.Add(hand[i] with { HandIndex = i });
        return result;
    }
}
