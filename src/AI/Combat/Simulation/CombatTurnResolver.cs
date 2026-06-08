using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class CombatTurnResolver {
    public static CombatState ResolveEndTurn(CombatState state) {
        var hp = state.PlayerHp;
        var block = state.PlayerBlock;
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var exhaust = state.ExhaustPile.ToList();
        var modifiers = state.Modifiers.ToList();
        var enemies = state.Enemies.ToList();
        var rngCounter = state.ShuffleRngCounter;
        int unblockedDamage = 0;

        PlayerPowerSimulator.ApplyTurnEndBlock(state.Buffs, ref block);

        List<CombatHandCard> retained;
        if (RelicCombatRules.RetainHandOnEndTurn(state.RelicIds)) {
            retained = state.Hand.ToList();
        } else {
            (retained, discard) = CombatPileSimulator.DiscardHand(state.Hand, discard);
        }

        foreach (var enemy in enemies.OrderBy(e => e.ActOrder).ToList()) {
            if (!enemy.IsAlive) continue;

            int idx = enemies.FindIndex(e => e.Index == enemy.Index);
            if (idx < 0) continue;
            var acting = enemies[idx];

            var moveId = string.IsNullOrWhiteSpace(acting.NextMoveId)
                ? acting.IntentSteps.FirstOrDefault()?.MoveId ?? ""
                : acting.NextMoveId;
            var effects = MoveEffectIndex.GetEffects(acting.MonsterId, moveId);
            bool hasExplicitAttack = effects.Any(e => e.Kind == MonsterMoveEffectKind.Attack);

            foreach (var effect in effects) {
                switch (effect.Kind) {
                    case MonsterMoveEffectKind.Attack:
                        var attackDamage = effect.Damage > 0
                            ? effect.Damage + acting.Strength
                            : acting.IntentDamage;
                        attackDamage = DebuffDamageCalc.MitigateWeakIncoming(attackDamage, acting.Weak);
                        (hp, block, var unblocked) = ApplyEnemyAttack(hp, block, attackDamage, modifiers);
                        unblockedDamage += unblocked;
                        break;
                    case MonsterMoveEffectKind.EnemyStrength:
                        acting = acting.AddStrength(effect.StrengthDelta);
                        enemies[idx] = acting;
                        break;
                    case MonsterMoveEffectKind.AllyStrength:
                        ApplyAllyStrength(enemies, effect.StrengthDelta);
                        acting = enemies[idx];
                        break;
                    case MonsterMoveEffectKind.StatusInject:
                        if (!string.IsNullOrWhiteSpace(effect.CardId) && effect.Count > 0) {
                            (draw, discard, rngCounter) = ApplyStatusInject(
                                draw, discard, effect, state.ShuffleRngSeed, rngCounter);
                        }
                        break;
                    case MonsterMoveEffectKind.Steal:
                        (draw, discard) = StealEffectSimulator.Apply(draw, discard);
                        break;
                    case MonsterMoveEffectKind.Summon:
                        if (!string.IsNullOrWhiteSpace(effect.SpawnMonsterId)) {
                            var summoned = CombatSummonFactory.TryCreateSummonedEnemy(
                                effect.SpawnMonsterId,
                                NextEnemyIndex(enemies),
                                acting.Index,
                                enemies,
                                state with {
                                    DrawPile = draw,
                                    DiscardPile = discard,
                                    Modifiers = modifiers,
                                    Enemies = enemies,
                                });
                            if (summoned != null)
                                enemies.Add(summoned);
                        }
                        break;
                    case MonsterMoveEffectKind.PowerDebuff:
                        modifiers.Add(MapPowerModifier(effect));
                        break;
                    case MonsterMoveEffectKind.PowerAffliction:
                        if (string.Equals(effect.PowerId, "SWIPE", StringComparison.OrdinalIgnoreCase)) {
                            (draw, discard) = StealEffectSimulator.Apply(draw, discard);
                        } else if (!effect.IsNonDeterministic) {
                            modifiers.Add(MapPowerModifier(effect));
                        }
                        break;
                }
            }

            if (acting.IntentDamage > 0 && !hasExplicitAttack) {
                int intentDamage = DebuffDamageCalc.MitigateWeakIncoming(acting.IntentDamage, acting.Weak);
                (hp, block, var unblocked) = ApplyEnemyAttack(hp, block, intentDamage, modifiers);
                unblockedDamage += unblocked;
            }

            enemies[idx] = AdvanceEnemyIntent(acting);
        }

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            enemies[i] = enemies[i].TickDownDebuffs();
        }

        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            if (enemies[i].IsMinion) continue;
            ThreatModel.OnPrimaryEnemyKilled(enemies, i);
        }

        int nextCombatRound = state.TurnNumber + 1;
        int handDraw = RelicCombatRules.HandDrawCount(state.RelicIds, nextCombatRound);
        (List<CombatHandCard> hand, List<CombatPileCard> drawAfter, List<CombatPileCard> discardAfter, int drawCounter) =
            CombatPileSimulator.DrawHand(
                retained,
                draw,
                discard,
                handDraw,
                state.ShuffleRngSeed,
                rngCounter);
        rngCounter = drawCounter;

        // Block absorbs during enemy phase; clears at start of the next player turn.
        block = 0;

        PlayerPowerSimulator.ApplyTurnStartPowers(state.Buffs, ref hp, ref block, ref enemies);

        return state with {
            PlayerHp = Math.Max(0, hp),
            PlayerBlock = block,
            Energy = RelicCombatRules.NextTurnEnergy(state),
            TurnNumber = state.TurnNumber + 1,
            Hand = hand,
            DrawPile = drawAfter,
            DiscardPile = discardAfter,
            ExhaustPile = exhaust,
            Modifiers = modifiers,
            Enemies = enemies,
            ShuffleRngCounter = rngCounter,
            NextPlayCostWaive = NextPlayCostWaive.None,
            AttacksPlayedThisTurn = 0,
            UnblockedDamageTakenThisTurn = unblockedDamage,
        };
    }

    static void ApplyAllyStrength(List<CombatEnemy> enemies, int delta) {
        if (delta == 0) return;
        for (int i = 0; i < enemies.Count; i++) {
            if (!enemies[i].IsAlive) continue;
            enemies[i] = enemies[i].AddStrength(delta);
        }
    }

    static (int hp, int block, int unblocked) ApplyEnemyAttack(
        int hp,
        int block,
        int damage,
        List<PlayerCombatModifier> modifiers) {
        if (damage <= 0) return (hp, block, 0);

        var net = Math.Max(0, damage - block);
        var newBlock = Math.Max(0, block - damage);
        return (Math.Max(0, hp - net), newBlock, net);
    }

    static PlayerCombatModifier MapPowerModifier(MonsterMoveEffect effect) =>
        PlayerCombatModifierRegistry.FromMoveEffect(effect);

    static (List<CombatPileCard> draw, List<CombatPileCard> discard, int rngCounter) ApplyStatusInject(
        List<CombatPileCard> draw,
        List<CombatPileCard> discard,
        MonsterMoveEffect effect,
        uint shuffleSeed,
        int rngCounter) {
        var pile = effect.Pile ?? "Discard";
        bool random = pile.Contains("Random", StringComparison.OrdinalIgnoreCase);
        var target = pile.Replace("Random", "", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(target, "Draw", StringComparison.OrdinalIgnoreCase)) {
            if (random)
                (draw, rngCounter) = CombatPileSimulator.InjectStatusAtRandom(
                    draw, effect.CardId!, effect.Count, shuffleSeed, rngCounter);
            else
                draw = CombatPileSimulator.InjectStatus(draw, effect.CardId!, effect.Count);
            return (draw, discard, rngCounter);
        }

        if (random)
            (discard, rngCounter) = CombatPileSimulator.InjectStatusAtRandom(
                discard, effect.CardId!, effect.Count, shuffleSeed, rngCounter);
        else
            discard = CombatPileSimulator.InjectStatus(discard, effect.CardId!, effect.Count);
        return (draw, discard, rngCounter);
    }

    /// <summary>Advance intent chains after the enemy phase without applying damage or pile effects.</summary>
    public static CombatState ProjectAfterEnemyPhase(CombatState state) {
        var enemies = new List<CombatEnemy>(state.Enemies.Count);
        foreach (var enemy in state.Enemies) {
            enemies.Add(enemy.IsAlive ? AdvanceEnemyIntent(enemy) : enemy);
        }

        return state with { Enemies = enemies };
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
