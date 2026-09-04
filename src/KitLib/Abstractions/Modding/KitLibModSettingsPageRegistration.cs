namespace KitLib.Abstractions.Modding;

/// <summary>
/// One KitLib-native mod settings page shown in KitModPanel.
/// <see cref="BuildBody"/> must return a Godot <c>Control</c> at runtime (typed as <c>object</c> so Abstractions stays UI-free).
/// </summary>
public sealed class KitLibModSettingsPageRegistration {
    /// <summary>Official mod manifest id (case-insensitive match).</summary>
    public required string ModId { get; init; }

    /// <summary>Stable page id within the mod (case-insensitive; used for tabs and deep-links).</summary>
    public required string PageId { get; init; }

    /// <summary>English fallback when <see cref="TitleKey"/> is unset or missing from locale files.</summary>
    public required string Title { get; init; }

    /// <summary>Optional loc key; KitModPanel resolves it at UI refresh so labels follow the active game locale.</summary>
    public string? TitleKey { get; init; }

    /// <summary>Lower sorts earlier. Ties break by <see cref="PageId"/>.</summary>
    public int SortOrder { get; init; }

    /// <summary>Builds the page body. Must return a Godot <c>Control</c> when KitModPanel renders the page.</summary>
    public required Func<object> BuildBody { get; init; }
}
