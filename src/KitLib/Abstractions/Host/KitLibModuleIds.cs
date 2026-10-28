namespace KitLib.Abstractions.Host;

public static class KitLibModuleIds {
    public const string Core = "KitLib";
    public const string ModPanel = "KitLib.ModPanel";
    public const string Hud = "KitLib.Hud";
    /// <summary>Legacy id of <see cref="Hud"/>; only used to migrate stored toggles.</summary>
    public const string LegacyPanel = "KitLib.Panel";
    public const string User = "KitLib.User";
    public const string Cheat = "KitLib.Cheat";
    public const string Dev = "KitLib.Dev";
}
