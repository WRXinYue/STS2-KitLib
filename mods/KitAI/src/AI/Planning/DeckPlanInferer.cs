using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

public static class DeckPlanInferer {
    public static DeckPlan Infer(JsonObject snapshot) {
        AiKnowledgeBootstrap.EnsureRegistered();
        var builder = new DeckPlan.Builder();
        var deck = snapshot["deck"]?.AsArray();
        var relics = snapshot["relics"]?.AsArray();
        var floor = snapshot["totalFloor"]?.GetValue<int>() ?? 0;
        var actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        var deckSize = deck?.Count ?? 0;

        builder.AddWeight(AiTag.Attack, 1f);
        builder.AddWeight(AiTag.Block, 0.8f);
        builder.AddWeight(AiTag.Draw, 0.6f);

        int exhaustCards = 0;
        int drawTagged = 0;
        if (deck != null) {
            foreach (var node in deck) {
                if (node is not JsonObject card) continue;
                var tags = CardCatalog.ResolveTags(
                    card["id"]?.GetValue<string>(),
                    card["cardType"]?.GetValue<string>(),
                    card["keywords"]?.AsArray());
                if (tags.Contains(AiTag.Exhaust)) exhaustCards++;
                if (tags.Contains(AiTag.Draw)) drawTagged++;
            }
        }

        bool hasExhaustRelic = false;
        bool hasThinRelic = false;
        if (relics != null) {
            foreach (var node in relics) {
                string? id = node switch {
                    JsonObject o => o["id"]?.GetValue<string>() ?? o["name"]?.GetValue<string>(),
                    _ => node?.GetValue<string>(),
                };
                var tags = RelicCatalog.ResolveTags(id);
                if (tags.Contains(AiTag.Exhaust)) {
                    hasExhaustRelic = true;
                    builder.AddWeight(AiTag.Exhaust, 0.5f);
                }
                if (tags.Contains(AiTag.Thin)) hasThinRelic = true;
            }
        }

        if (hasExhaustRelic || exhaustCards >= 3)
            builder.AddWeight(AiTag.Exhaust, 1.2f);

        if (deckSize > 18 && drawTagged < 2)
            builder.ThinPreference += 0.35f;

        if (deckSize > builder.TargetDeckSize + 4)
            builder.ThinPreference += 0.25f;

        if (hasThinRelic)
            builder.ThinPreference += 0.2f;

        if (actIndex >= 2 && deckSize > 22)
            builder.ThinPreference += 0.3f;

        var ascension = snapshot["ascensionLevel"]?.GetValue<int>() ?? 0;
        if (ascension >= 7)
            builder.AddWeight(AiTag.Block, 0.4f);

        if (ascension >= 10)
            builder.AddWeight(AiTag.Attack, 0.3f);

        builder.ThinPreference = Math.Clamp(builder.ThinPreference, -0.3f, 1f);
        DeckPlanContributorHub.ApplyContributors(builder, snapshot);
        return builder.Build();
    }

    public static float ScoreTags(IReadOnlyList<AiTag> tags, DeckPlan plan) =>
        tags.Sum(t => plan.GetWeight(t));

    public static float DilutionPenalty(int deckSize, DeckPlan plan) {
        if (deckSize <= plan.TargetDeckSize) return 0f;
        var over = deckSize - plan.TargetDeckSize;
        return over * plan.ThinPreference * 0.8f;
    }
}
