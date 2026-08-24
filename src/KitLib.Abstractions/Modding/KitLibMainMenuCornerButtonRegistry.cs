namespace KitLib.Abstractions.Modding;

/// <summary>
/// Register / read / unregister API for main-menu top-left corner icon buttons.
/// Content mods call this from Abstractions; KitLib Core renders icons on <c>NMainMenu</c>.
/// </summary>
public static class KitLibMainMenuCornerButtonRegistry {
    static readonly List<KitLibMainMenuCornerButtonRegistration> Buttons = [];
    static readonly object Gate = new();

    /// <summary>Vanilla mod icon convention: <c>res://&lt;modId&gt;/mod_image.png</c>.</summary>
    public static string DefaultModImagePath(string modId) {
        ArgumentException.ThrowIfNullOrWhiteSpace(modId);
        return $"res://{modId.Trim()}/mod_image.png";
    }

    /// <summary>Register or replace a button for <paramref name="button"/>.ModId + ButtonId.</summary>
    public static void Register(KitLibMainMenuCornerButtonRegistration button) {
        ArgumentNullException.ThrowIfNull(button);
        ArgumentException.ThrowIfNullOrWhiteSpace(button.ModId);
        ArgumentException.ThrowIfNullOrWhiteSpace(button.ButtonId);
        ArgumentNullException.ThrowIfNull(button.OnPressed);

        lock (Gate) {
            Buttons.RemoveAll(b => SameButton(b, button.ModId, button.ButtonId));
            Buttons.Add(button);
        }
    }

    /// <summary>Remove one button. Returns whether a button was removed.</summary>
    public static bool Unregister(string modId, string buttonId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(buttonId))
            return false;
        lock (Gate)
            return Buttons.RemoveAll(b => SameButton(b, modId, buttonId)) > 0;
    }

    /// <summary>Remove every button registered for <paramref name="modId"/>. Returns how many were removed.</summary>
    public static int UnregisterAll(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return 0;
        lock (Gate)
            return Buttons.RemoveAll(b =>
                string.Equals(b.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether a specific button is registered.</summary>
    public static bool Contains(string modId, string buttonId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(buttonId))
            return false;
        lock (Gate)
            return Buttons.Any(b => SameButton(b, modId, buttonId));
    }

    /// <summary>All registered buttons ordered for vertical stacking on the main menu.</summary>
    public static IReadOnlyList<KitLibMainMenuCornerButtonRegistration> GetOrderedButtons() {
        lock (Gate) {
            return Buttons
                .OrderBy(b => b.SortOrder)
                .ThenBy(b => b.ModId, StringComparer.OrdinalIgnoreCase)
                .ThenBy(b => b.ButtonId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>Resolve the icon path, defaulting to <see cref="DefaultModImagePath"/>.</summary>
    public static string ResolveIconPath(KitLibMainMenuCornerButtonRegistration button) {
        ArgumentNullException.ThrowIfNull(button);
        return string.IsNullOrWhiteSpace(button.IconPath)
            ? DefaultModImagePath(button.ModId)
            : button.IconPath.Trim();
    }

    /// <summary>
    /// Resolve tooltip text. When <paramref name="translate"/> is provided and
    /// <see cref="KitLibMainMenuCornerButtonRegistration.TooltipKey"/> is set, calls
    /// <c>translate(titleKey, titleFallback)</c>.
    /// </summary>
    public static string ResolveTooltip(
        KitLibMainMenuCornerButtonRegistration button,
        Func<string, string, string>? translate = null) {
        ArgumentNullException.ThrowIfNull(button);
        var fallback = button.Tooltip ?? button.ButtonId;
        if (string.IsNullOrWhiteSpace(button.TooltipKey) || translate == null)
            return fallback;
        return translate(button.TooltipKey, fallback);
    }

    static bool SameButton(KitLibMainMenuCornerButtonRegistration button, string modId, string buttonId) =>
        string.Equals(button.ModId, modId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(button.ButtonId, buttonId, StringComparison.OrdinalIgnoreCase);

    internal static void ClearForTests() {
        lock (Gate)
            Buttons.Clear();
    }
}
