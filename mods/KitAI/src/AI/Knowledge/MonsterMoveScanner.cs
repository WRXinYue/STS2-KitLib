using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace KitLib.AI.Knowledge;

/// <summary>Offline scan of monster move state machines (intent types + static effects per move).</summary>
internal static class MonsterMoveScanner {
    public static IReadOnlyList<MonsterMoveProfile> ScanMoves(MonsterModel canonical) {
        try {
            var monster = canonical.ToMutable();
            monster.SetUpForCombat();
            var monsterId = monster.Id.Entry ?? "";
            return ScanMachine(monster.MoveStateMachine, monsterId);
        }
        catch {
            return Array.Empty<MonsterMoveProfile>();
        }
    }

    static IReadOnlyList<MonsterMoveProfile> ScanMachine(MonsterMoveStateMachine? machine, string monsterId) {
        if (machine?.States == null || machine.States.Count == 0)
            return Array.Empty<MonsterMoveProfile>();

        var profiles = new List<MonsterMoveProfile>();
        foreach (var state in machine.States.Values) {
            if (state is not MoveState move)
                continue;

            var moveId = string.IsNullOrWhiteSpace(move.StateId) ? move.Id : move.StateId;
            if (string.IsNullOrWhiteSpace(moveId) || moveId == "UNSET_MOVE")
                continue;

            var intentTypes = move.Intents
                .Where(i => i.IntentType != IntentType.Hidden)
                .Select(i => i.IntentType)
                .Distinct()
                .ToList();

            var effects = MoveEffectIndex.MergeWithRuntimeIntents(monsterId, moveId, move.Intents)
                .ToList();
            EnrichFromStaticData(monsterId, moveId, effects);

            profiles.Add(new MonsterMoveProfile(moveId, intentTypes, effects));
        }

        return profiles;
    }

    static void EnrichFromStaticData(string monsterId, string moveId, List<MonsterMoveEffect> effects) {
        var staticEffects = MonsterMoveEffectData.GetEffects(monsterId, moveId);
        if (staticEffects.Count == 0)
            return;

        for (int i = 0; i < effects.Count; i++) {
            var runtime = effects[i];
            var match = staticEffects.FirstOrDefault(s => s.Kind == runtime.Kind);
            if (match == null) continue;

            effects[i] = runtime with {
                CardId = match.CardId ?? runtime.CardId,
                Count = match.Count > 0 ? match.Count : runtime.Count,
                Pile = !string.IsNullOrWhiteSpace(match.Pile) ? match.Pile : runtime.Pile,
                SpawnMonsterId = match.SpawnMonsterId ?? runtime.SpawnMonsterId,
                PowerId = match.PowerId ?? runtime.PowerId,
                AttackDamageMultiplier = match.AttackDamageMultiplier != 1f
                    ? match.AttackDamageMultiplier
                    : runtime.AttackDamageMultiplier,
                SkillCostPenalty = match.SkillCostPenalty > 0 ? match.SkillCostPenalty : runtime.SkillCostPenalty,
                AttackCostPenalty = match.AttackCostPenalty > 0 ? match.AttackCostPenalty : runtime.AttackCostPenalty,
                BoundCardsPerTurn = match.BoundCardsPerTurn > 0 ? match.BoundCardsPerTurn : runtime.BoundCardsPerTurn,
                Damage = match.Damage > 0 ? match.Damage : runtime.Damage,
                StrengthDelta = match.StrengthDelta != 0 ? match.StrengthDelta : runtime.StrengthDelta,
                IsNonDeterministic = match.IsNonDeterministic || runtime.IsNonDeterministic,
            };
        }

        foreach (var extra in staticEffects) {
            if (!effects.Any(e => e.Kind == extra.Kind))
                effects.Add(extra);
        }
    }

    public static EnemyMechanicFlags FlagsFromMoves(IReadOnlyList<MonsterMoveProfile> moves) {
        var flags = EnemyMechanicFlags.None;
        foreach (var move in moves) {
            foreach (var intent in move.IntentTypes) {
                flags |= intent switch {
                    IntentType.Summon => EnemyMechanicFlags.CanSummonAllies,
                    IntentType.Debuff or IntentType.DebuffStrong or IntentType.CardDebuff
                        => EnemyMechanicFlags.HasDebuffIntent,
                    IntentType.Buff => EnemyMechanicFlags.HasBuffIntent,
                    IntentType.Heal => EnemyMechanicFlags.HasHealIntent,
                    IntentType.DeathBlow => EnemyMechanicFlags.HasDeathBlow,
                    IntentType.StatusCard => EnemyMechanicFlags.HasStatusCardIntent,
                    _ => EnemyMechanicFlags.None,
                };
            }
        }

        return flags;
    }
}
