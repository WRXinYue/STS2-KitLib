using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace DevMode.AI.Characters.Vanilla;

sealed class IroncladPack : IDeckPlanContributor {
    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, ModelDb.GetId<Ironclad>().Entry, StringComparison.OrdinalIgnoreCase);

    public void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot) {
        builder.TargetDeckSize = 17;
        builder.AddWeight(AiTag.Attack, 0.6f);
        builder.AddWeight(AiTag.Exhaust, 0.8f);
        builder.AddWeight(AiTag.Scaling, 0.5f);
        builder.AddWeight(AiTag.Block, 0.3f);
    }
}
