using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Combat;

namespace DevMode.AI.Combat.Simulation;

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
    IReadOnlyList<CombatEnemy> Enemies) {

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
        var modifiers = ParseModifiers(combat?["playerPowers"]?.AsArray());
        var enemies = ParseEnemies(combat?["enemies"]?.AsArray());

        return new CombatState(
            hp, maxHp, block, energy, maxEnergy, statusDamage, turnNumber,
            hand, draw, discard, exhaust, modifiers, enemies);
    }

    public CombatState WithPlayer(int hp, int block, int energy) =>
        this with { PlayerHp = hp, PlayerBlock = block, Energy = energy };

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

    public JsonArray ToHandJson() {
        var arr = new JsonArray();
        foreach (var card in Hand)
            arr.Add(card.ToJson());
        return arr;
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
            var id = (power["modelId"]?.GetValue<string>()
                ?? power["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
            if (id.Contains("SHRINK", StringComparison.Ordinal))
                mods.Add(PlayerCombatModifier.Shrink());
            else if (id.Contains("SMOG", StringComparison.Ordinal))
                mods.Add(PlayerCombatModifier.Smoggy());
            else if (id.Contains("TANGLE", StringComparison.Ordinal))
                mods.Add(PlayerCombatModifier.Tangled());
            else if (id.Contains("CHAIN", StringComparison.Ordinal) && id.Contains("BIND", StringComparison.Ordinal))
                mods.Add(PlayerCombatModifier.ChainsOfBinding());
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
            var isAoe = tags.Contains(AiTag.Aoe) || targetType is "AllEnemy";

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
                card["hasExhaust"]?.GetValue<bool>() == true));
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
                e["nextMoveId"]?.GetValue<string>() ?? ""));
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

        return steps.Take(3).ToArray();
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
