namespace KitLib.Abstractions.Modding;

/// <summary>Runtime facts used to evaluate a <see cref="KitLibCompatDocument"/>.</summary>
public sealed class KitLibCompatRuntime {
    public string? GameVersion { get; init; }

    public string? KitLibVersion { get; init; }

    public Func<string, bool>? IsModuleLoaded { get; init; }
}
