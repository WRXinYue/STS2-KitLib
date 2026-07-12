namespace KitLib.Abstractions.Host;

/// <summary>Registration payload for a KitLib rail tab. Panel mod renders these descriptors.</summary>
public sealed class KitLibTabDescriptor {
    public required string Id { get; init; }
    public required string IconKey { get; init; }

    /// <summary>Localization key resolved at display time via <c>I18N.T</c>.</summary>
    public string? DisplayNameKey { get; init; }

    /// <summary>Fallback when <see cref="DisplayNameKey"/> is missing or untranslated.</summary>
    public string DisplayNameFallback { get; init; } = "";

    /// <summary>Legacy literal label when no <see cref="DisplayNameKey"/> is set.</summary>
    public string DisplayName { get; init; } = "";

    public int Order { get; init; }
    public KitLibTabGroup Group { get; init; } = KitLibTabGroup.Primary;
    public KitLibTabKind Kind { get; init; } = KitLibTabKind.Cheat;
    public required Action<object> OnActivate { get; init; }
    public Action<object>? OnDeactivate { get; init; }
    public string? OwningModuleId { get; init; }
}
