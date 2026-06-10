namespace KitLib.Abstractions.Modding;

[Flags]
public enum KitLibCompatFlags {
    None = 0,
    GameVersionMismatch = 1,
    KitLibVersionMismatch = 2,
    MissingKitLibModule = 4,
    ModDependencyVersionMismatch = 8,
}
