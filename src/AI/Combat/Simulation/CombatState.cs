using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;
using KitLib.AI.Combat;

namespace KitLib.AI.Combat.Simulation;

public sealed record CombatState(
    int PlayerHp,
    int PlayerMaxHp,
    int PlayerBlock,
    int Energy,
    int MaxEnergy,
    int StatusDamage,
    int TurnNumber,
    IReadOnlyList<CombatHandCard> Hand,
    IReadOnlyList<CombatPileCard> DrawPile,
    IReadOnlyList<CombatPileCard> DiscardPile,
    IReadOnlyList<CombatPileCard> ExhaustPile,
    IReadOnlyList<PlayerCombatModifier> Modifiers,
    IReadOnlyList<CombatEnemy> Enemies,
    IReadOnlyList<string> RelicIds,
    IReadOnlyList<CombatPotionSlot> Potions,
    bool PotionUsedThisTurn = false,
    uint ShuffleRngSeed = 0,
    int ShuffleRngCounter = 0,
    uint EnergyCostRngSeed = 0,
    int EnergyCostRngCounter = 0,
    NextPlayCostWaive NextPlayCostWaive = NextPlayCostWaive.None,
    int AttacksPlayedThisTurn = 0,
    int UnblockedDamageTakenThisTurn = 0,
    int OrbCount = 0,
    PlayerBuffState Buffs = null!) {
    public PlayerBuffState Buffs { get; init; } = Buffs ?? PlayerBuffState.Empty;

    public int AliveEnemyCount => Enemies.Count(e => e.IsAlive);

    public static CombatState FromSnapshot(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var block = combat?["playerBlock"]?.GetValue<int>() ?? 0;
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var maxEnergy = combat?["maxEnergy"]?.GetValue<int>() ?? 3;
        var statusDamage = EstimateStatusDamage(combat?["playerPowers"]?.AsArray());
        var turnNumber = combat?["turnNumber"]?.GetValue<int>() ?? 1;

        var hand = ParseHand(combat?["hand"]?.AsArray());
        var draw = ParsePile(combat?["drawPile"]?.AsArray());
        var discard = ParsePile(combat?["discardPile"]?.AsArray());
        var exhaust = ParsePile(combat?["exhaustPile"]?.AsArray());
        var playerPowers = combat?["playerPowers"]?.AsArray();
        var modifiers = ParseModifiers(playerPowers);
        var buffs = PlayerPowerSimulator.ParseBuffsFromPowers(playerPowers);
        var relicIds = RelicCombatRules.ParseRelicIds(snapshot);
        modifiers = RelicCombatRules.MergeModifiers(relicIds, modifiers);
        var enemies = ParseEnemies(combat?["enemies"]?.AsArray());
        var (shuffleSeed, shuffleCounter) = ParseShuffleRng(combat?["rngShuffle"]?.AsObject());
        var (energyCostSeed, energyCostCounter) = ParseShuffleRng(combat?["rngEnergyCosts"]?.AsObject());
        var potions = ParsePotions(snapshot["potions"]?.AsArray());
        var orbCount = combat?["orbCount"]?.GetValue<int>() ?? 0;

        if (turnNumber <= 1 && block == 0) {
            var relicBlock = RelicCombatRules.StartOfCombatBlock(relicIds);
            if (relicBlock > 0)
                block = relicBlock;
        }

        return new CombatState(
            hp, maxHp, block, energy, maxEnergy, statusDamage, turnNumber,
            hand, draw, discard, exhaust, modifiers, enemies, relicIds, potions,
            false,
            shuffleSeed, shuffleCounter, energyCostSeed, energyCostCounter,
            OrbCount: orbCount,
            Buffs: buffs);
    }

    public CombatState WithPlayer(int hp, int block, int energy) =>
        this with { PlayerHp = hp, PlayerBlock = block, Energy = energy };

    public CombatState WithPlayerVitals(int hp, int maxHp, int block, int energy) =>
        this with { PlayerHp = hp, PlayerMaxHp = maxHp, PlayerBlock = block, Energy = energy };

    public CombatState WithHand(IReadOnlyList<CombatHandCard> hand) =>
        this with { Hand = hand };

    public CombatState WithEnemies(IReadOnlyList<CombatEnemy> enemies) =>
        this with { Enemies = enemies };

    public CombatState WithPiles(
        IReadOnlyList<CombatPileCard> draw,
        IReadOnlyList<CombatPileCard> discard,
        IReadOnlyList<CombatPileCard> exhaust) =>
        this with { DrawPile = draw, DiscardPile = discard, ExhaustPile = exhaust };

    public CombatState WithModifiers(IReadOnlyList<PlayerCombatModifier> modifiers) =>
        this with { Modifiers = modifiers };

    public CombatState WithTurn(int turnNumber, int energy, int maxEnergy) =>
        this with { TurnNumber = turnNumber, Energy = energy, MaxEnergy = maxEnergy };

    public CombatState WithShuffleRng(uint seed, int counter) =>
        this with { ShuffleRngSeed = seed, ShuffleRngCounter = counter };

    public CombatState WithEnergyCostRng(uint seed, int counter) =>
        this with { EnergyCostRngSeed = seed, EnergyCostRngCounter = counter };

    public CombatState WithPotions(IReadOnlyList<CombatPotionSlot> potions, bool potionUsedThisTurn) =>
        this with { Potions = potions, PotionUsedThisTurn = potionUsedThisTurn };

    public CombatState WithNextPlayCostWaive(NextPlayCostWaive waive) =>
        this with { NextPlayCostWaive = waive };

    public JsonArray ToHandJson() {
        var arr = new JsonArray();
        foreach (var card in Hand)
            arr.Add(card.ToJson());
        return arr;
    }

    static List<CombatPotionSlot> ParsePotions(JsonArray? arr) {
        var potions = new List<CombatPotionSlot>();
        if (arr == null) return potions;

        foreach (var node in arr) {
            if (node is not JsonObject potion) continue;
            var id = potion["id"]?.GetValue<string>() ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;
            var slot = potion["slot"]?.GetValue<int>() ?? potions.Count;
            potions.Add(new CombatPotionSlot(slot, id));
        }

        return potions;
    }

    static List<CombatPileCard> ParsePile(JsonArray? arr) {
        var pile = new List<CombatPileCard>();
        if (arr == null) return pile;

        foreach (var node in arr) {
            if (node is JsonObject card)
                pile.Add(CombatPileCard.FromJson(card));
        }

        return pile;
    }

    static List<PlayerCombatModifier> ParseModifiers(JsonArray? powers) {
        var mods = new List<PlayerCombatModifier>();
        if (powers == null) return mods;

        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var mapped = PlayerCombatModifierRegistry.FromSnapshot(power);
            if (mapped != null)
                mods.Add(mapped);
        }

        return mods;
    }

    static List<CombatHandCard> ParseHand(JsonArray? handArr) {
        var hand = new List<CombatHandCard>();
        if (handArr == null) return hand;

        for (int i = 0; i < handArr.Count; i++) {
            if (handArr[i] is not JsonObject card) continue;
            var id = card["id"]?.GetValue<string>() ?? "";
            var profile = CombatCardStats.ResolveProfile(card);
            var tags = CardCatalog.ResolveTags(
                id, card["cardType"]?.GetValue<string>(), card["keywords"]?.AsArray());
            var targetType = card["targetType"]?.GetValue<string>() ?? "";
            var isAoe = tags.Contains(AiTag.Aoe) || CombatTargetTypes.IsAllEnemies(targetType);

            hand.Add(new CombatHandCard(
                i,
                id,
                card["name"]?.GetValue<string>() ?? id,
                card["cost"]?.GetValue<int>() ?? 1,
                CombatCardStats.ResolveDamage(card),
                CombatCardStats.ResolveBlock(card),
                card["cardType"]?.GetValue<string>() ?? "",
                targetType,
                card["canPlay"]?.GetValue<bool>() != false,
                profile,
                isAoe,
                card["hasRetain"]?.GetValue<bool>() == true,
                card["hasExhaust"]?.GetValue<bool>() == true,
                CombatCardStats.ResolveHitCount(card)));
        }

        return hand;
    }

    static List<CombatEnemy> ParseEnemies(JsonArray? enemiesArr) {
        var enemies = new List<CombatEnemy>();
        if (enemiesArr == null) return enemies;

        for (int i = 0; i < enemiesArr.Count; i++) {
            if (enemiesArr[i] is not JsonObject e) continue;
            var steps = ParseIntentSteps(e["intentSteps"]?.AsArray());

            var flags = EnemyMechanicResolver.ResolveFlags(e);
            var nonDamage = EnemyMechanicResolver.ResolveNonDamageThreat(e);

            enemies.Add(new CombatEnemy(
                e["index"]?.GetValue<int>() ?? i,
                e["currentHp"]?.GetValue<int>() ?? 0,
                e["maxHp"]?.GetValue<int>() ?? 1,
                e["block"]?.GetValue<int>() ?? 0,
                e["isAlive"]?.GetValue<bool>() != false,
                EnemyTargetPriority.IsMinion(e),
                e["intentDamage"]?.GetValue<int>() ?? 0,
                CombatPowerReader.GetVulnerable(e),
                CombatPowerReader.GetWeak(e),
                steps,
                flags,
                nonDamage,
                e["summonerIndex"]?.GetValue<int>() ?? -1,
                e["monsterId"]?.GetValue<string>() ?? "",
                e["nextMoveId"]?.GetValue<string>() ?? "",
                ActOrder: i,
                Strength: CombatPowerReader.GetStrength(e)));
        }

        return enemies;
    }

    static CombatIntentStep[] ParseIntentSteps(JsonArray? arr) {
        if (arr == null || arr.Count == 0)
            return [];

        var steps = new List<CombatIntentStep>();
        foreach (var node in arr) {
            if (node is not JsonObject step) continue;
            var intentTypes = ParseIntentTypeStrings(step["intentTypes"]?.AsArray());
            steps.Add(new CombatIntentStep(
                step["moveId"]?.GetValue<string>() ?? "",
                step["intentDamage"]?.GetValue<int>() ?? 0,
                step["isUncertain"]?.GetValue<bool>() == true,
                intentTypes,
                step["nonDamageThreat"]?.GetValue<int>() ?? 0));
        }

        return steps.Take(ThreatModel.LineFutureHorizonTurns + 2).ToArray();
    }

    static string[] ParseIntentTypeStrings(JsonArray? arr) {
        if (arr == null || arr.Count == 0)
            return [];

        var types = new List<string>();
        foreach (var node in arr) {
            if (node?.GetValue<string>() is { } tag && !string.IsNullOrWhiteSpace(tag))
                types.Add(tag);
        }

        return types.ToArray();
    }

    static (uint Seed, int Counter) ParseShuffleRng(JsonObject? rng) {
        if (rng == null) return (0, 0);
        var seed = rng["seed"]?.GetValue<uint>() ?? 0;
        var counter = rng["counter"]?.GetValue<int>() ?? 0;
        return (seed, counter);
    }

    static int EstimateStatusDamage(JsonArray? powers) {
        if (powers == null) return 0;
        int total = 0;
        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            var amount = power["amount"]?.GetValue<int>() ?? 0;
            if (amount <= 0) continue;
            if (id.Contains("BURN") || id.Contains("POISON") || id.Contains("INFEST") || id.Contains("DOOM"))
                total += amount;
        }
        return total;
    }
}
