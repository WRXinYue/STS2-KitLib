using System.Collections.Generic;
using System.Linq;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Models;

namespace KitLib;

internal static class CardLibraryVisibility {
    public static bool ShowHiddenCards => SettingsStore.Current.ShowHiddenCards;

    public static List<CardModel> GetLibraryCards() {
        var all = ModelDb.AllCards;
        if (ShowHiddenCards)
            return all.ToList();
        return all.Where(c => c.ShouldShowInCardLibrary).ToList();
    }
}
