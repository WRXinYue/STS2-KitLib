using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace KitLib.AI.Characters.Vanilla;

sealed class RegentPack : IDeckPlanContributor {
    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, ModelDb.GetId<Regent>().Entry, StringComparison.OrdinalIgnoreCase);

    public void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot) {
        builder.TargetDeckSize = 16;
        builder.AddWeight(AiTag.Scaling, 0.8f);
        builder.AddWeight(AiTag.Attack, 0.5f);
        builder.AddWeight(AiTag.Energy, 0.4f);
        builder.AddWeight(AiTag.Block, 0.2f);
    }
}
