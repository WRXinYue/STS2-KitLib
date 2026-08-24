namespace KitLib.Abstractions.Modding;

/// <summary>
/// One main-menu top-left corner icon below the vanilla patch-notes shortcut.
/// <see cref="OnPressed"/> receives the live <c>NMainMenu</c> instance as <c>object</c>.
/// </summary>
public sealed class KitLibMainMenuCornerButtonRegistration {
    /// <summary>Official mod manifest id (case-insensitive match).</summary>
    public required string ModId { get; init; }

    /// <summary>Stable button id within the mod (case-insensitive; re-<see cref="KitLibMainMenuCornerButtonRegistry.Register"/> replaces).</summary>
    public required string ButtonId { get; init; }

    /// <summary>
    /// Optional <c>res://</c> icon path. When unset, Core uses
    /// <see cref="KitLibMainMenuCornerButtonRegistry.DefaultModImagePath"/>.
    /// </summary>
    public string? IconPath { get; init; }

    /// <summary>English fallback when <see cref="TooltipKey"/> is unset or missing from locale files.</summary>
    public string? Tooltip { get; init; }

    /// <summary>Optional loc key resolved by Core at render time.</summary>
    public string? TooltipKey { get; init; }

    /// <summary>Lower sorts earlier. Ties break by <see cref="ModId"/> then <see cref="ButtonId"/>.</summary>
    public int SortOrder { get; init; }

    /// <summary>Invoked when the icon is clicked.</summary>
    public required Action<object> OnPressed { get; init; }

    /// <summary>
    /// Optional extra visibility filter. Core still hides icons when the main-menu shortcut surface is unavailable.
    /// </summary>
    public Func<object, bool>? IsVisible { get; init; }
}
