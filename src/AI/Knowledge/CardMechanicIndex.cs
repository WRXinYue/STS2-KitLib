using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using DevMode.Actions;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Models;

namespace DevMode.AI.Knowledge;

public sealed record CardMechanicProfile(
    string Id,
    CardMechanicFlags Flags,
    IReadOnlyList<string> DynamicVarKeys,
    int CanonicalCost,
    int? Damage,
    int? Block,
    string CardType,
    IReadOnlyList<AiTag> DerivedTags);

/// <summary>Indexes official card mechanics from <see cref="ModelDb.AllCards"/> at startup.</summary>
public static class CardMechanicIndex {
    static readonly Dictionary<string, CardMechanicProfile> ById = new(StringComparer.OrdinalIgnoreCase);
    static bool _initialized;

    public static void Initialize() {
        if (_initialized) return;
        _initialized = true;

        foreach (var card in ModelDb.AllCards) {
            try {
                var id = card.Id.Entry ?? "";
                if (string.IsNullOrWhiteSpace(id)) continue;
                ById[id] = BuildProfile(card);
            }
            catch (Exception ex) {
                MainFile.Logger.Warn($"[AiMechanic] Skipped card {card.Id.Entry}: {ex.Message}");
            }
        }

        MainFile.Logger.Info($"[AiMechanic] CardMechanicIndex indexed {ById.Count} cards.");
    }

    public static bool TryGet(string? id, out CardMechanicProfile profile) {
        EnsureInitialized();
        profile = null!;
        if (string.IsNullOrWhiteSpace(id)) return false;
        return ById.TryGetValue(id, out profile!);
    }

    public static CardMechanicProfile InferFromSnapshot(JsonObject card) {
        var id = card["id"]?.GetValue<string>();
        if (TryGet(id, out var profile))
            return profile;

        var flags = CardMechanicFlags.None;
        var cardType = card["cardType"]?.GetValue<string>() ?? "";
        if (cardType.Contains("Attack", StringComparison.OrdinalIgnoreCase))
            flags |= CardMechanicFlags.HasDamage;
        if (card["damage"] != null)
            flags |= CardMechanicFlags.HasDamage;
        if (card["block"] != null)
            flags |= CardMechanicFlags.HasBlock;

        var keywords = card["keywords"]?.AsArray();
        if (keywords != null) {
            foreach (var node in keywords) {
                var kw = node?.GetValue<string>() ?? "";
                flags |= OfficialMechanicProbe.FlagsFromKeywordName(kw);
            }
        }

        flags |= OfficialMechanicProbe.AnalyzeTokenBlob(id ?? "");

        var cost = card["cost"]?.GetValue<int>() ?? 1;
        var derived = CardTagRules.InferTagsFromSnapshot(id, cardType, keywords);
        return new CardMechanicProfile(
            id ?? "",
            flags,
            [],
            cost,
            card["damage"]?.GetValue<int>(),
            card["block"]?.GetValue<int>(),
            cardType,
            derived);
    }

    static CardMechanicProfile BuildProfile(CardModel card) {
        var id = card.Id.Entry ?? "";
        var flags = OfficialMechanicProbe.ProbeCard(card);

        if (OfficialMechanicProbe.NeedsCardTextFallback(flags)) {
            try {
                flags |= MechanicTextAnalyzer.AnalyzeCardTextFallback(
                    CardPreviewHelper.GetDescription(card),
                    card.GetType().Name);
            }
            catch { /* ignore */ }
        }

        var derived = new HashSet<AiTag>(CardTagRules.InferTags(card));
        derived.UnionWith(TagsFromFlags(flags));

        return new CardMechanicProfile(
            id,
            flags,
            CardEditActions.GetDynamicVarKeys(card).ToArray(),
            card.EnergyCost.Canonical,
            CardEditActions.GetDamage(card),
            CardEditActions.GetBlock(card),
            card.Type.ToString(),
            [.. derived]);
    }

    static IEnumerable<AiTag> TagsFromFlags(CardMechanicFlags flags) {
        if (flags.HasFlag(CardMechanicFlags.HasDraw)) yield return AiTag.Draw;
        if (flags.HasFlag(CardMechanicFlags.HasForge)) yield return AiTag.Scaling;
        if (flags.HasFlag(CardMechanicFlags.HasStarCost)) yield return AiTag.Energy;
        if (flags.HasFlag(CardMechanicFlags.Exhaust)) yield return AiTag.Exhaust;
        if (flags.HasFlag(CardMechanicFlags.Retain)) yield return AiTag.Setup;
        if (flags.HasFlag(CardMechanicFlags.Aoe)) yield return AiTag.Aoe;
        if (flags.HasFlag(CardMechanicFlags.TransformsHandAttacks)
            || flags.HasFlag(CardMechanicFlags.TransformsCards))
            yield return AiTag.Scaling;
        if (flags.HasFlag(CardMechanicFlags.HasDamage)) yield return AiTag.Attack;
        if (flags.HasFlag(CardMechanicFlags.HasBlock)) yield return AiTag.Block;
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
