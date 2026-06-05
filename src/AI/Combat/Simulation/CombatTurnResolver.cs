using System;
using System.Collections.Generic;
using System.Linq;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat.Simulation;

public static class CombatTurnResolver {
    public static CombatState ResolveEndTurn(CombatState state) {
        var hp = state.PlayerHp;
        var block = state.PlayerBlock;
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var exhaust = state.ExhaustPile.ToList();
        var modifiers = state.Modifiers.ToList();
        var enemies = state.Enemies.ToList();

        var (retained, newDiscard) = CombatPileSimulator.DiscardHand(state.Hand, discard);
        discard = newDiscard;
        block = 0;

        foreach (var enemy in enemies.OrderBy(e => e.Index).ToList()) {
            if (!enemy.IsAlive) continue;

            var moveId = string.IsNullOrWhiteSpace(enemy.NextMoveId)
                ? enemy.IntentSteps.FirstOrDefault()?.MoveId ?? ""
                : enemy.NextMoveId;
            var effects = MoveEffectIndex.GetEffects(enemy.MonsterId, moveId);

            foreach (var effect in effects) {
                switch (effect.Kind) {
                    case MonsterMoveEffectKind.Attack:
                        (hp, block) = ApplyEnemyAttack(hp, block, enemy.IntentDamage, modifiers);
                        break;
                    case MonsterMoveEffectKind.StatusInject:
                        if (!string.IsNullOrWhiteSpace(effect.CardId) && effect.Count > 0) {
                            if (string.Equals(effect.Pile, "Draw", StringComparison.OrdinalIgnoreCase))
                                draw = CombatPileSimulator.InjectStatus(draw, effect.CardId, effect.Count);
                            else
                                discard = CombatPileSimulator.InjectStatus(discard, effect.CardId, effect.Count);
                        }
                        break;
                    case MonsterMoveEffectKind.Summon:
                        if (!string.IsNullOrWhiteSpace(effect.SpawnMonsterId)) {
                            var summoned = CombatSummonFactory.TryCreateSummonedEnemy(
                                effect.SpawnMonsterId,
                                NextEnemyIndex(enemies),
                                enemy.Index,
                                enemies);
                            if (summoned != null)
                                enemies.Add(summoned);
                        }
                        break;
                    case MonsterMoveEffectKind.PowerDebuff:
                        modifiers.Add(MapPowerModifier(effect));
                        break;
                    case MonsterMoveEffectKind.PowerAffliction:
                        if (!effect.IsNonDeterministic)
                            modifiers.Add(MapPowerModifier(effect));
                        break;
                }
            }

            if (enemy.IntentDamage > 0 && !effects.Any(e => e.Kind == MonsterMoveEffectKind.Attack))
                (hp, block) = ApplyEnemyAttack(hp, block, enemy.IntentDamage, modifiers);

            int idx = enemies.FindIndex(e => e.Index == enemy.Index);
            if (idx >= 0)
                enemies[idx] = AdvanceEnemyIntent(enemies[idx]);
        }

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            if (enemies[i].IsMinion) continue;
            ThreatModel.OnPrimaryEnemyKilled(enemies, i);
        }

        var (hand, drawAfter, discardAfter) = CombatPileSimulator.DrawHand(
            retained,
            draw,
            discard,
            CombatPileSimulator.BaseHandDrawCount);

        return state with {
            PlayerHp = Math.Max(0, hp),
            PlayerBlock = block,
            Energy = state.MaxEnergy,
            TurnNumber = state.TurnNumber + 1,
            Hand = hand,
            DrawPile = drawAfter,
            DiscardPile = discardAfter,
            ExhaustPile = exhaust,
            Modifiers = modifiers,
            Enemies = enemies,
        };
    }

    static (int hp, int block) ApplyEnemyAttack(
        int hp,
        int block,
        int damage,
        List<PlayerCombatModifier> modifiers) {
        if (damage <= 0) return (hp, block);

        var net = Math.Max(0, damage - block);
        var newBlock = Math.Max(0, block - damage);
        return (Math.Max(0, hp - net), newBlock);
    }

    static PlayerCombatModifier MapPowerModifier(MonsterMoveEffect effect) {
        if (string.Equals(effect.PowerId, "SHRINK", StringComparison.OrdinalIgnoreCase))
            return PlayerCombatModifier.Shrink();
        if (string.Equals(effect.PowerId, "SMOGGY", StringComparison.OrdinalIgnoreCase))
            return PlayerCombatModifier.Smoggy();
        if (string.Equals(effect.PowerId, "TANGLED", StringComparison.OrdinalIgnoreCase))
            return PlayerCombatModifier.Tangled();
        if (string.Equals(effect.PowerId, "CHAINS_OF_BINDING", StringComparison.OrdinalIgnoreCase))
            return PlayerCombatModifier.ChainsOfBinding();

        return new PlayerCombatModifier(
            effect.PowerId ?? "UNKNOWN",
            effect.AttackDamageMultiplier,
            effect.SkillCostPenalty,
            effect.AttackCostPenalty,
            effect.BoundCardsPerTurn);
    }

    static CombatEnemy AdvanceEnemyIntent(CombatEnemy enemy) {
        if (enemy.IntentSteps.Length <= 1)
            return enemy;

        var remaining = enemy.IntentSteps.Skip(1).ToArray();
        if (remaining.Length == 0)
            return enemy;

        var next = remaining[0];
        return enemy.WithMove(
            next.MoveId,
            next.IntentDamage,
            next.NonDamageThreat,
            remaining);
    }

    static int NextEnemyIndex(IReadOnlyList<CombatEnemy> enemies) {
        int max = -1;
        foreach (var enemy in enemies)
            max = Math.Max(max, enemy.Index);
        return max + 1;
    }
}
