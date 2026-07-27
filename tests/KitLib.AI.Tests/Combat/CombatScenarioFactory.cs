using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Tests.Combat;

/// <summary>
/// Builds <see cref="CombatState"/> fixtures without launching the game.
/// Card stats use sandbox catalog (verified against official ModelDb) for headless sim.
/// </summary>
public static class CombatScenarioFactory {
    public static void EnsureMechanicIndexes() => OfficialCombatSimBootstrap.EnsureReady();

    public static CombatState BaseIronclad(int hp = 80, int maxHp = 80, int energy = 3, int turn = 1) =>
        new(
            hp,
            maxHp,
            0,
            energy,
            energy,
            0,
            turn,
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            [],
            false,
            0,
            0,
            0,
            0);

    public static CombatHandCard HandCard(int index, string cardId) {
        CardMechanicProfile profile;
        if (SandboxCardCatalog.TryGet(cardId, out profile)) {
            // Offline catalog aligned with official Ironclad starters.
        }
        else if (OfficialCombatSimBootstrap.IsReady
            && CardMechanicIndex.TryGet(cardId, out profile)) {
            // In-game index when Godot runtime is available.
        }
        else {
            throw new InvalidOperationException(
                $"Card '{cardId}' not found in sandbox catalog or CardMechanicIndex.");
        }

        int damage = profile.Damage ?? 0;
        int block = profile.Block ?? 0;
        var tags = profile.DerivedTags;
        var isAoe = tags.Contains(AiTag.Aoe);
        var targetType = damage > 0 || profile.AppliedVulnerable > 0
            ? "AnyEnemy"
            : "";

        return new CombatHandCard(
            index,
            cardId,
            cardId,
            profile.CanonicalCost,
            damage,
            block,
            profile.CardType,
            targetType,
            true,
            profile,
            isAoe,
            false,
            false,
            Math.Max(1, profile.AttackHitCount));
    }

    public static List<CombatHandCard> Hand(params string[] cardIds) {
        var hand = new List<CombatHandCard>(cardIds.Length);
        for (int i = 0; i < cardIds.Length; i++)
            hand.Add(HandCard(i, cardIds[i]));
        return hand;
    }

    /// <summary>
    /// SHRINKER_BEETLE WEAK turn 1: shrink → chomp → stomp loop.
    /// Move IDs and damage from official <c>ShrinkerBeetle.cs</c>.
    /// </summary>
    public static CombatEnemy ShrinkerBeetleWeakOpening() =>
        ShrinkerBeetle(hp: 40, chompDamage: 8, stompDamage: 14);

    /// <summary>Baseline weak encounter without deadly ascension (7/13 chomp/stomp).</summary>
    public static CombatEnemy ShrinkerBeetleVanillaWeak() =>
        ShrinkerBeetle(hp: 40, chompDamage: 7, stompDamage: 13);

    public static CombatEnemy ShrinkerBeetle(int hp, int chompDamage, int stompDamage) {
        var steps = new CombatIntentStep[] {
            new("SHRINKER_MOVE", 0, false, ["debuff"], 0),
            new("CHOMP_MOVE", chompDamage, false, ["attack"], 0),
            new("STOMP_MOVE", stompDamage, false, ["attack"], 0),
        };

        return new CombatEnemy(
            0,
            hp,
            hp,
            0,
            true,
            false,
            0,
            0,
            0,
            steps,
            EnemyMechanicFlags.HasDebuffIntent,
            0,
            -1,
            "SHRINKER_BEETLE",
            "SHRINKER_MOVE");
    }

    public static CombatState ShrinkerBeetleTurn1(IReadOnlyList<string> handCardIds) {
        CardPileEffectResolver.SeedNoPileEffects(handCardIds);
        return BaseIronclad()
            .WithHand(Hand(handCardIds.ToArray()))
            .WithEnemies([ShrinkerBeetleWeakOpening()]);
    }

    /// <summary>Full starter deck split + optional relics for multi-turn sandboxes.</summary>
    public static CombatState ShrinkerBeetleIroncladFight(
        IReadOnlyList<string> openingHandIds,
        IReadOnlyList<string>? relicIds = null,
        bool deadlyEnemies = true,
        int beetleHp = 40) {
        CardPileEffectResolver.SeedNoPileEffects(IroncladStarterDeck.FullDeckIds);
        var (hand, draw) = IroncladStarterDeck.SplitOpening(openingHandIds);
        var beetle = deadlyEnemies
            ? ShrinkerBeetle(beetleHp, 8, 14)
            : ShrinkerBeetleVanillaWeak() with { CurrentHp = beetleHp, MaxHp = beetleHp };
        var relics = relicIds ?? [];
        int startBlock = RelicCombatRules.StartOfCombatBlock(relics);

        return BaseIronclad()
            .WithPlayerVitals(80, 80, startBlock, 3)
            .WithHand(hand)
            .WithPiles(draw, [], [])
            .WithEnemies([beetle])
            with { RelicIds = relics };
    }
}
