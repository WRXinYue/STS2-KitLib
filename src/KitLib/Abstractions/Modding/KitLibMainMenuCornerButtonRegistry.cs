namespace KitLib.Abstractions.Modding;

/// <summary>
/// Register / read / unregister API for main-menu top-left corner icon buttons.
/// Content mods call this from Abstractions; KitLib Core renders a shared vertical stack
/// on <c>NMainMenu</c> below the vanilla patch-notes shortcut.
/// </summary>
/// <remarks>
/// STS2 loads each mod in its own <c>AssemblyLoadContext</c>, so this type may exist more than
/// once. Button state lives in <see cref="AppDomain"/> slots (BCL types only) so Core and
/// content mods share one list.
/// </remarks>
public static class KitLibMainMenuCornerButtonRegistry {
    const string ButtonsSlot = "KitLib.Abstractions.MainMenuCornerButtons.v1";
    const string RefreshSlot = "KitLib.Abstractions.MainMenuCornerButtons.Refresh.v1";
    const string GateKey = "KitLib.Abstractions.MainMenuCornerButtonRegistry.Gate";

    static object SharedGate => string.Intern(GateKey);

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

        lock (SharedGate) {
            var buttons = SharedButtons();
            for (var i = buttons.Count - 1; i >= 0; i--) {
                if (SameButton(buttons[i], button.ModId, button.ButtonId))
                    buttons.RemoveAt(i);
            }
            buttons.Add(ToShared(button));
        }

        NotifyRefreshRequested();
    }

    /// <summary>Remove one button. Returns whether a button was removed.</summary>
    public static bool Unregister(string modId, string buttonId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(buttonId))
            return false;
        lock (SharedGate) {
            var buttons = SharedButtons();
            var removed = 0;
            for (var i = buttons.Count - 1; i >= 0; i--) {
                if (!SameButton(buttons[i], modId, buttonId))
                    continue;
                buttons.RemoveAt(i);
                removed++;
            }
            return removed > 0;
        }
    }

    /// <summary>Remove every button registered for <paramref name="modId"/>. Returns how many were removed.</summary>
    public static int UnregisterAll(string modId) {
        if (string.IsNullOrWhiteSpace(modId))
            return 0;
        lock (SharedGate) {
            var buttons = SharedButtons();
            var removed = 0;
            for (var i = buttons.Count - 1; i >= 0; i--) {
                if (!string.Equals(ReadString(buttons[i], "ModId"), modId, StringComparison.OrdinalIgnoreCase))
                    continue;
                buttons.RemoveAt(i);
                removed++;
            }
            return removed;
        }
    }

    /// <summary>Whether a specific button is registered.</summary>
    public static bool Contains(string modId, string buttonId) {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(buttonId))
            return false;
        lock (SharedGate) {
            foreach (var entry in SharedButtons()) {
                if (SameButton(entry, modId, buttonId))
                    return true;
            }
            return false;
        }
    }

    /// <summary>All registered buttons ordered for vertical stacking on the main menu.</summary>
    public static IReadOnlyList<KitLibMainMenuCornerButtonRegistration> GetOrderedButtons() {
        lock (SharedGate) {
            var reconstructed = new List<KitLibMainMenuCornerButtonRegistration>();
            foreach (var entry in SharedButtons()) {
                var button = FromShared(entry);
                if (button != null)
                    reconstructed.Add(button);
            }

            reconstructed.Sort(static (a, b) => {
                var order = a.SortOrder.CompareTo(b.SortOrder);
                if (order != 0)
                    return order;
                order = string.Compare(a.ModId, b.ModId, StringComparison.OrdinalIgnoreCase);
                if (order != 0)
                    return order;
                return string.Compare(a.ButtonId, b.ButtonId, StringComparison.OrdinalIgnoreCase);
            });

            return reconstructed;
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
    /// Resolve the optional open-state icon. Null when
    /// <see cref="KitLibMainMenuCornerButtonRegistration.ActiveIconPath"/> is unset.
    /// </summary>
    public static string? ResolveActiveIconPath(KitLibMainMenuCornerButtonRegistration button) {
        ArgumentNullException.ThrowIfNull(button);
        return string.IsNullOrWhiteSpace(button.ActiveIconPath) ? null : button.ActiveIconPath.Trim();
    }

    /// <summary>
    /// Resolve the name shown left of the icon. When <see cref="KitLibMainMenuCornerButtonRegistration.Title"/>
    /// is unset, uses <paramref name="fallbackName"/> then <see cref="KitLibMainMenuCornerButtonRegistration.ModId"/>.
    /// </summary>
    public static string ResolveTitle(
        KitLibMainMenuCornerButtonRegistration button,
        Func<string, string, string>? translate = null,
        string? fallbackName = null) {
        ArgumentNullException.ThrowIfNull(button);
        var fallback = FirstNonEmpty(button.Title, fallbackName, button.ModId);
        if (string.IsNullOrWhiteSpace(button.TitleKey) || translate == null)
            return fallback;
        return translate(button.TitleKey, fallback);
    }

    /// <summary>
    /// Resolve the second line. Explicit <see cref="KitLibMainMenuCornerButtonRegistration.Description"/> wins;
    /// otherwise formats <see cref="KitLibMainMenuCornerButtonRegistration.Version"/> or
    /// <paramref name="fallbackVersion"/> as <c>v{version}</c>.
    /// </summary>
    public static string ResolveDescription(
        KitLibMainMenuCornerButtonRegistration button,
        string? fallbackVersion = null) {
        ArgumentNullException.ThrowIfNull(button);
        if (!string.IsNullOrWhiteSpace(button.Description))
            return button.Description.Trim();
        return FormatVersionLine(FirstNonEmpty(button.Version, fallbackVersion, null));
    }

    /// <summary>Two-line label: title, then description when present.</summary>
    public static string ResolveInfoLabelText(
        KitLibMainMenuCornerButtonRegistration button,
        Func<string, string, string>? translate = null,
        string? fallbackName = null,
        string? fallbackVersion = null) {
        var title = ResolveTitle(button, translate, fallbackName);
        var description = ResolveDescription(button, fallbackVersion);
        return string.IsNullOrEmpty(description) ? title : $"{title}\n{description}";
    }

    /// <summary>LustTravel-style version line: <c>v1.2.3</c>. Empty when <paramref name="version"/> is blank.</summary>
    public static string FormatVersionLine(string? version) {
        if (string.IsNullOrWhiteSpace(version))
            return "";
        var trimmed = version.Trim();
        if (trimmed.StartsWith("v", StringComparison.OrdinalIgnoreCase))
            return trimmed;
        return $"v{trimmed}";
    }

    /// <summary>
    /// Resolve tooltip text. When <paramref name="translate"/> is provided and
    /// <see cref="KitLibMainMenuCornerButtonRegistration.TooltipKey"/> is set, calls
    /// <c>translate(tooltipKey, tooltipFallback)</c>.
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

    static string FirstNonEmpty(string? a, string? b, string? c) {
        if (!string.IsNullOrWhiteSpace(a))
            return a.Trim();
        if (!string.IsNullOrWhiteSpace(b))
            return b.Trim();
        return (c ?? "").Trim();
    }

    static List<object> SharedButtons() {
        var raw = AppDomain.CurrentDomain.GetData(ButtonsSlot);
        if (raw is List<object> existing)
            return existing;
        var created = new List<object>();
        AppDomain.CurrentDomain.SetData(ButtonsSlot, created);
        return created;
    }

    static Dictionary<string, object?> ToShared(KitLibMainMenuCornerButtonRegistration button) => new() {
        ["ModId"] = button.ModId,
        ["ButtonId"] = button.ButtonId,
        ["IconPath"] = button.IconPath,
        ["ActiveIconPath"] = button.ActiveIconPath,
        ["Title"] = button.Title,
        ["TitleKey"] = button.TitleKey,
        ["Description"] = button.Description,
        ["Version"] = button.Version,
        ["Tooltip"] = button.Tooltip,
        ["TooltipKey"] = button.TooltipKey,
        ["SortOrder"] = button.SortOrder,
        ["OnPressed"] = button.OnPressed,
        ["IsVisible"] = button.IsVisible,
        ["IsOpen"] = button.IsOpen,
        ["OnMenuReady"] = button.OnMenuReady,
    };

    static KitLibMainMenuCornerButtonRegistration? FromShared(object? entry) {
        if (entry is not Dictionary<string, object?> data)
            return null;
        if (data.GetValueOrDefault("OnPressed") is not Action<object> onPressed)
            return null;
        var modId = ReadString(data, "ModId");
        var buttonId = ReadString(data, "ButtonId");
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(buttonId))
            return null;

        return new KitLibMainMenuCornerButtonRegistration {
            ModId = modId,
            ButtonId = buttonId,
            IconPath = ReadString(data, "IconPath"),
            Title = ReadString(data, "Title"),
            TitleKey = ReadString(data, "TitleKey"),
            Description = ReadString(data, "Description"),
            Version = ReadString(data, "Version"),
            Tooltip = ReadString(data, "Tooltip"),
            TooltipKey = ReadString(data, "TooltipKey"),
            SortOrder = data.GetValueOrDefault("SortOrder") is int sortOrder ? sortOrder : 0,
            OnPressed = onPressed,
            IsVisible = data.GetValueOrDefault("IsVisible") as Func<object, bool>,
            ActiveIconPath = ReadString(data, "ActiveIconPath"),
            IsOpen = data.GetValueOrDefault("IsOpen") as Func<object, bool>,
            OnMenuReady = data.GetValueOrDefault("OnMenuReady") as Action<object>,
        };
    }

    static bool SameButton(object? entry, string modId, string buttonId) =>
        string.Equals(ReadString(entry, "ModId"), modId, StringComparison.OrdinalIgnoreCase)
        && string.Equals(ReadString(entry, "ButtonId"), buttonId, StringComparison.OrdinalIgnoreCase);

    static string? ReadString(object? entry, string key) {
        if (entry is Dictionary<string, object?> data && data.TryGetValue(key, out var value))
            return value as string;
        return null;
    }

    internal static void ClearForTests() {
        lock (SharedGate)
            SharedButtons().Clear();
    }

    /// <summary>Core sets this to rebuild icons when registrations change after the main menu exists.</summary>
    public static Action? RequestRefresh {
        get => AppDomain.CurrentDomain.GetData(RefreshSlot) as Action;
        set => AppDomain.CurrentDomain.SetData(RefreshSlot, value);
    }

    static void NotifyRefreshRequested() {
        try {
            RequestRefresh?.Invoke();
        }
        catch {
            // Host wiring is optional until Core finishes initializing.
        }
    }
}
