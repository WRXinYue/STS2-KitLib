using KitLib.Abstractions.Host;

namespace KitLib;

/// <summary>Resolves dev-rail tab labels from i18n keys at display time.</summary>
public static class KitLibTabLabels {
    public static string Resolve(KitLibTabDescriptor tab) =>
        Resolve(tab.DisplayNameKey, tab.DisplayNameFallback, tab.DisplayName);

    public static string Resolve(string? displayNameKey, string displayNameFallback, string? legacyDisplayName = null) {
        if (!string.IsNullOrEmpty(displayNameKey))
            return I18N.T(displayNameKey, displayNameFallback);
        if (!string.IsNullOrEmpty(legacyDisplayName))
            return legacyDisplayName;
        return displayNameFallback;
    }
}
