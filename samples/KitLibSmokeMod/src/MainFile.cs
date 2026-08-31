using MegaCrit.Sts2.Core.Modding;

namespace KitLibSmokeMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLibSmokeMod";

    // Single-build sample. Dual-API packs use KitLib's eng/ModVariantContentLoader as the Workshop root.
    public static void Initialize() => KitLibSmokeRegistration.Register();
}
