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
    int StatusDamage,
    IReadOnlyList<CombatHandCard> Hand,
    IReadOnlyList<CombatEnemy> Enemies) {

    public int AliveEnemyCount => Enemies.Count(e => e.IsAlive);

    public static CombatState FromSnapshot(JsonObject snapshot) {
        var combat = snapshot["combat"]?.AsObject();
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var block = combat?["playerBlock"]?.GetValue<int>() ?? 0;
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var statusDamage = EstimateStatusDamage(combat?["playerPowers"]?.AsArray());

        var hand = ParseHand(combat?["hand"]?.AsArray());
        var enemies = ParseEnemies(combat?["enemies"]?.AsArray());

        return new CombatState(hp, maxHp, block, energy, statusDamage, hand, enemies);
    }

    public CombatState WithPlayer(int hp, int block, int energy) =>
        this with { PlayerHp = hp, PlayerBlock = block, Energy = energy };

    public CombatState WithHand(IReadOnlyList<CombatHandCard> hand) =>
        this with { Hand = hand };

    public CombatState WithEnemies(IReadOnlyList<CombatEnemy> enemies) =>
        this with { Enemies = enemies };

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
                isAoe));
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
                e["summonerIndex"]?.GetValue<int>() ?? -1));
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
