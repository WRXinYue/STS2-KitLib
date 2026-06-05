using System;
using System.Text.Json.Nodes;
using DevMode.AI.Combat;
using DevMode.AI.Core.Schema;

namespace DevMode.AI.AutoPlay.Scoring;

/// <summary>
/// Combat potion heuristics (id substring match on potion model id):
/// heal when HP &lt; 35%; AOE explosive when 2+ enemies and incoming ≥ 15;
/// block/smoke when incoming ≥ 20.
/// </summary>
public static class PotionScorer {
    public static GameAction? TryUsePotion(JsonObject snapshot) {
        var potions = snapshot["potions"]?.AsArray();
        if (potions == null || potions.Count == 0) return null;

        var hpRatio = IntentCalculator.HpRatio(snapshot);
        var incoming = IntentCalculator.TotalIncomingDamage(snapshot);
        var enemies = snapshot["combat"]?.AsObject()?["enemies"]?.AsArray()?.Count ?? 1;
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var netDamage = IntentCalculator.NetDamageAfterBlock(snapshot);
        var needsBlock = IntentCalculator.NeedsBlock(snapshot);

        for (int i = 0; i < potions.Count; i++) {
            if (potions[i] is not JsonObject potion) continue;
            var id = potion["id"]?.GetValue<string>() ?? "";
            var upper = id.ToUpperInvariant();

            if (hpRatio < 0.35f && MatchesHeal(upper))
                return Use(i, $"Low HP ({hp}) heal potion");

            if (netDamage >= hp - 1 && MatchesBlock(upper))
                return Use(i, "Block potion — lethal incoming");

            if (enemies >= 2 && incoming >= 15 && upper.Contains("EXPLOSIVE"))
                return Use(i, "AOE damage potion");

            if (needsBlock && incoming >= 20 && MatchesBlock(upper))
                return Use(i, "Block potion vs heavy hit");

            if (needsBlock && hpRatio < 0.5f && incoming >= 12 && MatchesBlock(upper))
                return Use(i, "Block potion — moderate threat");
        }

        return null;
    }

    static bool MatchesHeal(string upper) =>
        upper.Contains("BLOOD") || upper.Contains("HEAL") || upper.Contains("FRUIT")
        || upper.Contains("REGEN") || upper.Contains("FAIRY");

    static bool MatchesBlock(string upper) =>
        upper.Contains("BLOCK") || upper.Contains("SMOKE") || upper.Contains("SOLUTION")
        || upper.Contains("ARMOR") || upper.Contains("SHIELD");

    static GameAction Use(int index, string reason) => new() {
        Type = ActionType.UsePotion,
        TargetIndex = index,
        Reason = reason,
    };
}
