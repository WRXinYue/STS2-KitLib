using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;

namespace KitLib.AI.Knowledge;

/// <summary>Merges runtime intent scan with static handler effects from monster-move-effects.json.</summary>
public static class MoveEffectIndex {
    public static IReadOnlyList<MonsterMoveEffect> GetEffects(string? monsterId, string? moveId) {
        var staticEffects = MonsterMoveEffectData.GetEffects(monsterId, moveId);
        if (staticEffects.Count > 0)
            return staticEffects;

        if (!MonsterMechanicIndex.TryGet(monsterId, out var profile))
            return Array.Empty<MonsterMoveEffect>();

        var move = profile.Moves.FirstOrDefault(m =>
            string.Equals(m.MoveId, moveId, StringComparison.OrdinalIgnoreCase));
        return move?.Effects ?? Array.Empty<MonsterMoveEffect>();
    }

    public static IReadOnlyList<MonsterMoveEffect> MergeWithRuntimeIntents(
        string? monsterId,
        string? moveId,
        IEnumerable<AbstractIntent>? intents) {
        var merged = new List<MonsterMoveEffect>(GetEffects(monsterId, moveId));
        if (intents == null)
            return merged;

        foreach (var intent in intents) {
            if (intent.IntentType == IntentType.Hidden)
                continue;

            if (intent is StatusIntent status
                && !merged.Any(e => e.Kind == MonsterMoveEffectKind.StatusInject)) {
                merged.Add(new MonsterMoveEffect(
                    MonsterMoveEffectKind.StatusInject,
                    Count: status.CardCount));
            }
            else if (intent.IntentType == IntentType.Summon
                && !merged.Any(e => e.Kind == MonsterMoveEffectKind.Summon)) {
                merged.Add(new MonsterMoveEffect(MonsterMoveEffectKind.Summon));
            }
            else if (intent.IntentType == IntentType.CardDebuff
                && !merged.Any(e => e.Kind == MonsterMoveEffectKind.PowerAffliction)) {
                merged.Add(new MonsterMoveEffect(MonsterMoveEffectKind.PowerAffliction));
            }
            else if (intent.IntentType is IntentType.Debuff or IntentType.DebuffStrong
                && !merged.Any(e => e.Kind is MonsterMoveEffectKind.PowerDebuff or MonsterMoveEffectKind.PowerAffliction)) {
                merged.Add(new MonsterMoveEffect(MonsterMoveEffectKind.PowerDebuff));
            }
            else if (intent is AttackIntent
                && !merged.Any(e => e.Kind == MonsterMoveEffectKind.Attack)) {
                merged.Add(new MonsterMoveEffect(MonsterMoveEffectKind.Attack));
            }
        }

        return merged;
    }
}
