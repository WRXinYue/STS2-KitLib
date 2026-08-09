using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Rooms;

namespace KitLib.AI.Planning;

/// <summary>Builds stub combat enemies from predicted encounters for macro sim.</summary>
public static class EncounterCombatFactory {
    public static IReadOnlyList<CombatEnemy> CreateEnemies(
        EncounterModel encounter,
        RoomType roomType,
        int actIndex) {
        var monsters = encounter.AllPossibleMonsters?.ToList() ?? [];
        if (monsters.Count == 0)
            return [];

        int baseHp = DefaultHp(roomType);
        int hpScale = actIndex >= 2 ? 2 : actIndex >= 1 ? 1 : 1;
        int perMonsterHp = Math.Max(12, baseHp * hpScale / monsters.Count);

        var enemies = new List<CombatEnemy>();
        for (int i = 0; i < monsters.Count; i++) {
            var monster = monsters[i];
            var id = monster.Id.Entry ?? "";
            var profile = MonsterMechanicIndex.GetOrDefault(id);
            var steps = BuildIntentSteps(profile, 3);
            var first = steps.Length > 0 ? steps[0] : null;
            var flags = profile.Flags;
            if (profile.Flags.HasFlag(EnemyMechanicFlags.IsSecondaryEnemy))
                flags |= EnemyMechanicFlags.IsSecondaryEnemy;

            var moveId = profile.Moves.Count > 0 ? profile.Moves[0].MoveId : "";
            enemies.Add(new CombatEnemy(
                i,
                perMonsterHp,
                perMonsterHp,
                0,
                true,
                profile.Flags.HasFlag(EnemyMechanicFlags.IsSecondaryEnemy),
                first?.IntentDamage ?? 0,
                0,
                0,
                steps,
                flags,
                first?.NonDamageThreat ?? 0,
                -1,
                id,
                moveId,
                i));
        }

        return enemies;
    }

    public static IReadOnlyList<CombatEnemy> CreateEnemiesFromPreview(JsonArray? arr) {
        if (arr == null || arr.Count == 0)
            return [];

        var enemies = new List<CombatEnemy>();
        for (int i = 0; i < arr.Count; i++) {
            if (arr[i] is not JsonObject obj)
                continue;

            var monsterId = obj["monsterId"]?.GetValue<string>() ?? "";
            var profile = MonsterMechanicIndex.GetOrDefault(monsterId);
            var steps = BuildIntentSteps(profile, 3);
            var first = steps.Length > 0 ? steps[0] : null;
            int hp = obj["hp"]?.GetValue<int>() ?? DefaultHp(RoomType.Monster);

            enemies.Add(new CombatEnemy(
                obj["index"]?.GetValue<int>() ?? i,
                hp,
                hp,
                0,
                true,
                obj["isMinion"]?.GetValue<bool>() == true,
                obj["intentDamage"]?.GetValue<int>() ?? first?.IntentDamage ?? 0,
                0,
                0,
                steps,
                profile.Flags,
                obj["nonDamageThreat"]?.GetValue<int>() ?? first?.NonDamageThreat ?? 0,
                -1,
                monsterId,
                first?.MoveId ?? "",
                i));
        }

        return enemies;
    }

    static int DefaultHp(RoomType roomType) => roomType switch {
        RoomType.Elite => 90,
        RoomType.Boss => 250,
        _ => 45,
    };

    static CombatIntentStep[] BuildIntentSteps(MonsterMechanicProfile profile, int count) {
        if (profile.Moves.Count == 0)
            return [];

        var move = profile.Moves[0];
        var steps = new CombatIntentStep[count];
        var effects = MoveEffectIndex.GetEffects(profile.MonsterId, move.MoveId);
        int damage = MoveEffectPressure.AttackDamageFromEffects(effects);

        int nonDamage = MoveEffectPressure.FromMove(profile.MonsterId, move.MoveId);
        for (int i = 0; i < count; i++) {
            steps[i] = new CombatIntentStep(
                move.MoveId,
                damage,
                false,
                move.IntentTypes.Select(t => t.ToString()).ToArray(),
                nonDamage);
        }

        return steps;
    }
}
