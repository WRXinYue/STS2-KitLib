using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public sealed record RestEvBreakdown(
    int HealEv,
    int SmithEv,
    int HealAmount,
    int RouteValueBaseline,
    int HealRouteValue,
    int SmithRouteValue,
    string Recommended,
    int? UpgradeCardIndex,
    string? UpgradeCardId);

/// <summary>Compares rest heal vs smith using route simulation EV.</summary>
public static class RestEvScorer {
    public static RestEvBreakdown Evaluate(JsonObject snapshot) {
        var plan = DeckPlanInferer.Infer(snapshot);
        var route = NextFightRoute.ResolveFromSnapshot(snapshot);
        int baseline = RouteSimScorer.RouteValue(snapshot, plan, route);
        int maxHp = snapshot["maxHp"]?.GetValue<int>() ?? 1;
        int healAmount = RouteSimScorer.ComputeHealAmount(maxHp);

        var healed = RouteSimScorer.WithHeal(snapshot);
        int healRoute = RouteSimScorer.RouteValue(healed, plan, route);
        int healEv = healRoute - baseline;

        var upgradeTarget = RouteSimScorer.FindBestUpgradeTarget(snapshot, plan);
        int smithRoute = baseline;
        int smithEv = 0;

        var upgraded = RouteSimScorer.WithBestUpgrade(snapshot, plan);
        if (upgraded != null) {
            smithRoute = RouteSimScorer.RouteValue(upgraded, plan, route);
            smithEv = smithRoute - baseline;
        }

        string recommended = smithEv > healEv ? "Smith" : "Heal";
        if (route.Count == 0) {
            recommended = healEv >= smithEv ? "Heal" : "Smith";
            if (healEv == 0 && smithEv == 0)
                recommended = "Heal";
        }

        return new RestEvBreakdown(
            healEv,
            smithEv,
            healAmount,
            baseline,
            healRoute,
            smithRoute,
            recommended,
            upgradeTarget?.CardIndex,
            upgradeTarget?.CardId);
    }
}
