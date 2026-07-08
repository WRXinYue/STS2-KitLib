using MegaCrit.Sts2.Core.Modding;

namespace KitLibSmokeMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLibSmokeMod";

    public static void Initialize() => KitLibSmokeRegistration.Register();
}
