namespace KitLib.Abstractions.Modding;

/// <summary>
/// Unified register / read / unregister API for KitLib-native mod settings pages.
/// Content mods call this from Abstractions; KitModPanel reads it to render tabs.
/// </summary>
/// <remarks>
/// Convention:
/// <list type="bullet">
/// <item><description><c>ModId</c> must match the official mod manifest id.</description></item>
/// <item><description><c>PageId</c> is stable within the mod; re-<see cref="Register"/> replaces the same pair.</description></item>
/// <item><description><see cref="KitLibModSettingsPageRegistration.BuildBody"/> returns a Godot <c>Control</c> at render time.</description></item>
/// <item><description>If the same mod also registered pages via STS2-RitsuLib, KitModPanel prefers the Ritsu surface.</description></item>
/// </list>
/// </remarks>
public static class KitLibModSettingsRegistry {
    static readonly List<KitLibModSettingsPageRegistration> Pages = [];
    static readonly object Gate = new();

    /// <summary>Register or replace a page for <paramref name="page"/>.ModId + PageId.</summary>
    public static void Register(KitLibModSettingsPageRegistration page) {
        ArgumentNullException.ThrowIfNull(page);
        ArgumentException.ThrowIfNullOrWhiteSpace(page.ModId);
        ArgumentException.ThrowIfNullOrWhiteSpace(page.PageId);
        ArgumentException.ThrowIfNullOrWhiteSpace(page.Title);
        ArgumentNullException.ThrowIfNull(page.BuildBody);

        lock (Gate) {
            Pages.RemoveAll(p => SamePage(p, page.ModId, page.PageId));
            Pages.Add(page);
        }
    }

    /// <summary>Remove one page. Returns whether a page was removed.</summary>
    public static bool Unregister(string modId, string pageId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(pageId))
            return false;
        lock (Gate)
            return Pages.RemoveAll(p => SamePage(p, modId, pageId)) > 0;
    }

    /// <summary>Remove every page registered for <paramref name="modId"/>. Returns how many were removed.</summary>
    public static int UnregisterAll(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return 0;
        lock (Gate)
            return Pages.RemoveAll(p =>
                string.Equals(p.ModId, modId, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Whether <paramref name="modId"/> has at least one registered KitLib-native page.</summary>
    public static bool HasPages(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return false;
        lock (Gate) {
            return Pages.Any(p => string.Equals(p.ModId, modId, StringComparison.OrdinalIgnoreCase));
        }
    }

    /// <summary>Whether a specific page is registered.</summary>
    public static bool Contains(string modId, string pageId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(pageId))
            return false;
        lock (Gate)
            return Pages.Any(p => SamePage(p, modId, pageId));
    }

    /// <summary>Try read one page by mod + page id.</summary>
    public static bool TryGetPage(string modId, string pageId, out KitLibModSettingsPageRegistration? page) {
        page = null;
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(pageId))
            return false;
        lock (Gate) {
            foreach (var p in Pages) {
                if (!SamePage(p, modId, pageId))
                    continue;
                page = p;
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Pages for <paramref name="modId"/>, ordered by <see cref="KitLibModSettingsPageRegistration.SortOrder"/>
    /// then <see cref="KitLibModSettingsPageRegistration.PageId"/>.
    /// </summary>
    public static IReadOnlyList<KitLibModSettingsPageRegistration> GetPages(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return Array.Empty<KitLibModSettingsPageRegistration>();
        lock (Gate) {
            return Pages
                .Where(p => string.Equals(p.ModId, modId, StringComparison.OrdinalIgnoreCase))
                .OrderBy(p => p.SortOrder)
                .ThenBy(p => p.PageId, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>Distinct mod ids that currently have at least one KitLib-native page (sorted).</summary>
    public static IReadOnlyList<string> GetRegisteredModIds() {
        lock (Gate) {
            return Pages
                .Select(p => p.ModId)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }

    /// <summary>
    /// Resolve the display title. When <paramref name="translate"/> is provided and
    /// <see cref="KitLibModSettingsPageRegistration.TitleKey"/> is set, calls
    /// <c>translate(titleKey, titleFallback)</c>; otherwise returns <see cref="KitLibModSettingsPageRegistration.Title"/>.
    /// </summary>
    public static string ResolveTitle(
        KitLibModSettingsPageRegistration page,
        Func<string, string, string>? translate = null) {
        ArgumentNullException.ThrowIfNull(page);
        if (string.IsNullOrWhiteSpace(page.TitleKey) || translate == null)
            return page.Title;
        return translate(page.TitleKey, page.Title);
    }

    static bool SamePage(KitLibModSettingsPageRegistration page, string modId, string pageId) =>
        string.Equals(page.ModId, modId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(page.PageId, pageId, StringComparison.OrdinalIgnoreCase);

    internal static void ClearForTests() {
        lock (Gate)
            Pages.Clear();
    }
}
