using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

internal static class CombatSummonFactory {
    public static CombatEnemy? TryCreateSummonedEnemy(
        string spawnMonsterId,
        int newIndex,
        int summonerIndex,
        IReadOnlyList<CombatEnemy> existing,
        CombatState state) {
        if (existing.Any(e => e.IsAlive
                && string.Equals(e.MonsterId, spawnMonsterId, StringComparison.OrdinalIgnoreCase)))
            return null;

        return CreateSummonedEnemy(spawnMonsterId, newIndex, summonerIndex, existing, state);
    }

    public static CombatEnemy CreateSummonedEnemy(
        string spawnMonsterId,
        int newIndex,
        int summonerIndex,
        IReadOnlyList<CombatEnemy> existing,
        CombatState state) {
        var profile = MonsterMechanicIndex.GetOrDefault(spawnMonsterId);
        var flags = profile.Flags | EnemyMechanicResolver.ResolveFlags(null);
        if (profile.Flags.HasFlag(EnemyMechanicFlags.HasIllusionRevive))
            flags |= EnemyMechanicFlags.HasIllusionRevive | EnemyMechanicFlags.IsSecondaryEnemy;
        if (profile.Flags.HasFlag(EnemyMechanicFlags.IsSecondaryEnemy))
            flags |= EnemyMechanicFlags.IsSecondaryEnemy;

        var steps = BuildIntentSteps(state, profile, 3);
        var first = steps.Length > 0 ? steps[0] : null;

        int actOrder = existing.Count == 0 ? 0 : existing.Max(e => e.ActOrder) + 1;

        return new CombatEnemy(
            newIndex,
            DefaultHp(spawnMonsterId),
            DefaultHp(spawnMonsterId),
            0,
            true,
            true,
            first?.IntentDamage ?? 0,
            0,
            0,
            steps,
            flags,
            first?.NonDamageThreat ?? 0,
            summonerIndex,
            spawnMonsterId,
            first?.MoveId ?? "",
            actOrder);
    }

    public static int NextEnemyIndex(IReadOnlyList<CombatEnemy> enemies) =>
        enemies.Count == 0 ? 0 : enemies.Max(e => e.Index) + 1;

    static int DefaultHp(string monsterId) =>
        string.Equals(monsterId, "EYE_WITH_TEETH", StringComparison.OrdinalIgnoreCase) ? 6 : 20;

    static CombatIntentStep[] BuildIntentSteps(CombatState state, MonsterMechanicProfile profile, int count) {
        if (profile.Moves.Count == 0)
            return [];

        var move = profile.Moves[0];
        var steps = new CombatIntentStep[count];
        var effects = MoveEffectIndex.GetEffects(profile.MonsterId, move.MoveId);
        int damage = AttackDamageFromEffects(effects);
        int nonDamage = MoveEffectPressure.FromMove(state, profile.MonsterId, move.MoveId);

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

    static int AttackDamageFromEffects(IReadOnlyList<MonsterMoveEffect> effects) =>
        MoveEffectPressure.AttackDamageFromEffects(effects);
}
