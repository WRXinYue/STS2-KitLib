using System;
using System.Text.Json.Nodes;
using KitLib.AI.Planning;

namespace KitLib.AI.AutoPlay.Scoring;

/// <summary>Fallback card reward tweaks when next-fight sim lacks card coverage.</summary>
internal static class EarlyCardRewardAdjustments {
    const int EarlyFloorMax = 12;

    public static int Score(JsonObject card, JsonObject? snapshot) {
        if (snapshot == null) return 0;

        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        if (floor > EarlyFloorMax) return 0;

        var id = (card["id"]?.GetValue<string>() ?? "").ToUpperInvariant();
        var score = 0;

        if (id == "TRUE_GRIT")
            score -= 8;
        if (id == "BURNING_PACT")
            score -= 10;

        if (id == "IRON_WAVE" && floor <= 10)
            score += 4;

        return score;
    }
}
