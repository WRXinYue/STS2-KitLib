using KitLib.ModVariantLoader;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.ModVariantLoader.ContentEntry;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public static void Initialize() => ModVariantBootstrap.Initialize();
}
