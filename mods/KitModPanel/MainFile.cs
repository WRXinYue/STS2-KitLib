using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ProductLoaders;

/// <summary>
/// Thin Workshop entry for KitModPanel. Satellite DLLs under modules/ are loaded by KitLib Core.
/// </summary>
[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitModPanel";

    public static void Initialize() {
        // KitLib discovers sibling product folders and loads KitLib.ModPanel.dll.
    }
}
