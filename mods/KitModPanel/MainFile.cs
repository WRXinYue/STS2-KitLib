using KitLib.ModPanelMod;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ProductLoaders;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitModPanel";

    public static void Initialize() => ModuleEntry.Initialize();
}
