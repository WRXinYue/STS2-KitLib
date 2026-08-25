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

    /// <summary>
    /// Optional icon used while this overlay is open. When unset, Core keeps the idle icon.
    /// Opening any overlay still flies that button to the vanilla patch-notes slot and hides
    /// sibling shortcuts.
    /// </summary>
    public string? ActiveIconPath { get; init; }

    /// <summary>
    /// Optional name shown left of the icon. When unset, Core uses the mod display name, then <see cref="ModId"/>.
    /// </summary>
    public string? Title { get; init; }

    /// <summary>Optional loc key resolved by Core at render time. <see cref="Title"/> is the English fallback.</summary>
    public string? TitleKey { get; init; }

    /// <summary>
    /// Optional second line under the name. When unset, Core uses <c>v{version}</c> from
    /// <see cref="Version"/> or the loaded mod manifest.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>Optional version used when <see cref="Description"/> is unset.</summary>
    public string? Version { get; init; }

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

    /// <summary>
    /// Whether this button's overlay is open. When any overlay is open, Core hides sibling
    /// shortcuts (other KitLib corner icons, RitsuLib settings, vanilla patch notes).
    /// </summary>
    public Func<object, bool>? IsOpen { get; init; }

    /// <summary>
    /// Optional hook when Core attaches the corner host to a live <c>NMainMenu</c>.
    /// Use it to add overlay nodes; do not Harmony-patch the main menu for that.
    /// </summary>
    public Action<object>? OnMenuReady { get; init; }
}
