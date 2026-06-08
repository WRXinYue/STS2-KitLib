using System;
using System.Collections.Generic;
using KitLib.Icons;
using KitLib.Modding;
using Godot;

namespace KitLib.UI;

internal static class BrowserDetailHelpers {
    private static readonly MdiIcon ModSourceChipIcon = MdiIcon.From("zip-box");

    public static Label CreateModSourceRow(ContentModSource src) {
        var label = new Label {
            Text = string.Format(I18N.T("browser.modSource.label", "Source: {0}"), src.DisplayLabel),
            HorizontalAlignment = HorizontalAlignment.Center,
            TooltipText = src.ModId ?? src.Key
        };
        label.AddThemeFontSizeOverride("font_size", 11);
        label.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        return label;
    }

    public static Control? TryCreateModSourceFilterRow(
        IReadOnlyList<(string key, string label)> entries,
        HashSet<string> activeFilters,
        HashSet<string> excludedFilters,
        Action onFiltersChanged) {
        if (entries.Count == 0)
            return null;

        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 6);

        var heading = new Label {
            Text = I18N.T("browser.modSource", "Mod source"),
            VerticalAlignment = VerticalAlignment.Center
        };
        heading.AddThemeFontSizeOverride("font_size", 11);
        heading.AddThemeColorOverride("font_color", KitLibTheme.Subtle);
        heading.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(heading);

        row.AddChild(new ModPoolFilterDropdown(
            entries,
            activeFilters,
            excludedFilters,
            onFiltersChanged,
            I18N.T("browser.modSource.chipAll", "All"),
            I18N.T("browser.modSource.count", "Mod source ({0})"),
            I18N.T("browser.modSource.excludedCount", "Mod source (−{0})"),
            ModSourceChipIcon,
            I18N.T("browser.modSource", "Mod source")));

        row.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill });
        return row;
    }
}
