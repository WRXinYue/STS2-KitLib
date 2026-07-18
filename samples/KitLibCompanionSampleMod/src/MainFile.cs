using KitLibCompanionSampleMod.Pools;
using KitLibCompanionSampleMod.Relics;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Models;

namespace KitLibCompanionSampleMod;

[ModInitializer(nameof(Initialize))]
public static class MainFile {
    public const string ModId = "KitLibCompanionSampleMod";

    public static void Initialize() {
        ModelDb.Inject(typeof(CompanionSampleRelicPool));
        ModelDb.Inject(typeof(IroncladCompanionRelic));
        ModHelper.AddModelToPool(typeof(CompanionSampleRelicPool), typeof(IroncladCompanionRelic));

        CompanionSummonScheduler.Initialize();
    }
}
