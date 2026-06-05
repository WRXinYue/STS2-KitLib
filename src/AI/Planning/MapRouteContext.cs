using System.Text.Json.Nodes;

namespace DevMode.AI.Planning;

public sealed record MapRouteContext(
    float HpRatio,
    int Gold,
    int ActIndex,
    int TotalFloor,
    int Ascension,
    DeckPlan Plan,
    DeckMetrics Metrics,
    bool WantsShopRemoval
) {
    public static MapRouteContext FromSnapshot(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var metrics = DeckEvaluator.Evaluate(snapshot, plan);
        var hp = snapshot["currentHp"]?.GetValue<int>() ?? 0;
        var maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        var gold = snapshot["gold"]?.GetValue<int>() ?? 0;

        return new MapRouteContext(
            maxHp > 0 ? (float)hp / maxHp : 1f,
            gold,
            snapshot["actIndex"]?.GetValue<int>() ?? 0,
            snapshot["totalFloor"]?.GetValue<int>() ?? 0,
            snapshot["ascensionLevel"]?.GetValue<int>() ?? 0,
            plan,
            metrics,
            metrics.RemovalUplift >= DeckEvaluator.MinRemovalUplift && gold >= 75);
    }
}
