using HarmonyLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Settings;
using MegaCrit.Sts2.Core.Modding;

namespace KitLib.User;

[ModInitializer(nameof(Initialize))]
public static class ModuleEntry {
    public static void Initialize() {
        KitLibHost.AnnounceModule(KitLibModuleIds.User);
        KitLibUserOps.CurrentSessionLogFileName = () => GameLogFileHydrator.CurrentSessionLogFileName;
        SettingsStore.Load();
        LogCollector.Initialize();
        UserTabRegistration.Register();

        KitLibFeaturesPatches.EnsureApplied();
        MainFile.Logger.Info("KitLib.User module initialized.");
    }
}
