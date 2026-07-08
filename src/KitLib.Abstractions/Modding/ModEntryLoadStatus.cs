namespace KitLib.Abstractions.Modding;

/// <summary>Mirrors official <c>ModLoadState</c> without referencing MegaCrit.</summary>
public enum ModEntryLoadStatus {
    None,
    Loaded,
    Failed,
    Disabled,
    DisabledDuplicate,
    AddedAtRuntime,
}
