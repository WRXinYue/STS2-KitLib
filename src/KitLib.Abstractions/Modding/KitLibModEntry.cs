namespace KitLib.Abstractions.Modding;

/// <summary>One scanned mod row for ModPanel (all load states, not only loaded).</summary>
public readonly record struct KitLibModEntry(
    string Id,
    string DisplayName,
    string Version,
    IReadOnlyList<string> Dependencies,
    ModEntryLoadStatus LoadStatus,
    ModEntrySource Source,
    bool IsEnabledInSettings,
    string? InstallPath = null) {
    public bool IsLoaded => LoadStatus == ModEntryLoadStatus.Loaded;

    public KitLibModInfo ToModInfo() => new(Id, DisplayName, Version, Dependencies);
}
