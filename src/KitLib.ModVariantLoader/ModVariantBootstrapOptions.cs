namespace KitLib.ModVariantLoader;

public sealed class ModVariantBootstrapOptions {
    public string? ModId { get; init; }

    public string? ImplementationAssemblyFileName { get; init; }

    public string? LogPrefix { get; init; }

    public string? HarmonyId { get; init; }

    /// <summary>
    /// Optional override when the thin loader DLL is not colocated with <c>lib/</c>.
    /// </summary>
    public string? LoaderModDirectory { get; init; }
}
