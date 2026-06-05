using System;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;

namespace DevMode.AI.Combat;

public static class LethalChecker {
    public static bool CanLethal(JsonObject snapshot, out int targetIndex) {
        targetIndex = -1;
        var combat = snapshot["combat"]?.AsObject();
        var hand = combat?["hand"]?.AsArray();
        var energy = combat?["currentEnergy"]?.GetValue<int>() ?? 0;
        var enemies = combat?["enemies"]?.AsArray();
        if (hand == null || enemies == null) return false;

        for (int t = 0; t < enemies.Count; t++) {
            if (enemies[t] is not JsonObject enemy) continue;
            if (enemy["isAlive"]?.GetValue<bool>() == false) continue;

            var hp = enemy["currentHp"]?.GetValue<int>() ?? 0;
            var block = enemy["block"]?.GetValue<int>() ?? 0;
            var damageNeeded = hp + block;
            if (damageNeeded <= 0) continue;

            if (EstimateMaxDamage(hand, energy, t) >= damageNeeded) {
                targetIndex = t;
                return true;
            }
        }

        return false;
    }

    public static int EstimateMaxDamage(JsonArray hand, int energy, int targetIndex) {
        var attacks = hand
            .Select((node, i) => (Index: i, Card: node?.AsObject()))
            .Where(x => x.Card != null && IsAttack(x.Card!))
            .Select(x => (
                x.Index,
                Cost: x.Card!["cost"]?.GetValue<int>() ?? 99,
                Damage: x.Card!["damage"]?.GetValue<int>()
                    ?? GuessDamage(x.Card!)))
            .OrderByDescending(x => x.Damage)
            .ToList();

        var remaining = energy;
        var total = 0;
        foreach (var atk in attacks) {
            if (atk.Cost > remaining) continue;
            remaining -= atk.Cost;
            total += atk.Damage;
        }

        return total;
    }

    static bool IsAttack(JsonObject card) {
        var type = card["cardType"]?.GetValue<string>() ?? "";
        if (type.Contains("Attack", StringComparison.OrdinalIgnoreCase)) return true;
        return (card["damage"]?.GetValue<int>() ?? 0) > 0;
    }

    static int GuessDamage(JsonObject card) {
        var tags = CardCatalog.ResolveTags(
            card["id"]?.GetValue<string>(),
            card["cardType"]?.GetValue<string>(),
            card["keywords"]?.AsArray());
        if (tags.Contains(AiTag.Attack))
            return 6 + (card["cost"]?.GetValue<int>() ?? 0) * 3;
        return 0;
    }
}
