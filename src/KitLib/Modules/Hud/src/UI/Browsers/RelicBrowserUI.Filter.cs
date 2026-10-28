using System;
using System.Collections.Generic;
using System.Globalization;
using Godot;
using MegaCrit.Sts2.Core.Entities.Relics;
using MegaCrit.Sts2.Core.Models;

namespace KitLib.UI;

internal static partial class RelicBrowserUI {
    private enum SortField { Alphabet, Rarity }
    private enum BrowseSource { All, Owned }

    private static BrowseSource _browseSource = BrowseSource.All;
    private static bool IsAllSource => _browseSource == BrowseSource.All;

    private static readonly Dictionary<RelicRarity, Color> RarityColors = new() {
        [RelicRarity.None] = KitLibTheme.Subtle,
        [RelicRarity.Starter] = new Color(0.50f, 0.50f, 0.55f),
        [RelicRarity.Common] = KitLibTheme.RarityCommon,
        [RelicRarity.Uncommon] = KitLibTheme.RarityUncommon,
        [RelicRarity.Rare] = KitLibTheme.RarityRare,
        [RelicRarity.Shop] = new Color(0.35f, 0.75f, 0.55f),
        [RelicRarity.Event] = KitLibTheme.RaritySpecial,
        [RelicRarity.Ancient] = new Color(0.90f, 0.65f, 0.20f),
    };

    private static Color RarityToColor(RelicRarity rarity)
        => RarityColors.TryGetValue(rarity, out var c) ? c : KitLibTheme.Subtle;

    internal static RelicRarity GetRelicRarity(RelicModel relic) {
        try { return relic.Rarity; }
        catch { return RelicRarity.None; }
    }

    internal static string GetRelicDisplayName(RelicModel relic) {
        try { return relic.Title?.GetFormattedText() ?? ((AbstractModel)relic).Id.Entry ?? "?"; }
        catch { return ((AbstractModel)relic).Id.Entry ?? "?"; }
    }

    internal static string GetRelicDescription(RelicModel relic) {
        try { return relic.DynamicDescription?.GetFormattedText() ?? ""; }
        catch {
            try { return relic.Flavor?.GetFormattedText() ?? ""; }
            catch { return ""; }
        }
    }

    internal static string GetRelicFlavor(RelicModel relic) {
        try { return relic.Flavor?.GetFormattedText() ?? ""; }
        catch { return ""; }
    }

    internal static string GetRelicId(RelicModel relic) {
        try { return ((AbstractModel)relic).Id.Entry ?? ""; }
        catch { return ""; }
    }

    private static string RarityDisplayName(RelicRarity r) => r switch {
        RelicRarity.Starter => I18N.T("relicBrowser.rarityStarter", "Starter"),
        RelicRarity.Common => I18N.T("relicBrowser.rarityCommon", "Common"),
        RelicRarity.Uncommon => I18N.T("relicBrowser.rarityUncommon", "Uncommon"),
        RelicRarity.Rare => I18N.T("relicBrowser.rarityRare", "Rare"),
        RelicRarity.Shop => I18N.T("relicBrowser.rarityShop", "Shop"),
        RelicRarity.Event => I18N.T("relicBrowser.rarityEvent", "Event"),
        RelicRarity.Ancient => I18N.T("relicBrowser.rarityAncient", "Ancient"),
        _ => "?"
    };

    private static int GetRarityOrder(RelicRarity r) => r switch {
        RelicRarity.Starter => 0,
        RelicRarity.Common => 1,
        RelicRarity.Uncommon => 2,
        RelicRarity.Rare => 3,
        RelicRarity.Shop => 4,
        RelicRarity.Event => 5,
        RelicRarity.Ancient => 6,
        _ => 99
    };

    private static bool MatchesRaritySet(RelicModel relic, HashSet<RelicRarity> active) {
        if (active.Count == 0) return true;
        return active.Contains(GetRelicRarity(relic));
    }

    private static int CompareRelics(RelicModel a, RelicModel b, SortField field, bool asc) {
        int cmp = field switch {
            SortField.Alphabet => string.Compare(
                GetRelicDisplayName(a), GetRelicDisplayName(b),
                CultureInfo.CurrentCulture, CompareOptions.None),
            SortField.Rarity => GetRarityOrder(GetRelicRarity(a)).CompareTo(GetRarityOrder(GetRelicRarity(b))),
            _ => 0
        };
        if (cmp == 0 && field != SortField.Alphabet)
            cmp = string.Compare(GetRelicDisplayName(a), GetRelicDisplayName(b),
                CultureInfo.CurrentCulture, CompareOptions.None);
        return asc ? cmp : -cmp;
    }

    private static List<RelicRarity> DiscoverRarities(List<RelicModel> relics) {
        var seen = new HashSet<RelicRarity>();
        foreach (var r in relics) {
            var rarity = GetRelicRarity(r);
            if (rarity != RelicRarity.None) seen.Add(rarity);
        }
        var ordered = new List<RelicRarity>(seen);
        ordered.Sort((a, b) => GetRarityOrder(a).CompareTo(GetRarityOrder(b)));
        return ordered;
    }
}
