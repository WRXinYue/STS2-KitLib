using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;
using KitLib.AI.Combat.Simulation;
using KitLib.AI.Knowledge;

namespace KitLib.AI.Planning;

/// <summary>Rough partner hit rate from character card pool and remaining run rewards (option exercise probability).</summary>
public static class ComboExerciseEstimator {
    public static float EstimateForOffer(JsonObject card, JsonObject snapshot) {
        var profile = ResolveProfile(card);
        float best = 0f;

        foreach (var archetype in ComboOptionCatalog.Archetypes) {
            if (!ComboOptionCatalog.MatchesOffered(card, profile, archetype.Offered))
                continue;

            float hit = EstimatePartnerHit(snapshot, archetype.DeckPartners);
            best = Math.Max(best, hit);
        }

        return best;
    }

    public static float EstimatePartnerHit(JsonObject snapshot, ComboPartnerSpec[] partnerSpecs) {
        if (partnerSpecs.Length == 0)
            return 0f;

        var characterId = snapshot["characterId"]?.GetValue<string>() ?? "";
        int actIndex = snapshot["actIndex"]?.GetValue<int>() ?? 0;
        int poolSize = 0;
        int matches = 0;

        foreach (var entry in CardCatalog.EntriesForCharacter(characterId)) {
            if (IsStarterNoise(entry.Id))
                continue;

            poolSize++;
            if (MatchesCatalogEntry(entry, partnerSpecs))
                matches++;
        }

        if (poolSize == 0)
            return 0.35f;

        float density = (float)matches / poolSize;
        float rewardsLeft = Math.Max(1f, (3f - actIndex) * 3.5f);
        return Math.Clamp(density * rewardsLeft * 0.9f, 0.05f, 1f);
    }

    static bool IsStarterNoise(string id) {
        return id.Contains("STRIKE", StringComparison.OrdinalIgnoreCase)
            || id.Contains("DEFEND", StringComparison.OrdinalIgnoreCase);
    }

    static bool MatchesCatalogEntry(CardCatalogEntry entry, ComboPartnerSpec[] specs) {
        if (CardMechanicIndex.TryGet(entry.Id, out var profile)) {
            var cardLike = new JsonObject {
                ["id"] = entry.Id,
                ["cardType"] = entry.CardType,
            };
            foreach (var spec in specs) {
                if (ComboOptionCatalog.MatchesCardPublic(cardLike, profile, spec))
                    return true;
            }
        }

        foreach (var spec in specs) {
            if (spec.Kind == ComboPartnerKind.AiTag
                && Enum.TryParse<AiTag>(spec.Token, out var tag)
                && entry.Tags.Contains(tag))
                return true;
            if (spec.Kind == ComboPartnerKind.IdContains
                && entry.Id.Contains(spec.Token, StringComparison.OrdinalIgnoreCase))
                return true;
            if (spec.Kind == ComboPartnerKind.CardId
                && string.Equals(entry.Id, spec.Token, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    static CardMechanicProfile ResolveProfile(JsonObject card) {
        var id = card["id"]?.GetValue<string>();
        if (CardMechanicIndex.TryGet(id, out var profile))
            return profile;
        return CardMechanicIndex.InferFromSnapshot(card);
    }
}
