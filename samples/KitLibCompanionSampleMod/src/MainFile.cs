using System.Reflection;
using MegaCrit.Sts2.Core.Modding;
using STS2RitsuLib;

namespace KitLibCompanionSampleMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLibCompanionSampleMod";

    public static void Initialize() {
        var modDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
        RitsuLibFramework.CreateModLocalization(
            ModId,
            "default",
            resourceFolders: [Path.Combine(modDir, "localization")]);

        CompanionSummonScheduler.Initialize();
    }
}
