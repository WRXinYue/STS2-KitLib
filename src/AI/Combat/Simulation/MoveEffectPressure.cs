using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

/// <summary>Non-attack pressure from simulating move effects against the current combat state.</summary>
public static class MoveEffectPressure {
    const int MaxSummonDepth = 2;
    const string DefaultStatusCardId = "SLIMED";

    static readonly CombatState Deckless = new(
        default, default, default, default, default, default, default,
        [], [], [], [], [], [], [], []);

    /// <summary>Profile-only estimate when no hand/piles are available.</summary>
    public static int FromMove(string? monsterId, string? moveId) =>
        FromMove(Deckless, monsterId, moveId);

    public static int FromMove(CombatState state, string? monsterId, string? moveId) =>
        FromEffects(state, monsterId, moveId, MoveEffectIndex.GetEffects(monsterId, moveId));

    public static int FromEffects(CombatState state, IEnumerable<MonsterMoveEffect> effects) =>
        FromEffects(state, null, null, effects as IReadOnlyList<MonsterMoveEffect> ?? effects.ToList());

    public static int FromEffects(
        CombatState state,
        string? monsterId,
        string? moveId,
        IReadOnlyList<MonsterMoveEffect> effects) {
        if (effects.Count == 0)
            return 0;

        int pressure = 0;
        int strengthDelta = 0;
        int attackDamage = 0;

        foreach (var effect in effects) {
            switch (effect.Kind) {
                case MonsterMoveEffectKind.Attack:
                    attackDamage = Math.Max(attackDamage, effect.Damage);
                    break;
                case MonsterMoveEffectKind.EnemyStrength:
                case MonsterMoveEffectKind.AllyStrength:
                case MonsterMoveEffectKind.PowerBuff:
                    strengthDelta += effect.StrengthDelta;
                    break;
                default:
                    pressure += SingleEffectPressure(state, monsterId, moveId, effect, summonDepth: 0);
                    break;
            }
        }

        if (attackDamage > 0 && strengthDelta > 0)
            pressure += strengthDelta;

        if (attackDamage == 0 && strengthDelta > 0 && !string.IsNullOrWhiteSpace(monsterId))
            pressure += strengthDelta * NextAttackDamage(monsterId, moveId);

        return pressure;
    }

    static int SingleEffectPressure(
        CombatState state,
        string? monsterId,
        string? moveId,
        MonsterMoveEffect effect,
        int summonDepth) {
        return effect.Kind switch {
            MonsterMoveEffectKind.StatusInject => StatusInjectPressure(state, effect),
            MonsterMoveEffectKind.Steal => StealPressure(state),
            MonsterMoveEffectKind.Summon => SummonPressure(state, effect.SpawnMonsterId, summonDepth + 1),
            MonsterMoveEffectKind.PowerDebuff
                or MonsterMoveEffectKind.PowerAffliction
                or MonsterMoveEffectKind.PowerBuff => ModifierPressure(state, effect),
            MonsterMoveEffectKind.Heal => Math.Max(0, effect.Count),
            _ => 0,
        };
    }

    static int StatusInjectPressure(CombatState state, MonsterMoveEffect effect) {
        if (effect.IsNonDeterministic || effect.Count <= 0)
            return 0;

        int before = DeckPollutionEvaluator.EffectivePollutionBurden(state);
        var cardId = string.IsNullOrWhiteSpace(effect.CardId) ? DefaultStatusCardId : effect.CardId;
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        var pile = effect.Pile ?? "Discard";
        bool random = pile.Contains("Random", StringComparison.OrdinalIgnoreCase);
        var target = pile.Replace("Random", "", StringComparison.OrdinalIgnoreCase);

        if (string.Equals(target, "Draw", StringComparison.OrdinalIgnoreCase)) {
            if (random) {
                (draw, _) = CombatPileSimulator.InjectStatusAtRandom(
                    draw, cardId, effect.Count, state.ShuffleRngSeed, state.ShuffleRngCounter);
            } else {
                draw = CombatPileSimulator.InjectStatus(draw, cardId, effect.Count);
            }
        } else if (random) {
            (discard, _) = CombatPileSimulator.InjectStatusAtRandom(
                discard, cardId, effect.Count, state.ShuffleRngSeed, state.ShuffleRngCounter);
        } else {
            discard = CombatPileSimulator.InjectStatus(discard, cardId, effect.Count);
        }

        var after = state with { DrawPile = draw, DiscardPile = discard };
        return Math.Max(0, DeckPollutionEvaluator.EffectivePollutionBurden(after) - before);
    }

    static int StealPressure(CombatState state) {
        // Vuln-aware ExpectedPlayableDamage re-enters focus threat scoring — use flat hand damage here.
        int before = HandAttackDamage(state);
        var draw = state.DrawPile.ToList();
        var discard = state.DiscardPile.ToList();
        (draw, discard) = StealEffectSimulator.Apply(draw, discard);
        var after = state with { DrawPile = draw, DiscardPile = discard };
        return Math.Max(0, before - HandAttackDamage(after));
    }

    static int ModifierPressure(CombatState state, MonsterMoveEffect effect) {
        int beforeDamage = HandAttackDamage(state);
        int beforeBlock = DeckPollutionEvaluator.ExpectedPlayableBlock(state);
        var modifier = PlayerCombatModifierRegistry.FromMoveEffect(effect);
        var after = state with { Modifiers = state.Modifiers.Append(modifier).ToList() };
        int lostDamage = beforeDamage - HandAttackDamage(after);
        int lostBlock = beforeBlock - DeckPollutionEvaluator.ExpectedPlayableBlock(after);
        return Math.Max(0, lostDamage + lostBlock);
    }

    static int HandAttackDamage(CombatState state) {
        int total = 0;
        foreach (var card in state.Hand) {
            if (!card.IsAttack || card.Damage <= 0) continue;
            if (!CombatCardCost.CanAfford(card, state)) continue;
            total += CombatDamageCalc.OutgoingDamage(card, state, vulnerableOnTarget: 0);
        }

        return total;
    }

    static int SummonPressure(CombatState state, string? spawnMonsterId, int depth) {
        if (string.IsNullOrWhiteSpace(spawnMonsterId) || depth > MaxSummonDepth)
            return 0;
        if (!MonsterMechanicIndex.TryGet(spawnMonsterId, out var profile) || profile.Moves.Count == 0)
            return 0;

        var move = profile.Moves[0];
        var effects = MoveEffectIndex.GetEffects(spawnMonsterId, move.MoveId);
        int pressure = 0;
        foreach (var effect in effects) {
            if (effect.Kind is MonsterMoveEffectKind.Attack or MonsterMoveEffectKind.Summon)
                continue;
            pressure += SingleEffectPressure(state, spawnMonsterId, move.MoveId, effect, depth);
        }

        return pressure;
    }

    static int NextAttackDamage(string monsterId, string? currentMoveId) {
        if (!MonsterMechanicIndex.TryGet(monsterId, out var profile))
            return 0;

        bool passedCurrent = string.IsNullOrWhiteSpace(currentMoveId);
        foreach (var move in profile.Moves) {
            if (!passedCurrent) {
                if (string.Equals(move.MoveId, currentMoveId, StringComparison.OrdinalIgnoreCase))
                    passedCurrent = true;
                continue;
            }

            int damage = AttackDamageFromEffects(MoveEffectIndex.GetEffects(monsterId, move.MoveId));
            if (damage > 0)
                return damage;
        }

        foreach (var move in profile.Moves) {
            int damage = AttackDamageFromEffects(MoveEffectIndex.GetEffects(monsterId, move.MoveId));
            if (damage > 0)
                return damage;
        }

        return 0;
    }

    public static int AttackDamageFromEffects(IReadOnlyList<MonsterMoveEffect> effects) {
        int damage = 0;
        foreach (var effect in effects) {
            if (effect.Kind == MonsterMoveEffectKind.Attack)
                damage = Math.Max(damage, effect.Damage);
        }

        return damage;
    }
}
