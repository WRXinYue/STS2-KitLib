using System;
using System.Collections.Generic;
using KitLib.AI.Knowledge;
using MegaCrit.Sts2.Core.Random;

namespace KitLib.AI.Combat.Simulation;

internal static class PotionRandomPools {
    sealed record PoolCard(string Id, string Name, int Cost, int Damage, int Block, string CardType);

    static readonly Dictionary<string, PoolCard[]> Pools = new(StringComparer.OrdinalIgnoreCase) {
        ["COLORLESS"] = [
            new("STRIKE", "Strike", 1, 6, 0, "Attack"),
            new("DEFEND", "Defend", 1, 0, 5, "Skill"),
            new("DASH", "Dash", 2, 10, 10, "Attack"),
            new("BLIND", "Blind", 0, 0, 2, "Skill"),
            new("DEEP_BREATH", "Deep Breath", 0, 0, 0, "Skill"),
            new("IMPATIENCE", "Impatience", 0, 0, 0, "Skill"),
        ],
        ["ATTACK"] = [
            new("STRIKE", "Strike", 1, 6, 0, "Attack"),
            new("BASH", "Bash", 2, 8, 0, "Attack"),
            new("CLEAVE", "Cleave", 1, 8, 0, "Attack"),
            new("POMMEL_STRIKE", "Pommel Strike", 1, 9, 0, "Attack"),
            new("TWIN_STRIKE", "Twin Strike", 1, 5, 0, "Attack"),
        ],
        ["SKILL"] = [
            new("DEFEND", "Defend", 1, 0, 5, "Skill"),
            new("SHRUG_IT_OFF", "Shrug It Off", 1, 0, 8, "Skill"),
            new("ARMAMENTS", "Armaments", 1, 0, 0, "Skill"),
            new("TRUE_GRIT", "True Grit", 1, 0, 7, "Skill"),
            new("FEINT", "Feint", 0, 0, 0, "Skill"),
        ],
        ["POWER"] = [
            new("INFLAME", "Inflame", 1, 0, 0, "Power"),
            new("METALLICIZE", "Metallicize", 1, 0, 0, "Power"),
            new("BURNING_PACT", "Burning Pact", 1, 0, 0, "Skill"),
            new("EVOLVE", "Evolve", 1, 0, 0, "Power"),
            new("FEEL_NO_PAIN", "Feel No Pain", 1, 0, 0, "Power"),
        ],
    };

    public static CombatHandCard SampleCard(
        string poolName,
        CombatState state,
        int potionSlot,
        int mcBranch) {
        if (!Pools.TryGetValue(poolName, out var pool) || pool.Length == 0)
            pool = Pools["COLORLESS"];

        uint seed = state.ShuffleRngSeed == 0
            ? (uint)(potionSlot + 1) * 9973u + (uint)mcBranch * 7919u
            : state.ShuffleRngSeed;
        int counter = state.ShuffleRngCounter + potionSlot * 17 + mcBranch * 31;
        var rng = new Rng(seed, counter);
        var pick = pool[rng.NextInt(pool.Length)];

        var profile = CardMechanicIndex.InferFromSnapshot(new System.Text.Json.Nodes.JsonObject {
            ["id"] = pick.Id,
            ["cardType"] = pick.CardType,
            ["damage"] = pick.Damage > 0 ? pick.Damage : null,
            ["block"] = pick.Block > 0 ? pick.Block : null,
        });

        return new CombatHandCard(
            state.Hand.Count,
            pick.Id,
            pick.Name,
            0,
            pick.Damage,
            pick.Block,
            pick.CardType,
            pick.Damage > 0 ? "AnyEnemy" : "",
            CanPlay: true,
            profile,
            IsAoe: false);
    }
}
