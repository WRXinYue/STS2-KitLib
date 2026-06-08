using System;
using System.Collections.Generic;
using System.Linq;
using KitLib.AI.Core;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

public static class CardTagProviderHub {
    public static void Register(ICardTagProvider provider) =>
        KitLib.Host.KitLibHost.RegisterCardTagProvider(provider);

    public static IReadOnlyList<AiTag> MergeTags(string? cardId, IReadOnlyList<AiTag> baseTags) =>
        KitLib.Host.KitLibHost.MergeCardTags(cardId, baseTags);
}

public sealed record CardCatalogEntry(
    string Id,
    string Name,
    int Cost,
    string CardType,
    string Rarity,
    string? Pool,
    IReadOnlyList<AiTag> Tags);

public static class CardCatalog {
    static readonly Dictionary<string, CardCatalogEntry> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;

        foreach (var card in ModelDb.AllCards) {
            try {
                var id = card.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;

                string? pool = null;
                try { pool = card.Pool?.Title; } catch { }

                var baseTags = CardTagRules.InferTags(card);
                var tags = CardTagProviderHub.MergeTags(id, baseTags);

                ById[id] = new CardCatalogEntry(
                    id,
                    card.Title,
                    card.EnergyCost.Canonical,
                    card.Type.ToString(),
                    card.Rarity.ToString(),
                    pool,
                    tags);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiKnowledge] Skipped card {card.Id.Entry}: {ex.Message}");
            }
        }

        _initialized = true;
        MainFile.Logger.Info($"[AiKnowledge] CardCatalog indexed {ById.Count} cards.");
    }

    public static bool TryGet(string? id, out CardCatalogEntry entry) {
        entry = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out entry!);
    }

    public static CardCatalogEntry? GetOrNull(string? id) =>
        TryGet(id, out var entry) ? entry : null;

    public static IReadOnlyList<AiTag> ResolveTags(string? id, string? cardType = null, System.Text.Json.Nodes.JsonArray? keywords = null) {
        AiKnowledgeBootstrap.EnsureRegistered();
        if (TryGet(id, out var entry)) {
            var tags = new HashSet<AiTag>(entry.Tags);
            if (CardMechanicIndex.TryGet(id, out var profile)) {
                foreach (var tag in profile.DerivedTags)
                    tags.Add(tag);
            }
            return [.. tags];
        }

        var inferred = CardTagRules.InferTagsFromSnapshot(id, cardType, keywords);
        return CardTagProviderHub.MergeTags(id, inferred);
    }

    internal static void ClearForTests() {
        ById.Clear();
        _initialized = false;
    }
}
