using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Entities.Cards;
using MegaCrit.Sts2.Core.Entities.Players;
using MegaCrit.Sts2.Core.Localization;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Models.CardPools;

namespace KitLib.UI;

internal static partial class CardBrowserUI {
    // ── Filter / browse / sort enums ──

    private enum SortField { Type, Rarity, Cost, Alphabet }
    private const int CostFilterX = -1;

    /// <summary>Where the card grid sources its cards from.</summary>
    private enum BrowseSource { AllCards, Hand, DrawPile, DiscardPile, ExhaustPile, Deck }
    private static BrowseSource _browseSource = BrowseSource.AllCards;

    private static CardTarget? BrowseSourceToTarget(BrowseSource src) => src switch {
        BrowseSource.Hand => CardTarget.Hand,
        BrowseSource.DrawPile => CardTarget.DrawPile,
        BrowseSource.DiscardPile => CardTarget.DiscardPile,
        BrowseSource.ExhaustPile => CardTarget.ExhaustPile,
        BrowseSource.Deck => CardTarget.Deck,
        _ => null
    };

    private static bool IsLibrarySource => _browseSource == BrowseSource.AllCards;

    /// <summary>
    /// Type / rarity / cost / pool chips, multi-sort order, search text, and library upgrade preview.
    /// Survives closing the card browser (same game session).
    /// </summary>
    private static class CardBrowserFilterPersistence {
        public static readonly HashSet<CardType> ActiveTypeFilters = new();
        public static readonly HashSet<CardRarity> ActiveRarityFilters = new();
        public static readonly HashSet<int> ActiveCostFilters = new();
        public static readonly HashSet<string> ActivePoolFilters = new();
        public static readonly HashSet<CardType> ExcludedTypeFilters = new();
        public static readonly HashSet<CardRarity> ExcludedRarityFilters = new();
        public static readonly HashSet<int> ExcludedCostFilters = new();
        public static readonly HashSet<string> ExcludedPoolFilters = new();
        public static readonly HashSet<string> ActiveModSourceFilters = new();
        public static readonly HashSet<string> ExcludedModSourceFilters = new();
        public static readonly List<(SortField field, bool asc)> SortPriority = new() {
            (SortField.Rarity, true), (SortField.Type, true),
            (SortField.Cost, true), (SortField.Alphabet, true)
        };
        public static string LastSearchText = "";
        public static bool LibraryShowUpgradePreview;
    }

    // Pool chip key aligned with predicates registered in Show() for the run's character card pool.
    private static string? GetDefaultPoolFilterKeyForPlayer(Player player) {
        try {
            var pool = player.Character?.CardPool;
            if (pool == null) return null;
            if (pool is IroncladCardPool) return "ironclad";
            if (pool is SilentCardPool) return "silent";
            if (pool is DefectCardPool) return "defect";
            if (pool is RegentCardPool) return "regent";
            if (pool is NecrobinderCardPool) return "necrobinder";
            if (pool is ColorlessCardPool) return "colorless";
            return "mod_" + pool.Title;
        }
        catch {
            return null;
        }
    }

    // ── Rarity colors ──

    private static Color ColCommon => KitLibTheme.RarityCommon;
    private static Color ColUncommon => KitLibTheme.RarityUncommon;
    private static Color ColRare => KitLibTheme.RarityRare;
    private static Color ColSpecial => KitLibTheme.RaritySpecial;
    private static Color ColCurse => KitLibTheme.RarityCurse;

    // ── Card enum helpers ──

    private static CardType GetCardType(CardModel card) {
        try { return card.Type; }
        catch { return CardType.None; }
    }

    private static CardRarity GetCardRarity(CardModel card) {
        try { return card.Rarity; }
        catch { return CardRarity.None; }
    }

    internal static string GetLocalizedTypeName(CardModel card) {
        try {
            var t = card.Type;
            return t == CardType.None ? "" : t.ToLocString().GetFormattedText();
        }
        catch { return ""; }
    }

    internal static string GetLocalizedRarityName(CardModel card) {
        try {
            var r = card.Rarity;
            return r == CardRarity.None ? "" : r.ToLocString().GetFormattedText();
        }
        catch { return ""; }
    }

    private static Color RarityToColor(CardRarity rarity) {
        return rarity switch {
            CardRarity.Common or CardRarity.Basic or CardRarity.Token => ColCommon,
            CardRarity.Uncommon => ColUncommon,
            CardRarity.Rare => ColRare,
            CardRarity.Event => ColSpecial,
            CardRarity.Curse or CardRarity.Status => ColCurse,
            CardRarity.Ancient => ColRare,
            _ => ColCommon
        };
    }

    // ── Filter matchers ──

    private static bool MatchesTypeSet(CardModel card, HashSet<CardType> active) {
        if (active.Count == 0) return true;
        var t = GetCardType(card);
        if (active.Contains(t)) return true;
        if (active.Contains(CardType.None)) {
            bool isStandard = t == CardType.Attack || t == CardType.Skill || t == CardType.Power;
            return !isStandard;
        }
        return false;
    }

    private static bool IsExcludedByTypeSet(CardModel card, HashSet<CardType> excluded) {
        if (excluded.Count == 0) return false;
        return MatchesTypeSet(card, excluded);
    }

    private static bool MatchesRaritySet(CardModel card, HashSet<CardRarity> active) {
        if (active.Count == 0) return true;
        var r = GetCardRarity(card);
        if (active.Contains(r)) return true;
        if (active.Contains(CardRarity.None)) {
            bool isStandard = r == CardRarity.Common || r == CardRarity.Uncommon || r == CardRarity.Rare;
            return !isStandard;
        }
        return false;
    }

    private static bool IsExcludedByRaritySet(CardModel card, HashSet<CardRarity> excluded) {
        if (excluded.Count == 0) return false;
        return MatchesRaritySet(card, excluded);
    }

    private static bool MatchesCostSet(CardModel card, HashSet<int> active) {
        if (active.Count == 0) return true;
        try {
            var ec = card.EnergyCost;
            if (ec == null) return active.Contains(0);
            if (ec.CostsX || card.HasStarCostX) return active.Contains(CostFilterX);
            int c = ec.Canonical;
            if (c >= 3) return active.Contains(3);
            return active.Contains(c);
        }
        catch { return true; }
    }

    private static bool IsExcludedByCostSet(CardModel card, HashSet<int> excluded) {
        if (excluded.Count == 0) return false;
        return MatchesCostSet(card, excluded);
    }

    private static bool MatchesPoolSet(
        CardModel card,
        HashSet<string> active,
        Dictionary<string, Func<CardModel, bool>> predicates) {
        if (active.Count == 0) return true;
        try {
            foreach (var key in active) {
                if (predicates.TryGetValue(key, out var pred) && pred(card))
                    return true;
            }
            return false;
        }
        catch { return true; }
    }

    private static bool IsExcludedByPoolSet(
        CardModel card,
        HashSet<string> excluded,
        Dictionary<string, Func<CardModel, bool>> predicates) {
        if (excluded.Count == 0) return false;
        return MatchesPoolSet(card, excluded, predicates);
    }

    // ── Sorting ──

    private static int GetRarityOrder(CardRarity r) {
        if (r <= CardRarity.Ancient) return (int)r;
        return r switch {
            CardRarity.Status => 6,
            CardRarity.Curse => 7,
            CardRarity.Event => 8,
            CardRarity.Quest => 9,
            CardRarity.Token => 10,
            _ => 99
        };
    }

    private static int CompareCards(CardModel a, CardModel b, List<(SortField field, bool asc)> priority) {
        foreach (var (field, asc) in priority) {
            int cmp = field switch {
                SortField.Type => a.Type.CompareTo(b.Type),
                SortField.Rarity => GetRarityOrder(a.Rarity).CompareTo(GetRarityOrder(b.Rarity)),
                SortField.Cost => (a.EnergyCost?.Canonical ?? 0).CompareTo(b.EnergyCost?.Canonical ?? 0),
                SortField.Alphabet => string.Compare(a.Title, b.Title,
                    CultureInfo.CurrentCulture, CompareOptions.None),
                _ => 0
            };
            if (cmp != 0) return asc ? cmp : -cmp;
        }
        return 0;
    }
}
