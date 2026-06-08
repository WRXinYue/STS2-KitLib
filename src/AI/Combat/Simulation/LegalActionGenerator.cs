using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Combat;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class LegalActionGenerator {
    public const int MaxMcBranches = 3;

    public static IEnumerable<SimCombatAction> Generate(
        CombatState state,
        JsonObject? rootSnapshot = null) {
        bool anyPlay = false;

        foreach (var action in GenerateRaw(state)) {
            if (action.Kind == SimActionKind.PlayCard)
                anyPlay = true;
            if (!CombatActionHeuristic.ShouldPrune(state, action, rootSnapshot))
                yield return action;
        }

        if (anyPlay || state.Energy >= 0)
            yield return new SimCombatAction(SimActionKind.EndTurn);
    }

    public static IEnumerable<SimCombatAction> GenerateOrdered(
        CombatState state,
        int maxActions = int.MaxValue,
        JsonObject? rootSnapshot = null) =>
        Generate(state, rootSnapshot)
            .OrderByDescending(a => RankActionByLineOutcome(state, a, rootSnapshot))
            .Take(maxActions);

    static int RankActionByLineOutcome(
        CombatState state,
        SimCombatAction action,
        JsonObject? rootSnapshot) =>
        CombatSetupEvaluator.RankPlayAction(state, action, rootSnapshot);

    static IEnumerable<SimCombatAction> GenerateRaw(CombatState state) {
        for (int i = 0; i < state.Hand.Count; i++) {
            var card = state.Hand[i];
            if (!CombatCardCost.CanAfford(card, state)) continue;

            if (card.IsAoe || CombatTargetTypes.IsAllEnemies(card.TargetType)) {
                yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
                continue;
            }

            if (CombatTargetTypes.NeedsEnemyTarget(card)) {
                foreach (var enemyIndex in OrderedAttackTargets(state))
                    yield return new SimCombatAction(SimActionKind.PlayCard, i, enemyIndex);
                continue;
            }

            yield return new SimCombatAction(SimActionKind.PlayCard, i, -1);
        }

        foreach (var action in GeneratePotionActions(state))
            yield return action;
    }

    static IEnumerable<SimCombatAction> GeneratePotionActions(CombatState state) {
        if (state.PotionUsedThisTurn || state.Potions.Count == 0)
            yield break;

        foreach (var potion in state.Potions) {
            if (!PotionCombatEffectData.TryGetProfile(potion.Id, out var profile) || !profile.Simulatable)
                continue;

            if (profile.Random != null) {
                int samples = Math.Min(profile.Random.McSamples, MaxMcBranches);
                for (int branch = 1; branch <= samples; branch++)
                    yield return new SimCombatAction(SimActionKind.UsePotion, PotionSlot: potion.Slot, McBranch: branch);
                continue;
            }

            if (PotionSimulator.NeedsEnemyTarget(profile.TargetType)) {
                foreach (var enemyIndex in OrderedAttackTargets(state))
                    yield return new SimCombatAction(SimActionKind.UsePotion, PotionSlot: potion.Slot, EnemyIndex: enemyIndex);
                continue;
            }

            yield return new SimCombatAction(SimActionKind.UsePotion, PotionSlot: potion.Slot);
        }
    }

    static IEnumerable<int> OrderedAttackTargets(CombatState state) =>
        CombatSetupEvaluator.OrderEnemiesForGreedyAttacks(state)
            .Take(4)
            .Select(e => e.Index);
}
