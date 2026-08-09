using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ProductLoaders;

/// <summary>
/// Thin Workshop entry for KitAI. KitLib.AI.dll under modules/ is loaded by KitLib Core.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitAI";

    public static void Initialize() {
        // KitLib discovers sibling product folders and loads KitLib.AI.dll.
    }
}
