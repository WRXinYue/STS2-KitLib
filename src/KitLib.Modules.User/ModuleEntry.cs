using KitLib;
using KitLib.Abstractions.Host;
using KitLib.Host;
using KitLib.Settings;

namespace KitLib.User;

public static class ModuleEntry {
    public static void Initialize() {
        if (KitLibHost.IsModuleLoaded(KitLibModuleIds.User)) return;
        KitLibHost.CatalogAccessor = () => Modding.ModCatalog.Default;
        KitLibHost.AnnounceModule(KitLibModuleIds.User);
        KitLibUserOps.CurrentSessionLogFileName = () => GameLogFileHydrator.CurrentSessionLogFileName;
        KitLibUserOps.CurrentSessionLogPath = () => GameLogFileHydrator.CurrentSessionLogPath;
        SettingsStore.Load();
        LogCollector.Initialize();
        KitLogHub.RegisterSink(new KitLogUserSink());
        UserTabRegistration.Register();

        KitLibHarmony.Apply(typeof(ModuleEntry).Assembly, KitLibModuleIds.User);
        MainFile.Logger.Info("KitLib.User module initialized.");
    }
}
