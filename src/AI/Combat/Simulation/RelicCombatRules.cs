using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Combat.Simulation;

public static class RelicCombatRules {
    public static IReadOnlyList<string> ParseRelicIds(JsonObject? snapshot) {
        var ids = new List<string>();
        if (snapshot?["relics"] is not JsonArray arr)
            return ids;

        foreach (var node in arr) {
            if (node is not JsonObject relic) continue;
            var id = relic["id"]?.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(id))
                ids.Add(id.Trim().ToUpperInvariant());
        }

        return ids;
    }

    public static int PlannedHandDraw(CombatState state) =>
        HandDrawCount(state.RelicIds, state.TurnNumber + 1);

    public static int HandDrawCount(IReadOnlyList<string> relicIds, int combatRound, int baseDraw = CombatPileSimulator.BaseHandDrawCount) {
        int draw = baseDraw;
        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind is not (RelicCombatEffectKind.HandDrawBonus or RelicCombatEffectKind.HandDrawBonusLate))
                    continue;
                if (effect.MaxCombatRound.HasValue && combatRound > effect.MaxCombatRound.Value)
                    continue;
                draw += effect.Delta;
            }
        }

        return Math.Max(0, draw);
    }

    public static bool RetainHandOnEndTurn(IReadOnlyList<string> relicIds) {
        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind == RelicCombatEffectKind.RetainHandOnEndTurn)
                    return true;
            }
        }

        return false;
    }

    public static int DrawOnHandEmptyCount(IReadOnlyList<string> relicIds) {
        int total = 0;
        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind == RelicCombatEffectKind.DrawOnHandEmpty)
                    total += Math.Max(1, effect.Count);
            }
        }

        return total;
    }

    public static bool RetainsEnergyOnTurnStart(IReadOnlyList<string> relicIds, int combatRound) {
        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind != RelicCombatEffectKind.RetainEnergyOnTurnStart)
                    continue;
                if (effect.MinCombatRound.HasValue && combatRound < effect.MinCombatRound.Value)
                    continue;
                return true;
            }
        }

        return false;
    }

    public static int NextTurnEnergy(CombatState state) {
        int nextRound = state.TurnNumber + 1;
        if (RetainsEnergyOnTurnStart(state.RelicIds, nextRound))
            return Math.Min(999, state.Energy + state.MaxEnergy);
        return state.MaxEnergy;
    }

    public static int StartOfCombatBlock(IReadOnlyList<string> relicIds) {
        int block = 0;
        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind == RelicCombatEffectKind.StartOfCombatBlock)
                    block += effect.Block;
            }
        }

        return block;
    }

    public static List<PlayerCombatModifier> MergeModifiers(
        IReadOnlyList<string> relicIds,
        IReadOnlyList<PlayerCombatModifier> existing) {
        var merged = existing.ToList();
        var present = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in merged)
            present.Add(mod.PowerId);

        foreach (var profile in RelicCombatEffectData.GetProfiles(relicIds)) {
            foreach (var effect in profile.Effects) {
                if (effect.Kind != RelicCombatEffectKind.AppliesPower)
                    continue;
                if (string.IsNullOrWhiteSpace(effect.PowerId) || present.Contains(effect.PowerId))
                    continue;

                var mapped = PlayerCombatModifierRegistry.FromPowerId(
                    effect.PowerId,
                    Math.Max(1, effect.PowerAmount));
                if (mapped == null)
                    continue;

                merged.Add(mapped);
                present.Add(mapped.PowerId);
            }
        }

        return merged;
    }
}
