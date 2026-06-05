using System;
using System.Text.Json.Nodes;
using DevMode.AI.Knowledge;
using DevMode.AI.Planning;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace DevMode.AI.Characters.Vanilla;

sealed class DefectPack : IDeckPlanContributor {
    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, ModelDb.GetId<Defect>().Entry, StringComparison.OrdinalIgnoreCase);

    public void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot) {
        builder.TargetDeckSize = 15;
        builder.AddWeight(AiTag.Scaling, 0.9f);
        builder.AddWeight(AiTag.Aoe, 0.4f);
        builder.AddWeight(AiTag.Energy, 0.3f);
        builder.AddWeight(AiTag.Block, 0.2f);
    }
}
