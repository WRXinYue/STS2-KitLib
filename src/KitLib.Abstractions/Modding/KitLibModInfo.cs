namespace KitLib.Abstractions.Modding;

/// <summary>Stable snapshot of one loaded mod (manifest-backed).</summary>
public readonly record struct KitLibModInfo(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Dependencies);
