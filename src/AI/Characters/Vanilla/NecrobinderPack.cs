using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace DevMode.AI.Characters.Vanilla;

sealed class NecrobinderPack : IDeckPlanContributor {
    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, ModelDb.GetId<Necrobinder>().Entry, StringComparison.OrdinalIgnoreCase);

    public void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot) {
        builder.TargetDeckSize = 16;
        builder.AddWeight(AiTag.Scaling, 0.7f);
        builder.AddWeight(AiTag.Exhaust, 0.5f);
        builder.AddWeight(AiTag.Setup, 0.4f);
        builder.AddWeight(AiTag.Block, 0.3f);
    }
}
