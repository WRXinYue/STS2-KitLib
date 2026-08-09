using System;
using System.Text.Json.Nodes;

namespace KitLib.AI.Combat;

internal static class CombatPowerReader {
    public static int GetVulnerable(JsonObject? enemy) => GetPowerAmount(enemy, "VULNERABLE");

    public static int GetWeak(JsonObject? enemy) => GetPowerAmount(enemy, "WEAK");

    public static int GetStrength(JsonObject? creature) => GetPowerAmount(creature, "STRENGTH");

    static int GetPowerAmount(JsonObject? creature, string token) {
        if (creature == null) return 0;
        var powers = creature["powers"]?.AsArray();
        if (powers == null) return 0;

        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = power["id"]?.GetValue<string>() ?? "";
            var modelId = power["modelId"]?.GetValue<string>() ?? "";
            if (!id.Contains(token, StringComparison.OrdinalIgnoreCase)
                && !modelId.Contains(token, StringComparison.OrdinalIgnoreCase))
                continue;

            return power["amount"]?.GetValue<int>() ?? 0;
        }

        return 0;
    }

    public static void ApplyPower(JsonObject? creature, string token, int amount) {
        if (creature == null || amount <= 0) return;

        var powers = creature["powers"]?.AsArray();
        if (powers == null) {
            powers = new JsonArray();
            creature["powers"] = powers;
        }

        foreach (var node in powers) {
            if (node is not JsonObject power) continue;
            var id = power["id"]?.GetValue<string>() ?? "";
            var modelId = power["modelId"]?.GetValue<string>() ?? "";
            if (!id.Contains(token, StringComparison.OrdinalIgnoreCase)
                && !modelId.Contains(token, StringComparison.OrdinalIgnoreCase))
                continue;

            power["amount"] = (power["amount"]?.GetValue<int>() ?? 0) + amount;
            return;
        }

        powers.Add(new JsonObject {
            ["id"] = token,
            ["modelId"] = token,
            ["amount"] = amount,
        });
    }

    /// <summary>Multiplicative factor on incoming attack damage (STS2 vulnerable ≈ +50%).</summary>
    public static float AttackDamageMultiplier(JsonObject? enemy) {
        var vuln = GetVulnerable(enemy);
        if (vuln <= 0) return 1f;
        return 1.5f;
    }
}
