using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ProductLoaders;

/// <summary>
/// Thin Workshop entry for KitDevTools. Panel/Cheat/Dev satellites are loaded by KitLib Core.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitDevTools";

    public static void Initialize() {
        // KitLib discovers sibling product folders and loads Dev Tools satellites.
    }
}
