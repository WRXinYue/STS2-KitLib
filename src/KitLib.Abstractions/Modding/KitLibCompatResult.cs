namespace KitLib.Abstractions.Modding;

public sealed class KitLibCompatResult {
    public KitLibCompatFlags Flags { get; init; }

    public IReadOnlyList<string> GameVersionRanges { get; init; } = [];

    public IReadOnlyList<string> KitLibVersionRanges { get; init; } = [];

    public IReadOnlyList<string> MissingModules { get; init; } = [];

    public bool IsCompatible => Flags == KitLibCompatFlags.None;

    public bool HasSidecar { get; init; }
}
