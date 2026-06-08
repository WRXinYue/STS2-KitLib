using System.Collections.Generic;
using System.Text.Json.Nodes;
namespace KitLib.AI.Planning;

public static class DeckPlanContributorHub {
    static readonly List<IDeckPlanContributor> Contributors = [];

    public static void Register(IDeckPlanContributor contributor) {
        Contributors.Add(contributor);
        MainFile.Logger.Info($"[AiPlan] DeckPlan contributor registered type={contributor.GetType().Name}.");
    }

    public static void ApplyContributors(DeckPlan.Builder builder, JsonObject snapshot) {
        var characterId = snapshot["characterId"]?.GetValue<string>();
        foreach (var contributor in Contributors) {
            if (!contributor.AppliesTo(characterId)) continue;
            try {
                contributor.AdjustPlan(builder, snapshot);
            }
            catch (System.Exception ex) {
                MainFile.Logger.Warn($"[AiPlan] Contributor {contributor.GetType().Name} failed: {ex.Message}");
            }
        }
    }
}
