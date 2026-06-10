namespace KitLib.Abstractions.Modding;

/// <summary>Parsed <c>kitlib.compat.toml</c> constraints for a content mod.</summary>
public sealed class KitLibCompatDocument {
    public const string FileName = "kitlib.compat.toml";

    public IReadOnlyList<string> GameVersionRanges { get; init; } = [];

    /// <summary>KitLib bundle version range(s); satellites share this version.</summary>
    public IReadOnlyList<string> KitLibVersionRanges { get; init; } = [];

    public IReadOnlyList<string> KitLibModules { get; init; } = [];

    /// <summary>Manifest mod id → semver range(s) for other loaded mods (e.g. STS2-RitsuLib).</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<string>> ModVersionRanges { get; init; }
        = new Dictionary<string, IReadOnlyList<string>>();

    public bool HasConstraints =>
        GameVersionRanges.Count > 0
        || KitLibVersionRanges.Count > 0
        || KitLibModules.Count > 0
        || ModVersionRanges.Count > 0;
}
