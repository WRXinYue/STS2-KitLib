using System;
using System.Collections.Generic;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.AI.Knowledge;

public sealed record RelicMechanicProfile(
    string Id,
    RelicMechanicFlags Flags,
    string Rarity,
    IReadOnlyList<AiTag> DerivedTags);

/// <summary>Indexes official relic mechanics from <see cref="ModelDb.AllRelics"/> at startup.</summary>
public static class RelicMechanicIndex {
    static readonly Dictionary<string, RelicMechanicProfile> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        foreach (var relic in ModelDb.AllRelics) {
            try {
                var id = relic.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;
                ById[id] = BuildProfile(relic);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiMechanic] Skipped relic {relic.Id.Entry}: {ex.Message}");
            }
        }

        MainFile.Logger.Info($"[AiMechanic] RelicMechanicIndex indexed {ById.Count} relics.");
    }

    public static bool TryGet(string? id, out RelicMechanicProfile profile) {
        EnsureInitialized();
        profile = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out profile!);
    }

    static RelicMechanicProfile BuildProfile(RelicModel relic) {
        var id = relic.Id.Entry ?? "";
        var flags = OfficialMechanicProbe.ProbeRelic(relic);

        if (OfficialMechanicProbe.NeedsRelicTextFallback(flags)) {
            try {
                flags |= MechanicTextAnalyzer.AnalyzeRelicTextFallback(
                    relic.DynamicDescription?.GetFormattedText() ?? "");
            }
            catch { /* ignore */ }
        }

        var rarity = relic.Rarity.ToString();
        var derived = new HashSet<AiTag>(RelicCatalog.ResolveTags(id));
        derived.UnionWith(TagsFromFlags(flags));

        return new RelicMechanicProfile(id, flags, rarity, [.. derived]);
    }

    static IEnumerable<AiTag> TagsFromFlags(RelicMechanicFlags flags) {
        if (flags.HasFlag(RelicMechanicFlags.OffersRarePick)
            || flags.HasFlag(RelicMechanicFlags.OffersCardPick))
            yield return AiTag.Scaling;
        if (flags.HasFlag(RelicMechanicFlags.RemovesCard))
            yield return AiTag.Thin;
        if (flags.HasFlag(RelicMechanicFlags.CombatScaling))
            yield return AiTag.Scaling;
        if (flags.HasFlag(RelicMechanicFlags.GrantsPotion))
            yield return AiTag.Utility;
    }

    static void EnsureInitialized() {
        if (!_initialized)
            Initialize();
    }

    internal static void ClearForTests() {
        ById.Clear();
        _initialized = false;
    }
}
