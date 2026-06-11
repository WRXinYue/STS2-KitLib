namespace KitLib.Modding;

/// <summary>Manifest-backed mod snapshot stored in Core without referencing KitLib.Abstractions.</summary>
internal readonly record struct ModAssemblyInfo(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Dependencies,
    string? EntryAssemblySimpleName = null);
