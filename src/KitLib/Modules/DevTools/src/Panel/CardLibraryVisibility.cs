using System.Collections.Generic;
using System.Linq;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

internal static class CardLibraryVisibility {
    static List<CardModel>? _cachedLibraryCards;
    static bool _cachedShowHidden;

    public static bool ShowHiddenCards => SettingsStore.Current.ShowHiddenCards;

    public static void InvalidateCache() {
        _cachedLibraryCards = null;
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
        return _cachedLibraryCards;
    }
}
