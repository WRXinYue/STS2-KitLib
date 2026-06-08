using System;
using System.Collections.Generic;
using System.Linq;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

public sealed record RelicCatalogEntry(
    string Id,
    string Name,
    string Rarity,
    IReadOnlyList<AiTag> Tags);

public static class RelicCatalog {
    static readonly Dictionary<string, RelicCatalogEntry> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    static readonly (string IdFragment, AiTag Tag)[] IdTagRules = [
        ("DEAD_BRANCH", AiTag.Exhaust),
        ("CORRUPTION", AiTag.Exhaust),
        ("FROZEN_EYE", AiTag.Draw),
        ("TOXIC_EGG", AiTag.Scaling),
        ("MOLTEN_EGG", AiTag.Scaling),
        ("TOY_ORNITHOPTER", AiTag.Block),
        ("ORICHULUM", AiTag.Block),
        ("VENERABLE", AiTag.Block),
        ("KUNAI", AiTag.Attack),
        ("SHURIKEN", AiTag.Attack),
        ("ORNAMENTAL_FAN", AiTag.Attack),
        ("LETTER_OPENER", AiTag.Attack),
        ("SNECKO_EYE", AiTag.Energy),
        ("CHEMICAL_X", AiTag.Scaling),
        ("BIRD_FACED_URN", AiTag.Exhaust),
        ("FUSION_HAMMER", AiTag.Thin),
        ("SMITHING_HAMMER", AiTag.Scaling),
        ("BUSTED_CROWN", AiTag.Thin),
        ("COFFER", AiTag.Draw),
        ("PAPER_PHONOGRAPH", AiTag.Draw),
        ("BAG_OF_MARBLES", AiTag.Setup),
        ("ANCHOR", AiTag.Block),
        ("TURNIP", AiTag.Block),
        ("FOSSILIZED_FANG", AiTag.Scaling),
        ("MEAT_ON_THE_BONE", AiTag.Block),
    ];

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        foreach (var relic in ModelDb.AllRelics) {
            var id = relic.Id.Entry ?? "";
            if (string.IsNullOrWhiteSpace(id)) continue;

            ById[id] = new RelicCatalogEntry(
                id,
                relic.Title.GetFormattedText(),
                relic.Rarity.ToString(),
                InferRelicTags(id));
        }

        MainFile.Logger.Info($"[AiKnowledge] RelicCatalog indexed {ById.Count} relics.");
    }

    static IReadOnlyList<AiTag> InferRelicTags(string id) {
        var tags = new HashSet<AiTag>();
        foreach (var (fragment, tag) in IdTagRules) {
            if (id.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                tags.Add(tag);
        }

        return tags.Count > 0 ? [.. tags] : [AiTag.Utility];
    }

    public static bool TryGet(string? id, out RelicCatalogEntry entry) {
        entry = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out entry!);
    }

    public static RelicCatalogEntry? GetOrNull(string? id) =>
        TryGet(id, out var entry) ? entry : null;

    public static IReadOnlyList<AiTag> ResolveTags(string? id) {
        AiKnowledgeBootstrap.EnsureRegistered();
        if (TryGet(id, out var entry))
            return entry.Tags;
        return InferRelicTags(id ?? "");
    }

    internal static void ClearForTests() {
        ById.Clear();
        _initialized = false;
    }
}
