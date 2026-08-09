using System;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;
using KitLib.AI.Planning;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.Characters;

namespace KitLib.AI.Characters.Vanilla;

sealed class SilentPack : IDeckPlanContributor {
    public bool AppliesTo(string? characterId) =>
        string.Equals(characterId, ModelDb.GetId<Silent>().Entry, StringComparison.OrdinalIgnoreCase);

    public void AdjustPlan(DeckPlan.Builder builder, JsonObject snapshot) {
        builder.TargetDeckSize = 15;
        builder.TargetStrikeCount = 0;
        builder.TargetDefendCount = 1;
        builder.TargetBlockSources = 1;
        builder.TargetDrawSources = 3;
        builder.AddWeight(AiTag.Attack, 0.4f);
        builder.AddWeight(AiTag.Draw, 0.5f);
        builder.AddWeight(AiTag.Exhaust, 0.3f);
        builder.AddWeight(AiTag.Scaling, 0.6f);
    }
}
