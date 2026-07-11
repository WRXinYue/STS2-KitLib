using System.Collections.Generic;
using System.Linq;
using KitLib.Modding;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

internal static class CardLibraryVisibility {
    static List<CardModel>? _cachedLibraryCards;
    static bool _cachedShowHidden;
    static List<(string key, string label)>? _cachedModFilterEntries;

    public static bool ShowHiddenCards => SettingsStore.Current.ShowHiddenCards;

    public static void InvalidateCache() {
        _cachedLibraryCards = null;
        _cachedModFilterEntries = null;
    }

    public static List<CardModel> GetLibraryCards() {
        var showHidden = ShowHiddenCards;
        if (_cachedLibraryCards != null && _cachedShowHidden == showHidden)
            return _cachedLibraryCards;

        var all = ModelDb.AllCards;
        _cachedLibraryCards = showHidden
            ? all.ToList()
            : all.Where(c => c.ShouldShowInCardLibrary).ToList();
        _cachedShowHidden = showHidden;
        _cachedModFilterEntries = null;
        return _cachedLibraryCards;
    }

    /// <summary>Cached mod-source chips for the library browser (built once per card list revision).</summary>
    public static IReadOnlyList<(string key, string label)> GetModFilterEntries() {
        if (_cachedModFilterEntries != null && _cachedShowHidden == ShowHiddenCards)
            return _cachedModFilterEntries;

        _cachedModFilterEntries = ContentModResolver.BuildFilterEntries(GetLibraryCards());
        return _cachedModFilterEntries;
    }
}
