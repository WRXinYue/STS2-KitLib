using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.MonsterMoves.Intents;
using MegaCrit.Sts2.Core.MonsterMoves.MonsterMoveStateMachine;

namespace DevMode.AI.Knowledge;

/// <summary>Offline scan of monster move state machines (intent types per move).</summary>
internal static class MonsterMoveScanner {
    public static IReadOnlyList<MonsterMoveProfile> ScanMoves(MonsterModel canonical) {
        try {
            var monster = canonical.ToMutable();
            monster.SetUpForCombat();
            return ScanMachine(monster.MoveStateMachine);
        }
        catch {
            return Array.Empty<MonsterMoveProfile>();
        }
    }

    static IReadOnlyList<MonsterMoveProfile> ScanMachine(MonsterMoveStateMachine? machine) {
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

            profiles.Add(new MonsterMoveProfile(moveId, intentTypes));
        }

        return profiles;
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
                    _ => EnemyMechanicFlags.None,
                };
            }
        }

        return flags;
    }
}
