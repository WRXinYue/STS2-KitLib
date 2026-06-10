namespace KitLib.Abstractions.Modding;

/// <summary>Runtime facts used to evaluate a <see cref="KitLibCompatDocument"/>.</summary>
public sealed class KitLibCompatRuntime {
    public string? GameVersion { get; init; }

    public string? KitLibVersion { get; init; }

    public Func<string, bool>? IsModuleLoaded { get; init; }

    /// <summary>Returns the loaded manifest version for a mod id, or null when unknown.</summary>
    public Func<string, string?>? ResolveModVersion { get; init; }
}
