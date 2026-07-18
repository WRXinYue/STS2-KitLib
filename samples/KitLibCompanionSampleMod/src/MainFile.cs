using KitLibCompanionSampleMod.Pools;
using KitLibCompanionSampleMod.Relics;
using MegaCrit.Sts2.Core.Modding;

namespace KitLibCompanionSampleMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLibCompanionSampleMod";

    public static void Initialize() {
        // ModelDb.Init scans mod assemblies (ReflectionHelper.GetSubtypesInMods); do not Inject here.
        ModHelper.AddModelToPool(typeof(CompanionSampleRelicPool), typeof(IroncladCompanionRelic));

        CompanionSummonScheduler.Initialize();
    }
}
