using System.Text.Json.Nodes;

namespace KitLib.AI.Planning;

public interface IDeckPlanContributor {
    bool AppliesTo(string? characterId);
    void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot);
}
